import type { AppConfig } from '../app/core/config/app-config';

// `npm run start:mock` — no backend needed. An in-browser mock API serves the same endpoints,
// with two demo restaurants (used for demos and the Playwright e2e tests).
export const environment: AppConfig = {
  production: false,
  apiUrl: '',
  useMockApi: true,
  defaultTenant: 'ember-oak',
};
