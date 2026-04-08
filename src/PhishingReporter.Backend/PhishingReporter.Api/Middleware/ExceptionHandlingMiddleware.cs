using System.Net;
using System.Text.Json;

namespace PhishingReporter.Api.Middleware
{
    /// <summary>
    /// 全局异常处理中间件
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
                _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var (statusCode, error, code) = GetErrorDetails(exception);
            context.Response.StatusCode = (int)statusCode;

            var response = new ErrorResponse(error, code);
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
        }

        private static (HttpStatusCode statusCode, string error, string code) GetErrorDetails(Exception exception)
        {
            return exception switch
            {
                ArgumentException argEx => (HttpStatusCode.BadRequest, argEx.Message, "ARGUMENT_ERROR"),
                InvalidOperationException opEx => (HttpStatusCode.BadRequest, opEx.Message, "OPERATION_ERROR"),
                UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized access", "UNAUTHORIZED"),
                TimeoutException => (HttpStatusCode.RequestTimeout, "Request timed out", "TIMEOUT"),
                _ => (HttpStatusCode.InternalServerError, "An internal server error occurred", "INTERNAL_ERROR")
            };
        }

        private record ErrorResponse(string Error, string Code);
    }

    /// <summary>
    /// 注册异常处理中间件
    /// </summary>
    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}