using NetTopologySuite.Geometries;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BasarSoft.Entity
{
    public sealed class GeometryItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Type { get; set; }

        [Column("wkt", TypeName = "geometry")]
        [JsonIgnore]
        public Geometry Geo { get; set; } = null!;

        [NotMapped]
        public string Wkt { get; set; } = string.Empty;
    }
}
