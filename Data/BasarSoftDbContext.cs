using BasarSoft.Entity;
using Microsoft.EntityFrameworkCore;

namespace BasarSoft.Data
{
    public sealed class BasarSoftDbContext : DbContext
    {
        public BasarSoftDbContext(DbContextOptions<BasarSoftDbContext> options)
            : base(options) { }

        // GeometryItem -> points tablosu
        public DbSet<GeometryItem> Points => Set<GeometryItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var e = modelBuilder.Entity<GeometryItem>();

            e.ToTable("points");

            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");

            e.Property(x => x.Name)
                .HasColumnName("name")
                .HasMaxLength(50)
                .IsRequired();

            e.Property(x => x.Type)
                .HasColumnName("type")
                .IsRequired();

            e.Property(x => x.WKT)
                .HasColumnName("wkt")
                .IsRequired();

            // NTS / Spatial kolon (PostGIS)
            e.Property(x => x.Shape)
                .HasColumnName("shape")
                .HasColumnType("geometry(Geometry,4326)");

            // İndeksler
            e.HasIndex(x => x.Name).IsUnique();
            e.HasIndex(x => x.Shape).HasMethod("GIST"); // spatial index

            // İsteğe bağlı check constraint:
            // e.ToTable(t => t.HasCheckConstraint("ck_points_type", "type IN (1,2,3)"));
        }
    }
}
