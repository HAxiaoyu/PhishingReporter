namespace PhishingReporter.Core.Interfaces
{
    /// <summary>
    /// 文件存储服务接口
    /// </summary>
    public interface IFileStorageService
    {
        /// <summary>
        /// 保存 EML 文件
        /// </summary>
        Task<string> SaveEmlAsync(Guid reportId, byte[] content, CancellationToken cancellationToken);

        /// <summary>
        /// 获取 EML 文件
        /// </summary>
        Task<byte[]?> GetEmlAsync(string filePath, CancellationToken cancellationToken);

        /// <summary>
        /// 删除 EML 文件
        /// </summary>
        Task DeleteEmlAsync(string filePath);
    }
}