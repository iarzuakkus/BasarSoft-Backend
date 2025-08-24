using System.Text.RegularExpressions;
using BasarSoft.Dtos;

namespace BasarSoft.Validation
{
    /// <summary>
    /// Lightweight input validator + WKT normalizer.
    /// - Ensures required fields (Name, Type, WKT)
    /// - Ensures header/type consistency (POINT/LINESTRING/POLYGON)
    /// - Normalizes user WKT:
    ///     * Adds missing type keyword (based on dto.Type)
    ///     * Inserts commas for LINESTRING/POLYGON if user omitted them
    /// NOTE: Geometric/topologic checks (IsValid, ring closure, etc.) are left to NTS in the service.
    /// </summary>
    public static class GeometryValidator
    {
        // Optional: basic name rule (you already use a similar one in service)
        static readonly Regex rxName = new(@"^[A-Za-z0-9 _\-]{3,50}$");

        // Extract inner content helpers (loose, case-insensitive)
        static readonly Regex rxLineInner = new(@"(?i)LINESTRING\s*\(\s*([^\)]+)\s*\)\s*$");
        static readonly Regex rxPolyInner = new(@"(?i)POLYGON\s*\(\s*\(\s*([^\)]+)\s*\)\s*\)\s*$");

        public sealed class Result
        {
            public bool Success { get; init; }
            public string? Message { get; init; }
            /// <summary>Normalized WKT (only when Success=true)</summary>
            public string? Wkt { get; init; }

            public static Result Ok(string wkt) => new() { Success = true, Wkt = wkt };
            public static Result Fail(string msg) => new() { Success = false, Message = msg };
        }

        public static Result NormalizeAndValidate(GeometryDto dto)
        {
            // 1) Requireds
            if (dto is null) return Result.Fail("Payload is required.");
            if (string.IsNullOrWhiteSpace(dto.Name)) return Result.Fail("Name is required.");
            if (string.IsNullOrWhiteSpace(dto.WKT)) return Result.Fail("WKT is required.");
            if (dto.Type is < 1 or > 3) return Result.Fail("Type must be 1=POINT, 2=LINESTRING, 3=POLYGON.");

            // 2) Name pattern (optional but helpful)
            if (!rxName.IsMatch(dto.Name))
                return Result.Fail("Name must be 3–50 chars (letters/digits/space/_/-).");

            // 3) Normalize WKT (add header + insert commas if missing)
            var normalized = NormalizeWkt(dto.Type, dto.WKT);

            // 4) Header ↔ type check after normalization (should match now)
            var u = normalized.TrimStart().ToUpperInvariant();
            if (dto.Type == 1 && !u.StartsWith("POINT")) return Result.Fail("WKT must start with POINT for type=1.");
            if (dto.Type == 2 && !u.StartsWith("LINESTRING")) return Result.Fail("WKT must start with LINESTRING for type=2.");
            if (dto.Type == 3 && !u.StartsWith("POLYGON")) return Result.Fail("WKT must start with POLYGON for type=3.");

            // No topology checks here; NTS will validate later.
            return Result.Ok(normalized);
        }

        // =========================
        // Normalization helpers
        // =========================
        private static string NormalizeWkt(int type, string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;
            raw = raw.Trim();

            var u = raw.ToUpperInvariant();
            var hasType = u.StartsWith("POINT") || u.StartsWith("LINESTRING") || u.StartsWith("POLYGON");

            // If has header, maybe repair commas for LINESTRING/POLYGON
            if (hasType)
            {
                if (u.StartsWith("LINESTRING"))
                {
                    var inner = rxLineInner.Match(raw).Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(inner) && !inner.Contains(","))
                        return FixLineString(inner);
                    return raw;
                }
                if (u.StartsWith("POLYGON"))
                {
                    var inner = rxPolyInner.Match(raw).Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(inner) && !inner.Contains(","))
                        return FixPolygon(inner);
                    return raw;
                }
                return raw; // POINT or already with commas
            }

            // Missing header → add based on 'type', also fix commas if needed
            return type switch
            {
                1 => raw.StartsWith("(") ? $"POINT {raw}" : $"POINT ({raw})",
                2 => raw.StartsWith("(") ? FixLineString(raw.Trim(' ', '\t')) : FixLineString($"({raw})"),
                3 => raw.StartsWith("((")
                        ? FixPolygon(raw.Trim(' ', '\t', '(', ')').Trim())
                        : FixPolygon(raw.Trim(' ', '\t', '(', ')').Trim()),
                _ => raw
            };
        }

        private static string FixLineString(string body)
        {
            // Accept "x y x y ..." and convert to "LINESTRING (x y, x y, ...)"
            var nums = Regex.Matches(body, @"-?\d+(\.\d+)?");
            var pairs = new List<string>();
            for (int i = 0; i + 1 < nums.Count; i += 2)
                pairs.Add($"{nums[i].Value} {nums[i + 1].Value}");
            return $"LINESTRING ({string.Join(", ", pairs)})";
        }

        private static string FixPolygon(string body)
        {
            // Accept "x y x y ..." and convert to "POLYGON ((x y, x y, ...))"
            var nums = Regex.Matches(body, @"-?\d+(\.\d+)?");
            var pairs = new List<string>();
            for (int i = 0; i + 1 < nums.Count; i += 2)
                pairs.Add($"{nums[i].Value} {nums[i + 1].Value}");
            return $"POLYGON (({string.Join(", ", pairs)}))";
        }
    }
}
