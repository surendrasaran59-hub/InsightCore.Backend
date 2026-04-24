using Microsoft.AspNetCore.Mvc;

namespace InsightCore.Api.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception occurred");

                var statusCode = ex switch
                {
                    KeyNotFoundException => StatusCodes.Status404NotFound,
                    UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                    ArgumentException => StatusCodes.Status400BadRequest,
                    _ => StatusCodes.Status500InternalServerError
                };

                var problem = new ProblemDetails
                {
                    Title = "An unexpected error occurred",
                    Status = statusCode,
                    Detail = ex.Message,
                    Instance = context.Request.Path
                };

                context.Response.StatusCode = problem.Status.Value;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsJsonAsync(problem);
            }
        }
    }
}