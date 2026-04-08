using Microsoft.EntityFrameworkCore;
using PhishingReporter.Api.Middleware;
using PhishingReporter.Core.Interfaces;
using PhishingReporter.Core.Services;
using PhishingReporter.Infrastructure.Data;
using PhishingReporter.Infrastructure.Data.Repositories;
using PhishingReporter.Infrastructure.Exchange;
using PhishingReporter.Infrastructure.Storage;

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

// 配置数据库
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
        sqlOptions.CommandTimeout(30);
    });
});

// 注册核心服务
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IEmailArchiveService, EmailArchiveService>();
builder.Services.AddScoped<IAnalysisService, AnalysisService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IReportService, ReportService>();

// 配置 Exchange 设置
builder.Services.Configure<ExchangeSettings>(builder.Configuration.GetSection("Exchange"));

// 配置文件存储设置
builder.Services.Configure<FileStorageSettings>(builder.Configuration.GetSection("FileStorage"));

// 配置通知设置
builder.Services.Configure<NotificationSettings>(builder.Configuration.GetSection("Notification"));

// 配置 API 设置
builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));

// CORS 配置 - 限制为已知内部来源
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
            // 开发环境允许本地来源
            policy.WithOrigins("http://localhost:3000", "http://localhost:5000", "https://localhost:5001")
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

// 开发环境配置
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "钓鱼上报 API v1");
        options.RoutePrefix = "swagger";
    });
}

// HTTPS 重定向和 HSTS（生产环境）
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

// 中间件
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();

// CORS - 使用配置的策略
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
// 数据库迁移
// =====================================================

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    try
    {
        // 自动应用迁移
        db.Database.Migrate();
        Console.WriteLine("数据库迁移完成");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"数据库迁移失败: {ex.Message}");
        // 开发环境可以继续运行
        if (app.Environment.IsDevelopment())
        {
            Console.WriteLine("开发环境：尝试确保数据库创建");
            db.Database.EnsureCreated();
        }
        else
        {
            throw;
        }
    }
}

// =====================================================
// 启动应用
// =====================================================

Console.WriteLine("钓鱼邮件上报系统 API 启动中...");
Console.WriteLine($"环境: {app.Environment.EnvironmentName}");
Console.WriteLine($"监听端口: {builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5000"}");

app.Run();