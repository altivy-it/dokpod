import { DOCUMENT } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter, map } from 'rxjs';
import { AlertSummaryService } from '../core/alert-summary.service';
import { AuditAccessService } from '../core/audit-access.service';
import { DokIcon } from '../design-system/icon/dok-icon';
import { activeNavId, groupNavigation, NAV_ITEMS } from './navigation';
import { DokNotificationsPanel } from './notifications-panel';
import { ShellContextService } from './shell-context.service';
import { STAGES, StageFilterService, type Stage } from './stage-filter.service';

const COLLAPSE_BELOW = 1100;

/**
 * Moldura do console (AppShell + AppHeader do kit): menu lateral de vidro por seções,
 * cabeçalho com título, trilha, atalhos de estágio, sino e menu do usuário.
 */
@Component({
  selector: 'dok-app-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokIcon, DokNotificationsPanel, RouterLink],
  host: {
    '(window:resize)': 'onResize()',
    '(document:mousedown)': 'onDocumentDown($event)',
    '(document:keydown.escape)': 'userMenuOpen.set(false)',
  },
  styleUrl: './dok-app-shell.css',
  templateUrl: './dok-app-shell.html',
})
export class DokAppShell {
  readonly userName = input.required<string>();
  readonly userGroup = input<string | null>(null);
  readonly userInitials = input.required<string>();
  readonly scopes = input<ReadonlySet<string>>(new Set());
  readonly signOut = output<void>();

  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  protected readonly context = inject(ShellContextService);
  /** Contador do menu e sino (spec 016): instanciado com o shell autenticado. */
  private readonly alertSummary = inject(AlertSummaryService);
  protected readonly stageFilter = inject(StageFilterService);
  protected readonly stages = STAGES;
  protected readonly collapsed = signal(this.narrow());
  protected readonly userMenuOpen = signal(false);
  private readonly profileWrap = viewChild<ElementRef<HTMLElement>>('profileWrap');
  private userToggled = false;

  private readonly url = toSignal(
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd),
      map(event => (event as NavigationEnd).urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  private readonly auditAccess = inject(AuditAccessService);
  /** `audit:read` (spec 017 D-006) e Configurações (spec 022 D-002) vêm da decisão do Keycloak, não da lista de scopes do registro. */
  protected readonly groups = computed(() => {
    const scopes = new Set([...this.scopes()].filter(scope => scope !== 'audit:read' && scope !== 'settings:read'));
    if (this.auditAccess.canRead()) scopes.add('audit:read');
    if (this.auditAccess.settings()) scopes.add('settings:read');
    return groupNavigation(NAV_ITEMS, scopes);
  });
  protected readonly active = computed(() => activeNavId(this.url()));
  protected readonly notifications = computed(() => this.context.notifications());

  protected toggleCollapsed(): void {
    this.userToggled = true;
    this.collapsed.set(!this.collapsed());
  }

  protected pickStage(stage: Stage): void {
    this.stageFilter.select(this.stageFilter.stage() === stage ? null : stage);
  }

  protected countFor(id: string): number | null {
    return id === 'alerts' ? this.context.alertCount() : null;
  }

  protected skipToContent(event: Event): void {
    event.preventDefault();
    this.document.getElementById('dok-main')?.focus();
  }

  protected onResize(): void {
    if (!this.userToggled) this.collapsed.set(this.narrow());
  }

  protected onDocumentDown(event: MouseEvent): void {
    if (this.userMenuOpen() && !this.profileWrap()?.nativeElement.contains(event.target as Node)) {
      this.userMenuOpen.set(false);
    }
  }

  protected doSignOut(): void {
    this.userMenuOpen.set(false);
    this.signOut.emit();
  }

  private narrow(): boolean {
    return typeof window !== 'undefined' && window.innerWidth < COLLAPSE_BELOW;
  }
}
