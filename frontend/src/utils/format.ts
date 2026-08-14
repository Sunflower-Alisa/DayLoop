const WEEKDAYS = ['周日', '周一', '周二', '周三', '周四', '周五', '周六']

export function formatDate(dateStr: string): string {
  const d = new Date(dateStr + 'T00:00:00')
  const wd = WEEKDAYS[d.getDay()]
  return `${dateStr} (${wd})`
}

/** XSS-safe content rendering: escapes HTML, then unescapes markdown images */
export function renderContent(text: string): string {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/!\[([^\]]*)\]\(([^)]+)\)/g, '<img src="$2" alt="$1" style="max-width:100%;border-radius:8px;margin:8px 0">')
    .replace(/\n/g, '<br>')
}

export function renderMarkdown(text: string): string {
  let html = text
    .replace(/^## (.+)$/gm, '<h2>$1</h2>')
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/^- (.+)$/gm, '<li>$1</li>')

  const lines = html.split('\n')
  const result: string[] = []
  let inList = false

  for (const line of lines) {
    if (line.startsWith('<li>')) {
      if (!inList) { result.push('<ul>'); inList = true }
      result.push(line)
    } else {
      if (inList) { result.push('</ul>'); inList = false }
      if (line.trim()) {
        if (!line.startsWith('<h')) result.push(`<p>${line}</p>`)
        else result.push(line)
      }
    }
  }
  if (inList) result.push('</ul>')

  return result.join('\n')
}
