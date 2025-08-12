import { defineStore } from 'pinia'

type InitResponse = {
  accessToken: string
  showAdminPanel: boolean
  user?: { id: string; email: string; roles?: string[] }
}

const STORAGE_KEY = 'auth'

function decodeExp(token: string): number | null {
  try {
    const payload = JSON.parse(
      decodeURIComponent(escape(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'))))
    )
    return typeof payload.exp === 'number' ? payload.exp : null
  } catch { return null }
}

export const useAuthStore = defineStore('auth', {
  state: () => ({
    accessToken: null as string | null,
    user: null as InitResponse['user'] | null,
    showAdminPanel: false,
    initializing: false,
    refreshTimer: null as number | null,
  }),
  getters: {
    isAuthenticated(state): boolean {
      if (!state.accessToken) return false
      const exp = decodeExp(state.accessToken)
      if (!exp) return false
      const now = Math.floor(Date.now() / 1000)
      return exp > now + 5
    },
    authHeader(state): Record<string, string> {
      return state.accessToken ? { Authorization: `Bearer ${state.accessToken}` } : {}
    },
  },
  actions: {
    async init() {
      this.initializing = true
      const raw = localStorage.getItem(STORAGE_KEY)
      if (raw) {
        try {
          const { accessToken, exp } = JSON.parse(raw) as { accessToken: string; exp: number }
          const now = Math.floor(Date.now() / 1000)
          if (accessToken && exp && exp > now + 5) this.accessToken = accessToken
          else localStorage.removeItem(STORAGE_KEY)
        } catch { localStorage.removeItem(STORAGE_KEY) }
      }

      try {
        const res = await fetch('/api/auth/init', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', ...this.authHeader },
          credentials: 'include', // keep if you also use a refresh cookie
          body: JSON.stringify({}), // or any client hints you send
        })
        if (res.ok) {
          const data = (await res.json()) as InitResponse
          this.applyAuthPayload(data)
        } else {
          this.clearAuth()
        }
      } catch {
        this.showAdminPanel = false
      }

      this.initializing = false
    },

    async login(email: string, password: string) {
      const res = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ email, password }),
      })
      if (!res.ok) throw new Error('Invalid credentials')
      const data = (await res.json()) as InitResponse
      this.applyAuthPayload(data)
    },

    async logout() {
      try { await fetch('/api/auth/logout', { method: 'POST', credentials: 'include' }) } catch {}
      this.clearAuth()
    },

    applyAuthPayload(payload: InitResponse) {
      this.accessToken = payload.accessToken
      const exp = decodeExp(payload.accessToken)
      if (exp) localStorage.setItem(STORAGE_KEY, JSON.stringify({ accessToken: payload.accessToken, exp }))
      else localStorage.removeItem(STORAGE_KEY)

      this.showAdminPanel = !!payload.showAdminPanel
      this.user = payload.user ?? null

      this.scheduleRefresh(exp ?? null)
    },

    scheduleRefresh(exp: number | null) {
      if (!exp) return
      const whenMs = Math.max(exp * 1000 - 60_000 - Date.now(), 5_000)
      if (this.refreshTimer) clearTimeout(this.refreshTimer)
      this.refreshTimer = window.setTimeout(() => this.tryRefresh(), whenMs)
    },

    async tryRefresh() {
      const res = await fetch('/api/auth/refresh', { method: 'POST', credentials: 'include' })
      if (!res.ok) { this.clearAuth(); return false }
      const data = (await res.json()) as { accessToken: string }
      this.applyAuthPayload({
        accessToken: data.accessToken,
        showAdminPanel: this.showAdminPanel, // keep until next init/login
        user: this.user ?? undefined,
      })
      return true
    },

    clearAuth() {
      this.accessToken = null
      this.user = null
      this.showAdminPanel = false
      localStorage.removeItem(STORAGE_KEY)
      if (this.refreshTimer) { clearTimeout(this.refreshTimer); this.refreshTimer = null }
    },
  },
})