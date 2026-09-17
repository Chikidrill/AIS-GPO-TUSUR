import { createApp } from 'vue'

import App from './App.vue'
import router from './router'

import '@fontsource/montserrat/400.css'
import '@fontsource/montserrat/500.css'
import '@fontsource/montserrat/600.css'
import '@fontsource/montserrat/700.css'

import './styles/tokens.css'
import './styles/base.css'

createApp(App)
  .use(router)
  .mount('#app')