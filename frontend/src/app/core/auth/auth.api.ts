import { Injectable, inject } from '@angular/core';
import { ApiClient } from '@core/http/api-client';
import { LoginResponse } from '@shared/models';

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly api = inject(ApiClient);
  login = (email: string, password: string) => this.api.post<LoginResponse>('/auth/login', { email, password });
}
