<script setup lang="ts">
</script>

<template>
  <main class="layout">
    <header class="header">
      <img src="./assets/HouseWithLights.png" class="logo" alt="House with Christmas lights" />
      <h1>Jellyfish Lighting System</h1>
    </header>

    <div class="body">
      <aside class="sidebar">
        <nav class="nav">
          <router-link to="/">Home</router-link>
          <router-link to="/schedules">Schedules</router-link>
          <router-link to="/holidays">Holidays</router-link>
          <router-link to="/folders">Folders</router-link>
          <router-link to="/zones">Zones</router-link>
          <router-link to="/patterns">Patterns</router-link>
          <router-link to="/jellyfish">Jellyfish</router-link>
          <router-link to="/audit-logs">Audit Logs</router-link>
          <router-link to="/diagnostics">Diagnostics</router-link>
        </nav>
      </aside>
      <section class="content">
        <router-view />
      </section>
    </div>
  </main>
</template>

<style scoped>
.layout { max-width: 1200px; margin: 0 auto; padding: 1rem; }
.header { display: flex; align-items: center; gap: 1rem; margin-bottom: .75rem; }
.logo {
  height: 5em;
  filter: brightness(1.3);
}

.body { display: grid; grid-template-columns: 220px 1fr; gap: 1rem; align-items: start; }
.sidebar { position: sticky; top: 0; align-self: start; max-height: 100vh; overflow: auto; border-right: 1px solid #4444; padding-right: 1rem; }
.nav { display: flex; flex-direction: column; gap: .5rem; }
.nav a { color: inherit; text-decoration: none; padding: .35rem .5rem; border-radius: 6px; }
.nav a.router-link-active { background: #2c3e5033; font-weight: 600; }

.content { min-width: 0; }

/* Light theme adjustments */
@media (prefers-color-scheme: light) {
  .logo {
    filter: brightness(0.9);
  }
}

@media (max-width: 800px) {
  .body { grid-template-columns: 1fr; }
  .sidebar { border-right: 0; border-bottom: 1px solid #4444; padding-bottom: .75rem; }
  .nav { flex-direction: row; flex-wrap: wrap; gap: .5rem; }
}
</style>
