<script setup lang="ts">
import { onMounted, ref, computed } from 'vue'
import { useRouter } from 'vue-router'

import { http } from '../api/http'
import AppLayout from '../components/AppLayout.vue'

type UserRole = 'ADMIN' | 'TEACHER' | 'STUDENT'

type ProjectStatus =
  | 'DRAFT'
  | 'OPEN'
  | 'IN_PROGRESS'
  | 'COMPLETED'
  | 'ARCHIVED'

interface CurrentUser {
  id: number
  email: string
  fullName: string
  role: UserRole
}

interface TeacherProject {
  id: number
  code: string | null
  name: string
  faculty: string | null
  department: string
  status: ProjectStatus
  occupiedPlaces: number
  totalPlaces: number | null
}

const router = useRouter()

const currentUser = ref<CurrentUser | null>(null)
const projects = ref<TeacherProject[]>([])

const loading = ref(true)
const error = ref('')

const search = ref('')
const selectedStatus = ref('')

const filteredProjects = computed(() => {
  const query = search.value.trim().toLowerCase()

  return projects.value.filter((project) =>{
    const matchesSearch = 
      !query ||
      project.name.toLowerCase().includes(query) ||
      project.code?.toLowerCase().includes(query)

    const matchesStatus = !selectedStatus.value || project.status === selectedStatus.value

    return matchesSearch && matchesStatus
  })
})

const statusLabels: Record<ProjectStatus, string> = {
  DRAFT: 'Черновик',
  OPEN: 'Идёт набор',
  IN_PROGRESS: 'В работе',
  COMPLETED: 'Завершён',
  ARCHIVED: 'Архив',
}

async function loadPage() {
  loading.value = true
  error.value = ''

  try {
    const [userResponse, projectsResponse] = await Promise.all([
      http.get<CurrentUser>('/me'),
      http.get<TeacherProject[]>('/me/projects'),
    ])

    currentUser.value = userResponse.data
    projects.value = projectsResponse.data
  } catch {
    error.value = 'Не удалось загрузить проекты.'
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
    <main class="teacher-projects-content">
      <header class="teacher-projects-header">
        <h1>Мои проекты</h1>

        <p>
          Проекты ГПО, в которых вы назначены преподавателем.
        </p>
      </header>
      <div class="teacher-projects-filters">
        <input
        v-model="search"
        class="teacher-projects-filter"
        placeholder="Поиск по названию или коду проекта">

        <select
          v-model="selectedStatus"
          class="teacher-projects-filter teacher-projects-filter--status">
          <option value="">Статус: все</option>
          <option value="DRAFT">Черновик</option>
          <option value="OPEN">Идёт набор</option>
          <option value="IN_PROGRESS">В работе</option>
          <option value="COMPLETED">Завершён</option>
          <option value="ARCHIVED">Архив</option>
         </select> 
      </div>
      <p
        v-if="loading"
        class="teacher-projects-state"
      >
        Загрузка проектов...
      </p>

      <p
        v-else-if="error"
        class="teacher-projects-state teacher-projects-state--error"
      >
        {{ error }}
      </p>

      <p
        v-else-if="projects.length === 0"
        class="teacher-projects-state"
      >
        У вас пока нет назначенных проектов.
      </p>
      <p
        v-else-if="filteredProjects.length === 0"
        class="teacher-projects-state"
      >
        По заданным параметрам проекты не найдены.
      </p>
      <div
        v-else
        class="teacher-projects-table"
      >
        <div class="teacher-projects-table__header">
          <span>Код</span>
          <span>Название проекта</span>
          <span>Участники</span>
          <span>Статус</span>
          <span>Действия</span>
        </div>

        <div
          v-for="project in filteredProjects"
          :key="project.id"
          class="teacher-projects-table__row"
        >
          <span class="teacher-projects-table__code">
            {{ project.code ?? '—' }}
          </span>

          <span class="teacher-projects-table__name">
            {{ project.name }}
          </span>

          <span>
            {{ project.occupiedPlaces }} /
            {{ project.totalPlaces ?? '—' }}
          </span>

          <div class="teacher-projects-table__status-cell">
            <span
              class="teacher-projects-table__status"
              :class="`teacher-projects-table__status--${project.status.toLowerCase()}`"
            >
              {{ statusLabels[project.status] }}
            </span>
          </div>

          <div class="teacher-projects-table__actions">
            <RouterLink
              class="teacher-projects-table__action"
              :to="`/projects/${project.id}`"
            >
              Открыть
            </RouterLink>
            <RouterLink
              class="teacher-projects-table__action"
              :to="`/myprojects/${project.id}/participants`"
            >
              Участники
            </RouterLink>
            <RouterLink
              class="teacher-projects-table__action"
              :to="`/myprojects/${project.id}/works`"
            >
              Работы
            </RouterLink>
          </div>
        </div>
      </div>
    </main>
  </AppLayout>
</template>

<style
  scoped
  src="../styles/pages/teacher-projects.css"
></style>