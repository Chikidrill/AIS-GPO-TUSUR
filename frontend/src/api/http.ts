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

http.interceptors.response.use((response) => response, 
        (error)=>{
          if(
            error.response?.status === 401 &&
            !error.config.url?.includes('/auth/login')
          ) {
            localStorage.removeItem('accesToken')
            localStorage.removeItem('role')

            window.location.href='/login'
          }
        },
      )