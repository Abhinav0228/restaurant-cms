import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, firstValueFrom } from 'rxjs';
import { API_PREFIX, APP_CONFIG } from '@core/config/app-config';
import { ApiError } from './api-error';

export type QueryParams = Record<string, string | number | boolean | null | undefined>;

/**
 * Thin, typed transport used by every feature's API service. Returns Promises (the UI is signal-based)
 * and normalises every failure into an ApiError.
 */
@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);
  private readonly config = inject(APP_CONFIG);

  /** Absolute URL of an endpoint, e.g. url('/tables') → http://host/api/v1/tables */
  url(path: string) {
    return `${this.config.apiUrl}${API_PREFIX}${path}`;
  }

  get<T>(path: string, query?: QueryParams) {
    return this.run(this.http.get<T>(this.url(path), { params: toParams(query) }));
  }
  post<T>(path: string, body: unknown = {}) {
    return this.run(this.http.post<T>(this.url(path), body));
  }
  put<T>(path: string, body: unknown) {
    return this.run(this.http.put<T>(this.url(path), body));
  }
  patch<T>(path: string, body: unknown) {
    return this.run(this.http.patch<T>(this.url(path), body));
  }
  delete(path: string) {
    return this.run(this.http.delete<void>(this.url(path)));
  }

  private async run<T>(request: Observable<T>): Promise<T> {
    try {
      return await firstValueFrom(request);
    } catch (e) {
      throw ApiError.from(e);
    }
  }
}

function toParams(query?: QueryParams) {
  let params = new HttpParams();
  for (const [k, v] of Object.entries(query ?? {})) if (v !== undefined && v !== null && v !== '') params = params.set(k, String(v));
  return params;
}

/** URL-safe path segment. */
export const seg = (value: string) => encodeURIComponent(value);
