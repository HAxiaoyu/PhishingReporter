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
              <span class="value">{{ report.id }}</span>
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

        <!-- 分析指标 -->
        <div class="card" v-if="report.indicators?.length">
          <h3>分析指标</h3>
          <div class="indicators-list">
            <div
              v-for="(indicator, index) in report.indicators"
              :key="index"
              class="indicator-item"
            >
              <span class="indicator-type">{{ indicator.type }}</span>
              <span class="indicator-desc">{{ indicator.description }}</span>
              <span
                class="indicator-severity"
                :class="'severity-' + indicator.severity"
              >
                严重程度: {{ indicator.severity }}/5
              </span>
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
                class="input textarea"
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
  max-width: 1200px;
}

.page-header {
  display: flex;
  align-items: center;
  gap: var(--spacing-md);
  margin-bottom: var(--spacing-lg);
}

.page-title {
  font-size: 24px;
}

.detail-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(400px, 1fr));
  gap: var(--spacing-lg);
}

.info-card h3 {
  margin-bottom: var(--spacing-md);
  padding-bottom: var(--spacing-sm);
  border-bottom: 1px solid var(--color-border);
}

.info-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: var(--spacing-md);
}

.info-item {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-xs);
}

.info-item.full {
  grid-column: 1 / -1;
}

.info-item .label {
  font-size: 12px;
  color: var(--color-text-secondary);
}

.info-item .value {
  font-size: 14px;
}

.status-badge {
  display: inline-block;
  padding: 4px 12px;
  border-radius: 12px;
  color: white;
  font-size: 12px;
  width: fit-content;
}

.risk-score {
  display: inline-block;
  padding: 4px 12px;
  border-radius: 12px;
  color: white;
  font-size: 14px;
  font-weight: 600;
}

.indicators-list {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-sm);
}

.indicator-item {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-xs);
  padding: var(--spacing-sm);
  background: var(--color-bg);
  border-radius: var(--radius-sm);
}

.indicator-type {
  font-weight: 600;
  font-size: 14px;
}

.indicator-desc {
  font-size: 13px;
  color: var(--color-text-secondary);
}

.indicator-severity {
  font-size: 12px;
}

.severity-5, .severity-4 { color: var(--color-danger); }
.severity-3, .severity-2 { color: var(--color-warning); }
.severity-1 { color: var(--color-success); }

.attachments-list {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-sm);
}

.attachment-item {
  display: flex;
  align-items: center;
  gap: var(--spacing-sm);
  padding: var(--spacing-sm);
  background: var(--color-bg);
  border-radius: var(--radius-sm);
}

.attachment-icon {
  font-size: 20px;
}

.attachment-info {
  display: flex;
  flex-direction: column;
}

.attachment-name {
  font-weight: 500;
}

.attachment-meta {
  font-size: 12px;
  color: var(--color-text-secondary);
}

.malicious {
  color: var(--color-danger);
  margin-left: var(--spacing-sm);
}

.update-form {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-md);
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-xs);
}

.form-group label {
  font-weight: 500;
  font-size: 14px;
}

.textarea {
  resize: vertical;
  min-height: 80px;
}

.error-message {
  text-align: center;
  padding: var(--spacing-xl);
  color: var(--color-danger);
}
</style>