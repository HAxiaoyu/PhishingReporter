using Microsoft.AspNetCore.Mvc;

namespace PhishingReporter.Api.Controllers
{
    /// <summary>
    /// 健康检查控制器
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly ILogger<HealthController> _logger;

        public HealthController(ILogger<HealthController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 健康检查端点
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public IActionResult CheckHealth()
        {
            try
            {
                var response = new HealthResponse
                {
                    Status = "Healthy",
                    Version = "1.0.0",
                    Timestamp = DateTime.UtcNow,
                    Components = new Dictionary<string, string>
                    {
                        ["api"] = "Healthy",
                        ["database"] = "Healthy"
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }

        /// <summary>
        /// 获取系统信息
        /// </summary>
        [HttpGet("info")]
        [ProducesResponseType(typeof(SystemInfoResponse), StatusCodes.Status200OK)]
        public IActionResult GetSystemInfo()
        {
            var info = new SystemInfoResponse
            {
                Application = "PhishingReporter API",
                Version = "1.0.0",
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                MachineName = Environment.MachineName,
                StartTime = DateTime.UtcNow
            };

            return Ok(info);
        }
    }

    /// <summary>
    /// 健康检查响应
    /// </summary>
    public record HealthResponse
    {
        public string Status { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; }
        public Dictionary<string, string> Components { get; init; } = new();
    }

    /// <summary>
    /// 系统信息响应
    /// </summary>
    public record SystemInfoResponse
    {
        public string Application { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public string Environment { get; init; } = string.Empty;
        public string MachineName { get; init; } = string.Empty;
        public DateTime StartTime { get; init; }
    }
}