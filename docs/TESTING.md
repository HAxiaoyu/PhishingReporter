# 快速测试指南

本指南帮助你在没有 SQL Server 和 Exchange Server 的环境下快速验证项目。

## 测试环境配置

测试环境使用以下替代方案：

| 组件 | 生产环境 | 测试环境 |
|------|----------|----------|
| 数据库 | SQL Server | SQLite 文件数据库 |
| Exchange | EWS API | 模拟服务 |
| 通知 | SMTP/Teams | 模拟服务 |

## 前置要求

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Node.js 18+ 和 npm（用于前端）

## 快速启动

### 1. 启动后端 API

```bash
cd src/PhishingReporter.Backend/PhishingReporter.Api

# Windows (PowerShell)
$env:ASPNETCORE_ENVIRONMENT="Test"
dotnet run

# Windows (CMD)
set ASPNETCORE_ENVIRONMENT=Test
dotnet run

# Linux/macOS
ASPNETCORE_ENVIRONMENT=Test dotnet run
```

后端启动后会自动：
- 创建 SQLite 数据库文件 (`test.db`)
- 添加 4 条测试数据（包含不同状态的钓鱼报告）
- 启动模拟服务（无需 Exchange 和 SMTP）

### 2. 启动前端管理后台

```bash
cd src/frontend

# 安装依赖
npm install

# 启动开发服务器
npm run dev
```

### 3. 访问系统

| 服务 | 地址 |
|------|------|
| 前端管理后台 | http://localhost:3000 |
| 后端 API | http://localhost:5000 |
| Swagger 文档 | http://localhost:5000/swagger |

### 4. 登录系统

使用测试 API Key 登录：`test-api-key-12345`

## 测试数据说明

测试环境自动创建 4 条钓鱼报告：

| 主题 | 状态 | 风险评分 | 分类 |
|------|------|----------|------|
| 紧急: 您的账户将被冻结 | 已确认钓鱼 | 85 | CredentialHarvesting |
| 发票 #INV-2024-001 | 待处理 | 45 | MalwareDelivery |
| 会议通知: 下周一部门例会 | 误报 | 10 | - |
| 项目文档更新 | 分析中 | 30 | - |

每条报告都包含：
- 完整的邮件头信息
- 原始邮件源文件（EML格式）
- 分析指标
- 部分包含附件

## 验证功能

### 后端 API 测试

```bash
# 健康检查
curl http://localhost:5000/api/v1/health

# 获取报告列表（需要 API Key）
curl -H "X-API-Key: test-api-key-12345" \
  http://localhost:5000/api/v1/reports

# 获取统计信息
curl -H "X-API-Key: test-api-key-12345" \
  http://localhost:5000/api/v1/reports/statistics

# 获取报告详情（包含邮件源文件）
curl -H "X-API-Key: test-api-key-12345" \
  http://localhost:5000/api/v1/reports/{report-id}
```

### 前端功能验证

1. **仪表盘** - 查看统计数据、状态分布、风险分类、上报趋势
2. **报告列表** - 筛选状态、分页浏览
3. **报告详情** - 查看邮件头、分析指标、邮件源文件
4. **状态更新** - 修改报告状态并添加备注

## 配置说明

### 测试环境配置文件

`appsettings.Test.json` 关键配置：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=test.db",
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
  },
  "FileStorage": {
    "BasePath": "./eml-storage-test"
  }
}
```

### 数据持久化

测试环境使用文件数据库 `test.db`，数据在重启后保留。

如需重置数据：

```bash
# 删除数据库文件
rm src/PhishingReporter.Backend/PhishingReporter.Api/test.db

# 删除邮件存储
rm -rf src/PhishingReporter.Backend/PhishingReporter.Api/eml-storage-test

# 重启后端，将自动创建新的测试数据
```

## 功能验证清单

- [ ] API 健康检查返回 "Healthy"
- [ ] 可以提交钓鱼报告
- [ ] 报告列表分页正常
- [ ] 可以查看报告详情
- [ ] 可以查看邮件源文件（EML格式）
- [ ] 可以下载邮件源文件
- [ ] 可以更新报告状态
- [ ] 统计信息正确显示
- [ ] 风险分类分布有数据
- [ ] 前端可以登录
- [ ] 仪表盘显示数据
- [ ] 报告列表可以筛选

## 常见问题

### Q: 端口被占用怎么办？

```bash
# Windows - 查找并终止占用端口的进程
netstat -ano | findstr :5000
taskkill /PID <进程ID> /F
```

### Q: 为什么通知没有发送？

测试环境使用模拟服务，通知仅记录日志。查看控制台输出了解模拟操作。

### Q: 前端登录失败？

确保：
1. API 服务正在运行 (`curl http://localhost:5000/api/v1/health`)
2. API Key 正确: `test-api-key-12345`
3. 前端代理配置正确 (vite.config.ts)

### Q: 如何切换到生产环境？

```bash
# 设置环境变量
set ASPNETCORE_ENVIRONMENT=Production

# 配置 appsettings.Production.json
# - SQL Server 连接字符串
# - Exchange EWS 凭据
# - SMTP/Teams 通知配置
```

## 下一步

1. 开发 Outlook 插件（需要 Visual Studio + VSTO）
2. 配置 Exchange Server 集成
3. 设置生产环境数据库
4. 部署到服务器