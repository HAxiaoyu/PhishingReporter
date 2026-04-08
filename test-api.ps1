# 钓鱼邮件上报系统 - API 测试脚本 (PowerShell)

$BaseUrl = "http://localhost:5000/api/v1"
$ApiKey = "test-api-key-12345"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  钓鱼邮件上报系统 - API 测试" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. 健康检查
Write-Host "1. 健康检查..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$BaseUrl/health" -Method Get
    $health | ConvertTo-Json
} catch {
    Write-Host "健康检查失败: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# 2. 提交测试报告
Write-Host "2. 提交测试钓鱼报告..." -ForegroundColor Yellow
$headers = @{
    "Content-Type" = "application/json"
    "X-API-Key" = $ApiKey
}

$body = @{
    senderEmail = "phisher@evil.com"
    senderName = "Fake Bank"
    subject = "紧急：您的银行账户需要验证"
    bodyPreview = "请点击以下链接验证您的账户..."
    reportedBy = "user@test.local"
    userNotes = "可疑邮件，链接指向非银行域名"
    headers = @{
        From = "phisher@evil.com"
        "Reply-To" = "attacker@malware.com"
    }
} | ConvertTo-Json -Depth 3

try {
    $report = Invoke-RestMethod -Uri "$BaseUrl/reports" -Method Post -Headers $headers -Body $body
    $report | ConvertTo-Json
    $reportId = $report.reportId
    Write-Host "报告已创建，ID: $reportId" -ForegroundColor Green
} catch {
    Write-Host "提交报告失败: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# 3. 获取报告详情
if ($reportId) {
    Write-Host "3. 获取报告详情..." -ForegroundColor Yellow
    try {
        $detail = Invoke-RestMethod -Uri "$BaseUrl/reports/$reportId" -Method Get -Headers @{ "X-API-Key" = $ApiKey }
        $detail | ConvertTo-Json
    } catch {
        Write-Host "获取详情失败: $_" -ForegroundColor Red
    }
    Write-Host ""

    # 4. 更新状态
    Write-Host "4. 更新报告状态..." -ForegroundColor Yellow
    $updateBody = @{
        status = "Confirmed"
        notes = "已确认钓鱼邮件"
    } | ConvertTo-Json

    try {
        Invoke-RestMethod -Uri "$BaseUrl/reports/$reportId/status" -Method Patch -Headers $headers -Body $updateBody
        Write-Host "状态已更新为 Confirmed" -ForegroundColor Green
    } catch {
        Write-Host "更新状态失败: $_" -ForegroundColor Red
    }
    Write-Host ""
}

# 5. 获取报告列表
Write-Host "5. 获取报告列表..." -ForegroundColor Yellow
try {
    $reports = Invoke-RestMethod -Uri "$BaseUrl/reports?page=1&pageSize=10" -Method Get -Headers @{ "X-API-Key" = $ApiKey }
    $reports | ConvertTo-Json
} catch {
    Write-Host "获取列表失败: $_" -ForegroundColor Red
}
Write-Host ""

# 6. 获取统计信息
Write-Host "6. 获取统计信息..." -ForegroundColor Yellow
try {
    $stats = Invoke-RestMethod -Uri "$BaseUrl/reports/statistics" -Method Get -Headers @{ "X-API-Key" = $ApiKey }
    $stats | ConvertTo-Json
} catch {
    Write-Host "获取统计失败: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  测试完成" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green