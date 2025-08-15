// /src/stores/cart.ts
import { defineStore } from 'pinia'

/** ===== Types from your backend (simple cart) ===== */
export type CartLine = {
  productNumber: string
  name: string
  slug: string
  imageUrl?: string
  qty: number               // >= 1
  price: number             // unit price (minor units, e.g., cents)
}

export type CartDto = {
  id: string                // Guid as string
  currency: string          // e.g., "CAD"
  items: CartLine[]
  updatedAt?: string
}

/** (Optional) local snapshot for guests */
type PersistedCart = {
  v: 1
  cartId: string | null
  currency: string
  items: CartLine[]
  updatedAt: number
}

const LS_KEY = 'cart.v1'

export const useCartStore = defineStore('cart', {
  state: () => ({
    cartId: null as string | null,
    currency: 'CAD',
    items: [] as CartLine[],
    initializing: false,
    updating: false,
  }),

  getters: {
    isEmpty(state): boolean {
      return state.items.length === 0
    },
    count(state): number {
      return state.items.reduce((n, i) => n + i.qty, 0)
    },
    subtotalMinor(state): number {
      return state.items.reduce((sum, i) => sum + i.qty * i.price, 0)
    },
  },

  actions: {
    /** Hydrate/replace from server (authoritative) */
    setFromServer(cart: CartDto) {
      this.cartId = cart.id
      this.currency = cart.currency
      // Normalize: collapse duplicates by productNumber just in case
      const map = new Map<string, CartLine>()
      for (const line of cart.items ?? []) {
        const existing = map.get(line.productNumber)
        if (existing) {
          existing.qty += line.qty
          // keep first-seen price (or overwrite; choose policy)
        } else {
          map.set(line.productNumber, { ...line })
        }
      }
      this.items = Array.from(map.values())
      this.persistLocal()
    },

    /** Optional: restore guest snapshot (before calling your /cart/init) */
    restoreLocal() {
      const raw = localStorage.getItem(LS_KEY)
      if (!raw) return
      try {
        const snap = JSON.parse(raw) as PersistedCart
        if (snap?.v === 1) {
          this.cartId = snap.cartId
          this.currency = snap.currency || this.currency
          this.items = snap.items || []
        } else {
          localStorage.removeItem(LS_KEY)
        }
      } catch {
        localStorage.removeItem(LS_KEY)
      }
    },

    /** Optional: persist snapshot for guests */
    persistLocal() {
      const snap: PersistedCart = {
        v: 1,
        cartId: this.cartId,
        currency: this.currency,
        items: this.items,
        updatedAt: Date.now(),
      }
      localStorage.setItem(LS_KEY, JSON.stringify(snap))
    },

    add(
      line: { productNumber: string; name: string; slug: string; imageUrl?: string; price: number },
      qty = 1,
      opts: { updatePrice?: boolean } = {}
    ) {
      if (qty <= 0) return

      const idx = this.items.findIndex(i => i.productNumber === line.productNumber)

      if (idx >= 0) {
        const existing = this.items[idx]
        existing.qty += qty
        // refresh product metadata in case it changed
        existing.name = line.name
        existing.slug = line.slug
        existing.imageUrl = line.imageUrl
        // keep original captured price unless you explicitly want to update it
        if (opts.updatePrice) existing.price = line.price
      } else {
        // create new line with required fields
        this.items.push({ ...line, qty })
      }

      this.persistLocal()
    },

    /** Set an exact quantity (0 removes) */
    setQty(productNumber: string, qty: number) {
      const idx = this.items.findIndex(i => i.productNumber === productNumber)
      if (idx < 0) return
      if (qty <= 0) {
        this.items.splice(idx, 1)
      } else {
        this.items[idx].qty = qty
      }
      this.persistLocal()
    },

    /** Remove a line */
    remove(productNumber: string) {
      this.setQty(productNumber, 0)
    },

    /** Clear everything (use after order completes or server clears) */
    clear() {
      this.cartId = null
      this.items = []
      this.persistLocal()
    },

    /** Build the minimal payload your backend expects if you need to send it back */
    toRequestPayload(): { cartId: string | null; items: { productNumber: string; qty: number; price: number }[] } {
      return {
        cartId: this.cartId,
        items: this.items.map(i => ({ productNumber: i.productNumber, qty: i.qty, price: i.price })),
      }
    },

    async init() {
      this.initializing = true

      // 1) Restore guest snapshot first (so UI has something while fetching)
      this.restoreLocal()

      try {
        // 2) Fetch authoritative cart from backend
        const res = await fetch('/api/cart/init', {
          credentials: 'include', // keep cookies if needed
          headers: { 'Content-Type': 'application/json' },
        })

        if (!res.ok) throw new Error(`Cart init failed: ${res.status}`)

        const data = (await res.json()) as CartDto

        // 3) Replace local state with server state
        this.setFromServer(data)
      } catch (err) {
        console.error('Error initializing cart:', err)
        // fall back to whatever we had in localStorage
      } finally {
        this.initializing = false
      }
    },
  },
})
