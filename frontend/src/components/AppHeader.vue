<script setup lang="ts">
import {computed, ref} from 'vue'
import tusurLogo from '../assets/tusur_logo.svg'
import userIcon from '../assets/user_icon.svg'
type UserRole = 'ADMIN' | 'STUDENT' | 'TEACHER'

interface User{
    fullName: string
    role: UserRole
}

const props = defineProps<{
    user?: User | null
}>()

const emit = defineEmits<{
    logout: []
}>()

const menuOpen = ref(false)

const roleName = computed(() => {
  switch (props.user?.role) {
    case 'ADMIN':
      return 'Администратор'
    case 'TEACHER':
      return 'Преподаватель'
    case 'STUDENT':
      return 'Студент'
    default:
      return ''
  }
})
</script>

<template>
  <header class="app-header">
    <RouterLink
        to="/projects"
        class="app-header__brand"
        >
        <img
            class="app-header__logo"
            :src="tusurLogo"
            alt="ТУСУР"
        >

        <div class="app-header__divider" />

        <div class="app-header__title">
            Групповое проектное обучение
        </div>
        </RouterLink>

    <div class="app-header__divider" />

    

    <div class="app-header__spacer" />

    <div
      v-if="user"
      class="app-header__user"
    >
      <button
        class="app-header__user-button"
        type="button"
        @click="menuOpen = !menuOpen"
      >
        <img
            class="app-header__user-icon"
            :src="userIcon"
            alt=""
            >

        <div class="app-header__user-text">
          <span class="app-header__user-name">
            {{ user.fullName }}
          </span>

          <span class="app-header__user-role">
            {{ roleName }}
          </span>
        </div>
      </button>

      <div
        v-if="menuOpen"
        class="app-header__menu"
      >
        <button
          type="button"
          @click="emit('logout')"
        >
          Выйти
        </button>
      </div>
    </div>
  </header>
</template>

<style
  scoped
  src="../styles/components/app-header.css"
></style>
