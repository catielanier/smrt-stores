import { createApp } from "vue";
import { createPinia } from "pinia";
import "./style.css";
import App from "./App.vue";
import { useAuthStore } from "./stores/user";
import { useCartStore } from "./stores/cart";

const app = createApp(App).use(createPinia());

useAuthStore().init().finally(() => {
  useCartStore().init().finally(() => {
    app.mount("#app")
  })
})
