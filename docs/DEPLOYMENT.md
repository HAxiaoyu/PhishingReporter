# 钓鱼邮件上报系统部署指南

## 系统要求

### 后端服务

| 组件 | 最低要求 | 推荐配置 |
|------|----------|----------|
| 操作系统 | Windows Server 2016+ | Windows Server 2019+ |
| CPU | 2 核 | 4 核+ |
| 内存 | 4 GB | 8 GB+ |
| 存储 | 50 GB | 100 GB+ (取决于邮件量) |
| .NET | .NET 8 Runtime | .NET 8 SDK (开发) |
| 数据库 | SQL Server 2016+ | SQL Server 2019+ |

### Outlook 插件

| 组件 | 要求 |
|------|------|
| 操作系统 | Windows 10/11 |
| Outlook | Outlook 2016/2019/2021/365 (Classic) |
| .NET | .NET Framework 4.8 |
| 运行时 | VSTO Runtime |

---

## 后端服务部署

### 方式一：IIS 部署（推荐）

#### 1. 准备服务器

```powershell
# 安装 IIS 和 .NET 8
# Windows Server 使用 Server Manager 添加角色
# 或使用 PowerShell:

Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole
Enable-WindowsOptionalFeature -Online -FeatureName IIS-ASPNET45

# 安装 .NET 8 Hosting Bundle
# 下载地址: https://dotnet.microsoft.com/download/dotnet/8.0
```

#### 2. 准备数据库

```sql
-- 创建数据库
CREATE DATABASE PhishingReporter;

-- 创建服务账号（如果需要）
CREATE LOGIN PhishingReporterSvc WITH PASSWORD = 'YourStrongPassword123!';

USE PhishingReporter;
CREATE USER PhishingReporterSvc FOR LOGIN PhishingReporterSvc;
ALTER ROLE db_owner ADD MEMBER PhishingReporterSvc;
```

#### 3. 配置应用

```json
// appsettings.Production.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=your-sql-server;Database=PhishingReporter;User Id=PhishingReporterSvc;Password=YourPassword;"
  },
  "Exchange": {
    "EwsUrl": "https://exchange.company.com/EWS/Exchange.asmx",
    "Username": "service-account@company.com",
    "Password": "encrypted-password",
    "ArchiveFolderName": "Phishing Reports"
  },
  "ApiSettings": {
    "ApiKey": "your-production-api-key"
  }
}
```

#### 4. 发布应用

```bash
# 在开发机器上发布
cd src/PhishingReporter.Backend/PhishingReporter.Api
dotnet publish -c Release -o ./publish

# 将 publish 文件夹复制到服务器
```

#### 5. 创建 IIS 站点

```powershell
# 创建应用程序池
New-WebAppPool -Name "PhishingReporterAPI"
Set-ItemProperty IIS:\AppPools\PhishingReporterAPI -Name managedRuntimeVersion -Value ""

# 创建站点
New-Website -Name "PhishingReporterAPI" `
    -PhysicalPath "C:\inetpub\wwwroot\PhishingReporterAPI" `
    -ApplicationPool "PhishingReporterAPI" `
    -Port 443 -Ssl

# 绑定证书
$cert = Get-ChildItem -Path Cert:\LocalMachine\My | Where-Object {$_.Subject -like "*your-domain*"}
New-Item -Path "IIS:\SslBindings\0.0.0.0!443" -Value $cert
```

### 方式二：Docker 部署

#### 1. 创建 Dockerfile

```dockerfile
# PhishingReporter.Api/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish "PhishingReporter.Api/PhishingReporter.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PhishingReporter.Api.dll"]
```

#### 2. 使用 Docker Compose

```yaml
# docker-compose.yml
version: '3.8'

services:
  api:
    build: .
    ports:
      - "5000:80"
      - "5001:443"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=PhishingReporter;User Id=sa;Password=${SA_PASSWORD}
    depends_on:
      - sqlserver
    volumes:
      - ./eml-storage:/app/eml-storage

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=${SA_PASSWORD}
    volumes:
      - sqlserver-data:/var/opt/mssql/data

volumes:
  sqlserver-data:
```

#### 3. 启动服务

```bash
# 启动
docker-compose up -d

# 查看日志
docker-compose logs -f api

# 停止
docker-compose down
```

---

## Outlook 插件部署

### 方式一：MSI 安装包（推荐）

#### 1. 创建安装项目

在 Visual Studio 中：

1. 安装 **WiX Toolset** 扩展
2. 创建 **Setup Project**
3. 添加插件输出
4. 配置安装条件

#### 2. 配置 WiX 安装脚本

```xml
<!-- installer/Product.wxs -->
<Wix xmlns="http://schemas.microsoft.com/wix/2006/wi">
  <Product Id="*" Name="PhishingReporter" Language="1033"
           Version="1.0.0.0" Manufacturer="Your Company"
           UpgradeCode="YOUR-GUID-HERE">

    <Package InstallerVersion="200" Compressed="yes" />

    <Media Id="1" Cabinet="media1.cab" EmbedCab="yes" />

    <Directory Id="TARGETDIR" Name="SourceDir">
      <Directory Id="ProgramFilesFolder">
        <Directory Id="INSTALLFOLDER" Name="PhishingReporter" />
      </Directory>
    </Directory>

    <Component Id="MainComponent" Guid="YOUR-GUID">
      <File Id="PhishingReporter.dll"
            Source="$(var.PhishingReporter.TargetPath)" />
    </Component>

    <Feature Id="MainFeature" Title="PhishingReporter" Level="1">
      <ComponentRef Id="MainComponent" />
    </Feature>

  </Product>
</Wix>
```

#### 3. 构建 MSI

```bash
# 在 Visual Studio 中构建
# 或使用命令行
msbuild installer/Setup.wixproj /p:Configuration=Release
```

### 方式二：组策略部署

#### 1. 准备 MSI 包

将生成的 MSI 放在网络共享目录。

#### 2. 创建组策略

1. 打开 **Group Policy Management**
2. 创建新 GPO: "PhishingReporter Deployment"
3. 编辑 GPO → Computer Configuration → Policies → Software Settings
4. 右键 "Software installation" → New → Package
5. 选择 MSI 文件，选择 "Assigned"

#### 3. 链接 GPO

将 GPO 链接到目标 OU（组织单位）。

### 方式三：手动安装

对于测试或小规模部署：

1. 构建 VSTO 项目
2. 在目标机器上安装 VSTO Runtime
3. 运行生成的 .vsto 文件

---

## 配置 Exchange Server

### 1. 创建服务账号

```powershell
# 创建服务账号
New-Mailbox -Name "Phishing Reporter Service" `
    -Alias "phishing-svc" `
    -UserPrincipalName "phishing-svc@company.com" `
    -Password (ConvertTo-SecureString "YourPassword123!" -AsPlainText -Force)

# 授予存档权限
Add-MailboxFolderPermission -Identity "phishing-archive@company.com:\Inbox" `
    -User "phishing-svc@company.com" `
    -AccessRights Editor
```

### 2. 配置 EWS 访问

```powershell
# 启用 EWS 服务
Set-WebServicesVirtualDirectory -Identity "EWS (Default Web Site)" `
    -ExternalUrl "https://exchange.company.com/EWS/Exchange.asmx"

# 测试 EWS 连接
Test-OutlookWebServices -ClientAccessServer "exchange.company.com"
```

---

## 安全配置

### SSL 证书

1. 获取企业内部 CA 证书或公共证书
2. 配置 IIS 绑定证书
3. 确保证书链完整

### API 密钥

```json
// 生成强密钥
// PowerShell:
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }))

// 配置到 appsettings.json
{
  "ApiSettings": {
    "ApiKey": "生成的密钥"
  }
}
```

### Exchange 凭据加密

使用 Windows DPAPI 加密存储密码：

```csharp
// 加密
var encryptedPassword = ProtectedData.Protect(
    Encoding.UTF8.GetBytes(password),
    null,
    DataProtectionScope.LocalMachine
);

// 存储 encryptedPassword 到安全位置
```

---

## 验证部署

### 后端验证

```bash
# 健康检查
curl https://your-server/api/v1/health

# 预期响应
{
  "status": "Healthy",
  "version": "1.0.0",
  "timestamp": "2024-01-01T00:00:00Z"
}
```

### 插件验证

1. 打开 Outlook Classic
2. 选择一封邮件
3. 检查功能区和右键菜单是否有"上报钓鱼"按钮
4. 点击按钮测试上报流程

---

## 故障排除

### 常见问题

| 问题 | 可能原因 | 解决方案 |
|------|----------|----------|
| 插件不显示 | VSTO Runtime 缺失 | 安装 VSTO Runtime |
| API 连接失败 | SSL 证书问题 | 检查证书链 |
| 上报超时 | 网络问题 | 检查防火墙设置 |
| Exchange 存档失败 | 权限不足 | 检查服务账号权限 |

### 日志位置

- **插件日志**: `%LocalAppData%\PhishingReporter\logs\`
- **API 日志**: `C:\inetpub\logs\PhishingReporter\` 或 Docker logs
- **IIS 日志**: `C:\inetpub\logs\LogFiles\`

---

## 监控与维护

### 监控指标

- API 响应时间
- 数据库连接数
- 存储空间使用率
- 上报成功率

### 定期维护

- 数据库备份（每日）
- 日志清理（每周）
- 存储空间检查（每月）
- 安全审计（每季度）