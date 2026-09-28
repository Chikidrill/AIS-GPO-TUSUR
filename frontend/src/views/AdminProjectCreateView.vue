<script setup lang="ts">
import axios from 'axios'
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { http } from '../api/http'
import AppLayout from '../components/AppLayout.vue'

type UserRole = 'ADMIN' | 'TEACHER' | 'STUDENT'

interface CurrentUser {
  id: number
  email: string
  fullName: string
  role: UserRole
}

interface ApiError {
  code?: string
}

const router = useRouter()

const currentUser = ref<CurrentUser | null>(null)

const code = ref('')
const name = ref('')
const faculty = ref('')
const department = ref('')
const description = ref('')
const goal = ref('')
const direction = ref('')
const semester = ref<number | null>(null)
const competencies = ref('')
const totalPlaces = ref<number | null>(null)

const submitting = ref(false)
const error = ref('')

async function loadCurrentUser() {
  const { data } = await http.get<CurrentUser>('/me')
  currentUser.value = data
}

async function createProject() {
  error.value = ''

  if (!name.value.trim()) {
    error.value = 'Введите название проекта.'
    return
  }

  if (!department.value.trim()) {
    error.value = 'Введите кафедру.'
    return
  }

  submitting.value = true

  try {
    await http.post('/projects', {
      code: code.value.trim() || null,
      name: name.value.trim(),
      faculty: faculty.value.trim() || null,
      department: department.value.trim(),
      description: description.value.trim() || null,
      goal: goal.value.trim() || null,
      direction: direction.value.trim() || null,
      semester: semester.value,
      competencies: competencies.value
        .split(',')
        .map(item => item.trim())
        .filter(Boolean),
      totalPlaces: totalPlaces.value,
      supervisorId: null,
    })

    await router.push('/admin/projects')
  } catch (err) {
    if (axios.isAxiosError<ApiError>(err)) {
      switch (err.response?.data?.code) {
        case 'PROJECT_CODE_ALREADY_EXISTS':
          error.value = 'Проект с таким кодом уже существует.'
          break

        default:
          error.value = 'Не удалось создать проект.'
      }
    } else {
      error.value = 'Не удалось создать проект.'
    }
  } finally {
    submitting.value = false
  }
}

async function logout() {
  localStorage.removeItem('accessToken')
  localStorage.removeItem('role')

  await router.push('/login')
}

onMounted(loadCurrentUser)
</script>

<template>
  <AppLayout
    :user="currentUser"
    @logout="logout"
  >
    <main class="project-create-content">
      <header class="project-create-header">
        <div>
          <h1>Создание проекта</h1>

          <p>
            Заполните информацию о проекте ГПО.
          </p>
        </div>

        <RouterLink
          class="project-create-header__back"
          to="/admin/projects"
        >
          Назад к проектам
        </RouterLink>
      </header>

      <form
        class="project-create-form"
        @submit.prevent="createProject"
      >
        <section class="project-create-section">
          <h2>Основная информация</h2>

          <div class="project-create-grid">
            <label class="project-create-field">
              <span>Код проекта</span>

              <input
                v-model="code"
                type="text"
                maxlength="50"
                placeholder="Например, ПР1500"
              >
            </label>

            <label class="project-create-field">
              <span>Название проекта *</span>

              <input
                v-model="name"
                type="text"
                maxlength="255"
                placeholder="Название проекта"
                required
              >
            </label>

            <label class="project-create-field">
              <span>Подразделение</span>

              <input
                v-model="faculty"
                type="text"
                maxlength="255"
                placeholder="Например, ФВС"
              >
            </label>

            <label class="project-create-field">
              <span>Кафедра *</span>

              <input
                v-model="department"
                type="text"
                maxlength="255"
                placeholder="Например, Кафедра КСУП"
                required
              >
            </label>

            <label class="project-create-field">
              <span>Направление</span>

              <input
                v-model="direction"
                type="text"
                maxlength="255"
                placeholder="Информационные технологии"
              >
            </label>

            <label class="project-create-field">
              <span>Семестр</span>

              <input
                v-model.number="semester"
                type="number"
                min="1"
                max="20"
                placeholder="2"
              >
            </label>

            <label class="project-create-field">
              <span>Количество мест</span>

              <input
                v-model.number="totalPlaces"
                type="number"
                min="1"
                placeholder="8"
              >
            </label>

            <label class="project-create-field">
              <span>Компетенции</span>

              <input
                v-model="competencies"
                type="text"
                placeholder="Vue.js, REST API, Git"
              >
            </label>
          </div>
        </section>

        <section class="project-create-section">
          <h2>Описание проекта</h2>

          <label class="project-create-field">
            <span>Описание</span>

            <textarea
              v-model="description"
              rows="5"
              maxlength="10000"
              placeholder="Опишите содержание проекта"
            />
          </label>

          <label class="project-create-field">
            <span>Цель проекта</span>

            <textarea
              v-model="goal"
              rows="4"
              maxlength="10000"
              placeholder="Укажите основную цель проекта"
            />
          </label>
        </section>

        <p
          v-if="error"
          class="project-create-error"
        >
          {{ error }}
        </p>

        <div class="project-create-actions">
          <RouterLink
            class="project-create-button project-create-button--secondary"
            to="/admin/projects"
          >
            Отмена
          </RouterLink>

          <button
            class="project-create-button project-create-button--primary"
            type="submit"
            :disabled="submitting"
          >
            {{ submitting ? 'Создание...' : 'Создать проект' }}
          </button>
        </div>
      </form>
    </main>
  </AppLayout>
</template>

<style
  scoped
  src="../styles/pages/admin-project-create.css"
></style>