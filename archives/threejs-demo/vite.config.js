import { resolve } from 'node:path';
import { defineConfig } from 'vite';

// Two pages: the first demo (index.html) and the prologue (prologue.html).
export default defineConfig({
  build: {
    rolldownOptions: {
      input: {
        main: resolve(import.meta.dirname, 'index.html'),
        prologue: resolve(import.meta.dirname, 'prologue.html'),
      },
    },
  },
});
