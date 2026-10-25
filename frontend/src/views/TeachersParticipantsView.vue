<script setup lang="ts">
import AppLayout from '../components/AppLayout.vue';
import { onMounted, ref, computed, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { http } from '../api/http'

type UserRole = 'ADMIN' | 'TEACHER' | 'STUDENT'

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
  occupiedPlaces: number
  totalPlaces: number | null
}

interface ProjectParticipant{
    id: number,
    fullName: string,
    groupNumber: string | null,
    competencies: string | null,
    joinedAt: string
}

const router = useRouter()
const route = useRoute()
const currentUser = ref<CurrentUser | null>(null)
const projects = ref<TeacherProject[]>([])
const participants = ref<ProjectParticipant[]>([])

const loading = ref(true)
const participantsLoading = ref(false)
const error = ref('')
const participantsError = ref('')

const search = ref('')
const selectedGroup = ref('')

const excludingStudentId = ref<number | null>(null)
const excludeError = ref('')


const routeProjectId = computed(() => {
  const raw = route.params.id ?? route.query.projectId

  return typeof raw === 'string' && /^[1-9]\d*$/.test(raw)
    ? raw
    : ''
})

// При выборе проекта обновляем URL.
const selectedProjectId = computed<string>({
  get: () => routeProjectId.value,
  set: (id) => {
    void router.push(
      id
        ? { path: '/projects_participants', query: { projectId: id } }
        : '/projects_participants',
    )
  },
})

const selectedProject = computed(() => {
  return projects.value.find(
    (project) => project.id === Number(selectedProjectId.value),
  ) ?? null
})

const freePlaces = computed(() => {
  if (!selectedProject.value || selectedProject.value.totalPlaces === null) {
    return null
  }

  return Math.max(
    0,
    selectedProject.value.totalPlaces - selectedProject.value.occupiedPlaces,
  )
})

const groups = computed(() => {
  return [
    ...new Set(
      participants.value
        .map((participant) => participant.groupNumber)
        .filter((group): group is string => Boolean(group)),
    ),
  ].sort()
})

const filteredParticipants = computed(() => {
  const query = search.value.trim().toLowerCase()

  return participants.value.filter((participant) => {
    const matchesSearch =
      !query ||
      participant.fullName.toLowerCase().includes(query) ||
      participant.groupNumber?.toLowerCase().includes(query)

    const matchesGroup =
      !selectedGroup.value ||
      participant.groupNumber === selectedGroup.value

    return matchesSearch && matchesGroup
  })
})

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

// Загружаем участников только выбранного проекта.
let requestVersion = 0

watch(
  [selectedProject, loading],
  async ([project, pageLoading]) => {
    const version = ++requestVersion

    participants.value = []
    participantsError.value = ''
    participantsLoading.value = false

    if (pageLoading || !project) {
      return
    }

    participantsLoading.value = true

    try {
      const { data } = await http.get<ProjectParticipant[]>(
        `/projects/${project.id}/participants`,
      )

      if (version === requestVersion) {
        participants.value = data
      }
    } catch {
      if (version === requestVersion) {
        participantsError.value = 'Не удалось загрузить участников проекта.'
      }
    } finally {
      if (version === requestVersion) {
        participantsLoading.value = false
      }
    }
  },
  { immediate: true },
)

watch(selectedProjectId, () => {
  search.value = ''
  selectedGroup.value = ''
})

function formatDate(value: string) {
  return new Intl.DateTimeFormat('ru-RU', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(new Date(value))
}

async function excludeParticipant(participant: ProjectParticipant) {
  const project = selectedProject.value

  if (!project || excludingStudentId.value !== null) {
    return
  }

  const confirmed = window.confirm(
    `Исключить студента «${participant.fullName}» из проекта «${project.name}»?`,
  )

  if (!confirmed) return

  excludingStudentId.value = participant.id
  excludeError.value = ''

  try {
    await http.post(
      `/projects/${project.id}/participants/${participant.id}/exclude`,
    )

    if (selectedProject.value?.id === project.id) {
      participants.value = participants.value.filter(
        (item) => item.id !== participant.id,
      )

      project.occupiedPlaces = Math.max(0, project.occupiedPlaces - 1)
    }
  } catch {
    excludeError.value = 'Не удалось исключить участника из проекта.'
  } finally {
    excludingStudentId.value = null
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
    <main class="teacher-participants-content">
      <header class="teacher-participants-header">
        <h1>Участники проекта</h1>

        <p>
          Просмотр состава команды проекта.
        </p>
      </header>

      <p
        v-if="loading"
        class="teacher-participants-state"
      >
        Загрузка проектов...
      </p>

      <p
        v-else-if="error"
        class="teacher-participants-state teacher-participants-state--error"
      >
        {{ error }}
      </p>

      <p
        v-else-if="projects.length === 0"
        class="teacher-participants-state"
      >
        У вас пока нет назначенных проектов.
      </p>

      <template v-else>
        <div class="teacher-participants-project-picker">
          <label for="teacher-project-select">
            Проект
          </label>

          <select
            id="teacher-project-select"
            v-model="selectedProjectId"
            class="teacher-participants-select"
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
          class="teacher-participants-state"
        >
          Проект не найден среди назначенных вам проектов.
        </p>

        <p
          v-else-if="!selectedProject"
          class="teacher-participants-state"
        >
          Выберите проект, чтобы посмотреть его участников.
        </p>

        <template v-else>
          <h2 class="teacher-participants-project-name">
            {{ selectedProject.code }} — {{ selectedProject.name }}
          </h2>

          <div class="teacher-participants-stats">
            <div class="teacher-participants-stat">
              <span>Участники</span>
              <strong>{{ selectedProject.occupiedPlaces }}</strong>
            </div>

            <div class="teacher-participants-stat">
              <span>Свободные места</span>
              <strong class="teacher-participants-stat__free">
                {{ freePlaces ?? '—' }}
              </strong>
            </div>
          </div>

          <div class="teacher-participants-filters">
            <input
              v-model="search"
              class="teacher-participants-filter"
              type="search"
              placeholder="Поиск по ФИО или группе"
            >

            <select
              v-model="selectedGroup"
              class="teacher-participants-filter teacher-participants-filter--group"
            >
              <option value="">Группа: все</option>

              <option
                v-for="group in groups"
                :key="group"
                :value="group"
              >
                {{ group }}
              </option>
            </select>
          </div>

          <p
            v-if="participantsLoading"
            class="teacher-participants-state"
          >
            Загрузка участников...
          </p>

          <p
            v-else-if="participantsError"
            class="teacher-participants-state teacher-participants-state--error"
          >
            {{ participantsError }}
          </p>

          <p
            v-else-if="participants.length === 0"
            class="teacher-participants-state"
          >
            В этом проекте пока нет участников.
          </p>
          
          <p
            v-if="excludeError"
            class="teacher-participants-state teacher-participants-state--error"
          >
            {{ excludeError }}
          </p>
          <div
            v-else
            class="teacher-participants-table"
          >
            <div class="teacher-participants-table__header">
              <span>ФИО</span>
              <span>Группа</span>
              <span>Компетенции</span>
              <span>Статус</span>
              <span>Дата вступления</span>
              <span>Действия</span>
            </div>

            <div
              v-for="participant in filteredParticipants"
              :key="participant.id"
              class="teacher-participants-table__row"
            >
              <span class="teacher-participants-table__name">
                {{ participant.fullName }}
              </span>

              <span>
                {{ participant.groupNumber ?? '—' }}
              </span>

              <span>
                {{ participant.competencies || '—' }}
              </span>

              <span class="teacher-participants-table__status">
                Участник
              </span>

              <span>
                {{ formatDate(participant.joinedAt) }}
              </span>
              <div class="teacher-participants-table__actions">
                <button
                  class="teacher-participants-table__exclude"
                  type="button"
                  :disabled="excludingStudentId !== null"
                  @click="excludeParticipant(participant)"
                >
                  {{ excludingStudentId === participant.id ? 'Исключение...' : 'Исключить' }}
                </button>
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
  src="../styles/pages/teacher-participants.css"
></style>