# 钓鱼邮件上报系统 - 管理后台前端

基于 Vue 3 + TypeScript + Vite 构建的管理后台。

## 功能特性

- 📊 **仪表盘** - 统计概览、状态分布、风险分类
- 📧 **上报列表** - 分页、筛选、快速查看
- 🔍 **详情查看** - 邮件信息、分析指标、附件列表
- ✏️ **状态管理** - 更新状态、添加备注

## 技术栈

- **框架**: Vue 3 (Composition API)
- **语言**: TypeScript
- **构建**: Vite
- **路由**: Vue Router 4
- **状态**: Pinia
- **HTTP**: Axios

## 开发

### 安装依赖

```bash
cd src/frontend
npm install
```

### 启动开发服务器

```bash
npm run dev
```

访问 http://localhost:3000

### 构建

```bash
npm run build
```

### 预览构建结果

```bash
npm run preview
```

## 配置

### API 代理

开发环境通过 Vite 代理连接后端 API：

```typescript
// vite.config.ts
server: {
  port: 3000,
  proxy: {
    '/api': {
      target: 'http://localhost:5000',
      changeOrigin: true
    }
  }
}
```

### CORS 配置

生产环境需要在后端配置 CORS 白名单：

```json
// appsettings.json
{
  "Cors": {
    "AllowedOrigins": [
      "https://phishing-report.internal"
    ]
  }
}
```

## 部署

### 构建

```bash
npm run build
```

构建产物在 `dist/` 目录。

### Nginx 配置

```nginx
server {
    listen 80;
    server_name phishing-report.internal;

    root /var/www/phishing-reporter/dist;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    location /api {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
```

## 登录

使用后端配置的 API Key 登录。

```json
// appsettings.json
{
  "ApiSettings": {
    "ApiKey": "your-api-key-here"
  }
}
```