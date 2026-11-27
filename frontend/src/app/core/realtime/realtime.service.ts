import { DestroyRef, Injectable, effect, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Subject, debounceTime } from 'rxjs';
import { APP_CONFIG } from '@core/config/app-config';
import { AuthStore } from '@core/auth/auth.store';
import { MockDb } from '@core/mock/mock-db';
import { Order, Table } from '@shared/models';

export type RealtimeEvent =
  | { type: 'orderCreated'; order: Order }
  | { type: 'orderUpdated'; order: Order }
  | { type: 'orderDelayed'; order: Order }
  | { type: 'tableUpdated'; table: Table }
  | { type: 'sync' };

/**
 * Live updates for staff screens. Uses the SignalR hub (/hubs/orders, one group per restaurant) against the real API,
 * or the mock database's event stream in mock mode (which also syncs across browser tabs).
 */
@Injectable({ providedIn: 'root' })
export class Realtime {
  private readonly config = inject(APP_CONFIG);
  private readonly auth = inject(AuthStore);
  private readonly mock = inject(MockDb);
  private readonly subject = new Subject<RealtimeEvent>();
  private connection: HubConnection | null = null;

  readonly events$ = this.subject.asObservable();
  readonly connected = signal(this.config.useMockApi);

  constructor() {
    if (this.config.useMockApi) {
      // The mock hosts several restaurants; only pass on events for the signed-in staff member's one.
      this.mock.events$.subscribe(({ tenantId, event }) => {
        if (tenantId === null || tenantId === this.auth.tenant()?.id) this.subject.next(event);
      });
      return;
    }
    effect(() => {
      const token = this.auth.token();
      if (token) void this.start();
      else void this.stop();
    });
  }

  /** Calls `reload` whenever something relevant changes (debounced so bursts trigger one refresh). */
  onChange(destroyRef: DestroyRef, reload: (e: RealtimeEvent) => void, debounceMs = 150) {
    const sub = this.events$.pipe(debounceTime(debounceMs)).subscribe(reload);
    destroyRef.onDestroy(() => sub.unsubscribe());
  }

  private async start() {
    if (this.connection) return;
    const connection = new HubConnectionBuilder()
      .withUrl(`${this.config.apiUrl}/hubs/orders`, { accessTokenFactory: () => this.auth.token() ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('orderCreated', (order: Order) => this.subject.next({ type: 'orderCreated', order }));
    connection.on('orderUpdated', (order: Order) => this.subject.next({ type: 'orderUpdated', order }));
    connection.on('orderDelayed', (order: Order) => this.subject.next({ type: 'orderDelayed', order }));
    connection.on('tableUpdated', (table: Table) => this.subject.next({ type: 'tableUpdated', table }));
    connection.onreconnecting(() => this.connected.set(false));
    connection.onreconnected(() => {
      this.connected.set(true);
      this.subject.next({ type: 'sync' }); // catch up on anything missed
    });
    connection.onclose(() => this.connected.set(false));

    this.connection = connection;
    try {
      await connection.start();
      this.connected.set(true);
    } catch {
      this.connected.set(false);
    }
  }

  private async stop() {
    const c = this.connection;
    this.connection = null;
    this.connected.set(this.config.useMockApi);
    if (c && c.state !== HubConnectionState.Disconnected) await c.stop();
  }
}
