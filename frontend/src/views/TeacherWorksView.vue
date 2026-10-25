<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'

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

interface TeacherProject {
  id: number
  code: string | null
  name: string
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

const route = useRoute()
const router = useRouter()

const currentUser = ref<CurrentUser | null>(null)
const projects = ref<TeacherProject[]>([])
const works = ref<StudentWork[]>([])

const loading = ref(true)
const worksLoading = ref(false)
const error = ref('')
const worksError = ref('')

const search = ref('')
const selectedStatus = ref('')

const statusLabels: Record<WorkStatus, string> = {
  SUBMITTED: 'На проверке',
  NEEDS_REVISION: 'На доработке',
  ACCEPTED: 'Принята',
}

// Проект можно открыть из таблицы или выбрать в меню.
const selectedProjectId = computed<string>({
  get: () => {
    const id = route.params.id ?? route.query.projectId

    return typeof id === 'string' && /^[1-9]\d*$/.test(id)
      ? id
      : ''
  },
  set: (id) => {
    void router.push({
      path: '/projects_works',
      query: id ? { projectId: id } : {},
    })
  },
})

const selectedProject = computed(() =>
  projects.value.find(
    (project) => project.id === Number(selectedProjectId.value),
  ) ?? null,
)

const filteredWorks = computed(() => {
  const query = search.value.trim().toLowerCase()

  return works.value.filter((work) => {
    const matchesSearch =
      !query ||
      work.studentName.toLowerCase().includes(query) ||
      work.groupNumber?.toLowerCase().includes(query)

    const matchesStatus =
      !selectedStatus.value ||
      work.status === selectedStatus.value

    return matchesSearch && matchesStatus
  })
})

function formatDate(value: string) {
  return new Intl.DateTimeFormat('ru-RU', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(new Date(value))
}

async function loadPage() {
  loading.value = true
  error.value = ''

  try {
    const [userResponse, projectsResponse] = await Promise.all([
      http.get<CurrentUser>('/me'),
      http.get<TeacherProject[]>('/me/projects'),
    ])

    currentUser.value = userResponse.data
    projects.value = projectsResponse.data
  } catch {
    error.value = 'Не удалось загрузить проекты преподавателя.'
  } finally {
    loading.value = false
  }
}

let requestVersion = 0

watch(
  [selectedProject, loading],
  async ([project, pageLoading]) => {
    const version = ++requestVersion

    works.value = []
    worksError.value = ''
    worksLoading.value = false

    if (pageLoading || !project) return

    worksLoading.value = true

    try {
      const { data } = await http.get<StudentWork[]>(
        `/projects/${project.id}/works`,
      )

      if (version === requestVersion) {
        works.value = data
      }
    } catch {
      if (version === requestVersion) {
        worksError.value = 'Не удалось загрузить работы проекта.'
      }
    } finally {
      if (version === requestVersion) {
        worksLoading.value = false
      }
    }
  },
  { immediate: true },
)

watch(selectedProjectId, () => {
  search.value = ''
  selectedStatus.value = ''
})

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
    <main class="teacher-works-content">
      <header class="teacher-works-header">
        <h1>Работы студентов</h1>

        <p>
          Просмотр и проверка работ участников ваших проектов.
        </p>
      </header>

      <p
        v-if="loading"
        class="teacher-works-state"
      >
        Загрузка проектов...
      </p>

      <p
        v-else-if="error"
        class="teacher-works-state teacher-works-state--error"
      >
        {{ error }}
      </p>

      <p
        v-else-if="projects.length === 0"
        class="teacher-works-state"
      >
        У вас пока нет назначенных проектов.
      </p>

      <template v-else>
        <div class="teacher-works-project-picker">
          <label for="teacher-works-project">
            Проект
          </label>

          <select
            id="teacher-works-project"
            v-model="selectedProjectId"
            class="teacher-works-filter"
          >
            <option value="">
              Выберите проект
            </option>

            <option
              v-for="project in projects"
              :key="project.id"
              :value="String(project.id)"
            >
              {{ project.code ?? 'Без кода' }} — {{ project.name }}
            </option>
          </select>
        </div>

        <p
          v-if="selectedProjectId && !selectedProject"
          class="teacher-works-state"
        >
          Проект не найден среди назначенных вам проектов.
        </p>

        <p
          v-else-if="!selectedProject"
          class="teacher-works-state"
        >
          Выберите проект для просмотра работ студентов.
        </p>

        <template v-else>
          <h2 class="teacher-works-project-name">
            {{ selectedProject.code }} — {{ selectedProject.name }}
          </h2>

          <div class="teacher-works-filters">
            <input
              v-model="search"
              class="teacher-works-filter"
              type="search"
              placeholder="Поиск по ФИО студента или группе"
            >

            <select
              v-model="selectedStatus"
              class="teacher-works-filter"
            >
              <option value="">Статус: все</option>
              <option value="SUBMITTED">На проверке</option>
              <option value="NEEDS_REVISION">На доработке</option>
              <option value="ACCEPTED">Принята</option>
            </select>
          </div>

          <p
            v-if="worksLoading"
            class="teacher-works-state"
          >
            Загрузка работ...
          </p>

          <p
            v-else-if="worksError"
            class="teacher-works-state teacher-works-state--error"
          >
            {{ worksError }}
          </p>

          <p
            v-else-if="works.length === 0"
            class="teacher-works-state"
          >
            В этом проекте пока нет отправленных работ.
          </p>

          <p
            v-else-if="filteredWorks.length === 0"
            class="teacher-works-state"
          >
            Работы по заданным параметрам не найдены.
          </p>

          <div
            v-else
            class="teacher-works-table"
          >
            <div class="teacher-works-table__header">
              <span>Студент</span>
              <span>Группа</span>
              <span>Версия</span>
              <span>Дата отправки</span>
              <span>Статус</span>
              <span>Действия</span>
            </div>

            <div
              v-for="work in filteredWorks"
              :key="work.id"
              class="teacher-works-table__row"
            >
              <span class="teacher-works-table__student">
                {{ work.studentName }}
              </span>

              <span>{{ work.groupNumber ?? '—' }}</span>

              <span>№ {{ work.latestVersionNumber }}</span>

              <span>{{ formatDate(work.lastSubmittedAt) }}</span>

              <div class="teacher-works-table__status-cell">
                <span
                  class="teacher-works-table__status"
                  :class="`teacher-works-table__status--${work.status.toLowerCase()}`"
                >
                  {{ statusLabels[work.status] }}
                </span>
              </div>
              <div class="teacher-works-table__actions">
                <RouterLink
                    class="teacher-works-table__action"
                    :to="`/myprojects/${work.projectId}/works/${work.id}`"
                >
                    {{ work.status === 'SUBMITTED' ? 'Проверить' : 'Открыть' }}
                </RouterLink>
                </div>
            </div>
          </div>
        </template>
      </template>
    </main>
  </AppLayout>
</template>

<style
  scoped
  src="../styles/pages/teacher-works.css"
></style>