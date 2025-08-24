using BasarSoft.Data;
using BasarSoft.Dtos;
using BasarSoft.Entity;
using BasarSoft.Responses;
using BasarSoft.Services.Interfaces;
using BasarSoft.Validation;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

// NTS
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;   // for CoordinateArraySequenceFactory
using NetTopologySuite.IO;

namespace BasarSoft.Services;

public sealed class GeometryEfService : IGeometryService
{
    public BasarSoftDbContext db { get; set; }

    public static readonly Regex namePattern = new(@"^[A-Za-z0-9 _\-]{3,50}$");

    // 1=POINT, 2=LINESTRING, 3=POLYGON
    public static readonly Dictionary<int, string> GeometryTypeMap = new()
    {
        { 1, "POINT" },
        { 2, "LINESTRING" },
        { 3, "POLYGON" }
    };

    // --- NTS setup (SRID=4326) ---
    static readonly NtsGeometryServices Nts = new NtsGeometryServices(
        CoordinateArraySequenceFactory.Instance,
        new PrecisionModel(),
        4326);
    static readonly GeometryFactory Gf = Nts.CreateGeometryFactory(4326);
    static readonly WKTReader WktReader = new WKTReader(Nts);

    public GeometryEfService(BasarSoftDbContext dbContext)
    {
        db = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    // Parse WKT to NTS Geometry with validation (topology checks are here)
    private static Geometry ParseGeometryStrict(int type, string wkt)
    {
        Geometry g;
        try { g = WktReader.Read(wkt); }
        catch (Exception ex)
        {
            throw new ArgumentException($"Geçersiz WKT: {ex.Message}");
        }

        if (!g.IsValid)
            throw new ArgumentException("Geometri hatalı (IsValid=false).");

        if (g is LineString ls && ls.NumPoints < 2)
            throw new ArgumentException("LineString en az 2 nokta içermeli.");

        if (g is Polygon poly && !poly.Shell.IsClosed)
            throw new ArgumentException("Polygon kapanmıyor (ilk/son nokta aynı değil).");

        if (g.SRID != 4326) g.SRID = 4326;

        return g;
    }

    // =========================
    // READ (async)
    // =========================
    public async Task<ApiResponse<List<GeometryItem>>> GetAllAsync()
    {
        var items = await db.Points.AsNoTracking().ToListAsync();
        foreach (var it in items) it.Shape = null!;
        return ApiResponse<List<GeometryItem>>.Ok(items, "Listed");
    }

    public async Task<ApiResponse<GeometryItem>> GetByIdAsync(int id)
    {
        var item = await db.Points.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (item is null) return ApiResponse<GeometryItem>.Fail("Not found");

        item.Shape = null!;
        return ApiResponse<GeometryItem>.Ok(item, "Found");
    }

    // =========================
    // WRITE (async)
    // =========================
    public async Task<ApiResponse<GeometryItem>> CreateAsync(GeometryDto dto)
    {
        var norm = GeometryValidator.NormalizeAndValidate(dto); // header + commas
        if (!norm.Success)
            return ApiResponse<GeometryItem>.Fail(norm.Message ?? "Invalid input");

        dto.WKT = norm.Wkt!;

        Geometry geom;
        try { geom = ParseGeometryStrict(dto.Type, dto.WKT); }
        catch (Exception ex) { return ApiResponse<GeometryItem>.Fail(ex.Message); }

        var entity = new GeometryItem
        {
            Name = dto.Name,
            Type = dto.Type,
            WKT = dto.WKT,
            Shape = geom
        };

        await db.Points.AddAsync(entity);
        await db.SaveChangesAsync();

        entity.Shape = null!;
        return ApiResponse<GeometryItem>.Ok(entity, "Created");
    }

    public async Task<ApiResponse<GeometryItem>> UpdateAsync(int id, GeometryDto dto)
    {
        var norm = GeometryValidator.NormalizeAndValidate(dto);
        if (!norm.Success)
            return ApiResponse<GeometryItem>.Fail(norm.Message ?? "Invalid input");

        dto.WKT = norm.Wkt!;

        var entity = await db.Points.FirstOrDefaultAsync(p => p.Id == id);
        if (entity is null)
            return ApiResponse<GeometryItem>.Fail("Not found");

        Geometry geom;
        try { geom = ParseGeometryStrict(dto.Type, dto.WKT); }
        catch (Exception ex) { return ApiResponse<GeometryItem>.Fail(ex.Message); }

        entity.Name = dto.Name;
        entity.Type = dto.Type;
        entity.WKT = dto.WKT;
        entity.Shape = geom;

        await db.SaveChangesAsync();

        entity.Shape = null!;
        return ApiResponse<GeometryItem>.Ok(entity, "Updated");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        var entity = await db.Points.FirstOrDefaultAsync(p => p.Id == id);
        if (entity is null)
            return ApiResponse<bool>.Fail("Not found");

        db.Points.Remove(entity);
        await db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Deleted");
    }

    public async Task<ApiResponse<List<GeometryItem>>> AddRangeAsync(List<GeometryDto> items)
    {
        var toAdd = new List<GeometryItem>();

        foreach (var dto in items)
        {
            var v = GeometryValidator.NormalizeAndValidate(dto);
            if (!v.Success)
                return ApiResponse<List<GeometryItem>>.Fail(v.Message ?? "Invalid");

            dto.WKT = v.Wkt!;

            Geometry geom;
            try { geom = ParseGeometryStrict(dto.Type, dto.WKT); }
            catch (Exception ex) { return ApiResponse<List<GeometryItem>>.Fail(ex.Message); }

            toAdd.Add(new GeometryItem
            {
                Name = dto.Name,
                Type = dto.Type,
                WKT = dto.WKT,
                Shape = geom
            });
        }

        await db.Points.AddRangeAsync(toAdd);
        await db.SaveChangesAsync();

        foreach (var it in toAdd) it.Shape = null!;
        return ApiResponse<List<GeometryItem>>.Ok(toAdd, "Batch added");
    }
}
