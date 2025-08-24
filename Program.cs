using BasarSoft.Data;
using BasarSoft.Services;
using BasarSoft.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var cs = builder.Configuration.GetConnectionString("DefaultConnection")
         ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection bulunamadı.");

builder.Services.AddDbContext<BasarSoftDbContext>(opt =>
    opt.UseNpgsql(cs, x => x.UseNetTopologySuite())
);

builder.Services.AddScoped<IGeometryService, GeometryEfService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();
