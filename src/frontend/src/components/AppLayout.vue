<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/composables/useAuth'

const router = useRouter()
const authStore = useAuthStore()

const sidebarCollapsed = ref(false)

const menuItems = [
  { path: '/', icon: '📊', label: '仪表盘' },
  { path: '/reports', icon: '📧', label: '上报列表' }
]

function handleLogout() {
  authStore.logout()
  router.push('/login')
}
</script>

<template>
  <div class="layout">
    <!-- 侧边栏 -->
    <aside class="sidebar" :class="{ collapsed: sidebarCollapsed }">
      <div class="sidebar-header">
        <span class="logo">🎣</span>
        <span v-if="!sidebarCollapsed" class="title">钓鱼上报系统</span>
      </div>

      <nav class="sidebar-nav">
        <router-link
          v-for="item in menuItems"
          :key="item.path"
          :to="item.path"
          class="nav-item"
        >
          <span class="icon">{{ item.icon }}</span>
          <span v-if="!sidebarCollapsed" class="label">{{ item.label }}</span>
        </router-link>
      </nav>

      <div class="sidebar-footer">
        <button class="btn btn-outline btn-sm" @click="handleLogout">
          退出登录
        </button>
      </div>
    </aside>

    <!-- 主内容区 -->
    <main class="main">
      <header class="header">
        <button class="toggle-btn" @click="sidebarCollapsed = !sidebarCollapsed">
          {{ sidebarCollapsed ? '▶' : '◀' }}
        </button>
        <h1 class="page-title">钓鱼邮件上报系统 - 管理后台</h1>
      </header>

      <div class="content">
        <slot />
      </div>
    </main>
  </div>
</template>

<style scoped>
.layout {
  display: flex;
  min-height: 100vh;
}

.sidebar {
  width: 240px;
  background: #1e293b;
  color: white;
  display: flex;
  flex-direction: column;
  transition: width 0.3s ease;
}

.sidebar.collapsed {
  width: 60px;
}

.sidebar-header {
  display: flex;
  align-items: center;
  padding: var(--spacing-md);
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.logo {
  font-size: 24px;
}

.title {
  margin-left: var(--spacing-sm);
  font-weight: 600;
  white-space: nowrap;
}

.sidebar-nav {
  flex: 1;
  padding: var(--spacing-md) 0;
}

.nav-item {
  display: flex;
  align-items: center;
  padding: var(--spacing-sm) var(--spacing-md);
  color: rgba(255, 255, 255, 0.7);
  text-decoration: none;
  transition: all 0.2s ease;
}

.nav-item:hover {
  background: rgba(255, 255, 255, 0.1);
  color: white;
}

.nav-item.router-link-exact-active {
  background: var(--color-primary);
  color: white;
}

.icon {
  font-size: 18px;
  width: 24px;
  text-align: center;
}

.label {
  margin-left: var(--spacing-sm);
  white-space: nowrap;
}

.sidebar-footer {
  padding: var(--spacing-md);
  border-top: 1px solid rgba(255, 255, 255, 0.1);
}

.main {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.header {
  display: flex;
  align-items: center;
  padding: var(--spacing-md) var(--spacing-lg);
  background: var(--color-surface);
  border-bottom: 1px solid var(--color-border);
  box-shadow: var(--shadow-sm);
}

.toggle-btn {
  width: 32px;
  height: 32px;
  border: none;
  background: var(--color-bg);
  border-radius: var(--radius-sm);
  cursor: pointer;
  margin-right: var(--spacing-md);
}

.page-title {
  font-size: 18px;
  font-weight: 600;
}

.content {
  flex: 1;
  padding: var(--spacing-lg);
  overflow-y: auto;
}
</style>