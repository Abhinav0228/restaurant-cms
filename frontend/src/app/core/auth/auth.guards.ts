import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { StaffRole } from '@shared/models';
import { AuthStore } from './auth.store';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthStore);
  return auth.isLoggedIn() ? true : inject(Router).createUrlTree(['/login'], { queryParams: { next: state.url } });
};

/** Lets the route through only for the given roles; everyone else goes to their own home screen. */
export const roleGuard = (...roles: StaffRole[]): CanActivateFn => () => {
  const auth = inject(AuthStore);
  if (auth.hasRole(...roles)) return true;
  const role = auth.role();
  return inject(Router).parseUrl(role ? auth.homeFor(role) : '/login');
};

/** The restaurant portal needs a restaurant: platform administrators are sent to the platform console. */
export const restaurantStaffGuard: CanActivateFn = () => {
  const auth = inject(AuthStore);
  return auth.tenant() ? true : inject(Router).parseUrl(auth.isPlatformAdmin() ? '/platform' : '/login');
};
