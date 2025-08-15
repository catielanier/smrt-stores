import { createApp } from "vue";
import { createPinia } from "pinia";
import "./style.css";
import App from "./App.vue";
import { useAuthStore } from "./stores/user";
import { useCartStore } from "./stores/cart";
import { useCategoryStore } from "./stores/category";

const app = createApp(App).use(createPinia());

useAuthStore().init().finally(async () => {
  await useCartStore().init();
  await useCategoryStore().fetchCategories();
  app.mount("#app")
})
