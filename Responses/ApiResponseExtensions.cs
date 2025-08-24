using Microsoft.AspNetCore.Mvc;

namespace BasarSoft.Responses
{
    public static class ApiResponseExtensions
    {
        // Map ApiResponse<T> to IActionResult based on StatusCode
        public static ActionResult ToActionResult<T>(this ControllerBase ctrl, ApiResponse<T> r)
            => r.StatusCode switch
            {
                200 => ctrl.Ok(r),
                201 => ctrl.Created(string.Empty, r),
                204 => ctrl.StatusCode(204, r),
                400 => ctrl.BadRequest(r),
                404 => ctrl.NotFound(r),
                409 => ctrl.Conflict(r),
                _ => ctrl.StatusCode(r.StatusCode, r)
            };
    }
}
