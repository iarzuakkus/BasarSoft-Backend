using NetTopologySuite.Geometries;
using System.Text.Json.Serialization;

namespace BasarSoft.Entity
{
    public sealed class GeometryItem
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";

        // 1=POINT, 2=LINESTRING, 3=POLYGON (map dictionary servis içinde tutulacak)
        public int Type { get; set; }

        // WKT metin temsili (ör: "POINT (30 10)")
        public string WKT { get; set; } = "";

        [JsonIgnore]
        public Geometry Shape { get; set; } = null!;
    }
}
