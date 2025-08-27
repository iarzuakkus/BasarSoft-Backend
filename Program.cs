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

// ---------------- DB ----------------
var cs = builder.Configuration.GetConnectionString("DefaultConnection")
          ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection bulunamadı.");
builder.Services.AddDbContext<BasarSoftDbContext>(opt =>
    opt.UseNpgsql(cs, x => x.UseNetTopologySuite())
);

// ---------------- DI ----------------
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IGeometryService, GeometryEfService>();

// ---------------- Localization ----------------
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
builder.Services.AddControllers(); // no special DataAnnotations localization needed now

// ---------------- CORS (Dev) ----------------
// Allow Vite dev server (default: http://localhost:5173) to call this API.
// DEV CORS (React 5173)
builder.Services.AddCors(o => o.AddPolicy("Dev", p => p
    .WithOrigins("http://localhost:5173", "https://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .SetPreflightMaxAge(TimeSpan.FromHours(12))
));


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ---------------- ApiMessages localization wiring ----------------
using (var scope = app.Services.CreateScope())
{
    var loc = scope.ServiceProvider.GetRequiredService<IStringLocalizer<BasarSoft.SharedResources>>();
    ApiMessages.Configure(loc);
}

// ---------------- Request culture (EN) ----------------
var cultures = new[] { new CultureInfo("en") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = cultures,
    SupportedUICultures = cultures
});

// ---------------- Pipeline ----------------
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();   // keep HTTPS (e.g., https://localhost:7294)

app.UseCors("Dev");          // enable CORS for frontend dev origin

app.MapControllers();

app.Run();
