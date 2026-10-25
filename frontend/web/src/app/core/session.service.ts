import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** Campo de formulário que o antiforgery do ASP.NET Core lê quando o cabeçalho não vem (padrão do framework). */
export const ANTIFORGERY_FORM_FIELD = '__RequestVerificationToken';

interface AntiforgeryResponse {
  requestToken: string;
}

export interface BffSession {
  authenticated: boolean;
  subject?: string | null;
  name?: string | null;
  displayGroup?: string | null;
}

export type SessionState = 'loading' | 'error' | 'anonymous' | 'authenticated' | 'expired';

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

  /** Sessão do BFF expirou durante o uso (401 da API); a tela pede novo login sem perder a URL. */
  expire(): void {
    if (this.state() === 'authenticated') this.state.set('expired');
  }

  login(): void {
    const returnUrl = `${window.location.pathname}${window.location.search}`;
    window.location.assign(`bff/login?returnUrl=${encodeURIComponent(returnUrl)}`);
  }

  async antiforgeryToken(): Promise<string> {
    const { requestToken } = await firstValueFrom(this.http.get<AntiforgeryResponse>('bff/antiforgery'));
    return requestToken;
  }

  /**
   * Sai por um POST de formulário, que navega a página: o BFF responde com o redirecionamento para o encerramento
   * da sessão no Keycloak, que volta ao console. Por XHR o navegador seguiria os redirecionamentos dentro da
   * chamada e a página ficaria parada.
   */
  async logout(): Promise<void> {
    const requestToken = await this.antiforgeryToken();
    const form = document.createElement('form');
    form.method = 'post';
    form.action = 'bff/logout';
    form.hidden = true;
    const token = document.createElement('input');
    token.type = 'hidden';
    token.name = ANTIFORGERY_FORM_FIELD;
    token.value = requestToken;
    form.append(token);
    document.body.append(form);
    form.submit();
  }
}
