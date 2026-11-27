import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: number;
  text: string;
  kind: 'info' | 'error';
}

/** Short status messages shown at the bottom of the screen. */
@Injectable({ providedIn: 'root' })
export class Toasts {
  readonly list = signal<Toast[]>([]);
  private seq = 0;

  show(text: string, kind: Toast['kind'] = 'info', ms = 3500) {
    const id = ++this.seq;
    this.list.update((l) => [...l.slice(-2), { id, text, kind }]);
    setTimeout(() => this.dismiss(id), ms);
  }

  error(text: string) {
    this.show(text, 'error', 6000);
  }

  dismiss(id: number) {
    this.list.update((l) => l.filter((t) => t.id !== id));
  }
}
