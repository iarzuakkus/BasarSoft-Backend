using BasarSoft.Dtos;
using BasarSoft.Entity;
using BasarSoft.Responses;
using BasarSoft.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BasarSoft.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class GeometryController : ControllerBase
    {
        private readonly IGeometryService service;
        public GeometryController(IGeometryService s) => service = s;

        [HttpGet]
        public Task<ApiResponse<List<GeometryItem>>> GetAll()
            => service.GetAllAsync();

        [HttpGet("{id:int}")]
        public Task<ApiResponse<GeometryItem>> GetById(int id)
            => service.GetByIdAsync(id);

        [HttpPost]
        public Task<ApiResponse<GeometryItem>> Create([FromBody] GeometryDto dto)
            => service.CreateAsync(dto);

        [HttpPut("{id:int}")]
        public Task<ApiResponse<GeometryItem>> Update(int id, [FromBody] GeometryDto dto)
            => service.UpdateAsync(id, dto);

        [HttpDelete("{id:int}")]
        public Task<ApiResponse<bool>> Delete(int id)
            => service.DeleteAsync(id);

        [HttpPost("batch")]
        public Task<ApiResponse<List<GeometryItem>>> AddRange([FromBody] List<GeometryDto> items)
            => service.AddRangeAsync(items);

        // 
        [HttpGet("paged")]
        public Task<ApiResponse<PaginationResponse<GeometryItem>>> GetPaged([FromQuery] PaginationRequest request)
            => service.GetPagedAsync(request);
    }
}
