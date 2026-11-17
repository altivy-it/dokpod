import {fileURLToPath} from 'node:url';
import {defineConfig, devices} from '@playwright/test';

const port = Number(process.env.PORT ?? 4173);

export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.spec.mjs',
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: 0,
  reporter: [['list'], ['html', {outputFolder: '../playwright-report', open: 'never'}]],
  outputDir: '../test-results',
  use: {
    baseURL: `http://127.0.0.1:${port}`,
    locale: 'pt-BR',
    colorScheme: 'dark',
    trace: 'retain-on-failure',
  },
  webServer: {
    command: 'node tests/serve.mjs',
    cwd: fileURLToPath(new URL('..', import.meta.url)),
    url: `http://127.0.0.1:${port}/`,
    reuseExistingServer: !process.env.CI,
    stdout: 'pipe',
  },
  projects: [
    {name: 'mobile-320', use: {...devices['Desktop Chrome'], viewport: {width: 320, height: 720}}},
    {name: 'tablet-768', use: {...devices['Desktop Chrome'], viewport: {width: 768, height: 1024}}},
    {name: 'desktop-1280', use: {...devices['Desktop Chrome'], viewport: {width: 1280, height: 800}}},
  ],
});
