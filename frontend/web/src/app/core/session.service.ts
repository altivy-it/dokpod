import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

interface AntiforgeryResponse {
  requestToken: string;
}

export interface BffSession {
  authenticated: boolean;
  subject?: string | null;
  name?: string | null;
  displayGroup?: string | null;
}

export type SessionState = 'loading' | 'error' | 'anonymous' | 'authenticated';

export function initialsOf(name: string | null | undefined): string {
  return String(name ?? '')
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map(part => part[0])
    .join('')
    .toUpperCase();
}

/** Sessão do navegador via BFF; o Angular nunca recebe tokens. */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly http = inject(HttpClient);

  readonly session = signal<BffSession | null>(null);
  readonly state = signal<SessionState>('loading');
  readonly displayName = computed(() => this.session()?.name ?? this.session()?.subject ?? 'Usuário');
  readonly initials = computed(() => initialsOf(this.displayName()) || 'U');

  load(): void {
    this.state.set('loading');
    this.http.get<BffSession>('bff/session').subscribe({
      next: session => {
        this.session.set(session);
        this.state.set(session.authenticated ? 'authenticated' : 'anonymous');
      },
      error: () => {
        this.session.set(null);
        this.state.set('error');
      },
    });
  }

  login(): void {
    const returnUrl = `${window.location.pathname}${window.location.search}`;
    window.location.assign(`bff/login?returnUrl=${encodeURIComponent(returnUrl)}`);
  }

  async antiforgeryToken(): Promise<string> {
    const { requestToken } = await firstValueFrom(this.http.get<AntiforgeryResponse>('bff/antiforgery'));
    return requestToken;
  }

  async logout(): Promise<void> {
    const requestToken = await this.antiforgeryToken();
    await firstValueFrom(
      this.http.post<void>('bff/logout', null, { headers: { 'X-Dokpod-Antiforgery': requestToken } }),
    );
    this.session.set(null);
    this.state.set('anonymous');
    window.location.assign('/');
  }
}
