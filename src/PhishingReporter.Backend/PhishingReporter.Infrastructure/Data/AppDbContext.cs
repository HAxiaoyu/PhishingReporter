using Microsoft.EntityFrameworkCore;
using PhishingReporter.Core.Models;

namespace PhishingReporter.Infrastructure.Data
{
    /// <summary>
    /// 应用数据库上下文
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // DbSets
        public DbSet<PhishingReport> PhishingReports { get; set; }
        public DbSet<EmailHeader> EmailHeaders { get; set; }
        public DbSet<EmailAttachment> EmailAttachments { get; set; }
        public DbSet<AnalysisResult> AnalysisResults { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // PhishingReport 配置
            modelBuilder.Entity<PhishingReport>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ReportedBy);
                entity.HasIndex(e => e.SenderEmail);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.ReportedAt);
                entity.HasIndex(e => new { e.Status, e.ReportedAt });
            });

            // EmailHeader 配置
            modelBuilder.Entity<EmailHeader>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ReportId);
                entity.HasIndex(e => e.HeaderName);
            });

            // EmailAttachment 配置
            modelBuilder.Entity<EmailAttachment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ReportId);
                entity.HasIndex(e => e.FileHash);
            });

            // AnalysisResult 配置
            modelBuilder.Entity<AnalysisResult>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ReportId);
            });
        }

        /// <summary>
        /// 保存更改时自动更新时间戳
        /// </summary>
        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries<PhishingReport>();

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                }
            }
        }
    }
}