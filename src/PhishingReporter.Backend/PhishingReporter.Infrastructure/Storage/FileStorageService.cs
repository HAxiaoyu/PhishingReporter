using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhishingReporter.Core.Interfaces;

namespace PhishingReporter.Infrastructure.Storage
{
    /// <summary>
    /// 文件存储配置设置
    /// </summary>
    public class FileStorageSettings
    {
        public string BasePath { get; set; } = "./eml-storage";
        public long MaxFileSizeBytes { get; set; } = 52428800; // 50 MB
        public string[]? AllowedExtensions { get; set; }
    }

    /// <summary>
    /// 文件存储服务实现
    /// 用于保存和检索 EML 文件
    /// </summary>
    public class FileStorageService : IFileStorageService
    {
        private readonly FileStorageSettings _settings;
        private readonly ILogger<FileStorageService> _logger;
        private readonly string _basePath;

        public FileStorageService(
            IOptions<FileStorageSettings> settings,
            ILogger<FileStorageService> logger)
        {
            _settings = settings.Value;
            _logger = logger;

            // 确保基础路径是绝对路径
            _basePath = Path.IsPathRooted(_settings.BasePath)
                ? _settings.BasePath
                : Path.Combine(Directory.GetCurrentDirectory(), _settings.BasePath);

            // 确保目录存在
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
                _logger.LogInformation("Created storage directory: {BasePath}", _basePath);
            }
        }

        public async Task<string> SaveEmlAsync(Guid reportId, byte[] content, CancellationToken cancellationToken)
        {
            try
            {
                // 验证文件大小
                if (content.Length > _settings.MaxFileSizeBytes)
                {
                    throw new InvalidOperationException($"File size ({content.Length} bytes) exceeds maximum allowed ({_settings.MaxFileSizeBytes} bytes)");
                }

                // 按日期分目录存储
                var dateFolder = DateTime.UtcNow.ToString("yyyy-MM");
                var folderPath = Path.Combine(_basePath, dateFolder);

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                // 生成文件名
                var fileName = $"{reportId:N}.eml";
                var filePath = Path.Combine(folderPath, fileName);

                // 写入文件
                await File.WriteAllBytesAsync(filePath, content, cancellationToken);

                _logger.LogInformation(
                    "Saved EML file for report {ReportId} to {FilePath}",
                    reportId,
                    filePath
                );

                // 返回相对路径（用于数据库存储）
                return Path.Combine(dateFolder, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save EML file for report {ReportId}", reportId);
                throw;
            }
        }

        public async Task<byte[]?> GetEmlAsync(string filePath, CancellationToken cancellationToken)
        {
            try
            {
                var fullPath = Path.Combine(_basePath, filePath);

                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("EML file not found: {FilePath}", fullPath);
                    return null;
                }

                var content = await File.ReadAllBytesAsync(fullPath, cancellationToken);
                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read EML file: {FilePath}", filePath);
                return null;
            }
        }

        public async Task DeleteEmlAsync(string filePath)
        {
            try
            {
                var fullPath = Path.Combine(_basePath, filePath);

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogInformation("Deleted EML file: {FilePath}", fullPath);
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete EML file: {FilePath}", filePath);
            }
        }

        /// <summary>
        /// 获取存储统计信息
        /// </summary>
        public StorageStatistics GetStatistics()
        {
            var stats = new StorageStatistics();

            try
            {
                var files = Directory.GetFiles(_basePath, "*.eml", SearchOption.AllDirectories);
                stats.FileCount = files.Length;

                long totalSize = 0;
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    totalSize += fileInfo.Length;
                }
                stats.TotalSizeBytes = totalSize;

                // 获取磁盘可用空间
                var driveInfo = new DriveInfo(Path.GetPathRoot(_basePath) ?? "C:");
                stats.AvailableSpaceBytes = driveInfo.AvailableFreeSpace;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get storage statistics");
            }

            return stats;
        }
    }

    /// <summary>
    /// 存储统计信息
    /// </summary>
    public class StorageStatistics
    {
        public int FileCount { get; set; }
        public long TotalSizeBytes { get; set; }
        public long AvailableSpaceBytes { get; set; }

        public string TotalSizeFormatted => FormatSize(TotalSizeBytes);
        public string AvailableSpaceFormatted => FormatSize(AvailableSpaceBytes);

        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }
    }
}