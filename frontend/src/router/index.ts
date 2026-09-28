import { createRouter, createWebHistory } from 'vue-router'

import LoginView from '../views/LoginView.vue'
import ProjectsView from '../views/ProjectsView.vue'
import ProjectView from '../views/ProjectView.vue'
import MyApplicationsView from '../views/MyApplicationsView.vue'
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
    {
      path: '/applications',
      name: 'applications',
      component: MyApplicationsView,
      meta: {
        requiresAuth: true,
        roles: ['STUDENT'],
      },
    },
  ],
})

router.beforeEach((to) => {
  const token = localStorage.getItem('accessToken')
  const role = localStorage.getItem('role')

  if (to.meta.requiresAuth && !token) {
    return '/login'
  }

  if (
    to.meta.roles &&
    (
      !role ||
      !to.meta.roles.includes(
        role as 'ADMIN' | 'TEACHER' | 'STUDENT'
      )
    )
  ) {
    return '/projects'
  }
})

export default router