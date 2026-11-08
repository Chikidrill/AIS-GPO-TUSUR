
<script setup lang="ts">
import { computed, ref, watch } from 'vue'
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
const downloadError = ref('')
const downloadingFileId = ref<number | null>(null)

const selectedVersion = ref<number | null>(null)

const workId = computed(() => {
  const raw = route.params.workId

  if (typeof raw !== 'string' || !/^[1-9]\d*$/.test(raw)) {
    return null
  }

  const id = Number(raw)
  return Number.isSafeInteger(id) ? id : null
})

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

const selectedRevision = computed(() => {
  return sortedRevisions.value.find(
    revision => revision.versionNumber === selectedVersion.value,
  ) ?? sortedRevisions.value[0] ?? null
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

let requestVersion = 0

async function loadPage() {
  const version = ++requestVersion

  loading.value = true
  error.value = ''
  downloadError.value = ''
  work.value = null
  selectedVersion.value = null

  if (workId.value === null) {
    error.value = 'Некорректный идентификатор работы.'
    loading.value = false
    return
  }

  try {
    const [userResponse, workResponse] = await Promise.all([
      http.get<CurrentUser>('/me'),
      http.get<WorkDetails>(`/admin/works/${workId.value}`),
    ])

    if (version !== requestVersion) return

    currentUser.value = userResponse.data
    work.value = workResponse.data

    selectedVersion.value =
      sortedRevisions.value[0]?.versionNumber ?? null
  } catch {
    if (version === requestVersion) {
      error.value = 'Не удалось загрузить информацию о работе.'
    }
  } finally {
    if (version === requestVersion) {
      loading.value = false
    }
  }
}

async function downloadFile(file: WorkFile) {
  if (downloadingFileId.value !== null) return

  downloadingFileId.value = file.id
  downloadError.value = ''

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

    window.setTimeout(() => {
      URL.revokeObjectURL(url)
    }, 1000)
  } catch {
    downloadError.value = 'Не удалось скачать файл.'
  } finally {
    downloadingFileId.value = null
  }
}

async function logout() {
  localStorage.removeItem('accessToken')
  localStorage.removeItem('role')
  await router.push('/login')
}

watch(workId, () => {
  void loadPage()
}, { immediate: true })
</script>

<template>
  <AppLayout
    :user="currentUser"
    @logout="logout"
  >
    <main class="admin-work-details-content">
      <RouterLink
        to="/admin/works"
        class="admin-work-details-back"
      >
        ← Вернуться к списку работ
      </RouterLink>

      <p
        v-if="loading"
        class="admin-work-details-state"
      >
        Загрузка работы...
      </p>

      <div
        v-else-if="error"
        class="admin-work-details-state admin-work-details-error"
      >
        <p>{{ error }}</p>
        <button type="button" @click="loadPage">
          Повторить
        </button>
      </div>

      <template v-else-if="work">
        <header class="admin-work-details-header">
          <h1>Просмотр работы</h1>
          <p>{{ work.projectName }}</p>
        </header>

        <section class="admin-work-details-card">
          <h2>{{ work.studentName }}</h2>

          <p>
            Группа: {{ work.groupNumber ?? 'Не указана' }}
          </p>

          <p>
            Работа №{{ work.id }}
          </p>

          <span
            class="admin-work-details-status"
            :class="`admin-work-details-status--${work.status.toLowerCase()}`"
          >
            {{ statusLabels[work.status] }}
          </span>
        </section>

        <section class="admin-work-details-card">
          <div class="admin-work-details-section-header">
            <h2>Содержимое работы</h2>

            <select
              v-if="sortedRevisions.length > 0"
              v-model.number="selectedVersion"
              class="admin-work-details-select"
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

          <p
            v-if="!selectedRevision"
            class="admin-work-details-muted"
          >
            У этой работы пока нет отправленных версий.
          </p>

          <template v-else>
            <div class="admin-work-details-meta">
              <p>
                Отправлена:
                {{ formatDate(selectedRevision.submittedAt) }}
              </p>

              <span
                class="admin-work-details-status"
                :class="`admin-work-details-status--${selectedRevision.status.toLowerCase()}`"
              >
                {{ statusLabels[selectedRevision.status] }}
              </span>
            </div>

            <h3>Текст работы</h3>

            <p class="admin-work-details-text">
              {{
                selectedRevision.text ||
                'Текст не приложен.'
              }}
            </p>

            <h3>Прикреплённые файлы</h3>

            <p
              v-if="selectedRevision.files.length === 0"
              class="admin-work-details-muted"
            >
              Файлы не прикреплены.
            </p>

            <div
              v-for="file in selectedRevision.files"
              :key="file.id"
              class="admin-work-details-file"
            >
              <div class="admin-work-details-file__info">
                <strong>{{ file.fileName }}</strong>
                <span>
                  {{ formatFileSize(file.sizeBytes) }}
                </span>
              </div>

              <button
                class="admin-work-details-button"
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

            <div
              v-if="
                selectedRevision.reviewedAt ||
                selectedRevision.reviewComment ||
                selectedRevision.grade !== null
              "
              class="admin-work-details-review"
            >
              <h3>Результат проверки</h3>

              <p v-if="selectedRevision.reviewedAt">
                Проверена:
                {{ formatDate(selectedRevision.reviewedAt) }}
              </p>

              <p>
                Оценка:
                {{
                  selectedRevision.grade !== null
                    ? selectedRevision.grade
                    : 'Не выставлена'
                }}
              </p>

              <p v-if="selectedRevision.reviewComment">
                <strong>Комментарий преподавателя:</strong>
              </p>

              <p
                v-if="selectedRevision.reviewComment"
                class="admin-work-details-text"
              >
                {{ selectedRevision.reviewComment }}
              </p>
            </div>
          </template>
        </section>

        <section class="admin-work-details-card">
          <h2>История версий</h2>

          <p
            v-if="sortedRevisions.length === 0"
            class="admin-work-details-muted"
          >
            История пока пуста.
          </p>

          <div
            v-for="revision in sortedRevisions"
            :key="revision.id"
            class="admin-work-details-history-row"
          >
            <div>
              <strong>Версия {{ revision.versionNumber }}</strong>
              <span>{{ formatDate(revision.submittedAt) }}</span>
            </div>

            <span
              class="admin-work-details-status"
              :class="`admin-work-details-status--${revision.status.toLowerCase()}`"
            >
              {{ statusLabels[revision.status] }}
            </span>

            <button
              type="button"
              class="admin-work-details-button"
              @click="selectedVersion = revision.versionNumber"
            >
              Посмотреть
            </button>
          </div>
        </section>

        <p
          v-if="downloadError"
          class="admin-work-details-state admin-work-details-error"
          role="alert"
        >
          {{ downloadError }}
        </p>
      </template>
    </main>
  </AppLayout>
</template>

<style
  scoped
  src="../styles/pages/admin-work-details.css"
></style>
