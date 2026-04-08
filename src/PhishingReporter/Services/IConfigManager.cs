namespace PhishingReporter.Services
{
    /// <summary>
    /// 配置管理接口
    /// </summary>
    public interface IConfigManager
    {
        string ApiBaseUrl { get; }
        string ApiKey { get; }
        int RequestTimeoutSeconds { get; }
        string LogFilePath { get; }
        bool EnableAutoArchive { get; }
        string ArchiveFolderPath { get; }
        string LogLevel { get; }
    }
}