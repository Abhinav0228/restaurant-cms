import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { APP_CONFIG } from '@core/config/app-config';
import { TenantStore } from './tenant.store';

describe('TenantStore', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: APP_CONFIG, useValue: { production: false, apiUrl: '', useMockApi: true, defaultTenant: 'ember-oak' } }],
    });
  });

  it('starts on the default restaurant and builds links inside the current one', () => {
    const store = TestBed.inject(TenantStore);
    expect(store.link('menu')).toEqual(['/r', 'ember-oak', 'menu']);
    store.setPublic('blue-door');
    expect(store.link()).toEqual(['/r', 'blue-door']);
  });

  it('puts the restaurant in QR code addresses', () => {
    const store = TestBed.inject(TenantStore);
    expect(store.tableUrl('T 4', 'blue-door')).toBe(`${location.origin}/r/blue-door/t/T%204`);
  });
});
