<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { isAxiosError } from 'axios'
import { useRouter } from 'vue-router'

import { http } from '../api/http'
import AppLayout from '../components/AppLayout.vue'

type UserRole = 'ADMIN' | 'TEACHER' | 'STUDENT'

type WorkStatus =
  | 'SUBMITTED'
  | 'NEEDS_REVISION'
  | 'ACCEPTED'

interface CurrentUser {
  id: number
  email: string
  fullName: string
  role: UserRole
}

interface ActiveProject {
  id: number
  name: string
  description: string | null
}

interface StudentWork {
  id: number
  projectId: number
  projectName: string
  studentId: number
  studentName: string
  groupNumber: string | null
  status: WorkStatus
  latestVersionNumber: number
  lastSubmittedAt: string
  updatedAt: string
}

interface SubmittedWork {
  id: number
  projectId: number
  status: WorkStatus
}

const router = useRouter()

const currentUser = ref<CurrentUser | null>(null)
const activeProject = ref<ActiveProject | null>(null)
const works = ref<StudentWork[]>([])

const loading = ref(true)
const submitting = ref(false)

const error = ref('')
const submitError = ref('')

const workText = ref('')
const selectedFiles = ref<File[]>([])

const statusLabels: Record<WorkStatus, string> = {
  SUBMITTED: 'На проверке',
  NEEDS_REVISION: 'На доработке',
  ACCEPTED: 'Принята',
}

const hasWorkInActiveProject = computed(() => {
  if (!activeProject.value) return false

  return works.value.some(
    (work) => work.projectId === activeProject.value?.id,
  )
})

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

  try {
    const [userResponse, worksResponse, project] =
      await Promise.all([
        http.get<CurrentUser>('/me'),
        http.get<StudentWork[]>('/me/works'),

        http
          .get<ActiveProject>('/me/project')
          .then((response) => response.data)
          .catch((err: unknown) => {
            // Студент ещё не вступил в проект.
            if (
              isAxiosError(err) &&
              err.response?.status === 404
            ) {
              return null
            }

            throw err
          }),
      ])

    currentUser.value = userResponse.data
    works.value = worksResponse.data
    activeProject.value = project
  } catch {
    error.value = 'Не удалось загрузить работы.'
  } finally {
    loading.value = false
  }
}

async function submitWork() {
  if (
    !activeProject.value ||
    hasWorkInActiveProject.value ||
    !canSubmit.value ||
    submitting.value
  ) {
    return
  }

  submitting.value = true
  submitError.value = ''

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
    const { data } = await http.post<SubmittedWork>(
      `/projects/${activeProject.value.id}/works`,
      formData,
    )

    await router.push(`/works/${data.id}`)
  } catch (err) {
    if (isAxiosError(err) && err.response?.status === 409) {
      submitError.value =
        'Работа уже отправлена. Обновите страницу.'
    } else if (
      isAxiosError(err) &&
      err.response?.status === 403
    ) {
      submitError.value =
        'Отправлять работы могут только активные участники проекта.'
    } else {
      submitError.value =
        'Не удалось отправить работу. Попробуйте ещё раз.'
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
      <header class="student-works-header">
        <h1>Мои работы</h1>

        <p>
          Отправляйте результаты работы по проекту
          и отслеживайте их проверку преподавателем.
        </p>
      </header>

      <p
        v-if="loading"
        class="student-works-state"
      >
        Загрузка работ...
      </p>

      <p
        v-else-if="error"
        class="student-works-state student-works-error"
      >
        {{ error }}
      </p>

      <template v-else>
        <section class="student-works-panel">
          <div class="student-works-section-heading">
            <h2>Отправленные работы</h2>

            <span>Всего: {{ works.length }}</span>
          </div>

          <p
            v-if="works.length === 0"
            class="student-works-empty"
          >
            Вы ещё не отправляли работы.
          </p>

          <div
            v-else
            class="student-works-table"
          >
            <div class="student-works-table__header">
              <span>Проект</span>
              <span>Версия</span>
              <span>Дата отправки</span>
              <span>Статус</span>
              <span>Действия</span>
            </div>

            <div
              v-for="work in works"
              :key="work.id"
              class="student-works-table__row"
            >
              <span class="student-works-table__name">
                {{ work.projectName }}
              </span>

              <span>
                № {{ work.latestVersionNumber }}
              </span>

              <span>
                {{ formatDate(work.lastSubmittedAt) }}
              </span>

              <div class="student-works-table__cell">
                <span
                  class="student-works-status"
                  :class="`student-works-status--${work.status.toLowerCase()}`"
                >
                  {{ statusLabels[work.status] }}
                </span>
              </div>

              <div class="student-works-table__cell">
                <RouterLink
                  class="student-works-button"
                  :to="`/works/${work.id}`"
                >
                  {{ work.status === 'NEEDS_REVISION'
                    ? 'Доработать'
                    : 'Открыть' }}
                </RouterLink>
              </div>
            </div>
          </div>
        </section>

        <section
          v-if="activeProject && !hasWorkInActiveProject"
          class="student-works-panel"
        >
          <h2>Отправить работу</h2>

          <p class="student-works-muted">
            Проект: {{ activeProject.name }}
          </p>

          <form
            class="student-works-form"
            @submit.prevent="submitWork"
          >
            <label for="student-work-text">
              Текст работы
            </label>

            <textarea
              id="student-work-text"
              v-model="workText"
              rows="7"
              maxlength="20000"
              placeholder="Опишите выполненную работу, результаты и выводы..."
            />

            <p class="student-works-hint">
              {{ workText.length }} / 20000 символов
            </p>

            <label for="student-work-files">
              Прикреплённые файлы
            </label>

            <input
              id="student-work-files"
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
              Можно отправить только текст, только файлы
              или текст и файлы одновременно.
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
              {{ submitting ? 'Отправка...' : 'Отправить работу' }}
            </button>
          </form>
        </section>

        <section
          v-else-if="!activeProject"
          class="student-works-panel"
        >
          <h2>Отправка работы</h2>

          <p class="student-works-muted">
            Чтобы отправить работу, необходимо стать
            участником проекта ГПО.
          </p>

          <RouterLink
            class="student-works-button"
            to="/projects"
          >
            Каталог проектов
          </RouterLink>
        </section>
      </template>
    </main>
  </AppLayout>
</template>

<style
  scoped
  src="../styles/pages/student-works.css"
></style>