<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { isAxiosError } from 'axios'
import { useRouter } from 'vue-router'

import { http } from '../api/http'
import AppLayout from '../components/AppLayout.vue'

import {WORK_FILE_ACCEPT, validateWorkFiles} from '../utils/workFileValidation.ts'

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
  title: string
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
const fileError = ref('')

const workTitle = ref('')
const workText = ref('')
const selectedFiles = ref<File[]>([])

const statusLabels: Record<WorkStatus, string> = {
  SUBMITTED: 'На проверке',
  NEEDS_REVISION: 'На доработке',
  ACCEPTED: 'Принята',
}

const canSubmit = computed(() => {
  return (
    !fileError.value &&
    validateWorkFiles(selectedFiles.value) === null &&
    workTitle.value.trim().length > 0 &&
    workTitle.value.trim().length <= 255 &&
    (
      workText.value.trim().length > 0 ||
      selectedFiles.value.some((file) => file.size > 0)
    )
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
  const files = Array.from(input.files ?? [])
  input.value = ''

  const validationError = validateWorkFiles(files)

  if (validationError) {
    selectedFiles.value = []
    fileError.value = validationError
    return
  }

  selectedFiles.value = files
  fileError.value = ''
}

function removeFile(index: number) {
  selectedFiles.value = selectedFiles.value.filter(
    (_, fileIndex) => fileIndex !== index,
  )

  fileError.value = ''
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
    !canSubmit.value ||
    submitting.value
  ) {
    return
  }

  const validationError = validateWorkFiles(selectedFiles.value)

  if (validationError) {
    fileError.value = validationError
    return
  }

  submitting.value = true
  submitError.value = ''

  const formData = new FormData()

  formData.append('Title', workTitle.value.trim())

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
    if (isAxiosError(err)) {
      switch (err.response?.status) {
        case 400:
          submitError.value =
            'Проверьте название, содержимое и формат прикреплённых файлов.'
          break

        case 403:
          submitError.value =
            'Отправлять работы могут только активные участники проекта.'
          break

        case 409:
          submitError.value =
            'Отправлять работы можно только в проект, находящийся в процессе выполнения.'
          break

        case 413:
          submitError.value =
            'Превышен допустимый размер загружаемых файлов.'
          break

        default:
          submitError.value =
            'Не удалось отправить работу. Попробуйте ещё раз.'
      }
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
              <span>Название работы</span>
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
                {{ work.title }}
              </span>

              <span>
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
          v-if="activeProject"
          class="student-works-panel"
          id="new-work"
        >
          <h2>Отправить работу</h2>

          <p class="student-works-muted">
            Проект: {{ activeProject.name }}
          </p>

          <form
            class="student-works-form"
            @submit.prevent="submitWork"
          >
            <label for="student-work-title">Название работы</label>
            <input
              id="student-work-title"
              v-model="workTitle"
              type="text"
              maxlength="255"
              placeholder="Например: Отчёт №1 - ТЗ"
              required
              :disabled="submitting"
            >

            <p class="student-works-hint">
              {{ workTitle.length }} / 255 символов
            </p>

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
            <p class="student-works-files-muted">
              До 5 файлов, максимум 20 МБ на файл
              и 50 МБ суммарно.
              Форматы: PDF, DOC, DOCX, TXT, PNG, JPG, JPEG, ZIP.
            </p>

            <p
              v-if="fileError"
              class="student-works-error"
              role="alert"
            >
              {{ fileError }}
            </p>
            <input
              id="student-work-files"
              type="file"
              multiple
              :accept="WORK_FILE_ACCEPT"
              :disabled="submitting"
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