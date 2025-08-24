using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace BasarSoft.Data
{
    // EF CLI (dotnet ef migrations / update) sırasında kullanılır.
    public sealed class BasarSoftDbContextFactory : IDesignTimeDbContextFactory<BasarSoftDbContext>
    {
        public BasarSoftDbContext CreateDbContext(string[] args)
        {
            // Çalışma ortamını oku (Production varsayılan)
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

            // Proje kökü
            var basePath = Directory.GetCurrentDirectory();

            // appsettings.json + appsettings.{ENV}.json + UserSecrets + Env vars
            var config = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: false)
                .AddUserSecrets<BasarSoftDbContextFactory>(optional: true)
                .AddEnvironmentVariables()
                .Build();

            // TEK İSİM: DefaultConnection (Program.cs ile AYNI)
            var cs = config.GetConnectionString("DefaultConnection")
                     ?? "Host=127.0.0.1;Port=5432;Database=BasarSoftDb;Username=postgres;Password=postgres;SSL Mode=Disable";

            var options = new DbContextOptionsBuilder<BasarSoftDbContext>()
                .UseNpgsql(cs, o => o.UseNetTopologySuite())
                .Options;

            return new BasarSoftDbContext(options);
        }
    }
}
