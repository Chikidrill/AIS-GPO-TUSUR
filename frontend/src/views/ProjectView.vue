<script setup lang="ts">
import axios from 'axios'
import {computed, onMounted, ref} from 'vue'
import {useRoute} from 'vue-router'

import {http} from '../api/http'

type ProjectStatus =
  | 'DRAFT'
  | 'OPEN'
  | 'IN_PROGRESS'
  | 'COMPLETED'
  | 'ARCHIVED'

interface Project {
  id: number
  name: string
  description: string | null
  status: ProjectStatus

  // Поля, необходимые по макету
  code?: string
  department?: string
  supervisorName?: string
  direction?: string
  competencies?: string[]
  occupiedPlaces?: number
  totalPlaces?: number
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

  try {
    const { data } = await http.get<Project>(
      `/projects/${route.params.id}`,
    )

    project.value = data
  } catch {
    error.value = 'Не удалось загрузить проект.'
  } finally {
    loading.value = false
  }
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

onMounted(loadProject)
</script>

<template>
  <main>
    <RouterLink to="/projects">
      ← К списку проектов
    </RouterLink>

    <p v-if="loading">
      Загрузка...
    </p>

    <p v-else-if="error">
      {{ error }}
    </p>

    <section v-else-if="project">
      <h1>{{ project.name }}</h1>

      <p>
        {{ project.description || 'Описание отсутствует' }}
      </p>

      <p>
        Статус: {{ project.status }}
      </p>

      <button
        v-if="canApply"
        :disabled="submitting"
        @click="applyToProject"
      >
        {{ submitting ? 'Отправка...' : 'Подать заявку' }}
      </button>

      <p
        v-else-if="role === 'STUDENT' && project.status !== 'OPEN'"
      >
        Приём заявок закрыт.
      </p>

      <p v-if="applicationCreated">
        Заявка успешно отправлена.
      </p>

      <p v-if="applicationError">
        {{ applicationError }}
      </p>
    </section>
  </main>
</template>