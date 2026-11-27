import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { storage } from '@core/storage/storage';
import { LoginResponse, StaffRole } from '@shared/models';
import { AuthApi } from './auth.api';

const KEY = 'rcms.session';

/** The signed-in staff member, their role and the restaurant they work for. */
@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly api = inject(AuthApi);
  private readonly router = inject(Router);
  private readonly session = signal<LoginResponse | null>(this.restore());

  readonly user = computed(() => this.session()?.user ?? null);
  readonly token = computed(() => this.session()?.token ?? null);
  readonly tenant = computed(() => this.session()?.tenant ?? null);
  readonly isLoggedIn = computed(() => this.session() !== null);
  readonly role = computed(() => this.session()?.user.role ?? null);
  readonly isPlatformAdmin = computed(() => this.role() === 'PlatformAdmin');

  async login(email: string, password: string) {
    const res = await this.api.login(email, password);
    this.session.set(res);
    storage.set(KEY, JSON.stringify(res));
    return res.user;
  }

  logout(redirect = true) {
    this.session.set(null);
    storage.remove(KEY);
    if (redirect) void this.router.navigate(['/login']);
  }

  hasRole(...roles: StaffRole[]) {
    const r = this.role();
    return r !== null && roles.includes(r);
  }

  /** Where each role lands after signing in. */
  homeFor(role: StaffRole) {
    if (role === 'PlatformAdmin') return '/platform';
    return role === 'Kitchen' ? '/admin/kitchen' : '/admin';
  }

  private restore(): LoginResponse | null {
    const raw = storage.get(KEY);
    if (!raw) return null;
    try {
      const s = JSON.parse(raw) as LoginResponse;
      if (!('tenant' in s)) return null; // session from an older version
      return new Date(s.expiresAt).getTime() > Date.now() ? s : null;
    } catch {
      return null;
    }
  }
}
