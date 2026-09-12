<script setup lang="ts">
import axios from 'axios'
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'

import { http } from '../api/http'
import tusurLogo from '../assets/tusur_logo.svg'

interface LoginResponse {
  accessToken: string
  tokenType: string
  expiresInSeconds: number
  userId: number
  role: 'ADMIN' | 'TEACHER' | 'STUDENT'
}

interface ApiError {
  code?: string
  message?: string
}

const router = useRouter()

const email = ref('')
const password = ref('')
const showPassword = ref(false)

const loading = ref(false)
const error = ref('')

const canSubmit = computed(() => {
  return (
    email.value.trim().length > 0 &&
    password.value.length > 0 &&
    !loading.value
  )
})

async function login() {
  if (!canSubmit.value) {
    return
  }

  error.value = ''
  loading.value = true

  try {
    const { data } = await http.post<LoginResponse>('/auth/login', {
      email: email.value.trim(),
      password: password.value,
    })

    localStorage.setItem('accessToken', data.accessToken)
    localStorage.setItem('role', data.role)

    await router.push('/projects')
  } catch (err) {
    if (axios.isAxiosError<ApiError>(err)) {
      if (err.response?.data?.code === 'BAD_CREDENTIALS') {
        error.value = 'Неверная электронная почта или пароль.'
      } else if (!err.response) {
        error.value = 'Не удалось подключиться к серверу.'
      } else {
        error.value = 'Не удалось выполнить вход. Попробуйте ещё раз.'
      }
    } else {
      error.value = 'Не удалось выполнить вход. Попробуйте ещё раз.'
    }
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="login-page">
    <header class="login-header">
      <img
        class="login-header__logo"
        :src="tusurLogo"
        alt="ТУСУР"
      >

      <div class="login-header__divider" />

      <div class="login-header__title">
        Групповое проектное обучение
      </div>
    </header>

    <div class="login-brand-line" />

    <main class="login-content">
      <section class="login-card">
        <div class="login-card__header">
          <h1>Вход в систему</h1>

          <p>
            Введите данные вашей учётной записи
          </p>
        </div>

        <form
          class="login-form"
          @submit.prevent="login"
        >
          <label class="login-field">
            <span>Электронная почта</span>

            <input
              v-model="email"
              type="email"
              placeholder="example@tusur.ru"
              autocomplete="username"
              :disabled="loading"
              required
            >
          </label>

          <label class="login-field">
            <span>Пароль</span>

            <div class="login-password">
              <input
                v-model="password"
                :type="showPassword ? 'text' : 'password'"
                placeholder="Введите пароль"
                autocomplete="current-password"
                :disabled="loading"
                required
              >

              <button
                class="login-password__toggle"
                type="button"
                :disabled="loading"
                @click="showPassword = !showPassword"
              >
                {{ showPassword ? 'Скрыть' : 'Показать' }}
              </button>
            </div>
          </label>

          <div
            v-if="error"
            class="login-error"
            role="alert"
          >
            {{ error }}
          </div>

          <button
            class="login-submit"
            type="submit"
            :disabled="!canSubmit"
          >
            <span
              v-if="loading"
              class="login-submit__spinner"
            />

            {{ loading ? 'Вход...' : 'Войти' }}
          </button>
        </form>

        <p class="login-card__hint">
          Для входа используйте учётные данные,
          предоставленные администратором системы.
        </p>
      </section>
    </main>
  </div>
</template>

<style
  scoped
  src="../styles/pages/login.css"
></style>