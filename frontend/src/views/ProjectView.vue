<script setup lang="ts">
import axios from 'axios'
import {computed, onMounted, ref} from 'vue'
import {useRoute, useRouter} from 'vue-router'
import {
  mockProjects,
  type Project,
} from '../mocks/projects'
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
const applicationCreated = ref(false)
const applicationError = ref('')

const role = localStorage.getItem('role')

const canApply = computed(() => {
  return role === 'STUDENT'
    && project.value?.status === 'OPEN'
    && !applicationCreated.value
})

async function loadProject() {
  loading.value = true
  error.value = ''

  const projectId = Number(route.params.id);
  const foundProject = mockProjects.find(project=>project.id === projectId)

  if(!foundProject){
    error.value ='Проект не найден'
    loading.value = false
    return
  }
  
  project.value = foundProject
  loading.value = false
}

async function applyToProject() {
  if (!project.value) {
    return
  }

  submitting.value = true
  applicationError.value = ''

  try {
    await http.post<ApplicationResponse>(
      `/projects/${project.value.id}/applications`,
    )

    applicationCreated.value = true
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
        <button class="project-sidebar__button project-sidebar__button--primary"
          type="button"
          aria-disabled="true">
          Подать заявку
        </button>
        <section class="project-sidebar__description">
          <h6>После подачи</h6>
          <p>Статус заявки появится в разделе «Мои заявки».</p>
        </section>
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