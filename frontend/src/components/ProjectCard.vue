<script setup lang="ts">
import type { Project } from '../mocks/projects'

defineProps<{
  project: Project
}>()
</script>

<template>
  <article class="project-card">
    <div class="project-card__top">
      <span class="project-card__code">
        {{ project.code || `ПР${project.id}` }}
      </span>

      <span
        v-if="
          project.occupiedPlaces !== undefined &&
          project.totalPlaces !== undefined
        "
        class="project-card__places"
      >
        {{ project.occupiedPlaces }} из {{ project.totalPlaces }} мест
      </span>
    </div>

    <h2 class="project-card__title">
      {{ project.name }}
    </h2>

    <div class="project-card__info">
      <p
        v-if="project.department"
        class="project-card__department"
      >
        {{ project.department }}
      </p>

      <p v-if="project.supervisorName">
        Руководитель: {{ project.supervisorName }}
      </p>

      <p v-if="project.direction">
        {{ project.direction }}
      </p>

      <p
        v-if="
          !project.department &&
          !project.supervisorName &&
          !project.direction &&
          project.description
        "
        class="project-card__description"
      >
        {{ project.description }}
      </p>
    </div>

    <div
      v-if="project.competencies?.length"
      class="project-card__competencies"
    >
      <span
        v-for="competency in project.competencies"
        :key="competency"
        class="project-card__competency"
      >
        {{ competency }}
      </span>
    </div>

    <div class="project-card__spacer" />

    <div class="project-card__actions">
      <RouterLink
        class="project-card__button project-card__button--secondary"
        :to="`/projects/${project.id}`"
      >
        Подробнее
      </RouterLink>

      <button
        v-if="project.status === 'OPEN'"
        class="project-card__button project-card__button--primary"
        type="button"
        aria-disabled="true"
      >
        Подать заявку
      </button>
      <p v-if="project.status === 'COMPLETED'"
      class="project-card__closed">
        Приём заявок на этот проект закрыт.
      </p>
    </div>
  </article>
</template>

<style
  scoped
  src="../styles/components/project-card.css"
></style>