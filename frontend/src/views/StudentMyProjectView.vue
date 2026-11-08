<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { isAxiosError } from 'axios'
import { useRouter } from 'vue-router'

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

interface ProjectParticipant {
  id: number
  fullName: string
}

interface MyProject {
  id: number
  name: string
  description: string | null
  participants: ProjectParticipant[]
}

interface ProjectDetails {
  id: number
  code: string | null
  name: string
  department: string
  description: string | null
  semester: number | null
  supervisorName: string | null
  occupiedPlaces: number
  totalPlaces: number | null
}

interface StudentWork {
  id: number
  projectId: number
  projectName: string
  status: WorkStatus
  latestVersionNumber: number
  lastSubmittedAt: string
}

interface WorkRevision {
  id: number
  versionNumber: number
  grade: number | null
  reviewComment: string | null
}

interface WorkDetails {
  id: number
  revisions: WorkRevision[]
}

const router = useRouter()

const currentUser = ref<CurrentUser | null>(null)
const myProject = ref<MyProject | null>(null)
const projectDetails = ref<ProjectDetails | null>(null)
const works = ref<StudentWork[]>([])
const workDetails = ref<WorkDetails | null>(null)

const loading = ref(true)
const error = ref('')
const noProject = ref(false)

const statusLabels: Record<WorkStatus, string> = {
  SUBMITTED: 'На проверке',
  NEEDS_REVISION: 'На доработке',
  ACCEPTED: 'Принята',
}

const projectWorks = computed(() => {
  if (!myProject.value) return []

  return works.value.filter(
    (work) => work.projectId === myProject.value?.id,
  )
})

const currentWork = computed(() => projectWorks.value[0] ?? null)

const latestRevision = computed(() => {
  const revisions = workDetails.value?.revisions ?? []

  return [...revisions].sort(
    (a, b) => b.versionNumber - a.versionNumber,
  )[0] ?? null
})

const workButtonText = computed(() => {
  if (!currentWork.value) return 'Загрузить работу'

  return currentWork.value.status === 'NEEDS_REVISION'
    ? 'Доработать работу'
    : 'Открыть работу'
})

const workButtonUrl = computed(() => {
  return currentWork.value
    ? `/works/${currentWork.value.id}`
    : '/works'
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
  noProject.value = false

  try {
    const [userResponse, worksResponse, project] =
      await Promise.all([
        http.get<CurrentUser>('/me'),
        http.get<StudentWork[]>('/me/works'),

        http
          .get<MyProject>('/me/project')
          .then((response) => response.data)
          .catch((err: unknown) => {
            if (
              isAxiosError(err) &&
              err.response?.status === 404 &&
              err.response.data?.code === 'ACTIVE_PROJECT_NOT_FOUND'
            ) {
              return null
            }

            throw err
          }),
      ])

    currentUser.value = userResponse.data
    works.value = worksResponse.data
    myProject.value = project

    if (!project) {
      noProject.value = true
      return
    }

    const currentProjectWork = works.value.find(
      (work) => work.projectId === project.id,
    )

    // Дополнительные сведения могут быть недоступны
    // для закрытых проектов. Основная страница при этом
    // должна продолжать работать через /me/project.
    const [detailsResult, workResult] =
      await Promise.allSettled([
        http.get<ProjectDetails>(`/projects/${project.id}`),

        currentProjectWork
          ? http.get<WorkDetails>(
              `/me/works/${currentProjectWork.id}`,
            )
          : Promise.resolve(null),
      ])

    if (detailsResult.status === 'fulfilled') {
      projectDetails.value = detailsResult.value.data
    }

    if (
      workResult.status === 'fulfilled' &&
      workResult.value
    ) {
      workDetails.value = workResult.value.data
    }
  } catch {
    error.value = 'Не удалось загрузить данные проекта.'
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
    <main class="student-project-content">
      <header class="student-project-header">
        <h1>Мой проект</h1>
      </header>

      <p
        v-if="loading"
        class="student-project-state"
      >
        Загрузка проекта...
      </p>

      <p
        v-else-if="error"
        class="student-project-state student-project-state--error"
      >
        {{ error }}
      </p>

      <section
        v-else-if="noProject"
        class="student-project-card"
      >
        <h2>Вы пока не участвуете в проекте</h2>

        <p class="student-project-muted">
          После одобрения заявки и зачисления в проект
          здесь появятся сведения о вашей команде.
        </p>

        <RouterLink
          to="/projects"
          class="student-project-button student-project-button--primary"
        >
          Перейти в каталог проектов
        </RouterLink>
      </section>

      <template v-else-if="myProject">
        <div class="student-project-intro">
          <h2>
            <template v-if="projectDetails?.code">
              {{ projectDetails.code }} —
            </template>
            {{ myProject.name }}
          </h2>

          <p>Вы зачислены в проект как участник.</p>
        </div>

        <div class="student-project-top">
          <section class="student-project-card student-project-info">
            <h2>О проекте</h2>

            <p class="student-project-description">
              {{
                myProject.description ||
                'Описание проекта не указано.'
              }}
            </p>

            <div class="student-project-tags">
              <span v-if="projectDetails?.department">
                {{ projectDetails.department }}
              </span>

              <span>
                {{ myProject.participants.length }}
                {{
                  projectDetails?.totalPlaces != null
                    ? `из ${projectDetails.totalPlaces}`
                    : ''
                }}
                участников
              </span>

              <span v-if="projectDetails?.semester != null">
                Семестр: {{ projectDetails.semester }}
              </span>
            </div>

            <div
              v-if="projectDetails?.supervisorName"
              class="student-project-supervisor"
            >
              <span>Руководитель</span>
              <strong>{{ projectDetails.supervisorName }}</strong>
            </div>
          </section>

          <section class="student-project-card student-project-actions">
            <h2>Работа в проекте</h2>

            <p class="student-project-muted">
              Загружайте результаты работы
              и отслеживайте проверку преподавателем.
            </p>

            <RouterLink
              :to="workButtonUrl"
              class="student-project-button student-project-button--primary"
            >
              {{ workButtonText }}
            </RouterLink>

            <a
              href="#student-project-team"
              class="student-project-button"
            >
              Посмотреть участников
            </a>
          </section>
        </div>

        <section class="student-project-card student-project-works">
          <div class="student-project-section-header">
            <h2>Мои работы</h2>

            <RouterLink
              to="/works"
              class="student-project-button student-project-button--primary"
            >
              Перейти к работам
            </RouterLink>
          </div>

          <p
            v-if="projectWorks.length === 0"
            class="student-project-empty"
          >
            Вы пока не отправляли работы в этот проект.
          </p>

          <div
            v-else
            class="student-project-table"
          >
            <div class="student-project-table__header">
              <span>Работа</span>
              <span>Дата</span>
              <span>Статус</span>
              <span>Баллы</span>
              <span>Комментарий</span>
              <span>Действия</span>
            </div>

            <div
              v-for="work in projectWorks"
              :key="work.id"
              class="student-project-table__row"
            >
              <span class="student-project-table__name">
                Работа №{{ work.id }}
                (версия {{ work.latestVersionNumber }})
              </span>

              <span>
                {{ formatDate(work.lastSubmittedAt) }}
              </span>

              <span
                class="student-project-table__status"
                :class="`student-project-table__status--${work.status.toLowerCase()}`"
              >
                {{ statusLabels[work.status] }}
              </span>

              <span>
                {{
                  workDetails?.id === work.id
                    ? (latestRevision?.grade ?? '—')
                    : '—'
                }}
              </span>

              <span class="student-project-table__comment">
                {{
                  workDetails?.id === work.id
                    ? (latestRevision?.reviewComment || '—')
                    : '—'
                }}
              </span>

              <div class="student-project-table__action-cell">
                <RouterLink
                  :to="`/works/${work.id}`"
                  class="student-project-button"
                >
                  {{ work.status === 'NEEDS_REVISION' ? 'Доработать' : 'Открыть' }}
                </RouterLink>
              </div>
            </div>
          </div>
        </section>

        <section
          id="student-project-team"
          class="student-project-card student-project-team"
        >
          <h2>Участники проекта</h2>

          <p
            v-if="myProject.participants.length === 0"
            class="student-project-muted"
          >
            В проекте пока нет участников.
          </p>

          <div
            v-for="participant in myProject.participants"
            :key="participant.id"
            class="student-project-participant"
          >
            <span>{{ participant.fullName }}</span>

            <span
              v-if="participant.id === currentUser?.id"
              class="student-project-self"
            >
              Вы
            </span>
          </div>
        </section>
      </template>
    </main>
  </AppLayout>
</template>

<style
  scoped
  src="../styles/pages/student-my-project.css"
></style>