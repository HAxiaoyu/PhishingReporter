@echo off
echo ========================================
echo   钓鱼邮件上报系统 - 测试环境启动
echo ========================================
echo.

cd src\PhishingReporter.Backend\PhishingReporter.Api

echo 设置测试环境变量...
set ASPNETCORE_ENVIRONMENT=Test

echo.
echo 启动 API 服务...
echo - 数据库: SQLite 内存数据库
echo - Exchange: 模拟服务
echo - 通知: 模拟服务
echo.
echo API Key: test-api-key-12345
echo Swagger: http://localhost:5000/swagger
echo.

dotnet run

pause