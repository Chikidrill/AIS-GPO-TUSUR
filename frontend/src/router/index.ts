import { createRouter, createWebHistory } from 'vue-router'

import LoginView from '../views/LoginView.vue'
import ProjectsView from '../views/ProjectsView.vue'
import ProjectView from '../views/ProjectView.vue'
import MyApplicationsView from '../views/MyApplicationsView.vue'
import AdminProjectsView from '../views/AdminProjectsView.vue'
import AdminProjectCreateView from '../views/AdminProjectCreateView.vue'
import AdminApplicationsView from '../views/AdminApplicationsView.vue'
import AdminParticipantsView from '../views/AdminParticipantsView.vue'
import TeachersProjectsView from '../views/TeachersProjectsView.vue'
import TeachersParticipantsView from '../views/TeachersParticipantsView.vue'
import TeacherWorksView from '../views/TeacherWorksView.vue'
import TeacherWorkDetailsView from '../views/TeacherWorkDetailsView.vue'
import StudentWorkDetailsView from '../views/StudentWorkDetailsView.vue'
import StudentWorksView from '../views/StudentWorksView.vue'
const router = createRouter({
  history: createWebHistory(),

  routes: [
    {
      path: '/',
      redirect: '/login',
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
    {
      path: '/admin/projects',
      name: 'admin-projects',
      component: AdminProjectsView,
      meta: {
        requiresAuth: true,
        roles: ['ADMIN'],
      },
    },
    {
      path: '/admin/projects/create',
      name: 'admin-project-create',
      component: AdminProjectCreateView,
      meta:{
        requiresAuth: true,
        roles: ['ADMIN']
      }
    },
    {
      path: '/admin/projects/:id/edit',
      name: 'admin-project-edit',
      component: AdminProjectCreateView,
      meta: {
        requiresAuth: true,
        roles: ['ADMIN'],
      },
    },
    {
      path: '/admin/applications',
      name: 'admin-applications',
      component: AdminApplicationsView,
      meta:{
        requiresAuth: true,
        roles: ['ADMIN']
      }
    },
    {
      path: '/admin/participants',
      name: 'admin-participants',
      component: AdminParticipantsView,
      meta: {
        requiresAuth: true,
        roles: ['ADMIN'],
      },
    },
    {
      path: '/myprojects',
      name: 'teacher-projects',
      component: TeachersProjectsView,
      meta: {
        requiresAuth: true,
        roles: ['TEACHER'],
      }
    },
    {
      path: '/projects_participants',
      name: 'teacher-participants',
      component: TeachersParticipantsView,
      meta: {
        requiresAuth: true,
        roles: ['TEACHER'],
      },
    },
    {
      path: '/myprojects/:id/participants',
      name: 'teacher-project-participants',
      component: TeachersParticipantsView,
      meta: {
        requiresAuth: true,
        roles: ['TEACHER'],
      },
    },
    {
      path: '/projects_works',
      name: 'teacher-works',
      component: TeacherWorksView,
      meta: {
        requiresAuth: true,
        roles: ['TEACHER'],
      },
    },
    {
      path: '/myprojects/:id/works',
      name: 'teacher-project-works',
      component: TeacherWorksView,
      meta: {
        requiresAuth: true,
        roles: ['TEACHER'],
      },
    },
    {
      path: '/myprojects/:id/works/:workId',
      name: 'teacher-work-details',
      component: TeacherWorkDetailsView,
      meta:{
        requiresAuth: true,
        roles: ['TEACHER']
      },
    },
    {
      path: '/works',
      name: 'student-works',
      component: StudentWorksView,
      meta: {
        requiresAuth: true,
        roles: ['STUDENT'],
      },
    },
    {
      path: '/works/:workId',
      name: 'student-work-details',
      component: StudentWorkDetailsView,
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