namespace BasarSoft.Dtos
{
    public sealed class GeometryDto
    {
        public string Name { get; set; } = string.Empty;
        public int Type { get; set; }
        public string Wkt { get; set; } = string.Empty;
    }
}
