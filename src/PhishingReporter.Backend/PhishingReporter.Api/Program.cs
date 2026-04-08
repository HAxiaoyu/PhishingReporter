using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PhishingReporter.Api.Middleware;
using PhishingReporter.Core.Interfaces;
using PhishingReporter.Core.Models;
using PhishingReporter.Core.Services;
using PhishingReporter.Infrastructure.Data;
using PhishingReporter.Infrastructure.Data.Repositories;
using PhishingReporter.Infrastructure.Exchange;
using PhishingReporter.Infrastructure.Storage;
using PhishingReporter.Infrastructure.Notification;
using PhishingReporter.Infrastructure.MockServices;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// =====================================================
// 服务配置
// =====================================================

// 添加控制器
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// 添加 API 文档
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "钓鱼邮件上报系统 API",
        Version = "v1",
        Description = "企业级钓鱼邮件上报系统的后端 API 服务"
    });
});

// 配置数据库 - 支持多种数据库提供者
var dbProvider = builder.Configuration.GetConnectionString("Provider") ?? "SqlServer";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("数据库连接字符串未配置");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    switch (dbProvider.ToLowerInvariant())
    {
        case "sqlite":
            // SQLite 测试环境使用文件数据库以确保持久性
            var sqliteConnStr = connectionString ?? "Data Source=test.db";
            options.UseSqlite(sqliteConnStr);
            Console.WriteLine($"使用 SQLite 数据库: {sqliteConnStr}");
            break;

        case "sqlserver":
        default:
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                sqlOptions.CommandTimeout(30);
            });
            Console.WriteLine("使用 SQL Server 数据库");
            break;
    }
});

// 注册仓储
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IAnalysisService, AnalysisService>();

// 配置 Exchange 存档服务 - 支持模拟
var useMockExchange = builder.Configuration.GetValue<bool>("Exchange:UseMockService");
if (useMockExchange)
{
    builder.Services.AddScoped<IEmailArchiveService, MockEmailArchiveService>();
    Console.WriteLine("使用模拟 Exchange 服务");
}
else
{
    builder.Services.AddScoped<IEmailArchiveService, EmailArchiveService>();
}

// 配置通知服务 - 支持模拟
var useMockNotification = builder.Configuration.GetValue<bool>("Notification:UseMockService");
if (useMockNotification)
{
    builder.Services.AddScoped<INotificationService, MockNotificationService>();
    Console.WriteLine("使用模拟通知服务");
}
else
{
    builder.Services.AddScoped<INotificationService, NotificationService>();
}

// 注册核心报告服务
builder.Services.AddScoped<IReportService, ReportService>();

// 配置设置
builder.Services.Configure<ExchangeSettings>(builder.Configuration.GetSection("Exchange"));
builder.Services.Configure<FileStorageSettings>(builder.Configuration.GetSection("FileStorage"));
builder.Services.Configure<NotificationSettings>(builder.Configuration.GetSection("Notification"));
builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));

// CORS 配置
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .WithMethods("GET", "POST", "PATCH", "DELETE")
                  .WithHeaders("Content-Type", "X-API-Key", "Authorization");
        }
        else
        {
            policy.WithOrigins("http://localhost:3000", "http://localhost:5173", "https://localhost:5001")
                  .WithMethods("GET", "POST", "PATCH", "DELETE")
                  .WithHeaders("Content-Type", "X-API-Key", "Authorization");
        }
    });
});

// 速率限制配置
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("ApiPolicy", limiterOptions =>
    {
        limiterOptions.PermitLimit = 60;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 10;
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"error\":\"请求过于频繁，请稍后再试\",\"code\":\"RATE_LIMITED\"}",
            cancellationToken
        );
    };
});

// 健康检查
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

// =====================================================
// 应用配置
// =====================================================

var app = builder.Build();

// 开发/测试环境配置
if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Test")
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "钓鱼上报 API v1");
        options.RoutePrefix = "swagger";
    });
}

// HTTPS 重定向和 HSTS（生产环境）
if (!app.Environment.IsDevelopment() && app.Environment.EnvironmentName != "Test")
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

// 中间件
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();

// CORS
app.UseCors("AllowedOrigins");

// 速率限制
app.UseRateLimiter();

// 授权
app.UseAuthorization();

// 映射控制器
app.MapControllers();

// 健康检查端点
app.MapHealthChecks("/api/v1/health");

// =====================================================
// 数据库初始化
// =====================================================

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    try
    {
        // SQLite 内存数据库需要特殊处理
        if (dbProvider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
        {
            db.Database.OpenConnection();
            db.Database.EnsureCreated();
            Console.WriteLine("SQLite 数据库已创建");

            // 添加测试数据
            await SeedTestDataAsync(db, scope.ServiceProvider);
        }
        else
        {
            // SQL Server 迁移
            db.Database.Migrate();
            Console.WriteLine("数据库迁移完成");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"数据库初始化失败: {ex.Message}");
        if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Test")
        {
            Console.WriteLine("尝试确保数据库创建...");
            db.Database.EnsureCreated();
        }
        else
        {
            throw;
        }
    }
}

// =====================================================
// 测试数据种子
// =====================================================

static async Task SeedTestDataAsync(AppDbContext db, IServiceProvider services)
{
    // 检查是否已有数据
    if (await db.PhishingReports.AnyAsync())
    {
        Console.WriteLine("数据库已有数据，跳过种子数据");
        return;
    }

    Console.WriteLine("正在添加测试数据...");

    var fileStorage = services.GetRequiredService<IFileStorageService>();
    var testReports = CreateTestReports();

    foreach (var report in testReports)
    {
        // 保存原始邮件文件
        if (!string.IsNullOrEmpty(report.RawEmlContent))
        {
            var emlBytes = System.Text.Encoding.UTF8.GetBytes(report.RawEmlContent);
            report.EmlFilePath = await fileStorage.SaveEmlAsync(report.Id, emlBytes, CancellationToken.None);
        }

        db.PhishingReports.Add(report);
    }

    await db.SaveChangesAsync();
    Console.WriteLine($"已添加 {testReports.Count} 条测试数据");
}

static List<PhishingReport> CreateTestReports()
{
    var now = DateTime.UtcNow;
    var reports = new List<PhishingReport>();

    // 测试报告 1: 高风险钓鱼邮件
    var report1Id = Guid.NewGuid();
    var eml1 = CreateSampleEml(
        subject: "紧急: 您的账户将被冻结",
        senderEmail: "security@bank-verify.com",
        senderName: "银行安全中心",
        to: "user@company.com",
        body: "尊敬的用户，您的账户存在异常活动，请立即点击以下链接验证身份...\n\nhttps://bank-verify.com/verify?id=12345\n\n如不及时处理，您的账户将被永久冻结。",
        headers: new Dictionary<string, string>
        {
            ["X-Spam-Score"] = "85",
            ["X-Phishing-Indicator"] = "suspicious-link",
            ["Return-Path"] = "security@bank-verify.com",
            ["X-Originating-IP"] = "192.168.1.100"
        }
    );

    reports.Add(new PhishingReport
    {
        Id = report1Id,
        Subject = "紧急: 您的账户将被冻结",
        SenderEmail = "security@bank-verify.com",
        SenderName = "银行安全中心",
        SenderSmtpAddress = "security@bank-verify.com",
        ToRecipients = new List<string> { "user@company.com" },
        ReportedAt = now.AddDays(-2),
        ReportedBy = "user@company.com",
        UserNotes = "可疑邮件，链接指向未知网站",
        Status = "Confirmed",
        RiskScore = 85,
        Category = "CredentialHarvesting",
        RawEmlContent = eml1,
        Headers = new List<EmailHeader>
        {
            new() { Id = Guid.NewGuid(), ReportId = report1Id, HeaderName = "X-Spam-Score", HeaderValue = "85" },
            new() { Id = Guid.NewGuid(), ReportId = report1Id, HeaderName = "X-Phishing-Indicator", HeaderValue = "suspicious-link" },
            new() { Id = Guid.NewGuid(), ReportId = report1Id, HeaderName = "Return-Path", HeaderValue = "security@bank-verify.com" },
            new() { Id = Guid.NewGuid(), ReportId = report1Id, HeaderName = "X-Originating-IP", HeaderValue = "192.168.1.100" },
            new() { Id = Guid.NewGuid(), ReportId = report1Id, HeaderName = "Received", HeaderValue = "from mail.bank-verify.com ([192.168.1.100])" },
            new() { Id = Guid.NewGuid(), ReportId = report1Id, HeaderName = "Content-Type", HeaderValue = "text/html; charset=utf-8" }
        },
        Attachments = new List<EmailAttachment>(),
        AnalysisResults = new List<AnalysisResult>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ReportId = report1Id,
                AnalyzerType = "Default",
                ResultJson = "{\"RiskScore\":85,\"Category\":\"CredentialHarvesting\"}",
                RiskIndicatorsJson = "[{\"Type\":\"SuspiciousLink\",\"Description\":\"链接指向可疑域名 bank-verify.com\",\"Severity\":5},{\"Type\":\"UrgencyLanguage\",\"Description\":\"使用紧急语言诱导用户点击\",\"Severity\":4}]"
            }
        }
    });

    // 测试报告 2: 中风险伪装邮件
    var report2Id = Guid.NewGuid();
    var eml2 = CreateSampleEml(
        subject: "发票 #INV-2024-001",
        senderEmail: "invoice@supplier-biz.net",
        senderName: "供应商财务",
        to: "finance@company.com",
        body: "尊敬的客户，\n\n请查收本月发票，详情请见附件。\n\n谢谢合作。",
        headers: new Dictionary<string, string>
        {
            ["X-Spam-Score"] = "45",
            ["X-Originating-IP"] = "10.0.0.50"
        }
    );

    reports.Add(new PhishingReport
    {
        Id = report2Id,
        Subject = "发票 #INV-2024-001",
        SenderEmail = "invoice@supplier-biz.net",
        SenderName = "供应商财务",
        SenderSmtpAddress = "invoice@supplier-biz.net",
        ToRecipients = new List<string> { "finance@company.com" },
        ReportedAt = now.AddDays(-1),
        ReportedBy = "finance@company.com",
        UserNotes = "附件疑似恶意",
        Status = "Pending",
        RiskScore = 45,
        Category = "MalwareDelivery",
        RawEmlContent = eml2,
        Headers = new List<EmailHeader>
        {
            new() { Id = Guid.NewGuid(), ReportId = report2Id, HeaderName = "X-Spam-Score", HeaderValue = "45" },
            new() { Id = Guid.NewGuid(), ReportId = report2Id, HeaderName = "X-Originating-IP", HeaderValue = "10.0.0.50" },
            new() { Id = Guid.NewGuid(), ReportId = report2Id, HeaderName = "Content-Type", HeaderValue = "multipart/mixed" }
        },
        Attachments = new List<EmailAttachment>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ReportId = report2Id,
                FileName = "invoice.doc",
                MimeType = "application/msword",
                FileSize = 45000,
                IsMalicious = true
            }
        },
        AnalysisResults = new List<AnalysisResult>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ReportId = report2Id,
                AnalyzerType = "Default",
                ResultJson = "{\"RiskScore\":45,\"Category\":\"MalwareDelivery\"}",
                RiskIndicatorsJson = "[{\"Type\":\"SuspiciousAttachment\",\"Description\":\"可疑附件 invoice.doc 可能包含恶意代码\",\"Severity\":3},{\"Type\":\"UnknownSender\",\"Description\":\"发件人域名 supplier-biz.net 未知\",\"Severity\":2}]"
            }
        }
    });

    // 测试报告 3: 低风险正常邮件误报
    var report3Id = Guid.NewGuid();
    var eml3 = CreateSampleEml(
        subject: "会议通知: 下周一部门例会",
        senderEmail: "hr@company.com",
        senderName: "人力资源部",
        to: "all@company.com",
        body: "各位同事，\n\n下周一上午10点召开部门例会，请准时参加。\n\n会议室: A301\n\n人力资源部",
        headers: new Dictionary<string, string>
        {
            ["X-Spam-Score"] = "10",
            ["X-Originating-IP"] = "172.16.0.1"
        }
    );

    reports.Add(new PhishingReport
    {
        Id = report3Id,
        Subject = "会议通知: 下周一部门例会",
        SenderEmail = "hr@company.com",
        SenderName = "人力资源部",
        SenderSmtpAddress = "hr@company.com",
        ToRecipients = new List<string> { "all@company.com" },
        ReportedAt = now.AddHours(-5),
        ReportedBy = "employee@company.com",
        UserNotes = "误报测试",
        Status = "FalsePositive",
        RiskScore = 10,
        Category = null,
        RawEmlContent = eml3,
        Headers = new List<EmailHeader>
        {
            new() { Id = Guid.NewGuid(), ReportId = report3Id, HeaderName = "X-Spam-Score", HeaderValue = "10" },
            new() { Id = Guid.NewGuid(), ReportId = report3Id, HeaderName = "X-Originating-IP", HeaderValue = "172.16.0.1" }
        },
        Attachments = new List<EmailAttachment>(),
        AnalysisResults = new List<AnalysisResult>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ReportId = report3Id,
                AnalyzerType = "Default",
                ResultJson = "{\"RiskScore\":10,\"Category\":null}",
                RiskIndicatorsJson = "[{\"Type\":\"InternalSender\",\"Description\":\"内部邮件，来自已知发件人\",\"Severity\":1}]"
            }
        }
    });

    // 测试报告 4: 待分析状态
    var report4Id = Guid.NewGuid();
    var eml4 = CreateSampleEml(
        subject: "项目文档更新",
        senderEmail: "project@external-partner.com",
        senderName: "项目合作伙伴",
        to: "team@company.com",
        body: "团队成员，\n\n请查看最新的项目文档更新...\n\n附件包含最新的项目进度报告。",
        headers: new Dictionary<string, string>
        {
            ["X-Spam-Score"] = "30"
        }
    );

    reports.Add(new PhishingReport
    {
        Id = report4Id,
        Subject = "项目文档更新",
        SenderEmail = "project@external-partner.com",
        SenderName = "项目合作伙伴",
        SenderSmtpAddress = "project@external-partner.com",
        ToRecipients = new List<string> { "team@company.com" },
        ReportedAt = now.AddHours(-2),
        ReportedBy = "team@company.com",
        UserNotes = "",
        Status = "Analyzing",
        RiskScore = 30,
        RawEmlContent = eml4,
        Headers = new List<EmailHeader>
        {
            new() { Id = Guid.NewGuid(), ReportId = report4Id, HeaderName = "X-Spam-Score", HeaderValue = "30" }
        },
        Attachments = new List<EmailAttachment>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ReportId = report4Id,
                FileName = "project-update.pdf",
                MimeType = "application/pdf",
                FileSize = 120000,
                IsMalicious = false
            }
        },
        AnalysisResults = new List<AnalysisResult>()
    });

    return reports;
}

static string CreateSampleEml(
    string subject,
    string senderEmail,
    string senderName,
    string to,
    string body,
    Dictionary<string, string> headers)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("MIME-Version: 1.0");
    sb.AppendLine($"From: {senderName} <{senderEmail}>");
    sb.AppendLine($"To: {to}");
    sb.AppendLine($"Subject: {subject}");
    sb.AppendLine($"Date: {DateTime.UtcNow:ddd, dd MMM yyyy HH:mm:ss +0000}");
    sb.AppendLine("Content-Type: text/plain; charset=utf-8");
    sb.AppendLine("Content-Transfer-Encoding: 8bit");

    foreach (var header in headers)
    {
        sb.AppendLine($"{header.Key}: {header.Value}");
    }

    sb.AppendLine("");
    sb.AppendLine(body);
    sb.AppendLine("");

    return sb.ToString();
}

// =====================================================
// 启动信息
// =====================================================

Console.WriteLine("");
Console.WriteLine("========================================");
Console.WriteLine("  钓鱼邮件上报系统 API");
Console.WriteLine("========================================");
Console.WriteLine($"  环境:    {app.Environment.EnvironmentName}");
Console.WriteLine($"  数据库:  {dbProvider}");
Console.WriteLine($"  模拟服务: Exchange={useMockExchange}, Notification={useMockNotification}");
Console.WriteLine($"  地址:    {builder.Configuration["Kestrel:Endpoints:Http:Url"] ?? "http://localhost:5000"}");
Console.WriteLine($"  Swagger: /swagger");
Console.WriteLine("========================================");
Console.WriteLine("");

app.Run();