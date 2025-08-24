// Validation/GeometryValidator.cs
using System.Linq;
using System.Text.RegularExpressions;
using BasarSoft.Dtos;

namespace BasarSoft.Validation
{
    public static class GeometryValidator
    {
        static readonly Regex rxName = new(@"^[A-Za-z0-9 _\-]{3,50}$");
        static readonly Regex rxLineInner = new(@"(?i)LINESTRING\s*\(\s*([^\)]+)\s*\)\s*$");
        static readonly Regex rxPolyInner = new(@"(?i)POLYGON\s*\(\s*\(\s*([^\)]+)\s*\)\s*\)\s*$");

        public sealed class Result
        {
            public bool Success { get; init; }
            public string? Message { get; init; }
            public string? Wkt { get; init; }
            public static Result Ok(string wkt) => new() { Success = true, Wkt = wkt };
            public static Result Fail(string msg) => new() { Success = false, Message = msg };
        }

        public static Result NormalizeAndValidate(GeometryDto dto)
        {
            if (dto is null) return Result.Fail("Payload is required.");
            if (string.IsNullOrWhiteSpace(dto.Name)) return Result.Fail("Name is required.");
            if (string.IsNullOrWhiteSpace(dto.Wkt)) return Result.Fail("WKT is required.");
            if (dto.Type is < 1 or > 3) return Result.Fail("Type must be 1=POINT, 2=LINESTRING, 3=POLYGON.");
            if (!rxName.IsMatch(dto.Name)) return Result.Fail("Name must be 3–50 chars.");

            var normalized = NormalizeWkt(dto.Type, dto.Wkt);
            var u = normalized.TrimStart().ToUpperInvariant();
            if (dto.Type == 1 && !u.StartsWith("POINT")) return Result.Fail("WKT must start with POINT.");
            if (dto.Type == 2 && !u.StartsWith("LINESTRING")) return Result.Fail("WKT must start with LINESTRING.");
            if (dto.Type == 3 && !u.StartsWith("POLYGON")) return Result.Fail("WKT must start with POLYGON.");

            if (dto.Type == 3 && !IsPolygonRingClosed(normalized, out var err))
                return Result.Fail(err ?? "Polygon ring must be closed.");

            return Result.Ok(normalized);
        }

        private static string NormalizeWkt(int type, string raw)
        {
            raw = raw.Trim();
            var u = raw.ToUpperInvariant();
            var hasType = u.StartsWith("POINT") || u.StartsWith("LINESTRING") || u.StartsWith("POLYGON");

            if (hasType)
            {
                if (u.StartsWith("LINESTRING"))
                {
                    var inner = rxLineInner.Match(raw).Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(inner) && !inner.Contains(",")) return FixLineString(inner);
                    return raw;
                }
                if (u.StartsWith("POLYGON"))
                {
                    var inner = rxPolyInner.Match(raw).Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(inner) && !inner.Contains(",")) return FixPolygon(inner);
                    return raw;
                }
                return raw; // POINT
            }

            return type switch
            {
                1 => raw.StartsWith("(") ? $"POINT {raw}" : $"POINT ({raw})",
                2 => raw.StartsWith("(") ? FixLineString(raw.Trim()) : FixLineString($"({raw})"),
                3 => FixPolygon(raw.Trim(' ', '\t', '(', ')')),
                _ => raw
            };
        }

        private static string FixLineString(string body)
        {
            var nums = Regex.Matches(body, @"-?\d+(\.\d+)?");
            var pairs = new List<string>();
            for (int i = 0; i + 1 < nums.Count; i += 2)
                pairs.Add($"{nums[i].Value} {nums[i + 1].Value}");
            return $"LINESTRING ({string.Join(", ", pairs)})";
        }

        private static string FixPolygon(string body)
        {
            var nums = Regex.Matches(body, @"-?\d+(\.\d+)?");
            var pairs = new List<string>();
            for (int i = 0; i + 1 < nums.Count; i += 2)
                pairs.Add($"{nums[i].Value} {nums[i + 1].Value}");
            return $"POLYGON (({string.Join(", ", pairs)}))";
        }

        private static bool IsPolygonRingClosed(string normalizedPolygonWkt, out string? error)
        {
            error = null;
            var m = rxPolyInner.Match(normalizedPolygonWkt);
            if (!m.Success) { error = "Polygon body not recognized."; return false; }

            var parts = m.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 4) { error = "Polygon needs at least 4 points."; return false; }

            static (string x, string y) SplitPair(string s)
            {
                var p = Regex.Split(s.Trim(), @"\s+");
                return (p[0], p.Length > 1 ? p[1] : "");
            }

            var (fx, fy) = SplitPair(parts.First());
            var (lx, ly) = SplitPair(parts.Last());
            if (fx != lx || fy != ly) { error = "First and last points must match."; return false; }

            return true;
        }
    }
}
