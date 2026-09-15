import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    // Pure logic only — no DOM, no component rendering.
    environment: 'node',
    include: ['packages/*/src/**/*.test.ts'],
  },
});
