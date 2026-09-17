<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'

import { http } from '../api/http'
import AppLayout from '../components/AppLayout.vue'
import ProjectCard from '../components/ProjectCard.vue'

import {
  mockProjects,
  type Project,
} from '../mocks/projects'

type UserRole = 'ADMIN' | 'TEACHER' | 'STUDENT'

interface CurrentUser {
  id: number
  email: string
  fullName: string
  role: UserRole
}

const router = useRouter()

const currentUser = ref<CurrentUser | null>(null)
const projects = ref<Project[]>([])

const search = ref('')
const loading = ref(true)
const error = ref('')

const selectedFaculty = ref('')
const selectedDepartment = ref('')
const selectedCompetency = ref('')

const faculties = computed(() => {
  return [...new Set(
    projects.value
      .map(project => project.faculty)
      .filter((faculty): faculty is string => Boolean(faculty))
  )]
  
})

const departments = computed(() => {
  const source = selectedFaculty.value
    ? projects.value.filter(
        project => project.faculty === selectedFaculty.value
      )
    : projects.value

  return [...new Set(
    source
      .map(project => project.department)
      .filter((department): department is string => Boolean(department))
  )]
  
})

const competencies = computed(() => {
  return [...new Set(
    projects.value.flatMap(
      project => project.competencies ?? []
    )
  )]
})

const filteredProjects = computed(() => {
  const query = search.value.trim().toLowerCase()
  
  return projects.value.filter((project) => {
    const matchesSearch =
    !query ||
    project.name.toLowerCase().includes(query) ||
    project.supervisorName?.toLowerCase().includes(query)

    const matchesFaculty =
    !selectedFaculty.value ||
    project.faculty === selectedFaculty.value

    const matchesDepartment =
    !selectedDepartment.value ||
    project.department === selectedDepartment.value

    const matchesCompetency =
    !selectedCompetency.value ||
    (project.competencies ?? []).includes(selectedCompetency.value)
        return (
    matchesSearch &&
    matchesFaculty &&
    matchesDepartment &&
    matchesCompetency
    )
  })
})

async function loadPage() {
  loading.value = true
  error.value = ''

  try {
    const { data } = await http.get<CurrentUser>('/me')

    currentUser.value = data

    // Временно используем mockProjects для проверки дизайна.
    projects.value = mockProjects
  } catch {
    error.value = 'Не удалось загрузить каталог проектов.'
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
    <div class="projects-page">
      <main class="projects-content">
        <header class="projects-content__header">
          <h1>Каталог проектов ГПО</h1>

          <p>
            Выберите проект, изучите требования и подайте заявку онлайн.
          </p>
        </header>

        <div class="projects-toolbar">
          <input
            v-model="search"
            class="projects-search"
            type="search"
            placeholder="Поиск по названию проекта или руководителю"
          >

          <select v-model="selectedFaculty" class="projects-filter">
            <option value="">Факультет/подразделение: все</option>
            <option
                v-for="faculty in faculties"
                :key="faculty"
                :value="faculty"
                >
                {{ faculty }}
            </option>
            
          </select>

          <select v-model="selectedDepartment" class="projects-filter projects-filter--department">
            <option value=""> Кафедра: все </option>
            <option
                v-for="department in departments"
                :key="department"
                :value="department"
                >
                {{ department }}
            </option>
          </select>


          <select v-model="selectedCompetency" class="projects-filter projects-filter--skills">
            <option value=""> Компетенции: все </option>
            <option
                v-for="competency in competencies"
                :key="competency"
                :value="competency"
                >
                {{ competency }}
            </option>
          </select> 
        </div>

        <div
          v-if="loading"
          class="projects-state"
        >
          Загрузка проектов...
        </div>

        <div
          v-else-if="error"
          class="projects-state projects-state--error"
        >
          <p>{{ error }}</p>

          <button
            type="button"
            @click="loadPage"
          >
            Попробовать снова
          </button>
        </div>

        <template v-else>
          <p class="projects-count">
            Найдено {{ filteredProjects.length }} проектов
          </p>

          <div
            v-if="filteredProjects.length"
            class="projects-grid"
          >
            <ProjectCard
              v-for="project in filteredProjects"
              :key="project.id"
              :project="project"
            />
          </div>

          <div
            v-else
            class="projects-state"
          >
            Проекты не найдены.
          </div>
        </template>
      </main>
    </div>
  </AppLayout>
</template>

<style
  scoped
  src="../styles/pages/projects.css"
></style>