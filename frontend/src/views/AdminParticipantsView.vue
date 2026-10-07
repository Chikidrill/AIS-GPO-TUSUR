<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { http } from '../api/http'
import AppLayout from '../components/AppLayout.vue'

type UserRole = 'ADMIN' | 'TEACHER' | 'STUDENT'

interface CurrentUser {
  id: number
  email: string
  fullName: string
  role: UserRole
}

interface Participant {
  studentId: number
  studentName: string
  groupNumber: string | null
  projectId: number
  projectCode: string | null
  projectName: string
  faculty: string | null
  department: string
  joinedAt: string
}

const router = useRouter()

const currentUser = ref<CurrentUser | null>(null)
const participants = ref<Participant[]>([])

const loading = ref(true)
const error = ref('')

const search = ref('')
const selectedProject = ref('')
const selectedFaculty = ref('')
const selectedDepartment = ref('')

const projects = computed(() => {
  const map = new Map<number, string>()

  participants.value.forEach((participant) => {
    map.set(
      participant.projectId,
      participant.projectCode
        ? `${participant.projectCode} — ${participant.projectName}`
        : participant.projectName
    )
  })

  return [...map.entries()].map(([id, label]) => ({
    id,
    label,
  }))
})

const faculties = computed(() => {
  return [
    ...new Set(
      participants.value
        .map(participant => participant.faculty)
        .filter((faculty): faculty is string => Boolean(faculty))
    ),
  ].sort()
})

const departments = computed(() => {
  return [
    ...new Set(
      participants.value
        .map(participant => participant.department)
        .filter(Boolean)
    ),
  ].sort()
})

const stats = computed(() => ({
  participants: participants.value.length,

  projects: new Set(
    participants.value.map(participant => participant.projectId)
  ).size,
}))

const filteredParticipants = computed(() => {
  const query = search.value.trim().toLowerCase()

  return participants.value.filter((participant) => {
    const matchesSearch =
      !query ||
      participant.studentName.toLowerCase().includes(query) ||
      participant.groupNumber?.toLowerCase().includes(query)

    const matchesProject =
      !selectedProject.value ||
      participant.projectId === Number(selectedProject.value)

    const matchesFaculty =
      !selectedFaculty.value ||
      participant.faculty === selectedFaculty.value

    const matchesDepartment =
      !selectedDepartment.value ||
      participant.department === selectedDepartment.value

    return (
      matchesSearch &&
      matchesProject &&
      matchesFaculty &&
      matchesDepartment
    )
  })
})

function getProjectLabel(participant: Participant) {
  if (!participant.projectCode) {
    return participant.projectName
  }

  return `${participant.projectCode} — ${participant.projectName}`
}

async function loadPage() {
  loading.value = true
  error.value = ''

  try {
    const [userResponse, participantsResponse] =
      await Promise.all([
        http.get<CurrentUser>('/me'),
        http.get<Participant[]>('/admin/participants'),
      ])

    currentUser.value = userResponse.data
    participants.value = participantsResponse.data
  } catch {
    error.value = 'Не удалось загрузить участников.'
  } finally {
    loading.value = false
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
    <main class="admin-participants-content">
      <header class="admin-participants-header">
        <h1>Участники проектов</h1>

        <p>
          Просмотр состава проектов и участников ГПО.
        </p>
      </header>

      <div class="admin-participants-filters">
        <input
          v-model="search"
          class="admin-participants-filter"
          type="search"
          placeholder="Поиск по ФИО или группе"
        >

        <select
          v-model="selectedProject"
          class="admin-participants-filter"
        >
          <option value="">
            Проект: все
          </option>

          <option
            v-for="project in projects"
            :key="project.id"
            :value="project.id"
          >
            {{ project.label }}
          </option>
        </select>

        <select
          v-model="selectedFaculty"
          class="admin-participants-filter"
        >
          <option value="">
            Подразделение: все
          </option>

          <option
            v-for="faculty in faculties"
            :key="faculty"
            :value="faculty"
          >
            {{ faculty }}
          </option>
        </select>

        <select
          v-model="selectedDepartment"
          class="admin-participants-filter"
        >
          <option value="">
            Кафедра: все
          </option>

          <option
            v-for="department in departments"
            :key="department"
            :value="department"
          >
            {{ department }}
          </option>
        </select>
      </div>

      <section class="admin-participants-stats">
        <div class="admin-participants-stat">
          <span>Всего участников</span>

          <strong>
            {{ stats.participants }}
          </strong>
        </div>

        <div class="admin-participants-stat">
          <span>Проектов</span>

          <strong class="admin-participants-stat__projects">
            {{ stats.projects }}
          </strong>
        </div>
      </section>

      <div
        v-if="error"
        class="admin-participants-error"
      >
        <span>{{ error }}</span>

        <button
          type="button"
          aria-label="Закрыть сообщение"
          @click="error = ''"
        >
          ×
        </button>
      </div>

      <p
        v-if="loading"
        class="admin-participants-state"
      >
        Загрузка участников...
      </p>

      <p
        v-else-if="filteredParticipants.length === 0"
        class="admin-participants-state"
      >
        Участники не найдены.
      </p>

      <div
        v-else
        class="admin-participants-table"
      >
        <div class="admin-participants-table__header">
          <span>Студент</span>
          <span>Группа</span>
          <span>Проект</span>
          <span>Подразд.</span>
          <span>Кафедра</span>
          <span>Статус</span>
          <span>Действия</span>
        </div>

        <div
          v-for="participant in filteredParticipants"
          :key="`${participant.studentId}-${participant.projectId}`"
          class="admin-participants-table__row"
        >
          <span class="admin-participants-table__student">
            {{ participant.studentName }}
          </span>

          <span>
            {{ participant.groupNumber ?? '—' }}
          </span>

          <span>
            {{ getProjectLabel(participant) }}
          </span>

          <span>
            {{ participant.faculty ?? '—' }}
          </span>

          <span>
            {{ participant.department }}
          </span>

          <span class="admin-participants-table__status">
            Участник
          </span>
          <button
            class="admin-participants-table__action admin-participants-table__action--exclude"
            type="button"
            title="Исключить"
          >
            Исключить
          </button>
        </div>
      </div>
    </main>
  </AppLayout>
</template>

<style
  scoped
  src="../styles/pages/admin-participants.css"
></style>