import { createRouter, createWebHistory } from 'vue-router'
import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  { path: '/', name: 'home', component: () => import('../pages/Home.vue') },
  { path: '/schedules', name: 'schedules', component: () => import('../pages/Schedules.vue') },
  { path: '/holidays', name: 'holidays', component: () => import('../pages/Holidays.vue') },
  { path: '/folders', name: 'folders', component: () => import('../pages/Folders.vue') },
  { path: '/zones', name: 'zones', component: () => import('../pages/Zones.vue') },
  { path: '/patterns', name: 'patterns', component: () => import('../pages/Patterns.vue') },
  { path: '/jellyfish', name: 'jellyfish', component: () => import('../pages/Jellyfish.vue') },
  { path: '/audit-logs', name: 'audit-logs', component: () => import('../pages/AuditLogs.vue') },
  { path: '/diagnostics', name: 'diagnostics', component: () => import('../pages/Diagnostics.vue') },
]

export const router = createRouter({
  history: createWebHistory(),
  routes,
})

