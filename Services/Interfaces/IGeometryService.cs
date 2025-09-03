using BasarSoft.Dtos;
using BasarSoft.Entity;
using BasarSoft.Responses;

namespace BasarSoft.Services.Interfaces
{
    public interface IGeometryService
    {
        Task<ApiResponse<List<GeometryItem>>> GetAllAsync();
        Task<ApiResponse<GeometryItem>> GetByIdAsync(int id);
        Task<ApiResponse<GeometryItem>> CreateAsync(GeometryDto dto);
        Task<ApiResponse<GeometryItem>> UpdateAsync(int id, GeometryDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
        Task<ApiResponse<List<GeometryItem>>> AddRangeAsync(List<GeometryDto> items);
        Task<ApiResponse<PaginationResponse<GeometryItem>>> GetPagedAsync(PaginationRequest request);
    }
}
