namespace PhishingReporter.Services
{
    /// <summary>
    /// 日志服务接口
    /// </summary>
    public interface ILogger
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message);
        void Debug(string message);
    }
}