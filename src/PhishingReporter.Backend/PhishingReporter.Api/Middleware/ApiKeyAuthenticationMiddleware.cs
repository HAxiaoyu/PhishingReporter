using Microsoft.Extensions.Options;

namespace PhishingReporter.Api.Middleware
{
    /// <summary>
    /// API 设置
    /// </summary>
    public class ApiSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public int RateLimitPerMinute { get; set; } = 60;
        public int MaxRequestSizeBytes { get; set; } = 10485760;
    }

    /// <summary>
    /// API Key 认证中间件
    /// </summary>
    public class ApiKeyAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;
        private readonly ApiSettings _settings;

        private const string ApiKeyHeaderName = "X-API-Key";

        public ApiKeyAuthenticationMiddleware(
            RequestDelegate next,
            ILogger<ApiKeyAuthenticationMiddleware> logger,
            IOptions<ApiSettings> settings)
        {
            _next = next;
            _logger = logger;
            _settings = settings.Value;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // 跳过健康检查和 Swagger 端点
            if (IsExcludedPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            // 如果 API Key 未配置，跳过验证（开发环境）
            if (string.IsNullOrEmpty(_settings.ApiKey))
            {
                _logger.LogWarning("API key not configured, skipping authentication");
                await _next(context);
                return;
            }

            // 验证 API Key
            if (!ValidateApiKey(context))
            {
                _logger.LogWarning("Invalid or missing API key from {IpAddress}",
                    GetClientIpAddress(context));

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsync("{\"error\":\"Invalid or missing API key\",\"code\":\"UNAUTHORIZED\"}");
                return;
            }

            await _next(context);
        }

        /// <summary>
        /// 检查路径是否排除认证
        /// </summary>
        private static bool IsExcludedPath(PathString path)
        {
            return path.StartsWithSegments("/api/v1/health")
                || path.StartsWithSegments("/swagger")
                || path.Value == "/";
        }

        /// <summary>
        /// 验证 API Key - 仅支持 Header 方式
        /// </summary>
        private bool ValidateApiKey(HttpContext context)
        {
            // 安全考虑：仅从 Header 获取 API Key，不支持 Query String
            // Query String 方式会暴露 API Key 在日志和浏览器历史中
            if (context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var headerKey))
            {
                return headerKey.ToString() == _settings.ApiKey;
            }

            return false;
        }

        /// <summary>
        /// 获取客户端 IP 地址
        /// </summary>
        private static string GetClientIpAddress(HttpContext context)
        {
            // 检查代理头
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }
    }

    /// <summary>
    /// 注册 API Key 认证中间件
    /// </summary>
    public static class ApiKeyAuthenticationMiddlewareExtensions
    {
        public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ApiKeyAuthenticationMiddleware>();
        }
    }
}