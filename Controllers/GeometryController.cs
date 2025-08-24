using BasarSoft.Dtos;
using BasarSoft.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class GeometryController : ControllerBase
{
    private readonly IGeometryService service;
    public GeometryController(IGeometryService s) => service = s;

    [HttpGet] public async Task<IActionResult> GetAll() => Ok(await service.GetAllAsync());
    [HttpGet("{id:int}")] public async Task<IActionResult> GetById(int id) => Ok(await service.GetByIdAsync(id));
    [HttpPost] public async Task<IActionResult> Create([FromBody] GeometryDto dto) => Ok(await service.CreateAsync(dto));
    [HttpPut("{id:int}")] public async Task<IActionResult> Update(int id, [FromBody] GeometryDto dto) => Ok(await service.UpdateAsync(id, dto));
    [HttpDelete("{id:int}")] public async Task<IActionResult> Delete(int id) => Ok(await service.DeleteAsync(id));
    [HttpPost("batch")] public async Task<IActionResult> AddRange([FromBody] List<GeometryDto> items) => Ok(await service.AddRangeAsync(items));
}
