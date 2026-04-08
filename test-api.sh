#!/bin/bash
# 钓鱼邮件上报系统 - API 测试脚本

BASE_URL="http://localhost:5000/api/v1"
API_KEY="test-api-key-12345"

echo "========================================"
echo "  钓鱼邮件上报系统 - API 测试"
echo "========================================"
echo ""

# 1. 健康检查
echo "1. 健康检查..."
curl -s "$BASE_URL/health" | jq .
echo ""

# 2. 提交测试报告
echo "2. 提交测试钓鱼报告..."
REPORT_RESPONSE=$(curl -s -X POST "$BASE_URL/reports" \
  -H "Content-Type: application/json" \
  -H "X-API-Key: $API_KEY" \
  -d '{
    "senderEmail": "phisher@evil.com",
    "senderName": "Fake Bank",
    "subject": "紧急：您的银行账户需要验证",
    "bodyPreview": "请点击以下链接验证您的账户...",
    "reportedBy": "user@test.local",
    "userNotes": "可疑邮件，链接指向非银行域名",
    "headers": {
      "From": "phisher@evil.com",
      "Reply-To": "attacker@malware.com"
    }
  }')

echo "$REPORT_RESPONSE" | jq .
REPORT_ID=$(echo "$REPORT_RESPONSE" | jq -r '.reportId // empty')

if [ -n "$REPORT_ID" ]; then
  echo ""
  echo "报告已创建，ID: $REPORT_ID"

  # 3. 获取报告详情
  echo ""
  echo "3. 获取报告详情..."
  curl -s "$BASE_URL/reports/$REPORT_ID" \
    -H "X-API-Key: $API_KEY" | jq .

  # 4. 更新状态
  echo ""
  echo "4. 更新报告状态..."
  curl -s -X PATCH "$BASE_URL/reports/$REPORT_ID/status" \
    -H "Content-Type: application/json" \
    -H "X-API-Key: $API_KEY" \
    -d '{"status": "Confirmed", "notes": "已确认钓鱼邮件"}'
  echo "状态已更新为 Confirmed"
fi

# 5. 获取报告列表
echo ""
echo "5. 获取报告列表..."
curl -s "$BASE_URL/reports?page=1&pageSize=10" \
  -H "X-API-Key: $API_KEY" | jq .

# 6. 获取统计信息
echo ""
echo "6. 获取统计信息..."
curl -s "$BASE_URL/reports/statistics" \
  -H "X-API-Key: $API_KEY" | jq .

echo ""
echo "========================================"
echo "  测试完成"
echo "========================================"