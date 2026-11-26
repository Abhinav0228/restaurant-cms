import type { AppConfig } from '../app/core/config/app-config';

// `npm start` — talks to the .NET API running locally.
export const environment: AppConfig = {
  production: false,
  apiUrl: 'http://localhost:5080',
  useMockApi: false,
  defaultTenant: 'ember-oak',
};
