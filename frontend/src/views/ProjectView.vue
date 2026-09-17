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
        <RouterLink
          to="/projects"
          class="project-back"
        >
          ← К списку проектов
        </RouterLink>

        <section
          v-if="project"
          class="project-details"
        >
          <div class="project-details__top">
            <span class="project-details__code">
              {{ project.code || `ПР${project.id}` }}
            </span>

            <span
              v-if="
                project.occupiedPlaces !== undefined &&
                project.totalPlaces !== undefined
              "
              class="project-details__places"
            >
              {{ project.occupiedPlaces }} из {{ project.totalPlaces }} мест
            </span>
          </div>

          <h1 class="project-details__title">
            {{ project.name }}
          </h1>

          <p v-if="project.faculty">
            {{ project.faculty }}
          </p>

          <p v-if="project.department">
            {{ project.department }}
          </p>

          <p v-if="project.supervisorName">
            Руководитель: {{ project.supervisorName }}
          </p>

          <p v-if="project.direction">
            {{ project.direction }}
          </p>

          <div
            v-if="project.competencies?.length"
            class="project-details__competencies"
          >
            <span
              v-for="competency in project.competencies"
              :key="competency"
              class="project-details__competency"
            >
              {{ competency }}
            </span>
          </div>
        </section>
      </main>
    </div>
  </AppLayout>
</template>

<style
  scoped
  src="../styles/pages/project.css"
></style>