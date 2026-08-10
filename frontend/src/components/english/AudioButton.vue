<script setup lang="ts">
import { ref } from 'vue'
import { speak, stopSpeaking } from '../../utils/speech'

const props = defineProps<{ text: string; rate?: number }>()
const playing = ref(false)

function toggle() {
  if (playing.value) {
    stopSpeaking()
    playing.value = false
    return
  }
  if (props.text) {
    speak(props.text, props.rate ?? 0.9)
    playing.value = true
    setTimeout(() => (playing.value = false), 2500)
  }
}
</script>

<template>
  <button class="e-speak" :title="'播放：' + text" @click.stop="toggle">
    <template v-if="playing">🔊</template>
    <template v-else>🔈</template>
  </button>
</template>