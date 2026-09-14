<template>
  <nav class="app-navigation">
    <div class="app-navigation__inner">
      <RouterLink
        class="app-navigation__item"
        to="/projects"
      >
        Каталог проектов
      </RouterLink>

      <button
        class="app-navigation__item"
        type="button"
        aria-disabled="true"
        >
        Мои заявки
        </button>
    </div>
  </nav>
</template>

<style
  scoped
  src="../styles/components/app-navigation.css"
></style>