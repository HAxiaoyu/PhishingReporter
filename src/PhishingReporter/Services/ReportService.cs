using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PhishingReporter.Models;

namespace PhishingReporter.Services
{
    /// <summary>
    /// 上报结果
    /// </summary>
    public class ReportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public Guid? ReportId { get; set; }
        public string ErrorCode { get; set; }
    }

    /// <summary>
    /// 上报服务接口
    /// </summary>
    public interface IReportService
    {
        /// <summary>
        /// 上报钓鱼邮件到后端服务器
        /// </summary>
        Task<ReportResult> SubmitReportAsync(PhishingReport report, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 上报服务实现 - HTTP 提交到后端 API
    /// </summary>
    public class ReportService : IReportService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfigManager _config;
        private readonly ILogger _logger;

        public ReportService(IConfigManager config, ILogger logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(_config.RequestTimeoutSeconds)
            };

            // 添加认证头
            if (!string.IsNullOrEmpty(_config.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-API-Key", _config.ApiKey);
            }

            // 添加用户代理标识
            _httpClient.DefaultRequestHeaders.Add("X-Client-Version", "1.0.0");
        }

        /// <summary>
        /// 上报钓鱼邮件到后端 API
        /// </summary>
        public async Task<ReportResult> SubmitReportAsync(PhishingReport report, CancellationToken cancellationToken = default)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            try
            {
                _logger.Info($"Submitting report for message: {report.MessageId}");
                _logger.Debug($"API URL: {_config.ApiBaseUrl}/api/v1/reports");

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };

                var jsonPayload = JsonSerializer.Serialize(report, jsonOptions);
                _logger.Debug($"Payload size: {jsonPayload.Length} bytes");

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_config.ApiBaseUrl}/api/v1/reports",
                    content,
                    cancellationToken
                );

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse>(responseContent, jsonOptions);

                    _logger.Info($"Report submitted successfully. Report ID: {apiResponse?.Data?.ReportId}");

                    return new ReportResult
                    {
                        Success = true,
                        Message = apiResponse?.Data?.Message ?? "上报成功！感谢您的参与。",
                        ReportId = apiResponse?.Data?.ReportId
                    };
                }
                else
                {
                    _logger.Error($"Report submission failed. Status: {response.StatusCode}, Response: {responseContent}");

                    var errorResponse = JsonSerializer.Deserialize<ApiResponse>(responseContent, jsonOptions);

                    return new ReportResult
                    {
                        Success = false,
                        Message = errorResponse?.Error ?? $"上报失败: {response.ReasonPhrase}",
                        ErrorCode = ((int)response.StatusCode).ToString()
                    };
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.Error($"Network error during report submission: {ex.Message}");
                return new ReportResult
                {
                    Success = false,
                    Message = "网络错误，无法连接到服务器。请检查网络连接。",
                    ErrorCode = "NETWORK_ERROR"
                };
            }
            catch (TaskCanceledException)
            {
                _logger.Error("Report submission timed out");
                return new ReportResult
                {
                    Success = false,
                    Message = "请求超时，请稍后重试。",
                    ErrorCode = "TIMEOUT"
                };
            }
            catch (JsonException ex)
            {
                _logger.Error($"JSON serialization error: {ex.Message}");
                return new ReportResult
                {
                    Success = false,
                    Message = "数据格式错误，无法处理邮件内容。",
                    ErrorCode = "JSON_ERROR"
                };
            }
            catch (Exception ex)
            {
                _logger.Error($"Unexpected error during report submission: {ex.Message}");
                return new ReportResult
                {
                    Success = false,
                    Message = $"发生未知错误: {ex.Message}",
                    ErrorCode = "UNKNOWN_ERROR"
                };
            }
        }

        /// <summary>
        /// 检查后端服务是否可用
        /// </summary>
        public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"{_config.ApiBaseUrl}/api/v1/health",
                    cancellationToken
                );

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // API 响应模型
        private class ApiResponse
        {
            public bool Success { get; set; }
            public ApiData Data { get; set; }
            public string Error { get; set; }
        }

        private class ApiData
        {
            public Guid? ReportId { get; set; }
            public string Message { get; set; }
        }
    }
}