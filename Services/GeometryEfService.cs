using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

using BasarSoft.Dtos;
using BasarSoft.Entity;
using BasarSoft.Responses;
using BasarSoft.Services.Interfaces;
using BasarSoft.Repositories.Interfaces;
using BasarSoft.UnitOfWork;
using BasarSoft.Validation;

public sealed class GeometryEfService : IGeometryService
{
    private readonly IRepository<GeometryItem> repo;
    private readonly IUnitOfWork uow;
    static readonly NtsGeometryServices nts = NtsGeometryServices.Instance;
    static readonly WKTWriter wktWriter = new();

    public GeometryEfService(IRepository<GeometryItem> repo, IUnitOfWork uow)
    {
        this.repo = repo;
        this.uow = uow;
    }

    public async Task<ApiResponse<List<GeometryItem>>> GetAllAsync()
    {
        var list = await repo.GetAllAsync();
        foreach (var x in list) x.Wkt = x.Geo is null ? "" : wktWriter.Write(x.Geo);
        return ApiResponse<List<GeometryItem>>.OkKey(list, "success.listed");
    }

    public async Task<ApiResponse<GeometryItem>> GetByIdAsync(int id)
    {
        var item = await repo.GetByIdAsync(id);
        if (item is null) return ApiResponse<GeometryItem>.NotFoundKey("error.notfound");
        item.Wkt = item.Geo is null ? "" : wktWriter.Write(item.Geo);
        return ApiResponse<GeometryItem>.OkKey(item, "success.ok");
    }

    public async Task<ApiResponse<GeometryItem>> CreateAsync(GeometryDto dto)
    {
        var vr = GeometryValidator.NormalizeAndValidate(dto);
        if (!vr.Success) return ApiResponse<GeometryItem>.FailKey("error.validation");

        var geomRes = ParseGeometry(vr.Wkt!);
        if (!geomRes.Success) return ApiResponse<GeometryItem>.FailKey("error.geometry.invalid");

        var entity = new GeometryItem
        {
            Name = dto.Name,
            Type = dto.Type,
            Geo = geomRes.Data!,
            Wkt = vr.Wkt!
        };

        await repo.AddAsync(entity);
        await uow.CompleteAsync();

        entity.Wkt = wktWriter.Write(entity.Geo);
        return ApiResponse<GeometryItem>.CreatedKey(entity, "success.created");
    }

    public async Task<ApiResponse<GeometryItem>> UpdateAsync(int id, GeometryDto dto)
    {
        var entity = await repo.GetByIdAsync(id);
        if (entity is null) return ApiResponse<GeometryItem>.NotFoundKey("error.notfound");

        var vr = GeometryValidator.NormalizeAndValidate(dto);
        if (!vr.Success) return ApiResponse<GeometryItem>.FailKey("error.validation");

        var geomRes = ParseGeometry(vr.Wkt!);
        if (!geomRes.Success) return ApiResponse<GeometryItem>.FailKey("error.geometry.invalid");

        entity.Name = dto.Name;
        entity.Type = dto.Type;
        entity.Geo = geomRes.Data!;
        entity.Wkt = vr.Wkt!;

        repo.Update(entity);
        await uow.CompleteAsync();

        entity.Wkt = wktWriter.Write(entity.Geo);
        return ApiResponse<GeometryItem>.OkKey(entity, "success.updated");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        var entity = await repo.GetByIdAsync(id);
        if (entity is null) return ApiResponse<bool>.NotFoundKey("error.notfound");

        repo.Remove(entity);
        await uow.CompleteAsync();
        return ApiResponse<bool>.OkKey(true, "success.deleted");
    }

    public async Task<ApiResponse<List<GeometryItem>>> AddRangeAsync(List<GeometryDto> items)
    {
        var list = new List<GeometryItem>(items.Count);

        foreach (var dto in items)
        {
            var vr = GeometryValidator.NormalizeAndValidate(dto);
            if (!vr.Success) return ApiResponse<List<GeometryItem>>.FailKey("error.validation");

            var geomRes = ParseGeometry(vr.Wkt!);
            if (!geomRes.Success) return ApiResponse<List<GeometryItem>>.FailKey("error.geometry.invalid");

            list.Add(new GeometryItem
            {
                Name = dto.Name,
                Type = dto.Type,
                Geo = geomRes.Data!,
                Wkt = vr.Wkt!
            });
        }

        await repo.AddRangeAsync(list);
        await uow.CompleteAsync();

        foreach (var x in list) x.Wkt = wktWriter.Write(x.Geo);
        return ApiResponse<List<GeometryItem>>.OkKey(list, "success.inserted");
    }

    // ✅ Yeni metod: pagination + search
    public async Task<ApiResponse<PaginationResponse<GeometryItem>>> GetPagedAsync(PaginationRequest request)
    {
        var all = await repo.GetAllAsync();

        // Optional search filter
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            all = all
                .Where(x => x.Name.Contains(request.Search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var totalCount = all.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        var items = all
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        foreach (var x in items)
            x.Wkt = x.Geo is null ? "" : wktWriter.Write(x.Geo);

        var response = new PaginationResponse<GeometryItem>
        {
            Items = items,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return ApiResponse<PaginationResponse<GeometryItem>>.OkKey(response, "success.listed");
    }

    private static ApiResponse<Geometry> ParseGeometry(string wkt)
    {
        try
        {
            var reader = new WKTReader(nts);
            var geom = reader.Read(wkt);
            if (geom == null) return ApiResponse<Geometry>.FailKey("error.geometry.parse");
            if (geom.SRID != 4326) geom.SRID = 4326;
            return ApiResponse<Geometry>.OkKey(geom, "success.ok");
        }
        catch
        {
            return ApiResponse<Geometry>.FailKey("error.geometry.exception");
        }
    }
}
