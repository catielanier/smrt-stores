import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '../stores/user'

// Lazy-loaded views
// const Home = () => import('@/views/Home.vue')
// const Shop = () => import('@/views/Shop.vue')
// const Product = () => import('@/views/Product.vue')           // /product/:slug
// const Cart = () => import('@/views/Cart.vue')
// const Admin = () => import('@/views/admin/Admin.vue')
// const NotFound = () => import('@/views/NotFound.vue')

const routes: RouteRecordRaw[] = [
  // { path: '/', name: 'home', component: Home },
  // { path: '/shop', name: 'shop', component: Shop },
  // { path: '/product/:slug', name: 'product', component: Product, props: true },
  // { path: '/cart', name: 'cart', component: Cart },

  // // Protected admin area
  // { path: '/admin', name: 'admin', component: Admin, meta: { requiresAuth: true, requiresAdmin: true } },

  // // 404 catch-all (must be last)
  // { path: '/:pathMatch(.*)*', name: 'not-found', component: NotFound },
]

export const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
  scrollBehavior(to, from, saved) {
    if (saved) return saved
    return { top: 0 }
  },
})

// Global auth/admin guards
router.beforeEach(async (to) => {
  const auth = useAuthStore()

  // If the app booted without init, do it once here (so deep links work)
  if (!auth.initializing && auth.accessToken === null && !auth.user) {
    await auth.init().catch(() => {})
  }

  if (to.meta?.requiresAuth && !auth.isAuthenticated) {
    return { name: 'home', query: { next: to.fullPath } }
  }

  if (to.meta?.requiresAdmin && !auth.showAdminPanel) {
    return { name: 'home' }
  }

  return true
})

router.onError((err) => {
  // Optional: log to Sentry/etc.
  console.error('Router error:', err)
})

export default router
