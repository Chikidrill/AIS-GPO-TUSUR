
<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { isAxiosError } from 'axios'
import { http } from '../api/http'

type LeaveStatus =
  | 'CREATED'
  | 'UNDER_REVIEW'
  | 'APPROVED'
  | 'REJECTED'
  | 'CANCELLED'

interface LeaveRequest {
  id: number
  studentId: number
  studentName: string
  groupNumber: string | null
  projectId: number
  projectName: string
  status: LeaveStatus
  reason: string | null
  createdAt: string
  takenForReviewAt: string | null
  reviewedAt: string | null
  rejectionReason: string | null
}

const requests = ref<LeaveRequest[]>([])
const loading = ref(true)
const error = ref('')
const search = ref('')
const selectedStatus = ref('')
const processingId = ref<number | null>(null)

const rejectionTarget = ref<LeaveRequest | null>(null)
const rejectionReason = ref('')

const statusLabels: Record<LeaveStatus, string> = {
  CREATED: 'Новая',
  UNDER_REVIEW: 'На рассмотрении',
  APPROVED: 'Одобрена',
  REJECTED: 'Отклонена',
  CANCELLED: 'Отменена',
}

const filteredRequests = computed(() => {
  const query = search.value.trim().toLowerCase()

  return requests.value.filter((request) => {
    const matchesSearch =
      !query ||
      request.studentName.toLowerCase().includes(query) ||
      (request.groupNumber ?? '').toLowerCase().includes(query) ||
      request.projectName.toLowerCase().includes(query)

    const matchesStatus =
      !selectedStatus.value ||
      request.status === selectedStatus.value

    return matchesSearch && matchesStatus
  })
})

const stats = computed(() => ({
  pending: requests.value.filter(
    (request) =>
      request.status === 'CREATED' ||
      request.status === 'UNDER_REVIEW',
  ).length,
  approved: requests.value.filter(
    (request) => request.status === 'APPROVED',
  ).length,
  rejected: requests.value.filter(
    (request) => request.status === 'REJECTED',
  ).length,
}))

function formatDate(value: string) {
  return new Intl.DateTimeFormat('ru-RU', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(new Date(value))
}

async function loadRequests() {
  loading.value = true
  error.value = ''

  try {
    const { data } = await http.get<LeaveRequest[]>(
      '/admin/leave-requests',
    )

    requests.value = data
  } catch {
    error.value = 'Не удалось загрузить заявки на выход.'
  } finally {
    loading.value = false
  }
}

async function performAction(
  request: LeaveRequest,
  action: 'take-for-review' | 'approve',
) {
  if (processingId.value !== null) return

  if (
    (action === 'take-for-review' &&
      request.status !== 'CREATED') ||
    (action === 'approve' &&
      request.status !== 'UNDER_REVIEW')
  ) {
    return
  }

  processingId.value = request.id
  error.value = ''

  try {
    await http.post(
      `/admin/leave-requests/${request.id}/${action}`,
    )

    await loadRequests()
  } catch (err) {
    error.value =
      isAxiosError(err) && err.response?.status === 409
        ? 'Статус заявки изменился. Обновите список.'
        : 'Не удалось обработать заявку.'
  } finally {
    processingId.value = null
  }
}

function openRejection(request: LeaveRequest) {
  if (
    processingId.value !== null ||
    request.status !== 'UNDER_REVIEW'
  ) {
    return
  }

  rejectionTarget.value = request
  rejectionReason.value = ''
  error.value = ''
}

function closeRejection() {
  if (processingId.value !== null) return

  rejectionTarget.value = null
  rejectionReason.value = ''
}

async function rejectRequest() {
  const request = rejectionTarget.value
  const reason = rejectionReason.value.trim()

  if (
    !request ||
    !reason ||
    reason.length > 4000 ||
    processingId.value !== null
  ) {
    return
  }

  processingId.value = request.id
  error.value = ''

  try {
    await http.post(
      `/admin/leave-requests/${request.id}/reject`,
      { reason },
    )

    rejectionTarget.value = null
    rejectionReason.value = ''

    await loadRequests()
  } catch (err) {
    error.value =
      isAxiosError(err) && err.response?.status === 409
        ? 'Заявку уже обработали. Обновите список.'
        : 'Не удалось отклонить заявку.'
  } finally {
    processingId.value = null
  }
}

onMounted(loadRequests)
</script>

<template>
  <section class="leave-applications">
    <div class="leave-stats">
      <div>
        <span>Ожидают решения</span>
        <strong>{{ stats.pending }}</strong>
      </div>

      <div>
        <span>Одобрено</span>
        <strong>{{ stats.approved }}</strong>
      </div>

      <div>
        <span>Отклонено</span>
        <strong>{{ stats.rejected }}</strong>
      </div>
    </div>

    <div class="leave-filters">
      <input
        v-model="search"
        type="search"
        placeholder="Поиск по студенту, группе или проекту"
        aria-label="Поиск заявок"
      >

      <select
        v-model="selectedStatus"
        aria-label="Фильтр по статусу"
      >
        <option value="">Все статусы</option>
        <option value="CREATED">Новые</option>
        <option value="UNDER_REVIEW">На рассмотрении</option>
        <option value="APPROVED">Одобрены</option>
        <option value="REJECTED">Отклонены</option>
        <option value="CANCELLED">Отменены</option>
      </select>
    </div>

    <p v-if="error" class="leave-error" role="alert">
      {{ error }}
    </p>

    <p v-if="loading" class="leave-state">
      Загрузка заявок...
    </p>

    <p
      v-else-if="filteredRequests.length === 0"
      class="leave-state"
    >
      Заявки на выход не найдены.
    </p>

    <div v-else class="leave-table">
      <div class="leave-row leave-row--header">
        <span>Студент</span>
        <span>Группа</span>
        <span>Проект</span>
        <span>Причина выхода</span>
        <span>Статус</span>
        <span>Дата</span>
        <span>Действия</span>
      </div>

      <div
        v-for="request in filteredRequests"
        :key="request.id"
        class="leave-row"
      >
        <span>{{ request.studentName }}</span>

        <span>{{ request.groupNumber ?? '—' }}</span>

        <span>{{ request.projectName }}</span>

        <span>
          {{ request.reason || 'Не указана' }}

          <small
            v-if="
              request.status === 'REJECTED' &&
              request.rejectionReason
            "
          >
            Причина отклонения:
            {{ request.rejectionReason }}
          </small>
        </span>

        <span>
          <span
            class="leave-status"
            :class="`leave-status--${request.status.toLowerCase()}`"
          >
            {{ statusLabels[request.status] }}
          </span>
        </span>

        <span>{{ formatDate(request.createdAt) }}</span>

        <div class="leave-actions">
          <button
            v-if="request.status === 'CREATED'"
            type="button"
            :disabled="processingId !== null"
            @click="performAction(request, 'take-for-review')"
          >
            Взять в работу
          </button>

          <template
            v-else-if="request.status === 'UNDER_REVIEW'"
          >
            <button
              type="button"
              :disabled="processingId !== null"
              @click="performAction(request, 'approve')"
            >
              Одобрить
            </button>

            <button
              type="button"
              class="leave-danger"
              :disabled="processingId !== null"
              @click="openRejection(request)"
            >
              Отклонить
            </button>
          </template>

          <span v-else>Обработана</span>
        </div>
      </div>
    </div>

    <div
      v-if="rejectionTarget"
      class="leave-modal-backdrop"
      @click.self="closeRejection"
    >
      <div
        class="leave-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="leave-reject-title"
      >
        <h2 id="leave-reject-title">
          Отклонение заявки на выход
        </h2>

        <p>
          Студент: {{ rejectionTarget.studentName }}
        </p>

        <p>
          Проект: {{ rejectionTarget.projectName }}
        </p>

        <form @submit.prevent="rejectRequest">
          <label for="leave-rejection-reason">
            Причина отклонения
          </label>

          <textarea
            id="leave-rejection-reason"
            v-model="rejectionReason"
            rows="4"
            maxlength="4000"
            required
            :disabled="processingId !== null"
            placeholder="Объясните причину отклонения..."
          />

          <small>{{ rejectionReason.length }} / 4000</small>

          <div class="leave-modal-actions">
            <button
              type="button"
              :disabled="processingId !== null"
              @click="closeRejection"
            >
              Отмена
            </button>

            <button
              type="submit"
              class="leave-danger"
              :disabled="
                processingId !== null ||
                !rejectionReason.trim()
              "
            >
              Подтвердить отклонение
            </button>
          </div>
        </form>
      </div>
    </div>
  </section>
</template>

<style scoped src="../styles/components/admin-leave-applications.css">
</style>
