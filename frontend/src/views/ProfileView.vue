
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

interface StudentProfile {
  groupNumber: string | null
  about: string | null
  competencies: string | null
}

const router = useRouter()

const currentUser = ref<CurrentUser | null>(null)
const studentProfile = ref<StudentProfile | null>(null)

const about = ref('')
const competencies = ref('')

const loading = ref(true)
const saving = ref(false)

const error = ref('')
const saveError = ref('')
const saveSuccess = ref('')

const roleLabels: Record<UserRole, string> = {
  STUDENT: 'Студент',
  TEACHER: 'Преподаватель',
  ADMIN: 'Администратор',
}

const isStudent = computed(() =>
  currentUser.value?.role === 'STUDENT'
)

const hasChanges = computed(() => {
  if (!studentProfile.value) return false

  return (
    about.value !== (studentProfile.value.about ?? '') ||
    competencies.value !==
      (studentProfile.value.competencies ?? '')
  )
})

async function loadPage() {
  loading.value = true
  error.value = ''

  try {
    const { data: user } =
      await http.get<CurrentUser>('/me')

    currentUser.value = user

    // Дополнительный профиль есть только у STUDENT.
    if (user.role === 'STUDENT') {
      const { data: profile } =
        await http.get<StudentProfile>('/me/profile')

      studentProfile.value = profile
      about.value = profile.about ?? ''
      competencies.value = profile.competencies ?? ''
    } else {
      studentProfile.value = null
    }
  } catch {
    error.value = 'Не удалось загрузить профиль.'
  } finally {
    loading.value = false
  }
}

async function saveProfile() {
  if (
    !isStudent.value ||
    !studentProfile.value ||
    saving.value ||
    !hasChanges.value
  ) {
    return
  }

  saving.value = true
  saveError.value = ''
  saveSuccess.value = ''

  try {
    const { data } = await http.patch<StudentProfile>(
      '/me/profile',
      {
        // В текущем backend отсутствие groupNumber
        // обнуляет группу. Поэтому сохраняем
        // значение, ранее полученное от сервера.
        groupNumber: studentProfile.value.groupNumber,
        about: about.value.trim() || null,
        competencies: competencies.value.trim() || null,
      },
    )

    studentProfile.value = data
    about.value = data.about ?? ''
    competencies.value = data.competencies ?? ''

    saveSuccess.value = 'Изменения успешно сохранены.'
  } catch {
    saveError.value = 'Не удалось сохранить профиль.'
  } finally {
    saving.value = false
  }
}

function clearMessages() {
  saveError.value = ''
  saveSuccess.value = ''
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
    <main class="profile-content">
      <header class="profile-header">
        <h1>Мой профиль</h1>
        <p>
          Информация о вашей учётной записи.
        </p>
      </header>

      <p v-if="loading" class="profile-state">
        Загрузка профиля...
      </p>

      <div
        v-else-if="error"
        class="profile-state profile-error"
      >
        <p>{{ error }}</p>
        <button
          type="button"
          class="profile-button"
          @click="loadPage"
        >
          Повторить
        </button>
      </div>

      <template v-else-if="currentUser">
        <!-- Общий блок для всех ролей -->
        <section class="profile-card">
          <div class="profile-card-header">
            <div class="profile-avatar">
              {{ currentUser.fullName.charAt(0).toUpperCase() }}
            </div>

            <div>
              <h2>{{ currentUser.fullName }}</h2>
              <span class="profile-role">
                {{ roleLabels[currentUser.role] }}
              </span>
            </div>
          </div>

          <h3>Основная информация</h3>

          <div class="profile-info-grid">
            <div class="profile-info-field">
              <span>ФИО</span>
              <strong>{{ currentUser.fullName }}</strong>
            </div>

            <div class="profile-info-field">
              <span>Электронная почта</span>
              <strong>{{ currentUser.email }}</strong>
            </div>

            <div class="profile-info-field">
              <span>Роль в системе</span>
              <strong>
                {{ roleLabels[currentUser.role] }}
              </strong>
            </div>

            <div
              v-if="isStudent && studentProfile"
              class="profile-info-field"
            >
              <span>Учебная группа</span>
              <strong>
                {{
                  studentProfile.groupNumber ??
                  'Не указана'
                }}
              </strong>
            </div>
          </div>
        </section>

        <!-- Редактирование только для студента -->
        <section
          v-if="isStudent && studentProfile"
          class="profile-card"
        >
          <h2>Дополнительная информация</h2>

          <p class="profile-hint">
            Укажите сведения о себе и навыки,
            которые могут пригодиться при участии
            в проектах ГПО.
          </p>

          <form
            class="profile-form"
            @submit.prevent="saveProfile"
          >
            <label for="profile-about">
              О себе
            </label>

            <textarea
              id="profile-about"
              v-model="about"
              rows="5"
              maxlength="4000"
              :disabled="saving"
              placeholder="Расскажите о своих интересах, опыте и направлениях развития..."
              @input="clearMessages"
            />

            <p class="profile-help">
              Например: интересуюсь разработкой
              информационных систем, изучаю C# и
              проектирование баз данных.
            </p>

            <label for="profile-competencies">
              Компетенции
            </label>

            <textarea
              id="profile-competencies"
              v-model="competencies"
              rows="4"
              maxlength="4000"
              :disabled="saving"
              placeholder="Например: C#, ASP.NET Core, PostgreSQL, Git"
              @input="clearMessages"
            />

            <p class="profile-help">
              Перечислите технологии и навыки,
              которыми владеете.
            </p>

            <p
              v-if="saveError"
              class="profile-error"
              role="alert"
            >
              {{ saveError }}
            </p>

            <p
              v-if="saveSuccess"
              class="profile-success"
              role="status"
            >
              {{ saveSuccess }}
            </p>

            <div class="profile-form-actions">
              <button
                type="submit"
                class="profile-button profile-button--primary"
                :disabled="saving || !hasChanges"
              >
                {{
                  saving
                    ? 'Сохранение...'
                    : 'Сохранить изменения'
                }}
              </button>
            </div>
          </form>
        </section>

        <section
          v-else-if="!isStudent"
          class="profile-card"
        >
          <h2>Дополнительная информация</h2>

          <p class="profile-hint">
            Для вашей роли редактирование
            дополнительной информации пока
            не предусмотрено.
          </p>
        </section>
      </template>
    </main>
  </AppLayout>
</template>

<style
  scoped
  src="../styles/pages/profile.css"
></style>
