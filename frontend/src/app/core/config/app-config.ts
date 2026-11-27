import { InjectionToken } from '@angular/core';
import { environment } from '@env';

/** Deployment settings. Injected (not imported) so tests and white-label builds can override them. */
export interface AppConfig {
  production: boolean;
  /** Origin of the API; empty when it is served from the same origin. */
  apiUrl: string;
  /** Serve every request from the in-browser mock API instead of the network. */
  useMockApi: boolean;
  /** Restaurant shown when a guest opens the site without a restaurant in the URL. */
  defaultTenant: string;
}

export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG', { providedIn: 'root', factory: () => environment });

/** Version prefix shared by every endpoint. */
export const API_PREFIX = '/api/v1';
