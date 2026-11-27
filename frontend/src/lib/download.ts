/** Saves text as a file in the browser's downloads. */
export function saveText(fileName: string, text: string, type = 'text/plain') {
  const link = document.createElement('a')
  link.href = URL.createObjectURL(new Blob([text], { type }))
  link.download = fileName
  link.click()
  URL.revokeObjectURL(link.href)
}
