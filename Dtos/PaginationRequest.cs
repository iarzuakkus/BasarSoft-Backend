namespace BasarSoft.Dtos
{
    public class PaginationRequest
    {
        // Page number (e.g., 1, 2, 3…)
        public int Page { get; set; } = 1;

        // How many items per page
        public int PageSize { get; set; } = 10;

        // Optional search term
        public string? Search { get; set; }
    }
}
