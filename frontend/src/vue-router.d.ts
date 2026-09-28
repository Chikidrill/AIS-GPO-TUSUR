import 'vue-router'

type UserRole = "ADMIN" | 'TEACHER' | 'STUDENT'

declare module 'vue-router'{
    interface RouteMeta {
        requiresAuth?: boolean,
        roles?: UserRole[]
    }
}

export{}