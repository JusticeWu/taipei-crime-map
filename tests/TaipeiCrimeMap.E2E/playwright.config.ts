import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  timeout: 30 * 1000,
  retries: 1,
  fullyParallel: true,
  reporter: 'list',
  use: {
    baseURL: 'https://taipei-crime-map-uat.ambitioussand-7326440b.japaneast.azurecontainerapps.io',
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
