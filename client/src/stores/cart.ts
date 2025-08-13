// /src/stores/cart.ts
import { defineStore } from 'pinia'

// ---- Types ----
export type VariantKey = string; // e.g. "color=black|size=M"
export type CartItemInput = {
  productId: string
  variant?: VariantKey
  qty: number
  custom?: Record<string, any> // engravings, addons, etc.
}

export type CartItem = CartItemInput & {
  id: string // server line id or deterministic hash
  name: string
  imageUrl?: string
  unitPrice: number // authoritative from server
  lineTotal: number // qty * unitPrice after promos
  maxQty?: number // optional stock clamp from server
}

export type CartTotals = {
  subtotal: number
  discounts?: number
  tax?: number
  shipping?: number
  grandTotal: number
  currency: string
}

type PersistedCart = {
  v: 1
  cartId: string | null
  items: CartItemInput[] // client-side lightweight copy
  updatedAt: number
}

const LS_KEY = 'cart.v1'

// ---- Helpers ----
function keyOf(i: CartItemInput): string {
  const custom = i.custom ? JSON.stringify(i.custom) : ''
  return `${i.productId}::${i.variant ?? ''}::${custom}`
}

function now() { return Date.now() }

// ---- Store ----
export const useCartStore = defineStore('cart', {
  state: () => ({
    cartId: null as string | null,
    items: [] as CartItem[],      // server-normalized
    totals: { subtotal: 0, grandTotal: 0, currency: 'USD' } as CartTotals,
    updating: false,
    initialized: false,
    pendingLocal: new Map<string, CartItemInput>(), // local-only ops before server round-trip
    saveTimer: null as number | null,
  }),

  getters: {
    count(state): number {
      return state.items.reduce((n, i) => n + i.qty, 0)
    },
    isEmpty(): boolean {
      return this.items.length === 0 && this.pendingLocal.size === 0
    },
  },

  actions: {
    // ---- Init & Persistence ----
    async init(authHeader?: Record<string, string>) {
      // 1) load local snapshot (guest)
      const raw = localStorage.getItem(LS_KEY)
      if (raw) {
        try {
          const parsed = JSON.parse(raw) as PersistedCart
          if (parsed?.v === 1) {
            this.cartId = parsed.cartId
            // stage local inputs to merge on first sync
            for (const ii of parsed.items) {
              const k = keyOf(ii)
              this.pendingLocal.set(k, ii)
            }
          } else {
            localStorage.removeItem(LS_KEY)
          }
        } catch { localStorage.removeItem(LS_KEY) }
      }

      // 2) ask server to hydrate/rotate cart (works for guest or logged-in)
      await this.syncInit(authHeader)

      this.initialized = true
    },

    persistLocal() {
      // keep only light inputs; server truth stays in memory
      const items = Array.from(this.pendingLocal.values())
      const payload: PersistedCart = {
        v: 1,
        cartId: this.cartId,
        items,
        updatedAt: now(),
      }
      localStorage.setItem(LS_KEY, JSON.stringify(payload))
    },

    schedulePersist() {
      if (this.saveTimer) clearTimeout(this.saveTimer)
      this.saveTimer = window.setTimeout(() => this.persistLocal(), 300)
    },

    // ---- Local API (fast UI) ----
    add(item: CartItemInput) {
      // optimistic: merge into pendingLocal; UI updates immediately
      const k = keyOf(item)
      const existing = this.pendingLocal.get(k) ?? { ...item, qty: 0 }
      this.pendingLocal.set(k, { ...existing, qty: existing.qty + item.qty })
      this.schedulePersist()
      // fire-and-forget server add
      this.flushToServer().catch(() => {})
    },

    setQty(item: CartItemInput, qty: number) {
      const k = keyOf(item)
      if (qty <= 0) {
        this.pendingLocal.delete(k)
      } else {
        this.pendingLocal.set(k, { ...item, qty })
      }
      this.schedulePersist()
      this.flushToServer().catch(() => {})
    },

    remove(item: CartItemInput) {
      this.setQty(item, 0)
    },

    clearLocalOnly() {
      this.pendingLocal.clear()
      this.schedulePersist()
    },

    // ---- Server Syncs ----
    async syncInit(authHeader?: Record<string, string>) {
      // Send cartId (if any) and pending items so server can normalize & return totals
      const body = {
        cartId: this.cartId,
        items: Array.from(this.pendingLocal.values()),
      }
      const res = await fetch('/api/cart/init', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...(authHeader ?? {}) },
        credentials: 'include',
        body: JSON.stringify(body),
      })
      if (!res.ok) return
      const data = await res.json() as { cartId: string, items: CartItem[], totals: CartTotals }
      this.applyServerCart(data)
      // clear local pending since server now owns truth
      this.pendingLocal.clear()
      this.persistLocal()
    },

    async flushToServer() {
      if (this.updating) return
      this.updating = true
      try {
        const body = {
          cartId: this.cartId,
          items: Array.from(this.pendingLocal.values()),
        }
        const res = await fetch('/api/cart/merge', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          credentials: 'include',
          body: JSON.stringify(body),
        })
        if (res.ok) {
          const data = await res.json() as { cartId: string, items: CartItem[], totals: CartTotals }
          this.applyServerCart(data)
          this.pendingLocal.clear()
          this.persistLocal()
        }
      } finally {
        this.updating = false
      }
    },

    async clearServer() {
      await fetch('/api/cart/clear', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ cartId: this.cartId }),
      }).catch(() => {})
      this.cartId = null
      this.items = []
      this.totals = { subtotal: 0, grandTotal: 0, currency: this.totals.currency }
      this.clearLocalOnly()
    },

    applyServerCart(payload: { cartId: string, items: CartItem[], totals: CartTotals }) {
      this.cartId = payload.cartId
      this.items = payload.items
      this.totals = payload.totals
    },

    // ---- Auth integration hooks ----
    // call this right after successful login, passing auth header
    async onLogin(authHeader: Record<string, string>) {
      // Merge whatever local guest cart exists into the user's server cart
      await this.syncInit(authHeader) // server will attach cart to user & merge
    },

    // call on logout
    async onLogout() {
      // Keep guest cart locally (optional) or wipe:
      // Option A (keep): leave LS & pendingLocal as-is; nuke server id
      this.cartId = null
      // Option B (wipe): this.clearServer() then clearLocalOnly()
    },
  },
})
