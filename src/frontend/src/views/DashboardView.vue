<script setup lang="ts">
import { ref, onMounted } from 'vue'
import api from '@/api'
import type { Statistics } from '@/types'
import { STATUS_LABELS, getRiskLevel } from '@/types'

const statistics = ref<Statistics | null>(null)
const isLoading = ref(true)
const error = ref('')

async function loadStatistics() {
  try {
    isLoading.value = true
    statistics.value = await api.getStatistics()
  } catch (e) {
    error.value = '加载统计数据失败'
    console.error(e)
  } finally {
    isLoading.value = false
  }
}

onMounted(loadStatistics)
</script>

<template>
  <div class="dashboard">
    <h2 class="page-title">仪表盘</h2>

    <div v-if="isLoading" class="loading">
      <div class="spinner"></div>
    </div>

    <div v-else-if="error" class="error-message">
      {{ error }}
      <button class="btn btn-primary btn-sm" @click="loadStatistics">重试</button>
    </div>

    <template v-else-if="statistics">
      <!-- 统计卡片 -->
      <div class="stats-grid">
        <div class="stat-card card">
          <div class="stat-icon">📊</div>
          <div class="stat-content">
            <div class="stat-value">{{ statistics.totalReports }}</div>
            <div class="stat-label">总上报数</div>
          </div>
        </div>

        <div class="stat-card card">
          <div class="stat-icon">⏳</div>
          <div class="stat-content">
            <div class="stat-value">{{ statistics.pendingReports }}</div>
            <div class="stat-label">待处理</div>
          </div>
        </div>

        <div class="stat-card card">
          <div class="stat-icon">⚠️</div>
          <div class="stat-content">
            <div class="stat-value danger">{{ statistics.confirmedPhishing }}</div>
            <div class="stat-label">确认钓鱼</div>
          </div>
        </div>

        <div class="stat-card card">
          <div class="stat-icon">✅</div>
          <div class="stat-content">
            <div class="stat-value success">{{ statistics.falsePositives }}</div>
            <div class="stat-label">误报</div>
          </div>
        </div>
      </div>

      <!-- 状态分布 -->
      <div class="charts-grid">
        <div class="card chart-card">
          <h3>状态分布</h3>
          <div class="status-list">
            <div
              v-for="(count, status) in statistics.reportsByStatus"
              :key="status"
              class="status-item"
            >
              <span class="status-label">{{ STATUS_LABELS[status as keyof typeof STATUS_LABELS] || status }}</span>
              <span class="status-count">{{ count }}</span>
            </div>
          </div>
        </div>

        <div class="card chart-card">
          <h3>风险分类分布</h3>
          <div class="status-list">
            <div
              v-for="(count, category) in statistics.reportsByCategory"
              :key="category"
              class="status-item"
            >
              <span class="status-label">{{ category || '未分类' }}</span>
              <span class="status-count">{{ count }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- 近期趋势 -->
      <div class="card" v-if="statistics.recentTrend?.length">
        <h3>近期上报趋势</h3>
        <div class="trend-chart">
          <div
            v-for="item in statistics.recentTrend"
            :key="item.date"
            class="trend-bar"
            :style="{ height: `${Math.min(item.count * 5, 100)}px` }"
            :title="`${item.date}: ${item.count} 条`"
          >
            <span class="trend-value">{{ item.count }}</span>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.dashboard {
  width: 100%;
}

.page-title {
  font-size: 20px;
  margin-bottom: var(--spacing-md);
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: var(--spacing-sm);
  margin-bottom: var(--spacing-md);
}

@media (max-width: 1024px) {
  .stats-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 600px) {
  .stats-grid {
    grid-template-columns: 1fr;
  }
}

.stat-card {
  display: flex;
  align-items: center;
  gap: var(--spacing-sm);
  padding: var(--spacing-md);
}

.stat-icon {
  font-size: 28px;
}

.stat-content {
  flex: 1;
}

.stat-value {
  font-size: 24px;
  font-weight: 700;
}

.stat-value.danger {
  color: var(--color-danger);
}

.stat-value.success {
  color: var(--color-success);
}

.stat-label {
  color: var(--color-text-secondary);
  font-size: 13px;
}

.charts-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: var(--spacing-md);
  margin-bottom: var(--spacing-md);
}

@media (max-width: 768px) {
  .charts-grid {
    grid-template-columns: 1fr;
  }
}

.chart-card {
  padding: var(--spacing-md);
}

.chart-card h3 {
  margin-bottom: var(--spacing-sm);
  font-size: 15px;
}

.status-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.status-item {
  display: flex;
  justify-content: space-between;
  padding: var(--spacing-xs) var(--spacing-sm);
  background: var(--color-bg);
  border-radius: var(--radius-sm);
  font-size: 13px;
}

.status-label {
  color: var(--color-text);
}

.status-count {
  font-weight: 600;
}

.trend-chart {
  display: flex;
  align-items: flex-end;
  gap: 2px;
  height: 100px;
  padding-top: var(--spacing-sm);
}

.trend-bar {
  flex: 1;
  min-width: 16px;
  background: var(--color-primary);
  border-radius: var(--radius-sm) var(--radius-sm) 0 0;
  position: relative;
  cursor: pointer;
  transition: height 0.3s ease;
}

.trend-bar:hover {
  background: var(--color-primary-dark);
}

.trend-value {
  position: absolute;
  top: -16px;
  left: 50%;
  transform: translateX(-50%);
  font-size: 10px;
  color: var(--color-text-secondary);
}

.error-message {
  text-align: center;
  padding: var(--spacing-lg);
  color: var(--color-danger);
}
</style>