<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { http } from '../api/http'
import AppLayout from '../components/AppLayout.vue'


type UserRole = 'ADMIN' | 'TEACHER' | 'STUDENT'

interface ApplicationResponse{
    id: number
    studentId: number
    studentName: string
    projectId: number
    projectName:string
    status:
    |'CREATED'
    |'UNDER_REVIEW'
    |'APPROVED'
    |'REJECTED'
    |'CANCELLED'
    createdAt:string
    takenForReviewAt: string | null
    reviewAt: string | null
    rejectionReason: string | null
}

interface CurrentUser {
  id: number
  email: string
  fullName: string
  role: UserRole
}

interface ProjectResponse {
  id: number
  code: string | null
  name: string
  faculty: string | null
  department: string
}


const router = useRouter()
const search = ref('')
const selectedStatus = ref('')
const currentUser = ref<CurrentUser | null>(null)
const projects = ref<ProjectResponse[]>([])
const applications = ref<ApplicationResponse[]>([])
const loading = ref(true)
const error = ref('')
const selectedFaculty = ref('')

const filteredApplications = computed(() => {
  const query = search.value.trim().toLowerCase()

  return applications.value.filter((application) => {
    const project = getProject(application.projectId)

    const matchesSearch =
      !query ||
      application.projectName.toLowerCase().includes(query)

    const matchesStatus =
      !selectedStatus.value ||
      application.status === selectedStatus.value

    const matchesFaculty =
      !selectedFaculty.value ||
      project?.faculty === selectedFaculty.value

    return matchesSearch && matchesStatus && matchesFaculty
  })
})

const faculties = computed(() => {
  return [
    ...new Set(
      projects.value
        .map(project => project.faculty)
        .filter((faculty): faculty is string => Boolean(faculty))
    ),
  ]
})

async function loadPage() {
  loading.value = true
  error.value = ''

  try {
    const [
      userResponse,
      applicationsResponse,
      projectsResponse,
    ] = await Promise.all([
      http.get<CurrentUser>('/me'),
      http.get<ApplicationResponse[]>('/me/applications'),
      http.get<ProjectResponse[]>('/projects'),
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

function getProject(projectId: number) {
  return projects.value.find(
    project => project.id === projectId
  )
}

function getStatusLabel(status: ApplicationResponse['status']) {
  switch (status) {
    case 'CREATED':
      return 'Создана'

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

function formatDate(value: string) {
  return new Intl.DateTimeFormat('ru-RU', {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit',
  }).format(new Date(value))
}

async function cancelApplication(application: ApplicationResponse) {
  try {
    await http.delete(`/applications/${application.id}`)

    await loadPage()
  } catch {
    error.value = 'Не удалось отозвать заявку.'
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
    <main class="applications-content">
        <header class="applications-header">
        <h1>Мои заявки</h1>

        <p>
            Проекты, на которые вы подали заявку, и текущий статус рассмотрения.
        </p>
        </header>

       <div class="applications-filters">
            <input
                v-model="search"
                class="applications-filter applications-filter--search"
                type="search"
                placeholder="Поиск по названию проекта"
            >

            <select
                v-model="selectedStatus"
                class="applications-filter applications-filter--status"
            >
                <option value="">Статус: все</option>
                <option value="CREATED">Создана</option>
                <option value="UNDER_REVIEW">На рассмотрении</option>
                <option value="APPROVED">Принята</option>
                <option value="REJECTED">Отклонена</option>
                <option value="CANCELLED">Отозвана</option>
            </select>

            <select
                v-model="selectedFaculty"
                class="applications-filter applications-filter--faculty"
            >
                <option value="">Подразделение: все</option>

                <option
                v-for="faculty in faculties"
                :key="faculty"
                :value="faculty"
                >
                {{ faculty }}
                </option>
            </select>
        </div>

        <p v-if="loading">
            Загрузка заявок...
        </p>

        <p v-else-if="error">
            {{ error }}
        </p>

        <div v-else>
            <div class="applications-table">
                <div class="applications-table__title">
                    <h2>Поданные заявки</h2>

                    <span>
                    {{ filteredApplications.length }} заявок
                    </span>
                </div>

                <div class="applications-table__header">
                    <span>Код</span>
                    <span>Название проекта</span>
                    <span>Подразд.</span>
                    <span>Кафедра</span>
                    <span>Дата</span>
                    <span>Статус</span>
                    <span>Действия</span>
                </div>

                <div
                    v-for="application in filteredApplications"
                    :key="application.id"
                    class="applications-table__row"
                >
                    <span class="applications-table__code">
                    {{ getProject(application.projectId)?.code ?? '—' }}
                    </span>

                    <span class="applications-table__name">
                    {{ application.projectName }}
                    </span>

                    <span>
                    {{ getProject(application.projectId)?.faculty ?? '—' }}
                    </span>

                    <span>
                    {{ getProject(application.projectId)?.department ?? '—' }}
                    </span>

                    <span>
                    {{ formatDate(application.createdAt) }}
                    </span>

                    <div>
                    <span
                        class="applications-table__status"
                        :class="`applications-table__status--${application.status.toLowerCase()}`"
                    >
                        {{ getStatusLabel(application.status) }}
                    </span>
                    </div>

                   <div class="applications-table__actions">
                        <RouterLink
                            class="applications-table__action"
                            :to="`/projects/${application.projectId}`"
                        >
                            {{ application.status === 'APPROVED'
                            ? 'Перейти к проекту'
                            : 'Открыть'
                            }}
                        </RouterLink>

                        <button
                            v-if="
                            application.status === 'CREATED' ||
                            application.status === 'UNDER_REVIEW'
                            "
                            class="applications-table__action applications-table__action--cancel"
                            type="button"
                            @click="cancelApplication(application)"
                        >
                            Отозвать заявку
                        </button>
                    </div>
                </div>
            </div>
        </div>
    </main>
  </AppLayout>
</template>

<style
    scoped
    src="../styles/pages/applications.css"
></style>