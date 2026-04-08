<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '@/api'
import type { PhishingReport, ReportStatus } from '@/types'
import { STATUS_LABELS, STATUS_COLORS, getRiskLevel } from '@/types'

const route = useRoute()
const router = useRouter()

const report = ref<PhishingReport | null>(null)
const isLoading = ref(true)
const error = ref('')
const isUpdating = ref(false)
const showRawEmail = ref(false)

const statusOptions: ReportStatus[] = [
  'Pending',
  'Analyzing',
  'Confirmed',
  'FalsePositive',
  'Resolved'
]

const selectedStatus = ref<ReportStatus>('Pending')
const notes = ref('')

const reportId = computed(() => route.params.id as string)

async function loadReport() {
  try {
    isLoading.value = true
    report.value = await api.getReport(reportId.value)
    selectedStatus.value = report.value.status
  } catch (e) {
    error.value = '加载详情失败'
    console.error(e)
  } finally {
    isLoading.value = false
  }
}

async function updateStatus() {
  if (!report.value) return

  try {
    isUpdating.value = true
    await api.updateReportStatus(report.value.id, {
      status: selectedStatus.value,
      notes: notes.value || undefined
    })
    report.value.status = selectedStatus.value
    notes.value = ''
    alert('状态更新成功')
  } catch (e) {
    alert('更新失败，请重试')
    console.error(e)
  } finally {
    isUpdating.value = false
  }
}

function goBack() {
  router.push('/reports')
}

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleString('zh-CN')
}

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

function decodeBase64(base64: string): string {
  try {
    return atob(base64)
  } catch {
    return base64
  }
}

function getRawEmailContent(): string {
  if (!report.value?.rawEmlBase64) return ''
  return decodeBase64(report.value.rawEmlBase64)
}

function downloadEml() {
  if (!report.value?.rawEmlBase64) return

  const content = decodeBase64(report.value.rawEmlBase64)
  const blob = new Blob([content], { type: 'message/rfc822' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `report-${report.value.id}.eml`
  a.click()
  URL.revokeObjectURL(url)
}

function copyToClipboard() {
  const content = getRawEmailContent()
  navigator.clipboard.writeText(content).then(() => {
    alert('已复制到剪贴板')
  }).catch(() => {
    alert('复制失败')
  })
}

onMounted(loadReport)
</script>

<template>
  <div class="detail-page">
    <div class="page-header">
      <button class="btn btn-outline" @click="goBack">← 返回列表</button>
      <h2 class="page-title">上报详情</h2>
    </div>

    <div v-if="isLoading" class="loading">
      <div class="spinner"></div>
    </div>

    <div v-else-if="error" class="error-message">
      {{ error }}
      <button class="btn btn-primary btn-sm" @click="loadReport">重试</button>
    </div>

    <template v-else-if="report">
      <div class="detail-grid">
        <!-- 基本信息 -->
        <div class="card info-card">
          <h3>基本信息</h3>
          <div class="info-grid">
            <div class="info-item">
              <span class="label">报告 ID</span>
              <span class="value mono">{{ report.id }}</span>
            </div>
            <div class="info-item">
              <span class="label">上报时间</span>
              <span class="value">{{ formatDate(report.reportedAt) }}</span>
            </div>
            <div class="info-item">
              <span class="label">上报人</span>
              <span class="value">{{ report.reportedBy || '-' }}</span>
            </div>
            <div class="info-item">
              <span class="label">状态</span>
              <span
                class="status-badge"
                :style="{ backgroundColor: STATUS_COLORS[report.status] || '#777' }"
              >
                {{ STATUS_LABELS[report.status] || report.status }}
              </span>
            </div>
            <div class="info-item">
              <span class="label">风险评分</span>
              <span
                class="risk-score"
                :style="{ backgroundColor: getRiskLevel(report.riskScore).color }"
              >
                {{ report.riskScore }} ({{ getRiskLevel(report.riskScore).label }})
              </span>
            </div>
            <div class="info-item">
              <span class="label">分类</span>
              <span class="value">{{ report.category || '-' }}</span>
            </div>
          </div>
        </div>

        <!-- 邮件信息 -->
        <div class="card info-card">
          <h3>邮件信息</h3>
          <div class="info-grid">
            <div class="info-item full">
              <span class="label">主题</span>
              <span class="value">{{ report.subject || '-' }}</span>
            </div>
            <div class="info-item">
              <span class="label">发件人</span>
              <span class="value">{{ report.senderName }} &lt;{{ report.senderEmail }}&gt;</span>
            </div>
            <div class="info-item">
              <span class="label">上报人备注</span>
              <span class="value">{{ report.userNotes || '无' }}</span>
            </div>
          </div>
        </div>

        <!-- 邮件头 -->
        <div class="card full-width" v-if="report.headers?.length">
          <h3>邮件头 ({{ report.headers.length }})</h3>
          <div class="headers-table">
            <div
              v-for="(header, index) in report.headers"
              :key="index"
              class="header-row"
            >
              <span class="header-name">{{ header.name || '-' }}</span>
              <span class="header-value">{{ header.value || '-' }}</span>
            </div>
          </div>
        </div>

        <!-- 分析指标 -->
        <div class="card" v-if="report.indicators?.length">
          <h3>分析指标 ({{ report.indicators.length }})</h3>
          <div class="indicators-list">
            <div
              v-for="(indicator, index) in report.indicators"
              :key="index"
              class="indicator-item"
            >
              <div class="indicator-header">
                <span class="indicator-type">{{ indicator.type }}</span>
                <span
                  class="indicator-severity"
                  :class="'severity-' + indicator.severity"
                >
                  严重程度: {{ indicator.severity }}/5
                </span>
              </div>
              <span class="indicator-desc">{{ indicator.description }}</span>
            </div>
          </div>
        </div>

        <!-- 附件 -->
        <div class="card" v-if="report.attachments?.length">
          <h3>附件 ({{ report.attachments.length }})</h3>
          <div class="attachments-list">
            <div
              v-for="(attachment, index) in report.attachments"
              :key="index"
              class="attachment-item"
            >
              <span class="attachment-icon">📎</span>
              <div class="attachment-info">
                <span class="attachment-name">{{ attachment.fileName }}</span>
                <span class="attachment-meta">
                  {{ formatBytes(attachment.size) }}
                  <span v-if="attachment.isMalicious" class="malicious">⚠️ 可疑</span>
                </span>
              </div>
            </div>
          </div>
        </div>

        <!-- 原始邮件源文件 -->
        <div class="card full-width" v-if="report.hasRawEmail || report.rawEmlBase64">
          <div class="section-header">
            <h3>邮件源文件 (MIME)</h3>
            <div class="section-actions">
              <button class="btn btn-outline btn-sm" @click="showRawEmail = !showRawEmail">
                {{ showRawEmail ? '收起' : '展开' }}
              </button>
              <button class="btn btn-primary btn-sm" @click="downloadEml" v-if="report.rawEmlBase64">
                下载 .eml
              </button>
              <button class="btn btn-outline btn-sm" @click="copyToClipboard" v-if="report.rawEmlBase64 && showRawEmail">
                复制
              </button>
            </div>
          </div>

          <div v-if="showRawEmail && report.rawEmlBase64" class="raw-email-container">
            <pre class="raw-email-content">{{ getRawEmailContent() }}</pre>
          </div>
          <div v-else-if="!report.rawEmlBase64" class="no-content">
            暂无原始邮件文件
          </div>
        </div>

        <!-- 状态更新 -->
        <div class="card">
          <h3>更新状态</h3>
          <div class="update-form">
            <div class="form-group">
              <label>新状态</label>
              <select v-model="selectedStatus" class="select">
                <option v-for="status in statusOptions" :key="status" :value="status">
                  {{ STATUS_LABELS[status] }}
                </option>
              </select>
            </div>

            <div class="form-group">
              <label>备注（可选）</label>
              <textarea
                v-model="notes"
                class="textarea"
                placeholder="添加处理备注..."
                rows="3"
              ></textarea>
            </div>

            <button
              class="btn btn-primary"
              :disabled="isUpdating || selectedStatus === report.status"
              @click="updateStatus"
            >
              {{ isUpdating ? '更新中...' : '更新状态' }}
            </button>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.detail-page {
  width: 100%;
}

.page-header {
  display: flex;
  align-items: center;
  gap: var(--spacing-sm);
  margin-bottom: var(--spacing-md);
}

.page-title {
  font-size: 20px;
}

.detail-grid {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-md);
}

.full-width {
  /* 所有卡片默认占满宽度，此类名保留以兼容 */
}

.info-card {
  padding: var(--spacing-md);
}

.info-card h3 {
  margin-bottom: var(--spacing-sm);
  padding-bottom: var(--spacing-xs);
  border-bottom: 1px solid var(--color-border);
  font-size: 15px;
}

.info-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: var(--spacing-sm);
}

@media (max-width: 500px) {
  .info-grid {
    grid-template-columns: 1fr;
  }
}

.info-item {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.info-item.full {
  grid-column: 1 / -1;
}

.info-item .label {
  font-size: 11px;
  color: var(--color-text-secondary);
}

.info-item .value {
  font-size: 13px;
}

.info-item .value.mono {
  font-family: monospace;
  font-size: 12px;
}

.status-badge {
  display: inline-block;
  padding: 3px 10px;
  border-radius: 10px;
  color: white;
  font-size: 11px;
  width: fit-content;
}

.risk-score {
  display: inline-block;
  padding: 3px 10px;
  border-radius: 10px;
  color: white;
  font-size: 12px;
  font-weight: 600;
}

/* 邮件头表格 */
.headers-table {
  display: flex;
  flex-direction: column;
  gap: 2px;
  max-height: 200px;
  overflow-y: auto;
}

.header-row {
  display: grid;
  grid-template-columns: 150px 1fr;
  gap: var(--spacing-sm);
  padding: var(--spacing-xs) var(--spacing-sm);
  background: var(--color-bg);
  border-radius: var(--radius-sm);
  font-size: 12px;
}

.header-name {
  font-weight: 600;
  color: var(--color-text-secondary);
  white-space: nowrap;
}

.header-value {
  word-break: break-all;
  color: var(--color-text);
}

/* 分析指标 */
.indicators-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.indicator-item {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: var(--spacing-sm);
  background: var(--color-bg);
  border-radius: var(--radius-sm);
}

.indicator-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.indicator-type {
  font-weight: 600;
  font-size: 13px;
}

.indicator-desc {
  font-size: 12px;
  color: var(--color-text-secondary);
}

.indicator-severity {
  font-size: 11px;
}

.severity-5, .severity-4 { color: var(--color-danger); }
.severity-3, .severity-2 { color: var(--color-warning); }
.severity-1 { color: var(--color-success); }

/* 附件 */
.attachments-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.attachment-item {
  display: flex;
  align-items: center;
  gap: var(--spacing-sm);
  padding: var(--spacing-xs) var(--spacing-sm);
  background: var(--color-bg);
  border-radius: var(--radius-sm);
}

.attachment-icon {
  font-size: 16px;
}

.attachment-info {
  display: flex;
  flex-direction: column;
}

.attachment-name {
  font-weight: 500;
  font-size: 13px;
}

.attachment-meta {
  font-size: 11px;
  color: var(--color-text-secondary);
}

.malicious {
  color: var(--color-danger);
  margin-left: var(--spacing-sm);
}

/* 区块头部 */
.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: var(--spacing-sm);
}

.section-header h3 {
  margin: 0;
  font-size: 15px;
}

.section-actions {
  display: flex;
  gap: var(--spacing-xs);
}

/* 原始邮件 */
.raw-email-container {
  background: #1e1e1e;
  border-radius: var(--radius-sm);
  overflow: hidden;
}

.raw-email-content {
  margin: 0;
  padding: var(--spacing-sm);
  font-family: 'Consolas', 'Monaco', monospace;
  font-size: 11px;
  line-height: 1.4;
  color: #d4d4d4;
  white-space: pre-wrap;
  word-break: break-all;
  max-height: 400px;
  overflow-y: auto;
}

.no-content {
  padding: var(--spacing-md);
  text-align: center;
  color: var(--color-text-secondary);
  font-size: 13px;
}

/* 表单 */
.update-form {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-sm);
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.form-group label {
  font-weight: 500;
  font-size: 13px;
}

.textarea {
  width: 100%;
  padding: var(--spacing-xs) var(--spacing-sm);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  font-size: 13px;
  resize: vertical;
  min-height: 60px;
}

.textarea:focus {
  outline: none;
  border-color: var(--color-primary);
}

.error-message {
  text-align: center;
  padding: var(--spacing-lg);
  color: var(--color-danger);
}
</style>