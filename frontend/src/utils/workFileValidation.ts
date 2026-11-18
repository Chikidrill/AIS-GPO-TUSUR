
export const WORK_FILE_LIMITS = {
  maxFiles: 5,
  maxFileSize: 20 * 1024 * 1024,
  maxTotalSize: 50 * 1024 * 1024,
} as const

export const WORK_FILE_ACCEPT =
  '.pdf,.doc,.docx,.txt,.png,.jpg,.jpeg,.zip'

const allowedExtensions = new Set(
  WORK_FILE_ACCEPT.split(','),
)

export function validateWorkFiles(
  files: readonly File[],
): string | null {
  if (files.length > WORK_FILE_LIMITS.maxFiles) {
    return `Можно прикрепить не более ${WORK_FILE_LIMITS.maxFiles} файлов.`
  }

  let totalSize = 0

  for (const file of files) {
    const dotIndex = file.name.lastIndexOf('.')
    const extension =
      dotIndex > 0
        ? file.name.slice(dotIndex).toLowerCase()
        : ''

    if (!allowedExtensions.has(extension)) {
      return `Недопустимый формат файла «${file.name}». Разрешены PDF, DOC, DOCX, TXT, PNG, JPG, JPEG и ZIP.`
    }

    if (file.size === 0) {
      return `Файл «${file.name}» пустой.`
    }

    if (file.size > WORK_FILE_LIMITS.maxFileSize) {
      return `Файл «${file.name}» превышает 20 МБ.`
    }

    totalSize += file.size
  }

  if (totalSize > WORK_FILE_LIMITS.maxTotalSize) {
    return 'Суммарный размер файлов превышает 50 МБ.'
  }

  return null
}
