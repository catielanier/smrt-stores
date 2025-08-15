import { createApp } from "vue";
import { createPinia } from "pinia";
import "./style.css";
import App from "./App.vue";
import { useAuthStore } from "./stores/user";
import { useCartStore } from "./stores/cart";
import { useCategoryStore } from "./stores/category";
import router from "./services/router";

const app = createApp(App)
  .use(createPinia())
  .use(router)

await Promise.all([
  useAuthStore().init(),
  useCartStore().init(),
  useCategoryStore().fetchCategories()
])


app.mount("#app")
