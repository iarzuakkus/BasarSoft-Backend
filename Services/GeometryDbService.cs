using System.Text.RegularExpressions;
using BasarSoft.Dtos;
using BasarSoft.Entity;
using BasarSoft.Responses;
using BasarSoft.Services.Interfaces;
using BasarSoft.Validation;
using Npgsql;

namespace BasarSoft.Services
{
    public sealed class GeometryDbService : IGeometryService
    {
        private readonly NpgsqlDataSource dataSource;
        private static readonly Regex namePattern = new(@"^[A-Za-z0-9 _\-]{3,50}$");

        public GeometryDbService(NpgsqlDataSource dataSource) => this.dataSource = dataSource;

        public async Task<ApiResponse<List<GeometryItem>>> GetAllAsync()
        {
            var list = new List<GeometryItem>();
            await using var conn = await dataSource.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT id, name, ST_AsText(wkt) AS wkt FROM points ORDER BY id;", conn);
            await using var r = await cmd.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                list.Add(new GeometryItem
                {
                    Id = r.GetInt32(0),
                    Name = r.GetString(1),
                    Wkt = r.GetString(2)
                });
            }
            return ApiResponse<List<GeometryItem>>.Ok(list, "Listed");
        }

        public async Task<ApiResponse<GeometryItem>> GetByIdAsync(int id)
        {
            await using var conn = await dataSource.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT id, name, ST_AsText(wkt) AS wkt FROM points WHERE id = @id;", conn);
            cmd.Parameters.AddWithValue("@id", id);

            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                var item = new GeometryItem
                {
                    Id = r.GetInt32(0),
                    Name = r.GetString(1),
                    Wkt = r.GetString(2)
                };
                return ApiResponse<GeometryItem>.Ok(item, "Found");
            }
            return ApiResponse<GeometryItem>.NotFound("Point not found");
        }

        public async Task<ApiResponse<GeometryItem>> CreateAsync(GeometryDto input)
        {
            var v = GeometryValidator.NormalizeAndValidate(input);
            if (!v.Success) return ApiResponse<GeometryItem>.Fail(v.Message ?? "Invalid input");
            input.Wkt = v.Wkt!;

            await using var conn = await dataSource.OpenConnectionAsync();

            await using (var check = new NpgsqlCommand(
                "SELECT COUNT(*) FROM points WHERE lower(name) = lower(@name);", conn))
            {
                check.Parameters.AddWithValue("@name", input.Name.Trim());
                var count = (long)(await check.ExecuteScalarAsync() ?? 0L);
                if (count > 0) return ApiResponse<GeometryItem>.Conflict("Name already exists");
            }

            await using var cmd = new NpgsqlCommand(
                "INSERT INTO points (name, wkt) VALUES (@name, ST_GeomFromText(@wkt,4326)) RETURNING id;", conn);
            cmd.Parameters.AddWithValue("@name", input.Name.Trim());
            cmd.Parameters.AddWithValue("@wkt", input.Wkt.Trim());

            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            var created = new GeometryItem { Id = id, Name = input.Name.Trim(), Wkt = input.Wkt.Trim() };
            return ApiResponse<GeometryItem>.Created(created, "Point created");
        }

        public async Task<ApiResponse<GeometryItem>> UpdateAsync(int id, GeometryDto input)
        {
            var existing = await GetByIdAsync(id);
            if (!existing.Success) return ApiResponse<GeometryItem>.NotFound(existing.Message);

            if (!string.IsNullOrWhiteSpace(input.Name) && !namePattern.IsMatch(input.Name))
                return ApiResponse<GeometryItem>.Fail("Invalid name format", 400);

            if (!string.IsNullOrWhiteSpace(input.Wkt))
            {
                var v = GeometryValidator.NormalizeAndValidate(new GeometryDto
                {
                    Name = string.IsNullOrWhiteSpace(input.Name) ? existing.Data!.Name : input.Name!,
                    Type = input.Type == 0 ? existing.Data!.Type : input.Type,
                    Wkt = input.Wkt!
                });
                if (!v.Success) return ApiResponse<GeometryItem>.Fail(v.Message ?? "Invalid input");
                input.Wkt = v.Wkt!;
            }

            await using var conn = await dataSource.OpenConnectionAsync();

            if (!string.IsNullOrWhiteSpace(input.Name))
            {
                await using var dup = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM points WHERE lower(name)=lower(@name) AND id<>@id;", conn);
                dup.Parameters.AddWithValue("@name", input.Name.Trim());
                dup.Parameters.AddWithValue("@id", id);
                if ((long)(await dup.ExecuteScalarAsync() ?? 0L) > 0)
                    return ApiResponse<GeometryItem>.Conflict("Name must be unique");
            }

            await using var cmd = new NpgsqlCommand(
                @"UPDATE points
                  SET name = COALESCE(NULLIF(@name,''), name),
                      wkt  = COALESCE(
                                CASE WHEN @wkt IS NULL OR @wkt = '' THEN NULL
                                     ELSE ST_GeomFromText(@wkt,4326) END, wkt)
                  WHERE id = @id;", conn);

            cmd.Parameters.AddWithValue("@name", input.Name?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@wkt", input.Wkt?.Trim() ?? "");
            cmd.Parameters.AddWithValue("@id", id);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0 ? await GetByIdAsync(id) : ApiResponse<GeometryItem>.Fail("No changes made", 400);
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            await using var conn = await dataSource.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM points WHERE id=@id;", conn);
            cmd.Parameters.AddWithValue("@id", id);
            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0 ? ApiResponse<bool>.Ok(true, "Deleted") : ApiResponse<bool>.NotFound("Point not found");
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
                    var v = GeometryValidator.NormalizeAndValidate(dto);
                    if (!v.Success) throw new InvalidOperationException(v.Message);

                    await using (var check = new NpgsqlCommand(
                        "SELECT COUNT(*) FROM points WHERE lower(name)=lower(@name);", conn, tx))
                    {
                        check.Parameters.AddWithValue("@name", dto.Name.Trim());
                        if ((long)(await check.ExecuteScalarAsync() ?? 0L) > 0)
                            throw new InvalidOperationException($"Duplicate name: {dto.Name}");
                    }

                    await using var cmd = new NpgsqlCommand(
                        "INSERT INTO points (name, wkt) VALUES (@name, ST_GeomFromText(@wkt,4326)) RETURNING id;",
                        conn, tx);
                    cmd.Parameters.AddWithValue("@name", dto.Name.Trim());
                    cmd.Parameters.AddWithValue("@wkt", v.Wkt!.Trim());
                    var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                    added.Add(new GeometryItem { Id = id, Name = dto.Name.Trim(), Wkt = v.Wkt!.Trim() });
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

        // 
        public async Task<ApiResponse<PaginationResponse<GeometryItem>>> GetPagedAsync(PaginationRequest request)
        {
            var list = new List<GeometryItem>();
            await using var conn = await dataSource.OpenConnectionAsync();

            // Dinamik filtre
            var where = "";
            if (!string.IsNullOrWhiteSpace(request.Search))
                where = "WHERE lower(name) LIKE lower(@search)";

            // Toplam kayıt sayısı
            var countSql = $"SELECT COUNT(*) FROM points {where};";
            await using (var countCmd = new NpgsqlCommand(countSql, conn))
            {
                if (!string.IsNullOrWhiteSpace(request.Search))
                    countCmd.Parameters.AddWithValue("@search", $"%{request.Search.Trim()}%");
                var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

                var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

                // Sayfa verisi
                var dataSql = $@"
                    SELECT id, name, ST_AsText(wkt) AS wkt
                    FROM points
                    {where}
                    ORDER BY id
                    OFFSET @offset LIMIT @limit;";

                await using var dataCmd = new NpgsqlCommand(dataSql, conn);
                if (!string.IsNullOrWhiteSpace(request.Search))
                    dataCmd.Parameters.AddWithValue("@search", $"%{request.Search.Trim()}%");
                dataCmd.Parameters.AddWithValue("@offset", (request.Page - 1) * request.PageSize);
                dataCmd.Parameters.AddWithValue("@limit", request.PageSize);

                await using var r = await dataCmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new GeometryItem
                    {
                        Id = r.GetInt32(0),
                        Name = r.GetString(1),
                        Wkt = r.GetString(2)
                    });
                }

                var response = new PaginationResponse<GeometryItem>
                {
                    Items = list,
                    TotalCount = totalCount,
                    TotalPages = totalPages,
                    Page = request.Page,
                    PageSize = request.PageSize
                };

                return ApiResponse<PaginationResponse<GeometryItem>>.Ok(response, "Listed");
            }
        }
    }
}
