<script setup lang="ts">
import { computed } from 'vue'
import type { Word } from '../../types/english'
import AudioButton from './AudioButton.vue'

const props = defineProps<{ word: Word }>()
const emit = defineEmits<{ (e: 'click'): void }>()

const palette = ['#4f46e5', '#0891b2', '#059669', '#d97706', '#db2777', '#7c3aed', '#dc2626', '#2563eb']
const color = computed(() => palette[(props.word.id || 0) % palette.length])
</script>

<template>
  <div class="e-word-card" @click="emit('click')">
    <div class="e-word-image" :style="{ background: color }">
      <img v-if="word.image_url" :src="word.image_url" alt="" />
      <span v-else class="fallback-letter">{{ (word.word[0] || '?').toUpperCase() }}</span>
    </div>
    <div class="e-word-body">
      <div class="e-word-main">
        <span class="word">{{ word.word }}</span>
        <AudioButton v-if="word.word" :text="word.word" />
      </div>
      <div style="margin-top: 4px">
        <span v-if="word.phonetic" class="phonetic">{{ word.phonetic }}</span>
        <span v-if="word.pos" class="pos" style="margin-left: 6px">{{ word.pos }}</span>
      </div>
      <div class="e-word-main" style="margin-top: 4px">
        <span class="meaning">{{ word.meaning }}</span>
      </div>
    </div>
  </div>
</template>