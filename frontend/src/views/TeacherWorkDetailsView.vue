<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

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

interface WorkFile {
  id: number
  fileName: string
  contentType: string
  sizeBytes: number
}

interface WorkRevision {
  id: number
  versionNumber: number
  text: string | null
  status: WorkStatus
  grade: number | null
  reviewComment: string | null
  submittedAt: string
  reviewedAt: string | null
  files: WorkFile[]
}

interface WorkDetails {
  id: number
  projectId: number
  projectName: string
  studentId: number
  studentName: string
  groupNumber: string | null
  status: WorkStatus
  createdAt: string
  updatedAt: string
  revisions: WorkRevision[]
}

const route = useRoute()
const router = useRouter()

const currentUser = ref<CurrentUser | null>(null)
const work = ref<WorkDetails | null>(null)

const loading = ref(true)
const error = ref('')
const actionError = ref('')
const actionSuccess = ref('')

const reviewing = ref(false)
const downloadingFileId = ref<number | null>(null)

const comment = ref('')
const gradeInput = ref<number | ''>('')
const selectedVersion = ref<number | null>(null)

const projectId = Number(route.params.id)
const workId = Number(route.params.workId)

const statusLabels: Record<WorkStatus, string> = {
  SUBMITTED: 'На проверке',
  NEEDS_REVISION: 'На доработке',
  ACCEPTED: 'Принята',
}

const latestRevision = computed(() => {
  if (!work.value?.revisions.length) return null

  return [...work.value.revisions].sort(
    (a, b) => b.versionNumber - a.versionNumber,
  )[0] ?? null
})

const selectedRevision = computed(() => {
  return (
    work.value?.revisions.find(
      (revision) => revision.versionNumber === selectedVersion.value,
    ) ?? latestRevision.value
  )
})

const canReview = computed(() => {
  return (
    work.value?.status === 'SUBMITTED' &&
    selectedRevision.value?.id === latestRevision.value?.id
  )
})

function formatDate(value: string) {
  return new Intl.DateTimeFormat('ru-RU', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}

function formatFileSize(bytes: number) {
  return bytes < 1024 * 1024
    ? `${(bytes / 1024).toFixed(1)} КБ`
    : `${(bytes / 1024 / 1024).toFixed(1)} МБ`
}

async function loadPage() {
  loading.value = true
  error.value = ''

  try {
    const [userResponse, workResponse] = await Promise.all([
      http.get<CurrentUser>('/me'),
      http.get<WorkDetails>(
        `/projects/${projectId}/works/${workId}`,
      ),
    ])

    currentUser.value = userResponse.data
    work.value = workResponse.data

    selectedVersion.value = latestRevision.value?.versionNumber ?? null
  } catch {
    error.value = 'Не удалось загрузить работу.'
  } finally {
    loading.value = false
  }
}

async function acceptWork() {
  if (!work.value || !canReview.value || reviewing.value) return

  const grade = gradeInput.value === ''
    ? null
    : Number(gradeInput.value)

  if (grade !== null && !Number.isInteger(grade)) {
    actionError.value = 'Оценка должна быть целым числом.'
    return
  }

  reviewing.value = true
  actionError.value = ''
  actionSuccess.value = ''

  try {
    const { data } = await http.post<WorkDetails>(
      `/projects/${projectId}/works/${workId}/accept`,
      {
        grade,
        comment: comment.value.trim() || null,
      },
    )

    work.value = data
    actionSuccess.value = 'Работа принята.'
  } catch {
    actionError.value = 'Не удалось принять работу.'
  } finally {
    reviewing.value = false
  }
}

async function requestRevision() {
  if (!work.value || !canReview.value || reviewing.value) return

  if (!comment.value.trim()) {
    actionError.value = 'Укажи причину возврата работы на доработку.'
    return
  }

  reviewing.value = true
  actionError.value = ''
  actionSuccess.value = ''

  try {
    const { data } = await http.post<WorkDetails>(
      `/projects/${projectId}/works/${workId}/request-revision`,
      {
        comment: comment.value.trim(),
      },
    )

    work.value = data
    actionSuccess.value = 'Работа отправлена студенту на доработку.'
  } catch {
    actionError.value = 'Не удалось отправить работу на доработку.'
  } finally {
    reviewing.value = false
  }
}

async function downloadFile(file: WorkFile) {
  if (downloadingFileId.value !== null) return

  downloadingFileId.value = file.id
  actionError.value = ''

  try {
    const { data } = await http.get<Blob>(
      `/work-files/${file.id}`,
      { responseType: 'blob' },
    )

    const url = URL.createObjectURL(data)
    const link = document.createElement('a')

    link.href = url
    link.download = file.fileName

    document.body.appendChild(link)
    link.click()
    link.remove()

    window.setTimeout(() => URL.revokeObjectURL(url), 1000)
  } catch {
    actionError.value = 'Не удалось скачать файл.'
  } finally {
    downloadingFileId.value = null
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
    <main class="teacher-work-details-content">
      <RouterLink
        class="teacher-work-details-back"
        :to="`/myprojects/${projectId}/works`"
      >
        ← Вернуться к работам
      </RouterLink>

      <p
        v-if="loading"
        class="teacher-work-details-state"
      >
        Загрузка работы...
      </p>

      <p
        v-else-if="error"
        class="teacher-work-details-state teacher-work-details-error"
      >
        {{ error }}
      </p>

      <template v-else-if="work">
        <header class="teacher-work-details-header">
          <h1>Проверка работы</h1>

          <p>{{ work.projectName }}</p>
        </header>

        <section class="teacher-work-details-card">
          <h2>{{ work.studentName }}</h2>

          <p>Группа: {{ work.groupNumber ?? 'Не указана' }}</p>

          <span
            class="teacher-work-details-status"
            :class="`teacher-work-details-status--${work.status.toLowerCase()}`"
          >
            {{ statusLabels[work.status] }}
          </span>
        </section>

        <section
          v-if="selectedRevision"
          class="teacher-work-details-card"
        >
          <div class="teacher-work-details-section-header">
            <h2>Содержимое работы</h2>

            <select
              v-model.number="selectedVersion"
              class="teacher-work-details-select"
            >
              <option
                v-for="revision in [...work.revisions].sort(
                  (a, b) => b.versionNumber - a.versionNumber
                )"
                :key="revision.id"
                :value="revision.versionNumber"
              >
                Версия {{ revision.versionNumber }}
              </option>
            </select>
          </div>

          <p class="teacher-work-details-date">
            Отправлена: {{ formatDate(selectedRevision.submittedAt) }}
          </p>

          <h3>Текст работы</h3>

          <p class="teacher-work-details-text">
            {{ selectedRevision.text || 'Текст не приложен.' }}
          </p>

          <h3>Прикреплённые файлы</h3>

          <p
            v-if="selectedRevision.files.length === 0"
            class="teacher-work-details-muted"
          >
            Файлов нет.
          </p>

          <div
            v-for="file in selectedRevision.files"
            :key="file.id"
            class="teacher-work-details-file"
          >
            <div>
              <strong>{{ file.fileName }}</strong>
              <span>{{ formatFileSize(file.sizeBytes) }}</span>
            </div>

            <button
              type="button"
              class="teacher-work-details-button"
              :disabled="downloadingFileId !== null"
              @click="downloadFile(file)"
            >
              {{ downloadingFileId === file.id ? 'Загрузка...' : 'Скачать' }}
            </button>
          </div>

          <div
            v-if="selectedRevision.reviewComment"
            class="teacher-work-details-review-history"
          >
            <h3>Результат проверки версии</h3>

            <p>{{ selectedRevision.reviewComment }}</p>

            <p v-if="selectedRevision.grade !== null">
              Оценка: {{ selectedRevision.grade }}
            </p>

            <p v-if="selectedRevision.reviewedAt">
              Проверена: {{ formatDate(selectedRevision.reviewedAt) }}
            </p>
          </div>
        </section>

        <section
          v-if="canReview"
          class="teacher-work-details-card"
        >
          <h2>Проверка работы</h2>

          <label for="work-review-comment">
            Комментарий преподавателя
          </label>

          <textarea
            id="work-review-comment"
            v-model="comment"
            rows="5"
            maxlength="4000"
            class="teacher-work-details-textarea"
            placeholder="Замечания или рекомендации по работе..."
          />

          <label for="work-review-grade">
            Оценка (необязательно)
          </label>

          <input
            id="work-review-grade"
            v-model="gradeInput"
            type="number"
            step="1"
            class="teacher-work-details-input"
            placeholder="Введите баллы"
          >

          <p class="teacher-work-details-muted">
            При возврате на доработку комментарий обязателен.
            Оценка сохраняется только при принятии работы.
          </p>

          <div class="teacher-work-details-actions">
            <button
              type="button"
              class="teacher-work-details-button teacher-work-details-button--primary"
              :disabled="reviewing"
              @click="acceptWork"
            >
              {{ reviewing ? 'Сохранение...' : 'Принять работу' }}
            </button>

            <button
              type="button"
              class="teacher-work-details-button teacher-work-details-button--warning"
              :disabled="reviewing"
              @click="requestRevision"
            >
              Отправить на доработку
            </button>
          </div>
        </section>

        <p
          v-if="actionError"
          class="teacher-work-details-message teacher-work-details-error"
          role="alert"
        >
          {{ actionError }}
        </p>

        <p
          v-if="actionSuccess"
          class="teacher-work-details-message teacher-work-details-success"
          role="status"
        >
          {{ actionSuccess }}
        </p>
      </template>
    </main>
  </AppLayout>
</template>

<style
  scoped
  src="../styles/pages/teacher-work-details.css"
></style>