using BasarSoft.Entity;
using Microsoft.EntityFrameworkCore;

namespace BasarSoft.Data
{
    public sealed class BasarSoftDbContext : DbContext
    {
        public BasarSoftDbContext(DbContextOptions<BasarSoftDbContext> options) : base(options) { }

        public DbSet<GeometryItem> Points => Set<GeometryItem>();

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.HasPostgresExtension("postgis");

            var e = mb.Entity<GeometryItem>();
            e.ToTable("points");

            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");

            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
            e.Property(x => x.Type).HasColumnName("type").IsRequired();

            // SRID'i tip içinde veriyoruz -> HasSrid'e gerek yok
            e.Property(x => x.Geo)
             .HasColumnName("wkt")
             .HasColumnType("geometry(Geometry,4326)")
             .IsRequired();

            e.HasIndex(x => x.Name).IsUnique();
            e.HasIndex(x => x.Geo).HasMethod("GIST");
        }
    }
}
