namespace BasarSoft.Responses
{
    // Uniform API response envelope
    public sealed class ApiResponse<T>
    {
        public bool Success { get; init; }
        public string Message { get; init; } = "";
        public T? Data { get; init; }
        public int StatusCode { get; init; } = 200;

        // 200
        public static ApiResponse<T> Ok(T? data = default, string message = "Success", int statusCode = 200)
            => new() { Success = true, Message = message, Data = data, StatusCode = statusCode };

        // 201
        public static ApiResponse<T> Created(T data, string message = "Created")
            => new() { Success = true, Message = message, Data = data, StatusCode = 201 };

        // 204
        public static ApiResponse<T> NoContent(string message = "No Content")
            => new() { Success = true, Message = message, Data = default, StatusCode = 204 };

        // 400 (varsayılan), istenirse başka kod verilebilir
        public static ApiResponse<T> Fail(string message, int statusCode = 400)
            => new() { Success = false, Message = message, StatusCode = statusCode };

        // kısayollar
        public static ApiResponse<T> NotFound(string message = "Not found")
            => Fail(message, 404);

        public static ApiResponse<T> Conflict(string message = "Conflict")
            => Fail(message, 409);
    }
}
