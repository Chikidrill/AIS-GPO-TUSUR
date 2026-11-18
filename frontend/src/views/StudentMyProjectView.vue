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
  title: string
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



type LeaveRequestStatus =
  | 'CREATED'
  | 'UNDER_REVIEW'
  | 'APPROVED'
  | 'REJECTED'
  | 'CANCELLED'

interface ProjectLeaveRequest {
  id: number
  studentId: number
  studentName: string
  projectId: number
  projectName: string
  status: LeaveRequestStatus
  reason: string | null
  createdAt: string
  takenForReviewAt: string | null
  reviewedAt: string | null
  rejectionReason: string | null
}


const router = useRouter()

const currentUser = ref<CurrentUser | null>(null)
const myProject = ref<MyProject | null>(null)
const projectDetails = ref<ProjectDetails | null>(null)
const works = ref<StudentWork[]>([])
const latestRevisions = ref<
  Partial<Record<number, WorkRevision>>
>({})

const loading = ref(true)
const error = ref('')
const noProject = ref(false)


const leaveRequests = ref<ProjectLeaveRequest[]>([])
const leaveError = ref('')
const leaveBusy = ref(false)
const cancellingLeaveId = ref<number | null>(null)

const showLeaveModal = ref(false)
const leaveReason = ref('')

const leaveStatusLabels: Record<LeaveRequestStatus, string> = {
  CREATED: 'Ожидает рассмотрения',
  UNDER_REVIEW: 'На рассмотрении',
  APPROVED: 'Одобрена',
  REJECTED: 'Отклонена',
  CANCELLED: 'Отменена',
}

const activeLeaveRequest = computed(() => {
  if (!myProject.value) return null

  return leaveRequests.value.find(
    (request) =>
      request.projectId === myProject.value?.id &&
      (
        request.status === 'CREATED' ||
        request.status === 'UNDER_REVIEW'
      ),
  ) ?? null
})


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
  leaveError.value = ''
  noProject.value = false
  latestRevisions.value = {}
  projectDetails.value = null

  try {
    const [
      userResponse,
      worksResponse,
      project,
      leaveResponse,
    ] = await Promise.all([
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

      http.get<ProjectLeaveRequest[]>(
        '/me/project/leave-requests',
      ),
    ])

    currentUser.value = userResponse.data
    works.value = worksResponse.data
    myProject.value = project
    leaveRequests.value = leaveResponse.data
    if (!project) {
      noProject.value = true
      return
    }

    const [detailsResult, workResults] =
      await Promise.all([
        Promise.allSettled([
          http.get<ProjectDetails>(`/projects/${project.id}`),
        ]),

        Promise.allSettled(
          projectWorks.value.map((work) =>
            http.get<WorkDetails>(`/me/works/${work.id}`),
          ),
        ),
      ])

    const projectResult = detailsResult[0]

    if (projectResult?.status === 'fulfilled') {
      projectDetails.value = projectResult.value.data
    }

    for (const result of workResults) {
      if (result.status !== 'fulfilled') continue

      const details = result.value.data

      const latestRevision = [...details.revisions].sort(
        (a, b) => b.versionNumber - a.versionNumber,
      )[0]

      if (latestRevision) {
        latestRevisions.value[details.id] = latestRevision
      }
    }
  } catch {
    error.value = 'Не удалось загрузить данные проекта.'
  } finally {
    loading.value = false
  }
}


async function createLeaveRequest() {
  if (
    !myProject.value ||
    activeLeaveRequest.value ||
    leaveBusy.value ||
    leaveReason.value.length > 4000
  ) {
    return
  }

  leaveBusy.value = true
  leaveError.value = ''

  try {
    const { data } = await http.post<ProjectLeaveRequest>(
      '/me/project/leave-requests',
      {
        reason: leaveReason.value.trim() || null,
      },
    )

    leaveRequests.value = [
      data,
      ...leaveRequests.value,
    ]

    showLeaveModal.value = false
    leaveReason.value = ''
  } catch (err) {
    if (isAxiosError(err) && err.response?.status === 409) {
      leaveError.value =
        err.response.data?.code === 'LEAVE_REQUEST_ALREADY_EXISTS'
          ? 'У вас уже есть активная заявка на выход.'
          : 'Создание заявки сейчас недоступно. Обновите страницу.'
    } else {
      leaveError.value = 'Не удалось отправить заявку.'
    }
  } finally {
    leaveBusy.value = false
  }
}

async function cancelLeaveRequest(
  request: ProjectLeaveRequest,
) {
  if (cancellingLeaveId.value !== null) return

  if (!window.confirm('Отменить заявку на выход из проекта?')) {
    return
  }

  cancellingLeaveId.value = request.id
  leaveError.value = ''

  try {
    await http.delete(
      `/me/project/leave-requests/${request.id}`,
    )

    const { data } = await http.get<ProjectLeaveRequest[]>(
      '/me/project/leave-requests',
    )

    leaveRequests.value = data
  } catch {
    leaveError.value =
      'Не удалось отменить заявку. Возможно, её статус уже изменился.'

    // Обновляем состояние после конфликта с сервером.
    try {
      const { data } = await http.get<ProjectLeaveRequest[]>(
        '/me/project/leave-requests',
      )
      leaveRequests.value = data
    } catch {
      // Основную ошибку оставляем на экране.
    }
  } finally {
    cancellingLeaveId.value = null
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
              to="/works"
              class="student-project-button student-project-button--primary"
            >
              Загрузить новую работу
            </RouterLink>

            <a
              href="#student-project-team"
              class="student-project-button"
            >
              Посмотреть участников
            </a>

            <button
              v-if="!activeLeaveRequest"
              type="button"
              class="student-project-button student-project-button--danger"
              @click="
                leaveError = '';
                showLeaveModal = true
              "
            >
              Подать заявку на выход
            </button>

            <p v-else class="student-project-muted">
              Заявка на выход:
              {{ leaveStatusLabels[activeLeaveRequest.status] }}
            </p>

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
                {{ work.title }}
                <small>Версия {{ work.latestVersionNumber }}</small>
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
                {{ latestRevisions[work.id]?.grade ?? '—' }}
              </span>
              
              <span class="student-project-table__comment">
                {{ latestRevisions[work.id]?.reviewComment || '—' }}
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

<section
  v-if="!loading && !error && leaveRequests.length > 0"
  class="student-project-card student-project-leave-history"
>
  <h2>Заявки на выход из проекта</h2>

  <div
    v-for="request in leaveRequests"
    :key="request.id"
    class="student-project-leave-item"
  >
    <div class="student-project-leave-heading">
      <strong>{{ request.projectName }}</strong>
      <span>{{ leaveStatusLabels[request.status] }}</span>
    </div>

    <p class="student-project-muted">
      Подана: {{ formatDate(request.createdAt) }}
    </p>

    <p v-if="request.reason">
      <strong>Причина выхода:</strong>
      {{ request.reason }}
    </p>

    <p
      v-if="request.status === 'REJECTED' && request.rejectionReason"
    >
      <strong>Причина отклонения:</strong>
      {{ request.rejectionReason }}
    </p>

    <button
      v-if="
        request.status === 'CREATED' ||
        request.status === 'UNDER_REVIEW'
      "
      class="student-project-button"
      type="button"
      :disabled="cancellingLeaveId !== null"
      @click="cancelLeaveRequest(request)"
    >
      {{
        cancellingLeaveId === request.id
          ? 'Отмена...'
          : 'Отменить заявку'
      }}
    </button>
  </div>
</section>

      <p
        v-if="leaveError && !showLeaveModal"
        class="student-project-state student-project-state--error"
        role="alert"
      >
        {{ leaveError }}
      </p>

<div
  v-if="showLeaveModal && myProject"
  class="student-project-modal-overlay"
>
  <div
    class="student-project-modal"
    role="dialog"
    aria-modal="true"
    aria-labelledby="leave-modal-title"
  >
    <h2 id="leave-modal-title">
      Заявка на выход из проекта
    </h2>

    <p class="student-project-muted">
      Вы хотите выйти из проекта
      «{{ myProject.name }}».
      До одобрения заявки администратором
      вы останетесь участником проекта.
    </p>

    <form @submit.prevent="createLeaveRequest">
      <label for="student-leave-reason">
        Причина выхода (необязательно)
      </label>

      <textarea
        id="student-leave-reason"
        v-model="leaveReason"
        rows="5"
        maxlength="4000"
        :disabled="leaveBusy"
        placeholder="Укажите причину выхода из проекта..."
      />

      <p class="student-project-muted">
        {{ leaveReason.length }} / 4000 символов
      </p>

      <p
        v-if="leaveError"
        class="student-project-state student-project-state--error"
        role="alert"
      >
        {{ leaveError }}
      </p>

      <div class="student-project-modal-actions">
              <button
                type="button"
                class="student-project-button"
                :disabled="leaveBusy"
                @click="showLeaveModal = false"
              >
                Отмена
              </button>

              <button
                type="submit"
                class="student-project-button student-project-button--danger"
                :disabled="leaveBusy"
              >
                {{ leaveBusy ? 'Отправка...' : 'Подать заявку' }}
              </button>
            </div>
          </form>
        </div>
      </div>

    </main>
  </AppLayout>
</template>

<style
  scoped
  src="../styles/pages/student-my-project.css"
></style>