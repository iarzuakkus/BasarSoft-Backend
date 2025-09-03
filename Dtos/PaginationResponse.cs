namespace BasarSoft.Dtos
{
    public class PaginationResponse<T>
    {
        // The current page's records
        public List<T> Items { get; set; } = new();

        // Total number of records in DB (with or without filter)
        public int TotalCount { get; set; }

        // Total number of pages
        public int TotalPages { get; set; }

        // Current page number
        public int Page { get; set; }

        // Page size (items per page)
        public int PageSize { get; set; }
    }
}
