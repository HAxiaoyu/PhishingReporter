import axios, { AxiosInstance, AxiosError } from 'axios'
import type {
  PhishingReport,
  ReportFilter,
  PagedResponse,
  Statistics,
  UpdateStatusRequest,
  ReportStatus
} from '@/types'

const API_BASE_URL = '/api/v1'

class ApiService {
  private client: AxiosInstance

  constructor() {
    this.client = axios.create({
      baseURL: API_BASE_URL,
      timeout: 30000,
      headers: {
        'Content-Type': 'application/json'
      }
    })

    // 请求拦截器 - 添加 API Key
    this.client.interceptors.request.use(
      (config) => {
        const apiKey = localStorage.getItem('apiKey')
        if (apiKey) {
          config.headers['X-API-Key'] = apiKey
        }
        return config
      },
      (error) => Promise.reject(error)
    )

    // 响应拦截器 - 统一错误处理
    this.client.interceptors.response.use(
      (response) => response,
      (error: AxiosError<{ error?: string; code?: string }>) => {
        if (error.response?.status === 401) {
          // 清除无效的 API Key
          localStorage.removeItem('apiKey')
          window.location.href = '/login'
        }
        return Promise.reject(error)
      }
    )
  }

  // 健康检查
  async healthCheck(): Promise<{ status: string; version: string }> {
    const response = await this.client.get('/health')
    return response.data
  }

  // 获取上报列表
  async getReports(filter: ReportFilter): Promise<PagedResponse<PhishingReport>> {
    const params = new URLSearchParams()
    params.append('page', filter.page.toString())
    params.append('pageSize', filter.pageSize.toString())
    if (filter.status) params.append('status', filter.status)
    if (filter.reportedBy) params.append('reportedBy', filter.reportedBy)
    if (filter.senderEmail) params.append('senderEmail', filter.senderEmail)
    if (filter.fromDate) params.append('fromDate', filter.fromDate)
    if (filter.toDate) params.append('toDate', filter.toDate)

    const response = await this.client.get(`/reports?${params.toString()}`)
    return response.data
  }

  // 获取上报详情
  async getReport(id: string): Promise<PhishingReport> {
    const response = await this.client.get(`/reports/${id}`)
    return response.data
  }

  // 更新上报状态
  async updateReportStatus(
    id: string,
    request: UpdateStatusRequest
  ): Promise<void> {
    await this.client.patch(`/reports/${id}/status`, request)
  }

  // 获取统计信息
  async getStatistics(): Promise<Statistics> {
    const response = await this.client.get('/reports/statistics')
    return response.data
  }

  // 设置 API Key
  setApiKey(apiKey: string): void {
    localStorage.setItem('apiKey', apiKey)
  }

  // 获取当前 API Key
  getApiKey(): string | null {
    return localStorage.getItem('apiKey')
  }

  // 验证 API Key
  async validateApiKey(apiKey: string): Promise<boolean> {
    try {
      const response = await this.client.get('/reports', {
        headers: { 'X-API-Key': apiKey },
        params: { page: 1, pageSize: 1 }
      })
      return response.status === 200
    } catch {
      return false
    }
  }
}

export const api = new ApiService()
export default api