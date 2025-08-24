namespace BasarSoft.Responses
{
    // Uniform API response envelope
    public sealed class ApiResponse<T>
    {
        public bool Success { get; init; }
        public string Message { get; init; } = "";
        public T? Data { get; init; }
        public int StatusCode { get; init; } = 200;

        // ==== existing (keeps working with raw strings) ====
        public static ApiResponse<T> Ok(T? data = default, string message = "Success", int statusCode = 200)
            => new() { Success = true, Message = message, Data = data, StatusCode = statusCode };

        public static ApiResponse<T> Created(T data, string message = "Created")
            => new() { Success = true, Message = message, Data = data, StatusCode = 201 };

        public static ApiResponse<T> NoContent(string message = "No Content")
            => new() { Success = true, Message = message, Data = default, StatusCode = 204 };

        public static ApiResponse<T> Fail(string message, int statusCode = 400)
            => new() { Success = false, Message = message, StatusCode = statusCode };

        public static ApiResponse<T> NotFound(string message = "Not found") => Fail(message, 404);
        public static ApiResponse<T> Conflict(string message = "Conflict") => Fail(message, 409);

        // ==== NEW: key-based helpers (messages from Resources) ====
        // 200
        public static ApiResponse<T> OkKey(T? data = default, string key = "success.ok", int statusCode = 200)
            => new() { Success = true, Message = ApiMessages.Get(key), Data = data, StatusCode = statusCode };

        // 201
        public static ApiResponse<T> CreatedKey(T data, string key = "success.created")
            => new() { Success = true, Message = ApiMessages.Get(key), Data = data, StatusCode = 201 };

        // 204
        public static ApiResponse<T> NoContentKey(string key = "success.nocontent")
            => new() { Success = true, Message = ApiMessages.Get(key), Data = default, StatusCode = 204 };

        // 4xx/5xx
        public static ApiResponse<T> FailKey(string key, int statusCode = 400)
            => new() { Success = false, Message = ApiMessages.Get(key), StatusCode = statusCode };

        public static ApiResponse<T> NotFoundKey(string key = "error.notfound") => FailKey(key, 404);
        public static ApiResponse<T> ConflictKey(string key = "error.conflict") => FailKey(key, 409);
    }
}
