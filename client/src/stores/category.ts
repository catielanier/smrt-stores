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
    loading: false as boolean,
    lastFetchedAt: null as number | null,
  }),

  getters: {
    all(state): Category[] {
      return state.categories
    },
    byId: (state) => (id: string) =>
      state.categories.find(c => c.id === id) || null,
    bySlug: (state) => (slug: string) =>
      state.categories.find(c => c.slug === slug) || null,
  },

  actions: {
    setFromServer(categories: Category[]) {
      // Sort by name for stable UI order
      this.categories = [...categories].sort((a, b) =>
        a.name.localeCompare(b.name, undefined, { sensitivity: 'base' })
      )
      this.lastFetchedAt = Date.now()
    },

    clear() {
      this.categories = []
      this.loading = false
      this.lastFetchedAt = null
    },
  },
})
