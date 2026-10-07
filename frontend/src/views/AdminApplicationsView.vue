<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { http } from '../api/http'
import AppLayout from '../components/AppLayout.vue'

type UserRole = 'ADMIN' | 'TEACHER' | 'STUDENT'

type ApplicationStatus =
  | 'CREATED'
  | 'UNDER_REVIEW'
  | 'APPROVED'
  | 'REJECTED'
  | 'CANCELLED'

interface CurrentUser {
  id: number
  email: string
  fullName: string
  role: UserRole
}

interface Application {
  id: number
  studentId: number
  studentName: string
  projectId: number
  projectName: string
  status: ApplicationStatus
  createdAt: string
  takenForReviewAt: string | null
  reviewedAt: string | null
  rejectionReason: string | null

  groupNumber?: string | null
}

interface Project {
  id: number
  code: string | null
  name: string
  faculty: string | null
  department: string
}

const router = useRouter()

const currentUser = ref<CurrentUser | null>(null)

const applications = ref<Application[]>([])
const projects = ref<Project[]>([])

const loading = ref(true)
const error = ref('')
const processingId = ref<number | null>(null)

const search = ref('')
const selectedProject = ref('')
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

const applicationStats = computed(() => ({
  underReview: applications.value.filter(
    application =>
      application.status === 'CREATED' ||
      application.status === 'UNDER_REVIEW'
  ).length,

  approved: applications.value.filter(
    application => application.status === 'APPROVED'
  ).length,

  rejected: applications.value.filter(
    application => application.status === 'REJECTED'
  ).length,
}))

const filteredApplications = computed(() => {
  const query = search.value.trim().toLowerCase()

  return applications.value.filter((application) => {
    const project = getProject(application.projectId)

    const matchesSearch =
      !query ||
      application.studentName.toLowerCase().includes(query)

    const matchesProject =
      !selectedProject.value ||
      application.projectId === Number(selectedProject.value)

    const matchesFaculty =
      !selectedFaculty.value ||
      project?.faculty === selectedFaculty.value

    const matchesDepartment =
      !selectedDepartment.value ||
      project?.department === selectedDepartment.value

    const matchesStatus =
      !selectedStatus.value ||
      (
        selectedStatus.value === 'UNDER_REVIEW'
          ? application.status === 'CREATED' ||
            application.status === 'UNDER_REVIEW'
          : application.status === selectedStatus.value
      )

    return (
      matchesSearch &&
      matchesProject &&
      matchesFaculty &&
      matchesDepartment &&
      matchesStatus
    )
  })
})

function getProject(projectId: number) {
  return projects.value.find(project => project.id === projectId)
}

function getProjectLabel(application: Application) {
  const project = getProject(application.projectId)

  if (!project?.code) {
    return application.projectName
  }

  return `${project.code} — ${application.projectName}`
}

function getStatusLabel(status: ApplicationStatus) {
  switch (status) {
    case 'CREATED':
    case 'UNDER_REVIEW':
      return 'На рассмотрении'

    case 'APPROVED':
      return 'Принята'

    case 'REJECTED':
      return 'Отклонена'

    case 'CANCELLED':
      return 'Отозвана'
  }
}

function getStatusClass(status: ApplicationStatus) {
  switch (status) {
    case 'CREATED':
    case 'UNDER_REVIEW':
      return 'admin-applications-status--review'

    case 'APPROVED':
      return 'admin-applications-status--approved'

    case 'REJECTED':
      return 'admin-applications-status--rejected'

    case 'CANCELLED':
      return 'admin-applications-status--cancelled'
  }
}

async function ensureUnderReview(application: Application) {
  if (application.status !== 'CREATED') {
    return
  }

  await http.post(
    `/admin/applications/${application.id}/take-for-review`
  )
}

async function approveApplication(application: Application) {
  if (processingId.value !== null) {
    return
  }

  processingId.value = application.id
  error.value = ''

  try {
    await ensureUnderReview(application)

    await http.post(
      `/admin/applications/${application.id}/approve`
    )

    await loadApplications()
  } catch {
    error.value = 'Не удалось принять заявку.'
  } finally {
    processingId.value = null
  }
}

async function rejectApplication(application: Application) {
  if (processingId.value !== null) {
    return
  }

  processingId.value = application.id
  error.value = ''

  try {
    await ensureUnderReview(application)

    await http.post(
      `/admin/applications/${application.id}/reject`,
      {
        reason: null,
      }
    )

    await loadApplications()
  } catch {
    error.value = 'Не удалось отклонить заявку.'
  } finally {
    processingId.value = null
  }
}

async function loadApplications() {
  const { data } = await http.get<Application[]>(
    '/admin/applications'
  )

  applications.value = data
}

async function loadPage() {
  loading.value = true
  error.value = ''

  try {
    const [userResponse, applicationsResponse, projectsResponse] =
      await Promise.all([
        http.get<CurrentUser>('/me'),
        http.get<Application[]>('/admin/applications'),
        http.get<Project[]>('/projects'),
      ])

    currentUser.value = userResponse.data
    applications.value = applicationsResponse.data
    projects.value = projectsResponse.data
  } catch {
    error.value = 'Не удалось загрузить заявки.'
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
    <main class="admin-applications-content">
      <header class="admin-applications-header">
        <h1>Управление заявками</h1>

        <p>
          Рассмотрение заявок студентов на участие в проектах ГПО.
        </p>
      </header>

      <div class="admin-applications-filters">
        <input
          v-model="search"
          class="admin-applications-filter"
          type="search"
          placeholder="Поиск по ФИО студента"
        >

        <select
          v-model="selectedProject"
          class="admin-applications-filter"
        >
          <option value="">
            Проект: все
          </option>

          <option
            v-for="project in projects"
            :key="project.id"
            :value="project.id"
          >
            {{ project.code ?? '—' }} — {{ project.name }}
          </option>
        </select>

        <select
          v-model="selectedFaculty"
          class="admin-applications-filter"
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
          class="admin-applications-filter"
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
          class="admin-applications-filter"
        >
          <option value="">
            Статус: все
          </option>

          <option value="UNDER_REVIEW">
            На рассмотрении
          </option>

          <option value="APPROVED">
            Принята
          </option>

          <option value="REJECTED">
            Отклонена
          </option>

          <option value="CANCELLED">
            Отозвана
          </option>
        </select>
      </div>

      <section class="admin-applications-stats">
        <div class="admin-applications-stat">
          <span>На рассмотрении</span>

          <strong class="admin-applications-stat__review">
            {{ applicationStats.underReview }}
          </strong>
        </div>

        <div class="admin-applications-stat">
          <span>Принято</span>

          <strong class="admin-applications-stat__approved">
            {{ applicationStats.approved }}
          </strong>
        </div>

        <div class="admin-applications-stat">
          <span>Отклонено</span>

          <strong class="admin-applications-stat__rejected">
            {{ applicationStats.rejected }}
          </strong>
        </div>
      </section>

      <p
        v-if="loading"
        class="admin-applications-state"
      >
        Загрузка заявок...
      </p>

      <p
        v-else-if="error"
        class="admin-applications-state admin-applications-state--error"
      >
        {{ error }}
      </p>

      <p
        v-else-if="filteredApplications.length === 0"
        class="admin-applications-state"
      >
        Заявки не найдены.
      </p>

      <div
        v-else
        class="admin-applications-table"
      >
        <div class="admin-applications-table__header">
          <span>Студент</span>
          <span>Группа</span>
          <span>Проект</span>
          <span>Подразд.</span>
          <span>Кафедра</span>
          <span>Статус</span>
          <span>Действия</span>
        </div>

        <div
          v-for="application in filteredApplications"
          :key="application.id"
          class="admin-applications-table__row"
        >
          <span class="admin-applications-table__student">
            {{ application.studentName }}
          </span>

          <span>
            {{ application.groupNumber ?? '—' }}
          </span>

          <span>
            {{ getProjectLabel(application) }}
          </span>

          <span>
            {{ getProject(application.projectId)?.faculty ?? '—' }}
          </span>

          <span>
            {{ getProject(application.projectId)?.department ?? '—' }}
          </span>

          <span>
            <span
              class="admin-applications-status"
              :class="getStatusClass(application.status)"
            >
              {{ getStatusLabel(application.status) }}
            </span>
          </span>

          <div class="admin-applications-table__actions">
            <template
              v-if="
                application.status === 'CREATED' ||
                application.status === 'UNDER_REVIEW'
              "
            >
              <button
                class="
                  admin-applications-action
                  admin-applications-action--reject
                "
                type="button"
                :disabled="processingId !== null"
                @click="rejectApplication(application)"
              >
                Отклонить
              </button>

              <button
                class="
                  admin-applications-action
                  admin-applications-action--approve
                "
                type="button"
                :disabled="processingId !== null"
                @click="approveApplication(application)"
              >
                {{
                  processingId === application.id
                    ? 'Обработка...'
                    : 'Принять'
                }}
              </button>
            </template>

            <span
              v-else
              class="admin-applications-table__processed"
            >
              Рассмотрена
            </span>
          </div>
        </div>
      </div>
    </main>
  </AppLayout>
</template>

<style
  scoped
  src="../styles/pages/admin-applications.css"
></style>