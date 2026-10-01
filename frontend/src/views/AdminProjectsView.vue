<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
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

interface Project {
  id: number
  code: string | null
  name: string
  faculty: string | null
  department: string
  status: ProjectStatus
  supervisorId: number | null
  supervisorName: string | null
}

const router = useRouter()

const currentUser = ref<CurrentUser | null>(null)
const projects = ref<Project[]>([])

const loading = ref(true)
const error = ref('')

const search = ref('')
const selectedFaculty = ref('')
const selectedDepartment = ref('')
const selectedStatus = ref('')

const faculties = computed(() => {
  return [
    ...new Set(
      projects.value
        .map(project => project.faculty)
        .filter((faculty): faculty is string => Boolean(faculty))
    ),
  ].sort()
})

const departments = computed(() => {
  return [
    ...new Set(
      projects.value
        .map(project => project.department)
        .filter(Boolean)
    ),
  ].sort()
})

const projectStats = computed(() => ({
  total: projects.value.length,

  open: projects.value.filter(
    project => project.status === 'OPEN'
  ).length,

  withoutSupervisor: projects.value.filter(
    project => project.supervisorId === null
  ).length,
}))

const filteredProjects = computed(() => {
  const query = search.value.trim().toLowerCase()

  return projects.value.filter((project) => {
    const matchesSearch =
      !query ||
      project.name.toLowerCase().includes(query) ||
      project.code?.toLowerCase().includes(query)

    const matchesFaculty =
      !selectedFaculty.value ||
      project.faculty === selectedFaculty.value

    const matchesDepartment =
      !selectedDepartment.value ||
      project.department === selectedDepartment.value

    const matchesStatus =
      !selectedStatus.value ||
      project.status === selectedStatus.value

    return (
      matchesSearch &&
      matchesFaculty &&
      matchesDepartment &&
      matchesStatus
    )
  })
})

async function loadPage() {
  loading.value = true
  error.value = ''

  try {
    const [userResponse, projectsResponse] = await Promise.all([
      http.get<CurrentUser>('/me'),
      http.get<Project[]>('/projects'),
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
    <main class="admin-projects-content">
      <header class="admin-projects-header">
        <div>
          <h1>Управление проектами</h1>

          <p>
            Создание и редактирование проектов, назначение преподавателей.
          </p>
        </div>

        <RouterLink
            class="admin-projects-header__create"
            to="/admin/projects/create"
            >
            Создать проект
        </RouterLink>
      </header>

      <div class="admin-projects-filters">
        <input
          v-model="search"
          class="admin-projects-filter"
          type="search"
          placeholder="Поиск по названию проекта"
        >

        <select
          v-model="selectedFaculty"
          class="admin-projects-filter"
        >
          <option value="">
            Подразделение: все
          </option>

          <option
            v-for="faculty in faculties"
            :key="faculty"
            :value="faculty"
          >
            {{ faculty }}
          </option>
        </select>

        <select
          v-model="selectedDepartment"
          class="admin-projects-filter"
        >
          <option value="">
            Кафедра: все
          </option>

          <option
            v-for="department in departments"
            :key="department"
            :value="department"
          >
            {{ department }}
          </option>
        </select>

        <select
          v-model="selectedStatus"
          class="admin-projects-filter"
        >
          <option value="">
            Статус: все
          </option>

          <option value="OPEN">
            Идёт набор
          </option>

          <option value="IN_PROGRESS">
            В работе
          </option>

          <option value="COMPLETED">
            Завершён
          </option>

          <option value="ARCHIVED">
            Архив
          </option>

          <option value="DRAFT">
            Черновик
          </option>
        </select>
      </div>

      <section class="admin-projects-stats">
        <div class="admin-projects-stat">
          <span>Всего проектов</span>

          <strong>
            {{ projectStats.total }}
          </strong>
        </div>

        <div class="admin-projects-stat">
          <span>Идёт набор</span>

          <strong class="admin-projects-stat__value--open">
            {{ projectStats.open }}
          </strong>
        </div>

        <div class="admin-projects-stat">
          <span>Без преподавателя</span>

          <strong class="admin-projects-stat__value--warning">
            {{ projectStats.withoutSupervisor }}
          </strong>
        </div>
      </section>

      <p
        v-if="loading"
        class="admin-projects-state"
      >
        Загрузка проектов...
      </p>

      <p
        v-else-if="error"
        class="admin-projects-state admin-projects-state--error"
      >
        {{ error }}
      </p>

      <p
        v-else-if="filteredProjects.length === 0"
        class="admin-projects-state"
      >
        Проекты не найдены.
      </p>

      <div
        v-else
        class="admin-projects-table"
      >
        <div class="admin-projects-table__header">
          <span>Код</span>
          <span>Название</span>
          <span>Подразд.</span>
          <span>Кафедра</span>
          <span>Преподаватель</span>
          <span>Действия</span>
        </div>

        <div
          v-for="project in filteredProjects"
          :key="project.id"
          class="admin-projects-table__row"
        >
          <span class="admin-projects-table__code">
            {{ project.code ?? '—' }}
          </span>

          <span class="admin-projects-table__name">
            {{ project.name }}
          </span>

          <span>
            {{ project.faculty ?? '—' }}
          </span>

          <span>
            {{ project.department }}
          </span>

          <span
            :class="{
              'admin-projects-table__unassigned':
                !project.supervisorName,
            }"
          >
            {{ project.supervisorName ?? 'Не назначен' }}
          </span>

          <div class="admin-projects-table__actions">
            <button
              class="admin-projects-table__action"
              type="button"
            >
              Редактировать
            </button>

            <button
              class="admin-projects-table__action"
              type="button"
            >
              Назначить
            </button>
            <button
                class="admin-projects-table__action-delete"
                type="button"
            > 
                Удалить
            </button>
          </div>
        </div>
      </div>
    </main>
  </AppLayout>
</template>

<style
  scoped
  src="../styles/pages/admin-projects.css"
></style>