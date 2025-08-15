// /src/stores/categories.ts
import { defineStore } from 'pinia'

export type Category = {
  id: string
  slug: string
  name: string
}

export const useCategoryStore = defineStore('categories', {
  state: () => ({
    categories: [] as Category[],
    loading: false,
    lastFetchedAt: null as number | null,
  }),

  getters: {
    all: (state) => state.categories,
    byId: (state) => (id: string) => state.categories.find(c => c.id === id) || null,
    bySlug: (state) => (slug: string) => state.categories.find(c => c.slug === slug) || null,
  },

  actions: {
    setFromServer(categories: Category[]) {
      this.categories = [...categories].sort((a, b) =>
        a.name.localeCompare(b.name, undefined, { sensitivity: 'base' })
      )
      this.lastFetchedAt = Date.now()
    },

    async fetchCategories() {
      if (this.loading) return
      this.loading = true
      try {
        const res = await fetch('/api/categories') // adjust URL to your backend
        if (!res.ok) throw new Error('Failed to fetch categories')
        const data = (await res.json()) as Category[]
        this.setFromServer(data)
      } catch (err) {
        console.error('Error fetching categories:', err)
      } finally {
        this.loading = false
      }
    },

    clear() {
      this.categories = []
      this.loading = false
      this.lastFetchedAt = null
    },
  },
})
