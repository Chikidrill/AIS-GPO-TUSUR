<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { http } from '../api/http'
import AppLayout from '../components/AppLayout.vue'

type UserRole = 'ADMIN' | 'TEACHER' | 'STUDENT'
type WorkStatus = 'SUBMITTED' | 'NEEDS_REVISION' | 'ACCEPTED'

interface CurrentUser {
  id: number
  email: string
  fullName: string
  role: UserRole
}

interface StudentWork {
  id: number
  projectId: number
  projectName: string
  studentId: number
  studentName: string
  groupNumber: string | null
  status: WorkStatus
  latestVersionNumber: number
  lastSubmittedAt: string
  updatedAt: string
}

const router = useRouter()

const currentUser = ref<CurrentUser | null>(null)
const works = ref<StudentWork[]>([])

const loading = ref(true)
const error = ref('')

const search = ref('')
const selectedProject = ref('')
const selectedStatus = ref('')

const statusLabels: Record<WorkStatus, string> = {
  SUBMITTED: 'На проверке',
  NEEDS_REVISION: 'На доработке',
  ACCEPTED: 'Принята',
}

const projects = computed(() => {
  const map = new Map<number, string>()

  works.value.forEach(work => {
    map.set(work.projectId, work.projectName)
  })

  return [...map.entries()]
    .map(([id, name]) => ({ id, name }))
    .sort((a, b) => a.name.localeCompare(b.name, 'ru'))
})

const filteredWorks = computed(() => {
  const query = search.value.trim().toLowerCase()

  return works.value.filter(work => {
    const matchesSearch =
      !query ||
      work.studentName.toLowerCase().includes(query) ||
      work.groupNumber?.toLowerCase().includes(query) ||
      work.projectName.toLowerCase().includes(query)

    const matchesProject =
      !selectedProject.value ||
      work.projectId === Number(selectedProject.value)

    const matchesStatus =
      !selectedStatus.value ||
      work.status === selectedStatus.value

    return matchesSearch && matchesProject && matchesStatus
  })
})

function formatDate(value: string) {
  return new Intl.DateTimeFormat('ru-RU', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(new Date(value))
}

async function loadPage() {
  loading.value = true
  error.value = ''

  try {
    const [userResponse, worksResponse] = await Promise.all([
      http.get<CurrentUser>('/me'),
      http.get<StudentWork[]>('/admin/works'),
    ])

    currentUser.value = userResponse.data
    works.value = worksResponse.data
  } catch {
    error.value = 'Не удалось загрузить работы студентов.'
  } finally {
    loading.value = false
  }
}

async function logout() {
  localStorage.removeItem('accessToken')
  localStorage.removeItem('role')

  await router.push('/login')
}

onMounted(loadPage)
</script>

<template>
  <AppLayout
    :user="currentUser"
    @logout="logout"
  >
    <main class="admin-works-content">
      <header class="admin-works-header">
        <h1>Работы студентов</h1>
        <p>Просмотр результатов работы студентов по всем проектам ГПО.</p>
      </header>

      <div class="admin-works-filters">
        <input
          v-model="search"
          class="admin-works-filter"
          type="search"
          placeholder="Поиск по студенту, группе или проекту"
        >

        <select
          v-model="selectedProject"
          class="admin-works-filter"
        >
          <option value="">Проект: все</option>

          <option
            v-for="project in projects"
            :key="project.id"
            :value="String(project.id)"
          >
            {{ project.name }}
          </option>
        </select>

        <select
          v-model="selectedStatus"
          class="admin-works-filter"
        >
          <option value="">Статус: все</option>
          <option value="SUBMITTED">На проверке</option>
          <option value="NEEDS_REVISION">На доработке</option>
          <option value="ACCEPTED">Принята</option>
        </select>
      </div>

      <p
        v-if="loading"
        class="admin-works-state"
      >
        Загрузка работ...
      </p>

      <div
        v-else-if="error"
        class="admin-works-state admin-works-state--error"
      >
        <p>{{ error }}</p>
        <button type="button" @click="loadPage">
          Повторить
        </button>
      </div>

      <template v-else>
        <p class="admin-works-count">
          Найдено работ: {{ filteredWorks.length }}
        </p>

        <p
          v-if="filteredWorks.length === 0"
          class="admin-works-state"
        >
          Работы не найдены.
        </p>

        <div
          v-else
          class="admin-works-table"
        >
          <div class="admin-works-table__header">
            <span>Студент</span>
            <span>Группа</span>
            <span>Проект</span>
            <span>Версия</span>
            <span>Дата отправки</span>
            <span>Статус</span>
          </div>

          <RouterLink
            v-for="work in filteredWorks"
            :key="work.id"
            class="admin-works-table__row"
            :to="`/admin/works/${work.id}`"
          >
            <span class="admin-works-table__student">
              {{ work.studentName }}
            </span>

            <span>{{ work.groupNumber ?? '—' }}</span>

            <span>{{ work.projectName }}</span>

            <span>№ {{ work.latestVersionNumber }}</span>

            <span>{{ formatDate(work.lastSubmittedAt) }}</span>

            <span>
              <span
                class="admin-works-table__status"
                :class="`admin-works-table__status--${work.status.toLowerCase()}`"
              >
                {{ statusLabels[work.status] }}
              </span>
            </span>
          </RouterLink>
        </div>
      </template>
    </main>
  </AppLayout>
</template>

<style scoped src="../styles/pages/admin-works.css"></style>