<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { http } from '../api/http'
import AppLayout from '../components/AppLayout.vue'

type UserRole = 'ADMIN' | 'TEACHER' | 'STUDENT'

type ProjectStatus =
  | 'DRAFT'
  | 'OPEN'
  | 'IN_PROGRESS'
  | 'COMPLETED'
  | 'ARCHIVED'

interface CurrentUser {
  id: number
  email: string
  fullName: string
  role: UserRole
}

interface Project {
  id: number
  code: string | null
  name: string
  faculty: string | null
  department: string
  status: ProjectStatus
  supervisorId: number | null
  supervisorName: string | null
}

interface Teacher{
  id: number,
  fullName: string
}

const router = useRouter()

const currentUser = ref<CurrentUser | null>(null)
const projects = ref<Project[]>([])

const loading = ref(true)
const error = ref('')
const deletingProjectId = ref<number | null>(null)
const search = ref('')
const selectedFaculty = ref('')
const selectedDepartment = ref('')
const selectedStatus = ref('')

const teachers = ref<Teacher[]>([])

const supervisorModalOpen = ref(false)
const selectedProject = ref<Project | null>(null)
const selectedSupervisorId = ref<number | null>(null)
const assigningSupervisor = ref(false)

const faculties = computed(() => {
  return [
    ...new Set(
      projects.value
        .map(project => project.faculty)
        .filter((faculty): faculty is string => Boolean(faculty))
    ),
  ].sort()
})

const departments = computed(() => {
  return [
    ...new Set(
      projects.value
        .map(project => project.department)
        .filter(Boolean)
    ),
  ].sort()
})

const projectStats = computed(() => ({
  total: projects.value.length,

  open: projects.value.filter(
    project => project.status === 'OPEN'
  ).length,

  withoutSupervisor: projects.value.filter(
    project => project.supervisorId === null
  ).length,
}))

const filteredProjects = computed(() => {
  const query = search.value.trim().toLowerCase()

  return projects.value.filter((project) => {
    const matchesSearch =
      !query ||
      project.name.toLowerCase().includes(query) ||
      project.code?.toLowerCase().includes(query)

    const matchesFaculty =
      !selectedFaculty.value ||
      project.faculty === selectedFaculty.value

    const matchesDepartment =
      !selectedDepartment.value ||
      project.department === selectedDepartment.value

    const matchesStatus =
      !selectedStatus.value ||
      project.status === selectedStatus.value

    return (
      matchesSearch &&
      matchesFaculty &&
      matchesDepartment &&
      matchesStatus
    )
  })
})

async function deleteProject(project: Project) {
  if (deletingProjectId.value !== null) {
    return
  }

  const confirmed = window.confirm(
    `Удалить проект «${project.name}»?`
  )

  if (!confirmed) {
    return
  }

  deletingProjectId.value = project.id
  error.value = ''

  try {
    await http.delete(`/projects/${project.id}`)

    projects.value = projects.value.filter(
      item => item.id !== project.id
    )
  } catch {
    error.value =
      'Не удалось удалить проект. Возможно, у проекта уже есть заявки или участники.'
  } finally {
    deletingProjectId.value = null
  }
}

function openSupervisorModal(project: Project) {
  selectedProject.value = project
  selectedSupervisorId.value = project.supervisorId

  error.value = ''
  supervisorModalOpen.value = true
}

function closeSupervisorModal() {
  if (assigningSupervisor.value) {
    return
  }

  supervisorModalOpen.value = false
  selectedProject.value = null
  selectedSupervisorId.value = null
}

async function saveSupervisor() {
  if (!selectedProject.value) {
    return
  }

  assigningSupervisor.value = true
  error.value = ''

  try {
    const { data } = await http.patch<Project>(
      `/projects/${selectedProject.value.id}/supervisor`,
      {
        supervisorId: selectedSupervisorId.value,
      }
    )

    const index = projects.value.findIndex(
      project => project.id === data.id
    )

    if (index !== -1) {
      projects.value[index] = {
        ...projects.value[index],
        supervisorId: data.supervisorId,
        supervisorName: data.supervisorName,
      }
    }

    closeSupervisorModal()
  } catch {
    error.value = 'Не удалось назначить преподавателя.'
  } finally {
    assigningSupervisor.value = false
  }
}

function getProjectStatusLabel(status: ProjectStatus) {
  switch (status) {
    case 'DRAFT':
      return 'Черновик'

    case 'OPEN':
      return 'Идёт набор'

    case 'IN_PROGRESS':
      return 'В работе'

    case 'COMPLETED':
      return 'Завершён'

    case 'ARCHIVED':
      return 'Архив'
  }
}

async function loadPage() {
  loading.value = true
  error.value = ''

  try {
    const [
      userResponse,
      projectsResponse,
      teachersResponse,
    ] = await Promise.all([
      http.get<CurrentUser>('/me'),
      http.get<Project[]>('/projects'),
      http.get<Teacher[]>('/admin/teachers'),
    ])

    currentUser.value = userResponse.data
    projects.value = projectsResponse.data
    teachers.value = teachersResponse.data
  } catch {
    error.value = 'Не удалось загрузить проекты.'
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
    <main class="admin-projects-content">
      <header class="admin-projects-header">
        <div>
          <h1>Управление проектами</h1>

          <p>
            Создание и редактирование проектов, назначение преподавателей.
          </p>
        </div>

        <RouterLink
            class="admin-projects-header__create"
            to="/admin/projects/create"
            >
            Создать проект
        </RouterLink>
      </header>

      <div class="admin-projects-filters">
        <input
          v-model="search"
          class="admin-projects-filter"
          type="search"
          placeholder="Поиск по названию проекта"
        >

        <select
          v-model="selectedFaculty"
          class="admin-projects-filter"
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
          class="admin-projects-filter"
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

        <select
          v-model="selectedStatus"
          class="admin-projects-filter"
        >
          <option value="">
            Статус: все
          </option>

          <option value="OPEN">
            Идёт набор
          </option>

          <option value="IN_PROGRESS">
            В работе
          </option>

          <option value="COMPLETED">
            Завершён
          </option>

          <option value="ARCHIVED">
            Архив
          </option>

          <option value="DRAFT">
            Черновик
          </option>
        </select>
      </div>

      <section class="admin-projects-stats">
        <div class="admin-projects-stat">
          <span>Всего проектов</span>

          <strong>
            {{ projectStats.total }}
          </strong>
        </div>

        <div class="admin-projects-stat">
          <span>Идёт набор</span>

          <strong class="admin-projects-stat__value--open">
            {{ projectStats.open }}
          </strong>
        </div>

        <div class="admin-projects-stat">
          <span>Без преподавателя</span>

          <strong class="admin-projects-stat__value--warning">
            {{ projectStats.withoutSupervisor }}
          </strong>
        </div>
      </section>

      <div
  v-if="error"
  class="admin-projects-error"
>
  <span>
    {{ error }}
  </span>

  <button
    class="admin-projects-error__close"
    type="button"
    aria-label="Закрыть сообщение"
    @click="error = ''"
  >
    ×
  </button>
</div>

        <p
          v-if="loading"
          class="admin-projects-state"
        >
          Загрузка проектов...
        </p>

        <p
          v-else-if="!error && filteredProjects.length === 0"
          class="admin-projects-state"
        >
          Проекты не найдены.
        </p>

        <div
          v-else-if="filteredProjects.length > 0"
          class="admin-projects-table"
        >
        <div class="admin-projects-table__header">
          <span>Код</span>
          <span>Название</span>
          <span>Подразд.</span>
          <span>Кафедра</span>
          <span>Преподаватель</span>
          <span>Статус</span>
          <span>Действия</span>
        </div>

        <div
          v-for="project in filteredProjects"
          :key="project.id"
          class="admin-projects-table__row"
        >
          <span class="admin-projects-table__code">
            {{ project.code ?? '—' }}
          </span>

          <span class="admin-projects-table__name">
            {{ project.name }}
          </span>

          <span>
            {{ project.faculty ?? '—' }}
          </span>

          <span>
            {{ project.department }}
          </span>

          <span
            :class="{
              'admin-projects-table__unassigned':
                !project.supervisorName,
            }"
          >
            {{ project.supervisorName ?? 'Не назначен' }}
          </span>
          <span>
          <span
            class="admin-projects-table__status"
            :class="`admin-projects-table__status--${project.status.toLowerCase()}`"
          >
            {{ getProjectStatusLabel(project.status) }}
          </span>
        </span>
          <div class="admin-projects-table__actions">
            <button
              class="admin-projects-table__action"
              type="button"
              @click="router.push(`/admin/projects/${project.id}/edit`)"
            >
              Редактировать
            </button>

            <button
              class="admin-projects-table__action"
              type="button"
              @click="openSupervisorModal(project)"
            >
              {{ project.supervisorId ? 'Изменить' : 'Назначить' }}
            </button>
            <button
              class="admin-projects-table__action-delete"
              type="button"
              :disabled="deletingProjectId !== null"
              @click="deleteProject(project)"
            >
              {{
                deletingProjectId === project.id
                  ? 'Удаление...'
                  : 'Удалить'
              }}
            </button>
          </div>
        </div>
      </div>
      <Teleport to="body">
        <div
          v-if="supervisorModalOpen && selectedProject"
          class="admin-projects-modal-overlay"
          @click.self="closeSupervisorModal"
        >
          <div class="admin-projects-modal">
            <div class="admin-projects-modal__header">
              <div>
                <h2>Назначение преподавателя</h2>

                <p>
                  {{ selectedProject.code ?? '—' }}
                  —
                  {{ selectedProject.name }}
                </p>
              </div>

              <button
                class="admin-projects-modal__close"
                type="button"
                aria-label="Закрыть"
                @click="closeSupervisorModal"
              >
                ×
              </button>
            </div>

            <label class="admin-projects-modal__field">
              <span>Преподаватель</span>

              <select v-model="selectedSupervisorId">
                <option :value="null">
                  Не назначен
                </option>

                <option
                  v-for="teacher in teachers"
                  :key="teacher.id"
                  :value="teacher.id"
                >
                  {{ teacher.fullName }}
                </option>
              </select>
            </label>

            <div class="admin-projects-modal__actions">
              <button
                class="
                  admin-projects-modal__button
                  admin-projects-modal__button--secondary
                "
                type="button"
                :disabled="assigningSupervisor"
                @click="closeSupervisorModal"
              >
                Отмена
              </button>

              <button
                class="
                  admin-projects-modal__button
                  admin-projects-modal__button--primary
                "
                type="button"
                :disabled="assigningSupervisor"
                @click="saveSupervisor"
              >
                {{
                  assigningSupervisor
                    ? 'Сохранение...'
                    : 'Сохранить'
                }}
              </button>
            </div>
          </div>
        </div>
      </Teleport>
    </main>
  </AppLayout>
</template>

<style
  scoped
  src="../styles/pages/admin-projects.css"
></style>