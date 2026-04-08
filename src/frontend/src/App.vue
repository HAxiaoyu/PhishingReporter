<script setup lang="ts">
import { RouterView } from 'vue-router'
import AppLayout from '@/components/AppLayout.vue'
import { useAuthStore } from '@/composables/useAuth'
import { computed } from 'vue'

const authStore = useAuthStore()
const isAuthenticated = computed(() => authStore.isAuthenticated)
</script>

<template>
  <AppLayout v-if="isAuthenticated">
    <RouterView />
  </AppLayout>
  <RouterView v-else />
</template>