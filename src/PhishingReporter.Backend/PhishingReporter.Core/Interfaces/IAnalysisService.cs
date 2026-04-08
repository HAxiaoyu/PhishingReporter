using PhishingReporter.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace PhishingReporter.Core.Interfaces
{
    /// <summary>
    /// 钓鱼邮件分析服务接口
    /// </summary>
    public interface IAnalysisService
    {
        /// <summary>
        /// 分析钓鱼邮件特征
        /// </summary>
        Task<AnalysisOutcome> AnalyzeAsync(PhishingReport report, CancellationToken cancellationToken);
    }
}
