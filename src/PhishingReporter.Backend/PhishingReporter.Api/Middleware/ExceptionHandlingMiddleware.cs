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

            var response = new ErrorResponse();
            var statusCode = HttpStatusCode.InternalServerError;

            switch (exception)
            {
                case ArgumentException argEx:
                    statusCode = HttpStatusCode.BadRequest;
                    response.Error = argEx.Message;
                    response.Code = "ARGUMENT_ERROR";
                    break;

                case InvalidOperationException opEx:
                    statusCode = HttpStatusCode.BadRequest;
                    response.Error = opEx.Message;
                    response.Code = "OPERATION_ERROR";
                    break;

                case UnauthorizedAccessException:
                    statusCode = HttpStatusCode.Unauthorized;
                    response.Error = "Unauthorized access";
                    response.Code = "UNAUTHORIZED";
                    break;

                case TimeoutException:
                    statusCode = HttpStatusCode.RequestTimeout;
                    response.Error = "Request timed out";
                    response.Code = "TIMEOUT";
                    break;

                default:
                    statusCode = HttpStatusCode.InternalServerError;
                    response.Error = "An internal server error occurred";
                    response.Code = "INTERNAL_ERROR";
                    break;
            }

            context.Response.StatusCode = (int)statusCode;

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
        }

        private record ErrorResponse
        {
            public string Error { get; init; } = string.Empty;
            public string Code { get; init; } = string.Empty;
        }
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