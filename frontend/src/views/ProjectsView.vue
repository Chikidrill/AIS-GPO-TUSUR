<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'

import { http } from '../api/http'
import AppLayout from '../components/AppLayout.vue'
import ProjectCard from '../components/ProjectCard.vue'

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
  name: string
  description: string | null
  status: ProjectStatus

  code?: string
  faculty?: string
  department?: string
  supervisorName?: string
  direction?: string
  competencies?: string[]
  occupiedPlaces?: number
  totalPlaces?: number
}

const router = useRouter()

const currentUser = ref<CurrentUser | null>(null)
const projects = ref<Project[]>([])

const search = ref('')
const loading = ref(true)
const error = ref('')

/*
 * Временные данные только для проверки верстки карточек.
 * Когда backend начнет отдавать нужные поля,
 * заменим их на GET /projects.
 */
const mockProjects: Project[] = [
  {
    id: 1383,
    code: 'ПР1383',
    name: 'Разработка интеллектуальной системы мониторинга',
    description: null,
    status: 'OPEN',
    faculty: 'ФВС',
    department: 'Кафедра КСУП',
    supervisorName: 'Иванов И. И.',
    direction: 'Информационные технологии',
    competencies: ['Python', 'ML', 'Git'],
    occupiedPlaces: 6,
    totalPlaces: 8,
  },
  {
    id: 1264,
    code: 'ПР1264',
    name: 'Мобильный сервис для университетской инфраструктуры',
    description: null,
    status: 'OPEN',
    faculty: 'ФСУ',
    department: 'Кафедра АСУ',
    supervisorName: 'Петров П. П.',
    direction: 'Программная инженерия',
    competencies: ['Vue.js', 'REST API', 'Figma'],
    occupiedPlaces: 4,
    totalPlaces: 6,
  },
  {
    id: 1411,
    code: 'ПР1411',
    name: 'Робототехнический комплекс для учебной лаборатории',
    description: null,
    status: 'OPEN',
    faculty: 'ИРЭТ',
    department: 'Кафедра РТС',
    supervisorName: 'Сидоров С. С.',
    direction: 'Робототехника',
    competencies: ['C++', 'CAD', 'Electronics'],
    occupiedPlaces: 7,
    totalPlaces: 10,
  },
  {
    id: 1384,
    code: 'ПР1384',
    name: 'Разработка интеллектуальной системы мониторинга',
    description: null,
    status: 'OPEN',
    faculty: 'ФВС',
    department: 'Кафедра КСУП',
    supervisorName: 'Иванов И. И.',
    direction: 'Информационные технологии',
    competencies: ['Python', 'ML', 'Git'],
    occupiedPlaces: 6,
    totalPlaces: 8,
  },
  {
    id: 1265,
    code: 'ПР1265',
    name: 'Мобильный сервис для университетской инфраструктуры',
    description: null,
    status: 'OPEN',
    faculty: 'ФСУ',
    department: 'Кафедра АСУ',
    supervisorName: 'Петров П. П.',
    direction: 'Программная инженерия',
    competencies: ['Vue.js', 'REST API', 'Figma'],
    occupiedPlaces: 4,
    totalPlaces: 6,
  },
  {
    id: 1412,
    code: 'ПР1412',
    name: 'Робототехнический комплекс для учебной лаборатории',
    description: null,
    status: 'OPEN',
    faculty: 'ИРЭТ',
    department: 'Кафедра РТС',
    supervisorName: 'Сидоров С. С.',
    direction: 'Робототехника',
    competencies: ['C++', 'CAD', 'Electronics'],
    occupiedPlaces: 7,
    totalPlaces: 10,
  },
]

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