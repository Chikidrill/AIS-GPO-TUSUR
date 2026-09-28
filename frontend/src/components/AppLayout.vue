<script setup lang="ts">
import AppHeader from './AppHeader.vue'
import AppNavigation from './AppNavigation.vue'

type UserRole = 'ADMIN' | 'TEACHER' | 'STUDENT'

interface User {
  fullName: string
  role: UserRole
}

defineProps<{
  user: User | null
}>()

const emit = defineEmits<{
  logout: []
}>()
</script>

<template>
  <div class="app-layout">
    <AppHeader
      :user="user"
      @logout="emit('logout')"
    />

    <AppNavigation />

    <slot />
  </div>
</template>