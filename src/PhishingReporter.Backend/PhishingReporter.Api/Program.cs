using Microsoft.EntityFrameworkCore;
using PhishingReporter.Api.Middleware;
using PhishingReporter.Core.Interfaces;
using PhishingReporter.Core.Services;
using PhishingReporter.Infrastructure.Data;
using PhishingReporter.Infrastructure.Data.Repositories;
using PhishingReporter.Infrastructure.Exchange;
using PhishingReporter.Infrastructure.Storage;
using PhishingReporter.Infrastructure.Notification;
using PhishingReporter.Infrastructure.MockServices;

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
            options.UseSqlite(connectionString ?? "Data Source=:memory:");
            Console.WriteLine("使用 SQLite 数据库");
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