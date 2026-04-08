# 快速测试指南

本指南帮助你在没有 SQL Server 和 Exchange Server 的环境下快速验证项目。

## 测试环境配置

测试环境使用以下替代方案：

| 组件 | 生产环境 | 测试环境 |
|------|----------|----------|
| 数据库 | SQL Server | SQLite 内存数据库 |
| Exchange | EWS API | 模拟服务 |
| 通知 | SMTP/Teams | 模拟服务 |

## 前置要求

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- (可选) Node.js 18+ 用于前端

## 快速启动

### 方式一：使用启动脚本

```bash
# Windows
start-test.bat

# 或手动启动
cd src/PhishingReporter.Backend/PhishingReporter.Api
set ASPNETCORE_ENVIRONMENT=Test
dotnet run
```

### 方式二：使用命令行

```bash
cd src/PhishingReporter.Backend/PhishingReporter.Api
dotnet run --environment Test
```

## 验证 API

### 1. 打开 Swagger

访问: http://localhost:5000/swagger

### 2. 使用测试 API Key

```
X-API-Key: test-api-key-12345
```

### 3. 测试 API 端点

#### 健康检查
```bash
curl http://localhost:5000/api/v1/health
```

#### 提交报告
```bash
curl -X POST http://localhost:5000/api/v1/reports \
  -H "Content-Type: application/json" \
  -H "X-API-Key: test-api-key-12345" \
  -d '{
    "senderEmail": "phisher@evil.com",
    "subject": "测试钓鱼邮件",
    "reportedBy": "user@test.local"
  }'
```

#### 获取报告列表
```bash
curl http://localhost:5000/api/v1/reports \
  -H "X-API-Key: test-api-key-12345"
```

### 4. 运行测试脚本

```powershell
# PowerShell
.\test-api.ps1

# Bash (Git Bash / WSL)
./test-api.sh
```

## 启动前端管理后台

```bash
cd src/frontend
npm install
npm run dev
```

访问: http://localhost:5173

使用 API Key 登录: `test-api-key-12345`

## 配置说明

### 测试环境配置文件

`appsettings.Test.json` 关键配置：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=:memory:",
    "Provider": "Sqlite"
  },
  "Exchange": {
    "UseMockService": true
  },
  "Notification": {
    "UseMockService": true
  },
  "ApiSettings": {
    "ApiKey": "test-api-key-12345"
  }
}
```

### 切换到生产环境

修改配置或设置环境变量：

```bash
set ASPNETCORE_ENVIRONMENT=Production
```

然后配置：
- SQL Server 连接字符串
- Exchange EWS 凭据
- SMTP/Teams 通知

## 功能验证清单

- [ ] API 健康检查返回 "Healthy"
- [ ] 可以提交钓鱼报告
- [ ] 报告列表分页正常
- [ ] 可以查看报告详情
- [ ] 可以更新报告状态
- [ ] 统计信息正确
- [ ] 前端可以登录
- [ ] 仪表盘显示数据
- [ ] 报告列表可以筛选

## 常见问题

### Q: SQLite 数据库在哪里？

测试环境使用内存数据库，重启后数据丢失。如需持久化，修改连接字符串：

```json
"DefaultConnection": "Data Source=phishing.db"
```

### Q: 为什么通知没有发送？

测试环境使用模拟服务，通知仅记录日志。查看控制台输出的 `[MOCK]` 日志。

### Q: 如何添加测试数据？

运行测试脚本或使用 Swagger 手动提交报告。

### Q: 前端登录失败？

确保：
1. API 服务正在运行
2. API Key 正确: `test-api-key-12345`
3. CORS 配置正确

## 下一步

1. 开发 Outlook 插件（需要 Visual Studio + VSTO）
2. 配置 Exchange Server 集成
3. 设置生产环境数据库
4. 部署到服务器