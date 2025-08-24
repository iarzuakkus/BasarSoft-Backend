using System.Text.RegularExpressions;
using BasarSoft.Dtos;
using BasarSoft.Entity;
using BasarSoft.Responses;
using BasarSoft.Services.Interfaces;
using BasarSoft.Validation;
using Npgsql;

namespace BasarSoft.Services;

public sealed class GeometryDbService : IGeometryService
{
    private readonly NpgsqlDataSource dataSource;
    private static readonly Regex namePattern = new(@"^[A-Za-z0-9 _\-]{3,50}$");

    public GeometryDbService(NpgsqlDataSource dataSource)
    {
        this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    // =========================
    // READ
    // =========================
    public async Task<ApiResponse<List<GeometryItem>>> GetAllAsync()
    {
        var list = new List<GeometryItem>();

        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, name, wkt FROM points ORDER BY id;",
            conn
        );
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new GeometryItem
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                WKT = reader.GetString(2),
                // Shape yok; ADO.NET sürümünde sadece WKT saklıyoruz
            });
        }

        return ApiResponse<List<GeometryItem>>.Ok(list, "Listed");
    }

    public async Task<ApiResponse<GeometryItem>> GetByIdAsync(int id)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, name, wkt FROM points WHERE id = @id;",
            conn
        );
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var item = new GeometryItem
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                WKT = reader.GetString(2),
            };
            return ApiResponse<GeometryItem>.Ok(item, "Found");
        }

        return ApiResponse<GeometryItem>.NotFound("Point not found");
    }

    // =========================
    // WRITE
    // =========================
    public async Task<ApiResponse<GeometryItem>> CreateAsync(GeometryDto input)
    {
        // lightweight checks + WKT normalize (header/virgül)
        var v = GeometryValidator.NormalizeAndValidate(input);
        if (!v.Success) return ApiResponse<GeometryItem>.Fail(v.Message ?? "Invalid input");
        input.WKT = v.Wkt!;

        await using var conn = await dataSource.OpenConnectionAsync();

        // unique name check
        await using (var check = new NpgsqlCommand(
            "SELECT COUNT(*) FROM points WHERE lower(name) = lower(@name);", conn))
        {
            check.Parameters.AddWithValue("@name", input.Name.Trim());
            var count = (long)(await check.ExecuteScalarAsync() ?? 0L);
            if (count > 0)
                return ApiResponse<GeometryItem>.Conflict("A point with the same name already exists");
        }

        await using var cmd = new NpgsqlCommand(
            "INSERT INTO points (name, wkt) VALUES (@name, @wkt) RETURNING id;",
            conn
        );
        cmd.Parameters.AddWithValue("@name", input.Name.Trim());
        cmd.Parameters.AddWithValue("@wkt", input.WKT.Trim());

        var scalar = await cmd.ExecuteScalarAsync();
        var id = Convert.ToInt32(scalar);

        var created = new GeometryItem
        {
            Id = id,
            Name = input.Name.Trim(),
            WKT = input.WKT.Trim()
        };
        return ApiResponse<GeometryItem>.Created(created, "Point created");
    }

    public async Task<ApiResponse<GeometryItem>> UpdateAsync(int id, GeometryDto input)
    {
        // Kaydı var mı?
        var existing = await GetByIdAsync(id);
        if (!existing.Success)
            return ApiResponse<GeometryItem>.NotFound(existing.Message);

        // Name değişecekse temel kontrol
        if (!string.IsNullOrWhiteSpace(input.Name) && !namePattern.IsMatch(input.Name))
            return ApiResponse<GeometryItem>.Fail("Invalid name format", 400);

        // WKT verilmişse normalize et (boş/ null ise mevcut kalsın)
        if (!string.IsNullOrWhiteSpace(input.WKT))
        {
            var v = GeometryValidator.NormalizeAndValidate(new GeometryDto
            {
                Name = string.IsNullOrWhiteSpace(input.Name) ? existing.Data!.Name : input.Name!,
                Type = input.Type == 0 ? existing.Data!.Type : input.Type,
                WKT = input.WKT!
            });
            if (!v.Success) return ApiResponse<GeometryItem>.Fail(v.Message ?? "Invalid input");
            input.WKT = v.Wkt!;
        }

        await using var conn = await dataSource.OpenConnectionAsync();

        // same-name (başka id) var mı?
        if (!string.IsNullOrWhiteSpace(input.Name))
        {
            await using var dupCmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM points WHERE lower(name) = lower(@name) AND id != @id;", conn);
            dupCmd.Parameters.AddWithValue("@name", input.Name.Trim());
            dupCmd.Parameters.AddWithValue("@id", id);
            var dup = (long)(await dupCmd.ExecuteScalarAsync() ?? 0L);
            if (dup > 0) return ApiResponse<GeometryItem>.Conflict("Name must be unique");
        }

        await using var cmd = new NpgsqlCommand(
            @"UPDATE points
              SET name = COALESCE(NULLIF(@name, ''), name),
                  wkt  = COALESCE(NULLIF(@wkt,  ''), wkt)
              WHERE id = @id;", conn);

        cmd.Parameters.AddWithValue("@name", input.Name?.Trim() ?? "");
        cmd.Parameters.AddWithValue("@wkt", input.WKT?.Trim() ?? "");
        cmd.Parameters.AddWithValue("@id", id);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0
            ? await GetByIdAsync(id)
            : ApiResponse<GeometryItem>.Fail("No changes made", 400);
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM points WHERE id = @id;", conn);
        cmd.Parameters.AddWithValue("@id", id);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0
            ? ApiResponse<bool>.Ok(true, "Point deleted")
            : ApiResponse<bool>.NotFound("Point not found");
    }

    public async Task<ApiResponse<List<GeometryItem>>> AddRangeAsync(List<GeometryDto> items)
    {
        if (items is null || items.Count == 0)
            return ApiResponse<List<GeometryItem>>.Fail("Payload is empty", 400);

        var added = new List<GeometryItem>();

        await using var conn = await dataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            foreach (var dto in items)
            {
                // normalize + validate
                var v = GeometryValidator.NormalizeAndValidate(dto);
                if (!v.Success) throw new InvalidOperationException(v.Message);

                // name duplicate?
                await using (var check = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM points WHERE lower(name) = lower(@name);", conn, tx))
                {
                    check.Parameters.AddWithValue("@name", dto.Name.Trim());
                    var count = (long)(await check.ExecuteScalarAsync() ?? 0L);
                    if (count > 0)
                        throw new InvalidOperationException($"Duplicate name: {dto.Name}");
                }

                await using var cmd = new NpgsqlCommand(
                    "INSERT INTO points (name, wkt) VALUES (@name, @wkt) RETURNING id;",
                    conn, tx
                );
                cmd.Parameters.AddWithValue("@name", dto.Name.Trim());
                cmd.Parameters.AddWithValue("@wkt", v.Wkt!.Trim());
                var idObj = await cmd.ExecuteScalarAsync();
                var id = Convert.ToInt32(idObj);

                added.Add(new GeometryItem { Id = id, Name = dto.Name.Trim(), WKT = v.Wkt!.Trim() });
            }

            await tx.CommitAsync();
            return ApiResponse<List<GeometryItem>>.Ok(added, "Batch added");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return ApiResponse<List<GeometryItem>>.Fail(ex.Message, 400);
        }
    }
}
