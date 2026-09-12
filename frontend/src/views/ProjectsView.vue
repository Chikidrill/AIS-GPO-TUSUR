<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { http } from '../api/http'

interface Project {
  id: number
  name: string
  description: string | null
  status: ProjectStatus
}

type ProjectStatus = 'DRAFT' | 'OPEN' | 'IN_PROGRESS' | 'COMPLETED' | 'ARCHIVED'

const router = useRouter()

const projects = ref<Project[]>([])
const loading = ref(true)
const error = ref('')

async function loadProjects() {
  try {
    const { data } = await http.get<Project[]>('/projects')
    projects.value = data
  } catch {
    error.value = 'Не удалось загрузить проекты.'
  } finally {
    loading.value = false
  }
}

function logout() {
  localStorage.removeItem('accessToken')
  localStorage.removeItem('role')

  router.push('/login')
}

onMounted(loadProjects)
</script>

<template>
  <main>
    <header>
      <h1>Проекты ГПО</h1>

      <button @click="logout">
        Выйти
      </button>
    </header>

    <p v-if="loading">
      Загрузка...
    </p>

    <p v-else-if="error">
      {{ error }}
    </p>

    <section v-else>
      <article
        v-for="project in projects"
        :key="project.id"
      >
        <h2>{{ project.name }}</h2>

        <p>
          {{ project.description || 'Описание отсутствует' }}
        </p>

        <p>
          Статус: {{ project.status }}
        </p>
        <RouterLink
            :to="`/projects/${project.id}`"
        >
            Подробнее
        </RouterLink>
      </article>

      <p v-if="projects.length === 0">
        Проекты пока отсутствуют.
      </p>
    </section>
  </main>
</template>