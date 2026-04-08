using System;
using System.Configuration;
using System.IO;
using PhishingReporter.Models;

namespace PhishingReporter.Services
{
    /// <summary>
    /// 配置管理实现
    /// 从 App.config 或注册表读取配置
    /// </summary>
    public class ConfigManager : IConfigManager
    {
        private readonly AppSettings _settings;

        public ConfigManager()
        {
            _settings = LoadSettings();
        }

        public string ApiBaseUrl => _settings.ApiBaseUrl;
        public string ApiKey => _settings.ApiKey;
        public int RequestTimeoutSeconds => _settings.RequestTimeoutSeconds;
        public string LogFilePath => _settings.LogFilePath;
        public bool EnableAutoArchive => _settings.EnableAutoArchive;
        public string ArchiveFolderPath => _settings.ArchiveFolderPath;
        public string LogLevel => _settings.LogLevel;

        private AppSettings LoadSettings()
        {
            var settings = new AppSettings();

            try
            {
                // 从 App.config 读取（VSTO 项目使用）
                var appSettings = ConfigurationManager.AppSettings;

                if (appSettings["ApiBaseUrl"] != null)
                {
                    settings.ApiBaseUrl = appSettings["ApiBaseUrl"];
                }

                if (appSettings["ApiKey"] != null)
                {
                    settings.ApiKey = appSettings["ApiKey"];
                }

                if (appSettings["RequestTimeoutSeconds"] != null)
                {
                    int.TryParse(appSettings["RequestTimeoutSeconds"], out var timeout);
                    settings.RequestTimeoutSeconds = timeout > 0 ? timeout : 30;
                }

                if (appSettings["LogFilePath"] != null)
                {
                    settings.LogFilePath = appSettings["LogFilePath"];
                }
                else
                {
                    // 默认日志路径
                    settings.LogFilePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "PhishingReporter",
                        "logs",
                        $"log_{DateTime.Now:yyyyMMdd}.txt"
                    );
                }

                if (appSettings["LogLevel"] != null)
                {
                    settings.LogLevel = appSettings["LogLevel"];
                }

                if (appSettings["EnableAutoArchive"] != null)
                {
                    bool.TryParse(appSettings["EnableAutoArchive"], out var enable);
                    settings.EnableAutoArchive = enable;
                }

                if (appSettings["ArchiveFolderPath"] != null)
                {
                    settings.ArchiveFolderPath = appSettings["ArchiveFolderPath"];
                }
            }
            catch (Exception ex)
            {
                // 配置读取失败时使用默认值
                System.Diagnostics.Debug.WriteLine($"Config load failed: {ex.Message}");
            }

            return settings;
        }

        /// <summary>
        /// 保存配置到 App.config（管理员功能）
        /// </summary>
        public void SaveSettings(AppSettings newSettings)
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

                config.AppSettings.Settings["ApiBaseUrl"].Value = newSettings.ApiBaseUrl;
                config.AppSettings.Settings["ApiKey"].Value = newSettings.ApiKey ?? string.Empty;

                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");

                _settings.ApiBaseUrl = newSettings.ApiBaseUrl;
                _settings.ApiKey = newSettings.ApiKey;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save settings: {ex.Message}", ex);
            }
        }
    }
}