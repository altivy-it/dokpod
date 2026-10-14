import { AfterViewInit, ChangeDetectionStrategy, Component, computed, DestroyRef, ElementRef, inject, input, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import type { Terminal } from '@xterm/xterm';
import type { FitAddon } from '@xterm/addon-fit';
import { DokButton, DokStatusChip } from '../../design-system';
import { DokCheckbox } from '../../design-system/checkbox/dok-checkbox';
import { DokIcon } from '../../design-system/icon/dok-icon';
import { TerminalFacade, terminalReason } from './terminal.facade';

/**
 * Aba Terminal (board `03 · Detalhe`, spec 014): painel escuro monoespaçado de no mínimo 360 px; a sessão abre ao
 * ativar a aba (justificativa em Produção). Emulador restritivo: sem links automáticos, sem OSC 52 e sem API
 * proposta; colar várias linhas pede confirmação; Ctrl+] tira o foco do terminal.
 */
@Component({
  selector: 'app-terminal-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokButton, DokCheckbox, DokIcon, DokStatusChip, FormsModule],
  providers: [TerminalFacade],
  styleUrl: './terminal.tab.css',
  templateUrl: './terminal.tab.html',
})
export class TerminalTab implements AfterViewInit {
  readonly environmentId = input.required<string>();
  readonly containerId = input.required<string>();

  protected readonly facade = inject(TerminalFacade);
  protected readonly reasonText = terminalReason;
  private readonly host = viewChild.required<ElementRef<HTMLElement>>('term');
  private readonly statusBar = viewChild<ElementRef<HTMLElement>>('statusBar');
  private terminal: Terminal | null = null;
  private fit: FitAddon | null = null;
  private resizeObserver: ResizeObserver | null = null;

  protected readonly justification = signal('');
  protected readonly screenReader = signal(false);
  protected readonly pendingPaste = signal<string | null>(null);
  protected readonly pasteLines = computed(() => (this.pendingPaste() ?? '').split(/\r?\n/).length);
  protected readonly justificationValid = computed(() => {
    const value = this.justification().trim();
    const limits = this.facade.policy()?.limits;
    return value.length >= (limits?.justificationMinLength ?? 10) && value.length <= (limits?.justificationMaxLength ?? 500);
  });
  protected readonly statusChip = computed(() => {
    switch (this.facade.state()) {
      case 'connected':
        return { label: 'Conectado', tone: 'success' };
      case 'connecting':
      case 'loading':
        return { label: 'Conectando', tone: 'info' };
      case 'closed':
        return { label: 'Encerrado', tone: 'neutral' };
      case 'error':
      case 'blocked':
        return { label: 'Indisponível', tone: 'error' };
      default:
        return { label: 'Aguardando', tone: 'warning' };
    }
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.resizeObserver?.disconnect();
      this.facade.close();
      this.terminal?.dispose();
    });
  }

  async ngAfterViewInit(): Promise<void> {
    const [{ Terminal }, { FitAddon }] = await Promise.all([import('@xterm/xterm'), import('@xterm/addon-fit')]);
    const terminal = new Terminal({
      allowProposedApi: false,
      cursorBlink: true,
      convertEol: false,
      fontFamily: "'JetBrains Mono', ui-monospace, monospace",
      fontSize: 13,
      scrollback: 2000,
      screenReaderMode: this.screenReader(),
      // OSC 8 (hiperlinks) não abre nada; sem addon de links nem de clipboard (OSC 52).
      linkHandler: { activate: () => undefined },
      theme: { background: '#0b1220', foreground: '#e2e8f0', cursor: '#5eead4', selectionBackground: '#334155' },
    });
    const fit = new FitAddon();
    terminal.loadAddon(fit);
    terminal.open(this.host().nativeElement);
    terminal.attachCustomKeyEventHandler(event => {
      if (event.type === 'keydown' && event.ctrlKey && event.key === ']') {
        this.statusBar()?.nativeElement.focus();
        return false;
      }
      return true;
    });
    terminal.onData(data => this.facade.input(data));
    terminal.onResize(({ cols, rows }) => this.facade.resize(cols, rows));
    this.terminal = terminal;
    this.fit = fit;
    this.resizeObserver = new ResizeObserver(() => this.safeFit());
    this.resizeObserver.observe(this.host().nativeElement);
    this.host().nativeElement.addEventListener('paste', event => this.onPaste(event), { capture: true });
    this.safeFit();
    if (await this.facade.prepare(this.environmentId(), this.containerId())) await this.connect(null);
  }

  protected async connect(justification: string | null): Promise<void> {
    if (!this.terminal) return;
    this.terminal.reset();
    this.safeFit();
    await this.facade.open(this.environmentId(), this.containerId(), this.terminal.cols, this.terminal.rows, justification, bytes => this.terminal?.write(bytes));
    this.terminal.focus();
  }

  protected submitJustification(): void {
    if (this.justificationValid()) void this.connect(this.justification().trim());
  }

  protected async reopen(): Promise<void> {
    if (await this.facade.prepare(this.environmentId(), this.containerId())) await this.connect(null);
  }

  protected toggleScreenReader(value: boolean): void {
    this.screenReader.set(value);
    if (this.terminal) this.terminal.options.screenReaderMode = value;
  }

  protected confirmPaste(): void {
    const text = this.pendingPaste();
    this.pendingPaste.set(null);
    if (text) this.terminal?.paste(text);
    this.terminal?.focus();
  }

  protected cancelPaste(): void {
    this.pendingPaste.set(null);
    this.terminal?.focus();
  }

  private onPaste(event: ClipboardEvent): void {
    const text = event.clipboardData?.getData('text/plain') ?? '';
    if (!/\r?\n/.test(text.replace(/\r?\n$/, ''))) return;
    event.preventDefault();
    event.stopImmediatePropagation();
    this.pendingPaste.set(text);
  }

  private safeFit(): void {
    try {
      this.fit?.fit();
    } catch {
      // Elemento oculto (aba inativa): ajusta na próxima mudança de tamanho.
    }
  }
}
