using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

using BasarSoft.Data;
using BasarSoft.Repositories;
using BasarSoft.Repositories.Interfaces;
using BasarSoft.Services;
using BasarSoft.Services.Interfaces;
using BasarSoft.UnitOfWork;
using BasarSoft.Responses; // ApiMessages

var builder = WebApplication.CreateBuilder(args);

// DB
var cs = builder.Configuration.GetConnectionString("DefaultConnection")
          ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection bulunamadı.");
builder.Services.AddDbContext<BasarSoftDbContext>(opt =>
    opt.UseNpgsql(cs, x => x.UseNetTopologySuite())
);

// DI
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IGeometryService, GeometryEfService>();

// Localization
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
builder.Services.AddControllers(); // DataAnnotations için özel ayar gerekmiyorsa bu kadar yeter

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ApiMessages → SharedResources
using (var scope = app.Services.CreateScope())
{
    var loc = scope.ServiceProvider.GetRequiredService<IStringLocalizer<BasarSoft.SharedResources>>();
    ApiMessages.Configure(loc);
}

// Request culture (EN)
var cultures = new[] { new CultureInfo("en") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = cultures,
    SupportedUICultures = cultures
});

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.Run();
