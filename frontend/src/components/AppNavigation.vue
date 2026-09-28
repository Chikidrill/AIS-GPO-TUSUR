<script setup lang="ts">
import { computed } from 'vue'

type UserRole = 'ADMIN' | 'TEACHER' | 'STUDENT'

interface NavigationItem {
  label: string
  to: string
  roles?: UserRole[]
}

const props = defineProps<{
  role: UserRole | null
}>()

const navigationItems: NavigationItem[] = [
  {
    label: 'Каталог проектов',
    to: '/projects',
    roles: ['ADMIN', 'STUDENT', 'TEACHER']
  },
  {
    label: 'Мои заявки',
    to: '/applications',
    roles: ['STUDENT'],
  },
  {
    label: 'Работы',
    to: '/works',
    roles: ['STUDENT'],
  },
  {
    label: 'Мой проект',
    to: '/myproject',
    roles: ['STUDENT'],
  },
  {
    label: 'Мои проекты',
    to: '/myprojects',
    roles: ['TEACHER'],
  },
  {
    label: 'Участники',
    to: '/projects_participants',
    roles: ['TEACHER'],
  },
  {
    label: 'Работы',
    to: '/projects_works',
    roles: ['TEACHER'],
  },
  {
    label: 'Управление проектами',
    to: '/admin/projects',
    roles: ['ADMIN'],
  },
  {
    label: 'Заявки',
    to: '/applications',
    roles: ['ADMIN'],
  },
  {
    label: 'Участники',
    to: '/participants',
    roles: ['ADMIN'],
  }
]

const visibleNavigationItems = computed(() => {
  return navigationItems.filter((item) => {
    if (!item.roles) {
      return true
    }

    if (!props.role) {
      return false
    }

    return item.roles.includes(props.role)
  })
})
</script>

<template>
  <nav class="app-navigation">
    <div class="app-navigation__inner">
      <RouterLink
        v-for="item in visibleNavigationItems"
        :key="item.to"
        class="app-navigation__item"
        :to="item.to"
      >
        {{ item.label }}
      </RouterLink>
    </div>
  </nav>
</template>

<style
  scoped
  src="../styles/components/app-navigation.css"
></style>