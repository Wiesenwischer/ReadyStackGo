import { defineConfig } from 'vitest/config';

// Date formatting shows the local date; a fixed zone keeps the tests independent of the machine
// (the test workers inherit it).
process.env.TZ = 'UTC';

export default defineConfig({
  test: {
    // Pure logic only — no DOM, no component rendering.
    environment: 'node',
    include: ['packages/*/src/**/*.test.ts', 'apps/*/src/**/*.test.ts'],
  },
});
