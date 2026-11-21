<script setup lang="ts">
import RunPatternPanel from '../components/RunPatternPanel.vue'
import ActiveZonesCard from '../components/ActiveZonesCard.vue'
import DailyScheduleTimeline from '../components/DailyScheduleTimeline.vue'
</script>

<template>
  <main class="container">
    <section class="grid">
      <RunPatternPanel />
      <ActiveZonesCard />
      <DailyScheduleTimeline />
    </section>
  </main>
</template>

<style scoped>
.container { max-width: 1100px; margin: 0 auto; padding: 1rem; }
.grid { display: grid; grid-template-columns: 1fr; gap: 1rem; }
@media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
</style>

