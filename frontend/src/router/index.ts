import { createRouter, createWebHistory } from 'vue-router'

import LoginView from '../views/LoginView.vue'
import ProjectsView from '../views/ProjectsView.vue'
import ProjectView from '../views/ProjectView.vue'

const router = createRouter({
  history: createWebHistory(),

  routes: [
    {
      path: '/',
      redirect: '/projects',
    },
    {
      path: '/login',
      name: 'login',
      component: LoginView,
    },
    {
      path: '/projects',
      name: 'projects',
      component: ProjectsView,
      meta: {
        requiresAuth: true,
      },
    },
    {
      path: '/projects/:id',
      name: 'project',
      component: ProjectView,
      meta: {
        requiresAuth: true,
      },
    },
  ],
})

router.beforeEach((to) => {
  const token = localStorage.getItem('accessToken')

  if (to.meta.requiresAuth && !token) {
    return '/login'
  }
})

export default router