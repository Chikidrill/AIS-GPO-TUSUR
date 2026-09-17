export type ProjectStatus =
  | 'DRAFT'
  | 'OPEN'
  | 'IN_PROGRESS'
  | 'COMPLETED'
  | 'ARCHIVED'

export interface Project {
  id: number
  name: string
  description: string | null
  status: ProjectStatus

  code?: string
  faculty?: string
  department?: string
  supervisorName?: string
  direction?: string
  competencies?: string[]
  occupiedPlaces?: number
  totalPlaces?: number
}

export const mockProjects: Project[] = [
  {
    id: 1383,
    code: 'ПР1383',
    name: 'Разработка интеллектуальной системы мониторинга',
    description: null,
    status: 'OPEN',
    faculty: 'ФВС',
    department: 'Кафедра КСУП',
    supervisorName: 'Иванов И. И.',
    direction: 'Информационные технологии',
    competencies: ['Python', 'ML', 'Git'],
    occupiedPlaces: 6,
    totalPlaces: 8,
  },
  {
    id: 1264,
    code: 'ПР1264',
    name: 'Мобильный сервис для университетской инфраструктуры',
    description: null,
    status: 'OPEN',
    faculty: 'ФСУ',
    department: 'Кафедра АСУ',
    supervisorName: 'Петров П. П.',
    direction: 'Программная инженерия',
    competencies: ['Vue.js', 'REST API', 'Figma'],
    occupiedPlaces: 4,
    totalPlaces: 6,
  },
  {
    id: 1411,
    code: 'ПР1411',
    name: 'Робототехнический комплекс для учебной лаборатории',
    description: null,
    status: 'OPEN',
    faculty: 'ИРЭТ',
    department: 'Кафедра РТС',
    supervisorName: 'Сидоров С. С.',
    direction: 'Робототехника',
    competencies: ['C++', 'CAD', 'Electronics'],
    occupiedPlaces: 7,
    totalPlaces: 10,
  },
  {
    id: 1384,
    code: 'ПР1384',
    name: 'Разработка интеллектуальной системы мониторинга',
    description: null,
    status: 'OPEN',
    faculty: 'ФВС',
    department: 'Кафедра КСУП',
    supervisorName: 'Иванов И. И.',
    direction: 'Информационные технологии',
    competencies: ['Python', 'ML', 'Git'],
    occupiedPlaces: 6,
    totalPlaces: 8,
  },
  {
    id: 1265,
    code: 'ПР1265',
    name: 'Мобильный сервис для университетской инфраструктуры',
    description: null,
    status: 'OPEN',
    faculty: 'ФСУ',
    department: 'Кафедра АСУ',
    supervisorName: 'Петров П. П.',
    direction: 'Программная инженерия',
    competencies: ['Vue.js', 'REST API', 'Figma'],
    occupiedPlaces: 4,
    totalPlaces: 6,
  },
  {
    id: 1412,
    code: 'ПР1412',
    name: 'Робототехнический комплекс для учебной лаборатории',
    description: null,
    status: 'OPEN',
    faculty: 'ИРЭТ',
    department: 'Кафедра РТС',
    supervisorName: 'Сидоров С. С.',
    direction: 'Робототехника',
    competencies: ['C++', 'CAD', 'Electronics'],
    occupiedPlaces: 7,
    totalPlaces: 10,
  },
]