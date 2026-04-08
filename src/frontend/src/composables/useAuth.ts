import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import api from '@/api'

export const useAuthStore = defineStore('auth', () => {
  const apiKey = ref<string | null>(localStorage.getItem('apiKey'))
  const isValidating = ref(false)
  const error = ref<string | null>(null)

  const isAuthenticated = computed(() => !!apiKey.value)

  async function login(key: string): Promise<boolean> {
    isValidating.value = true
    error.value = null

    try {
      const isValid = await api.validateApiKey(key)
      if (isValid) {
        apiKey.value = key
        api.setApiKey(key)
        return true
      } else {
        error.value = 'API Key 无效'
        return false
      }
    } catch (e) {
      error.value = '验证失败，请检查网络连接'
      return false
    } finally {
      isValidating.value = false
    }
  }

  function logout(): void {
    apiKey.value = null
    localStorage.removeItem('apiKey')
  }

  return {
    apiKey,
    isAuthenticated,
    isValidating,
    error,
    login,
    logout
  }
})