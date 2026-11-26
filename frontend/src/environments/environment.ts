import type { AppConfig } from '../app/core/config/app-config';

// Production: the API is served from the same origin (behind a reverse proxy) at /api.
export const environment: AppConfig = {
  production: true,
  apiUrl: '',
  useMockApi: false,
  defaultTenant: 'ember-oak',
};
