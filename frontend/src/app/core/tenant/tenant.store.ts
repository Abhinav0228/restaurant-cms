import { Injectable, computed, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { APP_CONFIG } from '@core/config/app-config';
import { AuthStore } from '@core/auth/auth.store';

const SLUG = /^[a-z0-9](?:[a-z0-9-]{1,38}[a-z0-9])$/;

/**
 * Which restaurant the app is working for.
 * Guests: the slug in the URL (/r/:tenant/...). Staff: the restaurant in their sign-in token.
 */
@Injectable({ providedIn: 'root' })
export class TenantStore {
  private readonly config = inject(APP_CONFIG);
  private readonly auth = inject(AuthStore);

  /** Restaurant of the guest site currently open. */
  readonly publicSlug = signal(this.config.defaultTenant);
  /** Restaurant of the signed-in staff member (null for platform administrators). */
  readonly staffTenant = computed(() => this.auth.tenant());

  setPublic(slug: string) {
    this.publicSlug.set(slug);
  }

  /** Router commands for a page of the current guest site, e.g. link('menu') → ['/r', 'ember-oak', 'menu']. */
  link(...segments: string[]) {
    return ['/r', this.publicSlug(), ...segments];
  }

  /** The address printed on a table's QR code. */
  tableUrl(code: string, slug = this.staffTenant()?.slug ?? this.publicSlug()) {
    return `${location.origin}/r/${slug}/t/${encodeURIComponent(code)}`;
  }
}

/** Reads :tenant from the URL for the guest site; malformed slugs go to the default restaurant. */
export const tenantRouteGuard: CanActivateFn = (route) => {
  const slug = (route.paramMap.get('tenant') ?? '').toLowerCase();
  const store = inject(TenantStore);
  if (!SLUG.test(slug)) return inject(Router).createUrlTree(['/r', inject(APP_CONFIG).defaultTenant]);
  store.setPublic(slug);
  return true;
};
