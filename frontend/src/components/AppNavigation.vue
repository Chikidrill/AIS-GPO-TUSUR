

<template>
  <nav class="app-navigation">
    <div class="app-navigation__inner">
      <RouterLink
        class="app-navigation__item"
        to="/projects"
      >
        Каталог проектов
      </RouterLink>

      <RouterLink
        class="app-navigation__item"
        to="/applications"
      >
        Мои заявки
      </RouterLink>
    </div>
  </nav>
</template>

<style
  scoped
  src="../styles/components/app-navigation.css"
></style>