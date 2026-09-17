import axios from 'axios'

export const http = axios.create({
  baseURL: '/api/v1',
})

http.interceptors.request.use((config) => {
  const token = localStorage.getItem('accessToken')

  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }

  return config
})