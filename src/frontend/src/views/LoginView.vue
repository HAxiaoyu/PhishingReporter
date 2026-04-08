<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/composables/useAuth'

const router = useRouter()
const authStore = useAuthStore()

const apiKey = ref('')
const isLoading = ref(false)
const error = ref('')

async function handleLogin() {
  if (!apiKey.value.trim()) {
    error.value = '请输入 API Key'
    return
  }

  isLoading.value = true
  error.value = ''

  try {
    const success = await authStore.login(apiKey.value.trim())
    if (success) {
      router.push('/')
    } else {
      error.value = authStore.error || '登录失败'
    }
  } finally {
    isLoading.value = false
  }
}
</script>

<template>
  <div class="login-page">
    <div class="login-card card">
      <div class="login-header">
        <span class="logo">🎣</span>
        <h1>钓鱼邮件上报系统</h1>
        <p>管理后台登录</p>
      </div>

      <form @submit.prevent="handleLogin" class="login-form">
        <div class="form-group">
          <label for="apiKey">API Key</label>
          <input
            id="apiKey"
            v-model="apiKey"
            type="password"
            class="input"
            placeholder="请输入 API Key"
            :disabled="isLoading"
          />
        </div>

        <p v-if="error" class="error">{{ error }}</p>

        <button type="submit" class="btn btn-primary" :disabled="isLoading">
          {{ isLoading ? '验证中...' : '登录' }}
        </button>
      </form>

      <div class="login-footer">
        <p>请使用系统管理员提供的 API Key 登录</p>
      </div>
    </div>
  </div>
</template>

<style scoped>
.login-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #1e293b 0%, #334155 100%);
}

.login-card {
  width: 400px;
  max-width: 90%;
}

.login-header {
  text-align: center;
  margin-bottom: var(--spacing-lg);
}

.logo {
  font-size: 48px;
  display: block;
  margin-bottom: var(--spacing-sm);
}

.login-header h1 {
  font-size: 20px;
  margin-bottom: var(--spacing-xs);
}

.login-header p {
  color: var(--color-text-secondary);
  font-size: 14px;
}

.login-form {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-md);
}

.form-group label {
  display: block;
  margin-bottom: var(--spacing-xs);
  font-weight: 500;
}

.error {
  color: var(--color-danger);
  font-size: 14px;
  margin: 0;
}

.login-footer {
  margin-top: var(--spacing-lg);
  padding-top: var(--spacing-md);
  border-top: 1px solid var(--color-border);
  text-align: center;
}

.login-footer p {
  color: var(--color-text-secondary);
  font-size: 12px;
}
</style>