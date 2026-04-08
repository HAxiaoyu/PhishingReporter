using System;
using System.IO;

namespace PhishingReporter.Services
{
    /// <summary>
    /// 日志服务实现
    /// </summary>
    public class Logger : ILogger
    {
        private readonly string _logFilePath;
        private readonly string _logLevel;

        public Logger(string logFilePath, string logLevel = "Info")
        {
            _logFilePath = logFilePath;
            _logLevel = logLevel;

            // 确保日志目录存在
            if (!string.IsNullOrEmpty(_logFilePath))
            {
                var directory = Path.GetDirectoryName(_logFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
        }

        public void Info(string message)
        {
            WriteLog("INFO", message);
        }

        public void Warning(string message)
        {
            if (_logLevel != "Error")
            {
                WriteLog("WARN", message);
            }
        }

        public void Error(string message)
        {
            WriteLog("ERROR", message);
        }

        public void Debug(string message)
        {
            if (_logLevel == "Debug")
            {
                WriteLog("DEBUG", message);
            }
        }

        private void WriteLog(string level, string message)
        {
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

            // 写入文件
            if (!string.IsNullOrEmpty(_logFilePath))
            {
                try
                {
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }
                catch
                {
                    // 日志写入失败时忽略
                }
            }

            // 同时输出到调试窗口
            System.Diagnostics.Debug.WriteLine(logEntry);
        }
    }
}