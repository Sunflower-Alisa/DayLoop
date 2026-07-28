const WEEKDAYS = ['周日', '周一', '周二', '周三', '周四', '周五', '周六']

export function formatDate(dateStr: string): string {
  const d = new Date(dateStr + 'T00:00:00')
  const wd = WEEKDAYS[d.getDay()]
  return `${dateStr} (${wd})`
}
