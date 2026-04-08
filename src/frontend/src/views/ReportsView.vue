<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import api from '@/api'
import type { PhishingReport, ReportStatus, ReportFilter, PagedResponse } from '@/types'
import { STATUS_LABELS, STATUS_COLORS, getRiskLevel } from '@/types'

const router = useRouter()
const route = useRoute()

const reports = ref<PhishingReport[]>([])
const isLoading = ref(true)
const error = ref('')
const totalCount = ref(0)

const filter = ref<ReportFilter>({
  page: 1,
  pageSize: 20,
  status: undefined,
  reportedBy: undefined
})

const statusOptions: (ReportStatus | undefined)[] = [
  undefined,
  'Pending',
  'Analyzing',
  'Confirmed',
  'FalsePositive',
  'Resolved'
]

async function loadReports() {
  try {
    isLoading.value = true
    const response: PagedResponse<PhishingReport> = await api.getReports(filter.value)
    reports.value = response.items
    totalCount.value = response.totalCount
  } catch (e) {
    error.value = '加载数据失败'
    console.error(e)
  } finally {
    isLoading.value = false
  }
}

function handlePageChange(page: number) {
  filter.value.page = page
  loadReports()
}

function handleFilterChange() {
  filter.value.page = 1
  loadReports()
}

function viewDetail(id: string) {
  router.push(`/reports/${id}`)
}

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleString('zh-CN')
}

const totalPages = ref(1)
watch([totalCount, filter], () => {
  totalPages.value = Math.ceil(totalCount.value / filter.value.pageSize)
}, { immediate: true })

onMounted(loadReports)
</script>

<template>
  <div class="reports-page">
    <div class="page-header">
      <h2 class="page-title">上报列表</h2>
    </div>

    <!-- 筛选器 -->
    <div class="filters card">
      <div class="filter-row">
        <div class="filter-item">
          <label>状态</label>
          <select v-model="filter.status" class="select" @change="handleFilterChange">
            <option v-for="status in statusOptions" :key="status" :value="status">
              {{ status ? STATUS_LABELS[status] : '全部' }}
            </option>
          </select>
        </div>

        <div class="filter-item">
          <label>上报人</label>
          <input
            v-model="filter.reportedBy"
            type="text"
            class="input"
            placeholder="输入邮箱搜索"
            @change="handleFilterChange"
          />
        </div>

        <div class="filter-item">
          <button class="btn btn-primary" @click="loadReports">刷新</button>
        </div>
      </div>
    </div>

    <div v-if="isLoading" class="loading">
      <div class="spinner"></div>
    </div>

    <div v-else-if="error" class="error-message">
      {{ error }}
      <button class="btn btn-primary btn-sm" @click="loadReports">重试</button>
    </div>

    <template v-else>
      <!-- 表格 -->
      <div class="card table-container">
        <table class="table">
          <thead>
            <tr>
              <th>上报时间</th>
              <th>发件人</th>
              <th>主题</th>
              <th>风险评分</th>
              <th>状态</th>
              <th>上报人</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="report in reports" :key="report.id">
              <td>{{ formatDate(report.reportedAt) }}</td>
              <td>
                <div class="sender-info">
                  <div class="sender-name">{{ report.senderName || '-' }}</div>
                  <div class="sender-email">{{ report.senderEmail }}</div>
                </div>
              </td>
              <td>
                <div class="subject" :title="report.subject">
                  {{ report.subject?.substring(0, 40) }}{{ report.subject?.length && report.subject.length > 40 ? '...' : '' }}
                </div>
              </td>
              <td>
                <span
                  class="risk-score"
                  :style="{ backgroundColor: getRiskLevel(report.riskScore).color }"
                >
                  {{ report.riskScore }}
                </span>
              </td>
              <td>
                <span
                  class="status-badge"
                  :style="{ backgroundColor: STATUS_COLORS[report.status] || '#777' }"
                >
                  {{ STATUS_LABELS[report.status] || report.status }}
                </span>
              </td>
              <td>{{ report.reportedBy || '-' }}</td>
              <td>
                <button class="btn btn-outline btn-sm" @click="viewDetail(report.id)">
                  查看详情
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- 分页 -->
      <div class="pagination" v-if="totalPages > 1">
        <button
          class="btn btn-outline"
          :disabled="filter.page === 1"
          @click="handlePageChange(filter.page - 1)"
        >
          上一页
        </button>

        <span class="page-info">
          第 {{ filter.page }} / {{ totalPages }} 页，共 {{ totalCount }} 条
        </span>

        <button
          class="btn btn-outline"
          :disabled="filter.page >= totalPages"
          @click="handlePageChange(filter.page + 1)"
        >
          下一页
        </button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.reports-page {
  max-width: 1400px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: var(--spacing-md);
}

.page-title {
  font-size: 24px;
}

.filters {
  margin-bottom: var(--spacing-md);
}

.filter-row {
  display: flex;
  gap: var(--spacing-md);
  flex-wrap: wrap;
}

.filter-item {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-xs);
  min-width: 150px;
}

.filter-item label {
  font-size: 12px;
  color: var(--color-text-secondary);
}

.sender-info {
  font-size: 12px;
}

.sender-name {
  font-weight: 500;
}

.sender-email {
  color: var(--color-text-secondary);
}

.subject {
  max-width: 300px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.risk-score {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 12px;
  color: white;
  font-size: 12px;
  font-weight: 600;
}

.status-badge {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 12px;
  color: white;
  font-size: 12px;
}

.page-info {
  padding: 0 var(--spacing-md);
  color: var(--color-text-secondary);
}

.error-message {
  text-align: center;
  padding: var(--spacing-xl);
  color: var(--color-danger);
}
</style>