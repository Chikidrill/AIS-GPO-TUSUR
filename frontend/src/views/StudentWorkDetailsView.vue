<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { isAxiosError } from 'axios'
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

const workId = Number(route.params.workId)

const currentUser = ref<CurrentUser | null>(null)
const work = ref<WorkDetails | null>(null)

const loading = ref(true)
const submitting = ref(false)

const error = ref('')
const submitError = ref('')
const successMessage = ref('')

const workText = ref('')
const selectedFiles = ref<File[]>([])
const selectedVersion = ref<number | null>(null)

const downloadingFileId = ref<number | null>(null)

const statusLabels: Record<WorkStatus, string> = {
  SUBMITTED: 'На проверке',
  NEEDS_REVISION: 'На доработке',
  ACCEPTED: 'Принята',
}

const sortedRevisions = computed(() => {
  return [...(work.value?.revisions ?? [])].sort(
    (a, b) => b.versionNumber - a.versionNumber,
  )
})

const latestRevision = computed(
  () => sortedRevisions.value[0] ?? null,
)

const displayedRevision = computed(() => {
  return (
    sortedRevisions.value.find(
      (revision) =>
        revision.versionNumber === selectedVersion.value,
    ) ?? latestRevision.value
  )
})

const canResubmit = computed(
  () => work.value?.status === 'NEEDS_REVISION',
)

const canSubmit = computed(() => {
  return (
    workText.value.trim().length > 0 ||
    selectedFiles.value.some((file) => file.size > 0)
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
  if (bytes < 1024) return `${bytes} Б`

  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} КБ`
  }

  return `${(bytes / 1024 / 1024).toFixed(1)} МБ`
}

function handleFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  selectedFiles.value = Array.from(input.files ?? [])
}

function removeFile(index: number) {
  selectedFiles.value = selectedFiles.value.filter(
    (_, fileIndex) => fileIndex !== index,
  )
}

async function loadPage() {
  loading.value = true
  error.value = ''

  if (!Number.isSafeInteger(workId) || workId <= 0) {
    error.value = 'Некорректный идентификатор работы.'
    loading.value = false
    return
  }

  try {
    const [userResponse, workResponse] = await Promise.all([
      http.get<CurrentUser>('/me'),
      http.get<WorkDetails>(`/me/works/${workId}`),
    ])

    currentUser.value = userResponse.data
    work.value = workResponse.data

    selectedVersion.value =
      latestRevision.value?.versionNumber ?? null

    // Студент может исправлять ранее отправленный текст.
    // Старые файлы при этом не копируются автоматически.
    workText.value = latestRevision.value?.text ?? ''
  } catch {
    error.value = 'Не удалось загрузить работу.'
  } finally {
    loading.value = false
  }
}

async function downloadFile(file: WorkFile) {
  if (downloadingFileId.value !== null) return

  downloadingFileId.value = file.id
  error.value = ''

  try {
    const { data } = await http.get<Blob>(
      `/work-files/${file.id}`,
      {
        responseType: 'blob',
      },
    )

    const url = URL.createObjectURL(data)
    const link = document.createElement('a')

    link.href = url
    link.download = file.fileName

    document.body.appendChild(link)
    link.click()
    link.remove()

    window.setTimeout(() => {
      URL.revokeObjectURL(url)
    }, 1000)
  } catch {
    error.value = 'Не удалось скачать файл.'
  } finally {
    downloadingFileId.value = null
  }
}

async function resubmitWork() {
  if (
    !work.value ||
    !canResubmit.value ||
    !canSubmit.value ||
    submitting.value
  ) {
    return
  }

  submitting.value = true
  submitError.value = ''
  successMessage.value = ''

  const formData = new FormData()

  if (workText.value.trim()) {
    formData.append('Text', workText.value.trim())
  }

  selectedFiles.value
    .filter((file) => file.size > 0)
    .forEach((file) => {
      formData.append('Files', file)
    })

  try {
    const { data } = await http.post<WorkDetails>(
      `/me/works/${workId}/revisions`,
      formData,
    )

    work.value = data

    selectedVersion.value =
      latestRevision.value?.versionNumber ?? null

    selectedFiles.value = []

    successMessage.value =
      'Исправленная работа успешно отправлена на проверку.'
  } catch (err) {
    if (
      isAxiosError(err) &&
      err.response?.status === 409
    ) {
      submitError.value =
        'Повторная отправка сейчас недоступна. Обновите страницу.'
    } else if (
      isAxiosError(err) &&
      err.response?.status === 403
    ) {
      submitError.value =
        'Отправлять работу могут только активные участники проекта.'
    } else {
      submitError.value =
        'Не удалось отправить исправленную работу.'
    }
  } finally {
    submitting.value = false
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
    <main class="student-works-content">
      <RouterLink
        class="student-works-back"
        to="/works"
      >
        ← Вернуться к работам
      </RouterLink>

      <p
        v-if="loading"
        class="student-works-state"
      >
        Загрузка работы...
      </p>

      <p
        v-else-if="error && !work"
        class="student-works-state student-works-error"
      >
        {{ error }}
      </p>

      <template v-else-if="work">
        <header class="student-works-header">
          <h1>Моя работа</h1>

          <p>{{ work.projectName }}</p>
        </header>

        <section class="student-works-panel">
          <div class="student-works-section-heading">
            <h2>Текущий статус</h2>

            <span
              class="student-works-status"
              :class="`student-works-status--${work.status.toLowerCase()}`"
            >
              {{ statusLabels[work.status] }}
            </span>
          </div>

          <p class="student-works-muted">
            Текущая версия:
            № {{ latestRevision?.versionNumber ?? '—' }}
          </p>

          <p
            v-if="work.status === 'SUBMITTED'"
            class="student-works-muted"
          >
            Работа отправлена и ожидает проверки преподавателем.
          </p>

          <p
            v-else-if="work.status === 'NEEDS_REVISION'"
            class="student-works-muted"
          >
            Преподаватель вернул работу на доработку.
            Ознакомьтесь с замечаниями и отправьте новую версию.
          </p>

          <p
            v-else
            class="student-works-muted"
          >
            Преподаватель принял вашу работу.
          </p>
        </section>

        <section
          v-if="displayedRevision"
          class="student-works-panel"
        >
          <div class="student-works-section-heading">
            <h2>Содержимое работы</h2>

            <select
              v-model.number="selectedVersion"
              class="student-works-select"
            >
              <option
                v-for="revision in sortedRevisions"
                :key="revision.id"
                :value="revision.versionNumber"
              >
                Версия {{ revision.versionNumber }}
              </option>
            </select>
          </div>

          <p class="student-works-muted">
            Отправлена:
            {{ formatDate(displayedRevision.submittedAt) }}
          </p>

          <h3>Текст работы</h3>

          <p class="student-works-work-text">
            {{ displayedRevision.text || 'Текст не приложен.' }}
          </p>

          <h3>Прикреплённые файлы</h3>

          <p
            v-if="displayedRevision.files.length === 0"
            class="student-works-muted"
          >
            Файлы не приложены.
          </p>

          <div
            v-else
            class="student-works-files"
          >
            <div
              v-for="file in displayedRevision.files"
              :key="file.id"
              class="student-works-file"
            >
              <div>
                <strong>{{ file.fileName }}</strong>

                <span>{{ formatFileSize(file.sizeBytes) }}</span>
              </div>

              <button
                class="student-works-button"
                type="button"
                :disabled="downloadingFileId !== null"
                @click="downloadFile(file)"
              >
                {{
                  downloadingFileId === file.id
                    ? 'Загрузка...'
                    : 'Скачать'
                }}
              </button>
            </div>
          </div>

          <div
            v-if="displayedRevision.reviewedAt"
            class="student-works-review"
          >
            <h3>Результат проверки</h3>

            <p>
              Статус:
              {{ statusLabels[displayedRevision.status] }}
            </p>

            <p v-if="displayedRevision.reviewComment">
              <strong>Комментарий преподавателя:</strong>
              {{ displayedRevision.reviewComment }}
            </p>

            <p v-if="displayedRevision.grade !== null">
              <strong>Оценка:</strong>
              {{ displayedRevision.grade }}
            </p>

            <p>
              Проверена:
              {{ formatDate(displayedRevision.reviewedAt) }}
            </p>
          </div>
        </section>

        <section
          v-if="canResubmit"
          class="student-works-panel"
        >
          <h2>Отправить исправленную работу</h2>

          <p class="student-works-muted">
            Новая отправка создаст следующую версию.
            История предыдущих версий сохранится.
          </p>

          <form
            class="student-works-form"
            @submit.prevent="resubmitWork"
          >
            <label for="student-revision-text">
              Исправленный текст работы
            </label>

            <textarea
              id="student-revision-text"
              v-model="workText"
              rows="7"
              maxlength="20000"
              placeholder="Внесите исправления..."
            />

            <p class="student-works-hint">
              {{ workText.length }} / 20000 символов
            </p>

            <label for="student-revision-files">
              Файлы новой версии
            </label>

            <input
              id="student-revision-files"
              type="file"
              multiple
              @change="handleFileChange"
            >

            <div
              v-if="selectedFiles.length > 0"
              class="student-works-files"
            >
              <div
                v-for="(file, index) in selectedFiles"
                :key="`${file.name}-${index}`"
                class="student-works-file"
              >
                <div>
                  <strong>{{ file.name }}</strong>

                  <span>{{ formatFileSize(file.size) }}</span>
                </div>

                <button
                  class="student-works-remove"
                  type="button"
                  @click="removeFile(index)"
                >
                  Удалить
                </button>
              </div>
            </div>

            <p class="student-works-muted">
              Если нужны файлы в новой версии,
              прикрепите их заново.
            </p>

            <p
              v-if="submitError"
              class="student-works-error"
              role="alert"
            >
              {{ submitError }}
            </p>

            <button
              class="student-works-button student-works-button--primary"
              type="submit"
              :disabled="!canSubmit || submitting"
            >
              {{
                submitting
                  ? 'Отправка...'
                  : 'Отправить на повторную проверку'
              }}
            </button>
          </form>
        </section>

        <p
          v-if="successMessage"
          class="student-works-message student-works-success"
          role="status"
        >
          {{ successMessage }}
        </p>

        <p
          v-if="error"
          class="student-works-message student-works-error"
          role="alert"
        >
          {{ error }}
        </p>
      </template>
    </main>
  </AppLayout>
</template>

<style
  scoped
  src="../styles/pages/student-works.css"
></style>