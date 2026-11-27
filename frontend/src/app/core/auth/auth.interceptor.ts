import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { tap } from 'rxjs';
import { API_PREFIX, APP_CONFIG } from '@core/config/app-config';
import { AuthStore } from './auth.store';

/** Adds the bearer token to staff API calls and signs out when the token is rejected. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthStore);
  const api = inject(APP_CONFIG).apiUrl + API_PREFIX;
  const token = auth.token();
  const isStaffApi = req.url.startsWith(api) && !req.url.startsWith(`${api}/public/`);
  const authed = token && isStaffApi ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  return next(authed).pipe(
    tap({
      error: (e: { status?: number }) => {
        if (e?.status === 401 && token && !req.url.endsWith('/auth/login')) auth.logout();
      },
    }),
  );
};
