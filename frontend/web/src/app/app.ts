import { ChangeDetectionStrategy, Component, effect, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { EnvironmentDirectoryService } from './core/environment-directory.service';
import { SessionService } from './core/session.service';
import { DokButton } from './design-system/button/dok-button';
import { DokEmptyState } from './design-system/empty-state/dok-empty-state';
import { DokToastHost } from './design-system/toast/dok-toast';
import { DokAppShell } from './shell/dok-app-shell';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokAppShell, DokButton, DokEmptyState, DokToastHost, RouterOutlet],
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App implements OnInit {
  protected readonly sessionService = inject(SessionService);
  protected readonly directory = inject(EnvironmentDirectoryService);

  constructor() {
    effect(() => {
      if (this.sessionService.state() === 'authenticated') void this.directory.load();
    });
  }

  ngOnInit(): void {
    this.loadSession();
  }

  protected loadSession(): void {
    this.sessionService.load();
  }

  protected login(): void {
    this.sessionService.login();
  }

  protected async logout(): Promise<void> {
    await this.sessionService.logout();
  }
}
