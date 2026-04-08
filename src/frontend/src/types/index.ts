// API 类型定义

export interface PhishingReport {
  id: string
  messageId?: string
  subject?: string
  senderEmail: string
  senderName?: string
  status: ReportStatus
  riskScore: number
  category?: string
  reportedAt: string
  reportedBy?: string
  userNotes?: string
  attachments?: Attachment[]
  indicators?: AnalysisIndicator[]
  headers?: EmailHeader[]
  rawEmlBase64?: string
  hasRawEmail?: boolean
}

export interface EmailHeader {
  name?: string
  value?: string
}

export interface Attachment {
  fileName?: string
  mimeType?: string
  size: number
  sha256Hash?: string
  isMalicious: boolean
}

export interface AnalysisIndicator {
  type: string
  description: string
  severity: number
}

export type ReportStatus =
  | 'Pending'
  | 'Analyzing'
  | 'Confirmed'
  | 'FalsePositive'
  | 'Resolved'
  | 'AnalysisFailed'

export interface ReportFilter {
  page: number
  pageSize: number
  status?: ReportStatus
  reportedBy?: string
  senderEmail?: string
  fromDate?: string
  toDate?: string
}

export interface PagedResponse<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface Statistics {
  totalReports: number
  pendingReports: number
  confirmedPhishing: number
  falsePositives: number
  reportsByStatus: Record<string, number>
  reportsByCategory: Record<string, number>
  recentTrend: DailyReportCount[]
}

export interface DailyReportCount {
  date: string
  count: number
}

export interface UpdateStatusRequest {
  status: ReportStatus
  notes?: string
}

// UI 状态类型
export const STATUS_LABELS: Record<ReportStatus, string> = {
  Pending: '待处理',
  Analyzing: '分析中',
  Confirmed: '已确认钓鱼',
  FalsePositive: '误报',
  Resolved: '已解决',
  AnalysisFailed: '分析失败'
}

export const STATUS_COLORS: Record<ReportStatus, string> = {
  Pending: '#f0ad4e',
  Analyzing: '#5bc0de',
  Confirmed: '#d9534f',
  FalsePositive: '#5cb85c',
  Resolved: '#337ab7',
  AnalysisFailed: '#777'
}

export const RISK_LEVELS = {
  high: { min: 70, label: '高风险', color: '#d32f2f' },
  medium: { min: 40, label: '中风险', color: '#f57c00' },
  low: { min: 20, label: '低风险', color: '#388e3c' },
  minimal: { min: 0, label: '可忽略', color: '#9e9e9e' }
}

export function getRiskLevel(score: number): { label: string; color: string } {
  if (score >= 70) return RISK_LEVELS.high
  if (score >= 40) return RISK_LEVELS.medium
  if (score >= 20) return RISK_LEVELS.low
  return RISK_LEVELS.minimal
}