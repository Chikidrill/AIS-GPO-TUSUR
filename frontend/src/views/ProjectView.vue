<script setup lang="ts">
import axios from 'axios'
import {computed, onMounted, ref} from 'vue'
import {useRoute, useRouter} from 'vue-router'
import {  type Project } from '../mocks/projects'
import {http} from '../api/http'
import AppLayout from '../components/AppLayout.vue'

type UserRole = 'ADMIN' | 'TEACHER' | 'STUDENT'

interface CurrentUser {
  id: number
  email: string
  fullName: string
  role: UserRole
}

interface ApplicationResponse {
  id: number
  studentId: number
  studentName: string
  projectId: number
  projectName: string
  status:
    | 'CREATED'
    | 'UNDER_REVIEW'
    | 'APPROVED'
    | 'REJECTED'
    | 'CANCELLED'
  createdAt: string
  takenForReviewAt: string | null
  reviewedAt: string | null
  rejectionReason: string | null
}

interface ApiError {
  code: string
  message: string
  timestamp: string
}

const route = useRoute()
const router = useRouter()
const currentUser = ref<CurrentUser | null>(null)

const project = ref<Project | null>(null)

const loading = ref(true)
const error = ref('')

const submitting = ref(false)
const activeApplication = ref<ApplicationResponse | null>(null)
const applicationError = ref('')

const role = localStorage.getItem('role')

const canApply = computed(() => {
  return role === 'STUDENT'
    && project.value?.status === 'OPEN'
    && !activeApplication.value
})

async function loadProject() {
  loading.value = true
  error.value = ''

  try {
    const { data } = await http.get<Project>(
      `/projects/${route.params.id}`
    )

    project.value = data

    await loadApplications()
  } catch {
    error.value = 'Не удалось загрузить проект.'
  } finally {
    loading.value = false
  }
}

async function loadApplications() {
  if (role !== 'STUDENT') {
    return
  }

  try {
    const { data } = await http.get<ApplicationResponse[]>(
      '/me/applications'
    )

   activeApplication.value =
  data.find(
    application =>
      application.projectId === project.value?.id &&
      (
        application.status === 'CREATED' ||
        application.status === 'UNDER_REVIEW'
      )
  ) ?? null
  } catch {
    activeApplication.value = null
  }
}

async function applyToProject() {
  if (!project.value) {
    return
  }

  submitting.value = true
  applicationError.value = ''

  try {
    const {data} = await http.post<ApplicationResponse>(
      `/projects/${project.value.id}/applications`,
    )

    activeApplication.value = data
  } catch (err) {
    if (axios.isAxiosError<ApiError>(err)) {
      const code = err.response?.data?.code

      switch (code) {
        case 'ACTIVE_APPLICATION_ALREADY_EXISTS':
          applicationError.value =
            'Вы уже подали заявку на этот проект.'
          break

        case 'STUDENT_ALREADY_HAS_PROJECT':
          applicationError.value =
            'Вы уже участвуете в другом проекте.'
          break

        case 'PROJECT_NOT_OPEN':
          applicationError.value =
            'Приём заявок на этот проект закрыт.'
          break

        case 'PROJECT_NOT_FOUND':
          applicationError.value =
            'Проект не найден.'
          break

        default:
          applicationError.value =
            'Не удалось подать заявку.'
      }
    } else {
      applicationError.value =
        'Не удалось подать заявку.'
    }
  } finally {
    submitting.value = false
  }
}

async function cancelApplication() {
  if (!activeApplication.value) {
    return
  }

  applicationError.value = ''

  try {
    await http.delete(
      `/applications/${activeApplication.value.id}`
    )

    activeApplication.value = null
  } catch {
    applicationError.value =
      'Не удалось отменить заявку.'
  }
}

async function loadCurrentUser() {
  try {
    const { data } = await http.get<CurrentUser>('/me')
    currentUser.value = data
  } catch {
    currentUser.value = null
  }
}

async function logout() {
  localStorage.removeItem('accessToken')
  localStorage.removeItem('role')

  await router.push('/login')
}

onMounted(() => {
  loadProject()
  loadCurrentUser()
})
</script>

<template>
  <AppLayout
    :user="currentUser"
    @logout="logout"
  >
    <div class="project-page">
  <main class="project-content">

      <div
      v-if="loading"
      class="project-state"
    >
      Загрузка проекта...
    </div>

    <div
      v-else-if="error"
      class="project-state project-state--error"
    >
      <h2>Проект не найден</h2>

      <p>{{ error }}</p>

      <RouterLink
        to="/projects"
        class="project-state__button"
      >
        Вернуться в каталог
      </RouterLink>
    </div>

    <h1 class="project-title">
      {{ project?.name }}
    </h1>

    <div
      v-if="project"
      class="project-tags"
    >
      <span v-if="project.direction">
        {{ project.direction }}
      </span>

      <span
        v-if="
          project.occupiedPlaces !== undefined &&
          project.totalPlaces !== undefined
        "
      >
        {{ project.occupiedPlaces }} из {{ project.totalPlaces }} мест
      </span>

      <span>
        2 семестр
      </span>
    </div>

    <div
      v-if="project"
      class="project-layout"
    >
      <div class="project-main">
        <section class="project-section">
          <h2>О проекте</h2>
          <p>
            {{ project.description || 'Описание проекта пока отсутствует.' }}
          </p>
        </section>

        <section class="project-section">
          <h2>Цель проекта</h2>
          <p>
            Информация о цели проекта пока отсутствует.
          </p>
        </section>

        <section class="project-section">
          <h2>Требуемые компетенции</h2>

          <p v-if="project.competencies?.length">
            {{ project.competencies.join(' · ') }}
          </p>
        </section>
      </div>

      <aside class="project-sidebar">
        <span class="project-sidebar__label">
          Руководитель проекта
        </span>

        <strong>
          {{ project.supervisorName }}
        </strong>

        <p>
          {{ project.department }}
        </p>

        <hr>

        <p>
          Свободных мест:
          {{ (project.totalPlaces ?? 0) - (project.occupiedPlaces ?? 0) }}
        </p>
        <p class="project-sidebar__application-hint">
          Заявка будет отправлена руководителю проекта на рассмотрение.
        </p>
        <button
          v-if="canApply"
          class="project-sidebar__button project-sidebar__button--primary"
          type="button"
          :disabled="submitting"
          @click="applyToProject"
        >
          {{ submitting ? 'Отправка...' : 'Подать заявку' }}
        </button>
      
        <template v-if="activeApplication">
          <button
            class="project-sidebar__button project-sidebar__button--secondary"
            type="button"
            @click="cancelApplication"
          >
            Отозвать заявку
          </button>

          <div class="project-sidebar__description">
            <h6>После подачи</h6>

            <p>
              Статус заявки появится в разделе «Мои заявки».
            </p>
          </div>
        </template>
        <p
          v-if="applicationError"
          class="project-sidebar__error"
        >
          Ошибка: {{ applicationError }}
        </p>

        <p
          v-if="
          
          project?.status !== 'OPEN' &&
          !activeApplication"
          class="project-sidebar__closed"
        >
          Приём заявок на этот проект закрыт.
        </p>
      </aside>
    </div>
  </main>
</div>
  </AppLayout>
</template>

<style
  scoped
  src="../styles/pages/project.css"
></style>