import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { App } from '../app';
import { sessionExpiryInterceptor } from '../core/session-expiry.interceptor';
import { SessionService } from '../core/session.service';
import { DokAppShell } from './dok-app-shell';
import { activeNavId, groupNavigation, NAV_ITEMS } from './navigation';
import { StageFilterService } from './stage-filter.service';

@Component({ template: '<p>conteúdo</p>' })
class Blank {}

const routes = [
  { path: '', component: Blank },
  { path: 'containers', component: Blank },
  { path: 'em-breve/:item', component: Blank },
];

describe('navigation', () => {
  it('groups items by section and hides items that need an absent scope', () => {
    const without = groupNavigation(NAV_ITEMS, new Set());
    expect(without.map(g => g.section)).toEqual(['Operação', 'Infraestrutura', 'Governança']);
    expect(without.flatMap(g => g.items).some(i => i.id === 'audit')).toBe(false);
    const withAudit = groupNavigation(NAV_ITEMS, new Set(['audit:read']));
    expect(withAudit.flatMap(g => g.items).some(i => i.id === 'audit')).toBe(true);
  });

  it('resolves the active item from the URL, ignoring query strings', () => {
    expect(activeNavId('/?stage=production')).toBe('overview');
    expect(activeNavId('/containers/env/abc?tab=logs')).toBe('containers');
    expect(activeNavId('/em-breve/configuracoes')).toBe('settings');
    expect(activeNavId('/imagens?semUso=1')).toBe('images');
    expect(activeNavId('/em-breve/pods')).toBe('pods');
    expect(activeNavId('/nao-existe')).toBe('');
  });
});

describe('stage filter', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideRouter(routes)] }));

  it('persists the stage in the URL and toggles it off', async () => {
    const router = TestBed.inject(Router);
    const stage = TestBed.inject(StageFilterService);
    await router.navigateByUrl('/containers?search=api');
    stage.select('production');
    await new Promise(resolve => setTimeout(resolve));
    expect(router.url).toBe('/containers?search=api&stage=production');
    expect(stage.stage()).toBe('production');
    stage.select(null);
    await new Promise(resolve => setTimeout(resolve));
    expect(router.url).toBe('/containers?search=api');
    expect(stage.stage()).toBeNull();
  });

  it('ignores unknown stage values', async () => {
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/?stage=qa');
    expect(TestBed.inject(StageFilterService).stage()).toBeNull();
  });
});

describe('dok-app-shell', () => {
  @Component({
    imports: [DokAppShell],
    template: `<dok-app-shell userName="Mariana Costa" userGroup="Plataforma · SRE" userInitials="MC"><main id="dok-main" tabindex="-1">x</main></dok-app-shell>`,
  })
  class Host {}

  beforeEach(() => TestBed.configureTestingModule({ providers: [provideRouter(routes)] }));

  function setWidth(width: number): void {
    Object.defineProperty(window, 'innerWidth', { value: width, configurable: true });
  }

  it('marks the current page, offers a skip link and names the main menu', async () => {
    setWidth(1440);
    await TestBed.inject(Router).navigateByUrl('/containers');
    const fixture = TestBed.createComponent(Host);
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('nav[aria-label="Menu principal"]')).not.toBeNull();
    const current = el.querySelector('[aria-current=page]')!;
    expect(current.textContent).toContain('Containers');
    const skip = el.querySelector<HTMLAnchorElement>('.dok-skip-link')!;
    expect(skip.textContent).toContain('Pular para o conteúdo');
    skip.click();
    expect(document.activeElement?.id).toBe('dok-main');
    fixture.nativeElement.remove();
  });

  it('collapses the menu below 1100px and keeps item names for assistive technology', () => {
    setWidth(900);
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    const toggle = el.querySelector<HTMLButtonElement>('button[aria-label="Expandir menu"]')!;
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    const link = el.querySelector('a.alt-app-shell__item')!;
    expect(link.getAttribute('aria-label')).toBe('Visão geral');
    toggle.click();
    fixture.detectChanges();
    expect(toggle.getAttribute('aria-expanded')).toBe('true');
    expect(toggle.getAttribute('aria-label')).toBe('Recolher menu');
    setWidth(1440);
  });

  it('opens the user menu and closes it with Escape', () => {
    setWidth(1440);
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    const profile = el.querySelector<HTMLButtonElement>('button.alt-app-header__profile')!;
    expect(profile.textContent!.replace(/\s+/g, ' ').trim()).toBe('MC, menu do usuário');
    profile.click();
    fixture.detectChanges();
    expect(profile.getAttribute('aria-expanded')).toBe('true');
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    expect(profile.getAttribute('aria-expanded')).toBe('false');
  });
});

describe('session expiry', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(withInterceptors([sessionExpiryInterceptor])), provideHttpClientTesting()],
    }),
  );

  it('shows the expired state when an authenticated call returns 401', async () => {
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    http.expectOne('bff/session').flush({ authenticated: true, name: 'Mariana Costa' });
    fixture.detectChanges();
    expect(TestBed.inject(SessionService).state()).toBe('authenticated');

    const call = firstValueFrom(TestBed.inject(HttpClient).get('api/v1/overview')).catch(() => undefined);
    http.expectOne('api/v1/overview').flush(null, { status: 401, statusText: 'Unauthorized' });
    await call;
    fixture.detectChanges();
    expect(TestBed.inject(SessionService).state()).toBe('expired');
    expect(fixture.nativeElement.textContent).toContain('Sessão expirada');
    expect(fixture.nativeElement.textContent).toContain('Entrar novamente');
  });

  it('does not treat the anonymous session read as expiry', () => {
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    http.expectOne('bff/session').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(TestBed.inject(SessionService).state()).toBe('error');
  });
});
