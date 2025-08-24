using BasarSoft.Dtos;
using BasarSoft.Entity;
using BasarSoft.Responses;
using BasarSoft.Services.Interfaces;
using BasarSoft.Validation;

namespace BasarSoft.Services
{
    public sealed class GeometryService : IGeometryService
    {
        private static readonly List<GeometryItem> store = new();
        private static readonly object gate = new();
        private static int nextId = 0;

        public Task<ApiResponse<List<GeometryItem>>> GetAllAsync()
        {
            List<GeometryItem> snapshot;
            lock (gate) snapshot = store.Select(Clone).ToList();
            return Task.FromResult(ApiResponse<List<GeometryItem>>.Ok(snapshot, "Listed"));
        }

        public Task<ApiResponse<GeometryItem>> GetByIdAsync(int id)
        {
            GeometryItem? item;
            lock (gate) item = store.FirstOrDefault(p => p.Id == id);
            return Task.FromResult(
                item is null
                    ? ApiResponse<GeometryItem>.NotFound("Point not found")
                    : ApiResponse<GeometryItem>.Ok(Clone(item), "Found")
            );
        }

        public Task<ApiResponse<GeometryItem>> CreateAsync(GeometryDto input)
        {
            var v = GeometryValidator.NormalizeAndValidate(input);
            if (!v.Success) return Task.FromResult(ApiResponse<GeometryItem>.Fail(v.Message ?? "Invalid input"));
            input.Wkt = v.Wkt!;

            lock (gate)
            {
                if (store.Any(x => x.Name.Equals(input.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                    return Task.FromResult(ApiResponse<GeometryItem>.Conflict("A point with the same name already exists"));

                var entity = new GeometryItem
                {
                    Id = System.Threading.Interlocked.Increment(ref nextId),
                    Name = input.Name.Trim(),
                    Type = input.Type,
                    Wkt = input.Wkt.Trim()
                };
                store.Add(entity);
                return Task.FromResult(ApiResponse<GeometryItem>.Created(Clone(entity), "Point created"));
            }
        }

        public Task<ApiResponse<GeometryItem>> UpdateAsync(int id, GeometryDto input)
        {
            lock (gate)
            {
                var e = store.FirstOrDefault(p => p.Id == id);
                if (e is null) return Task.FromResult(ApiResponse<GeometryItem>.NotFound("Point not found"));

                if (!string.IsNullOrWhiteSpace(input.Name))
                {
                    var newName = input.Name.Trim();
                    var dup = store.Any(x => x.Id != id && x.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
                    if (dup) return Task.FromResult(ApiResponse<GeometryItem>.Conflict("Name must be unique"));
                    e.Name = newName;
                }

                if (!string.IsNullOrWhiteSpace(input.Wkt))
                {
                    var v = GeometryValidator.NormalizeAndValidate(new GeometryDto
                    {
                        Name = e.Name,
                        Type = input.Type == 0 ? e.Type : input.Type,
                        Wkt = input.Wkt!
                    });
                    if (!v.Success) return Task.FromResult(ApiResponse<GeometryItem>.Fail(v.Message ?? "Invalid input"));

                    e.Wkt = v.Wkt!;
                    e.Type = input.Type == 0 ? e.Type : input.Type;
                }

                return Task.FromResult(ApiResponse<GeometryItem>.Ok(Clone(e), "Point updated"));
            }
        }

        public Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            lock (gate)
            {
                var e = store.FirstOrDefault(p => p.Id == id);
                if (e is null) return Task.FromResult(ApiResponse<bool>.NotFound("Point not found"));
                store.Remove(e);
                return Task.FromResult(ApiResponse<bool>.Ok(true, "Point deleted"));
            }
        }

        public Task<ApiResponse<List<GeometryItem>>> AddRangeAsync(List<GeometryDto> items)
        {
            if (items is null || items.Count == 0)
                return Task.FromResult(ApiResponse<List<GeometryItem>>.Fail("Payload is empty", 400));

            var added = new List<GeometryItem>();

            lock (gate)
            {
                foreach (var dto in items)
                {
                    var v = GeometryValidator.NormalizeAndValidate(dto);
                    if (!v.Success)
                        return Task.FromResult(ApiResponse<List<GeometryItem>>.Fail(v.Message ?? "Invalid input"));

                    if (store.Any(x => x.Name.Equals(dto.Name.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                        added.Any(x => x.Name.Equals(dto.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                        return Task.FromResult(ApiResponse<List<GeometryItem>>.Conflict($"Duplicate name: {dto.Name}"));

                    var entity = new GeometryItem
                    {
                        Id = System.Threading.Interlocked.Increment(ref nextId),
                        Name = dto.Name.Trim(),
                        Type = dto.Type,
                        Wkt = v.Wkt!.Trim()
                    };
                    store.Add(entity);
                    added.Add(Clone(entity));
                }
            }

            return Task.FromResult(ApiResponse<List<GeometryItem>>.Ok(added, "Batch added"));
        }

        private static GeometryItem Clone(GeometryItem x) => new()
        {
            Id = x.Id,
            Name = x.Name,
            Type = x.Type,
            Wkt = x.Wkt,
            Geo = null! // in-memory modda geometry tutulmuyor
        };
    }
}
