import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, input, output, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { debounceTime } from 'rxjs';
import { EnvironmentDirectoryService } from '../../core/environment-directory.service';
import type { ContainerCreationRequest } from '../../data-access/generated/control-plane';
import { renderEquivalentCommand } from '../../data-access/equivalent-command';
import { parseImageReference } from '../../data-access/image-reference';
import { DokButton, DokStatusChip, formatBytes, formatDateTime, type DrawerChip, type SelectOption } from '../../design-system';
import { DokCheckbox } from '../../design-system/checkbox/dok-checkbox';
import { DokDrawer } from '../../design-system/drawer/dok-drawer';
import { DokField } from '../../design-system/field/dok-field';
import { DokIcon } from '../../design-system/icon/dok-icon';
import { DokSelect } from '../../design-system/select/dok-select';
import { StageFilterService, stageLabel } from '../../shell/stage-filter.service';
import { createFailureLabel, createIneligibility, NewContainerFacade, type CreationState } from './new-container.facade';

const NAME = /^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,62}$/;
const VARIABLE = /^[A-Za-z_][A-Za-z0-9_]{0,127}$/;
const VOLUME = /^([^:\s]+):(\/[^:\s]*)(?::(ro|rw))?$/;

const STATE_LABEL: Record<CreationState, { label: string; tone: 'success' | 'error' | 'warning' | 'info' }> = {
  idle: { label: 'Não enviado', tone: 'warning' },
  submitting: { label: 'Enviando', tone: 'info' },
  pending: { label: 'Aguardando o agente', tone: 'info' },
  dispatched: { label: 'Enviado ao agente', tone: 'info' },
  accepted: { label: 'Baixando, criando e iniciando', tone: 'info' },
  succeeded: { label: 'Em execução', tone: 'success' },
  failed: { label: 'Falhou', tone: 'error' },
  indeterminate: { label: 'Indeterminado', tone: 'warning' },
};

/** Preset do board: GET /healthz na primeira porta do container (ou 80), 15 s, 3 tentativas, 5 s. */
export function healthPreset(port: number): string[] {
  const url = `http://localhost:${port}/healthz`;
  return ['CMD-SHELL', `curl -fsS ${url} || wget -qO- ${url} || exit 1`];
}

/** Drawer "Criar e iniciar container" (board `04 · Novo container`, spec 013 US1-US3). */
@Component({
  selector: 'app-new-container-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokButton, DokCheckbox, DokDrawer, DokField, DokIcon, DokSelect, DokStatusChip, ReactiveFormsModule, RouterLink],
  providers: [NewContainerFacade],
  styleUrl: './new-container.drawer.css',
  templateUrl: './new-container.drawer.html',
})
export class NewContainerDrawer {
  readonly open = input(false);
  readonly closed = output<void>();

  protected readonly facade = inject(NewContainerFacade);
  private readonly directory = inject(EnvironmentDirectoryService);
  private readonly stageFilter = inject(StageFilterService);
  private readonly fb = inject(FormBuilder).nonNullable;
  protected readonly states = STATE_LABEL;
  protected readonly failureLabel = createFailureLabel;

  protected readonly form = this.fb.group({
    image: ['', [Validators.required]],
    name: ['', [Validators.required, Validators.pattern(NAME)]],
    engine: ['' as string],
    host: ['', [Validators.required]],
    ports: this.fb.array([this.port()]),
    environment: this.fb.array<ReturnType<NewContainerDrawer['variable']>>([]),
    volume: [''],
    cpu: [''],
    memory: [''],
    restart: ['unless-stopped' as string],
    health: [false],
    healthTest: [''],
    healthInterval: [15],
    healthRetries: [3],
    healthTimeout: [5],
    autoRemove: [false],
  });
  protected readonly submitted = signal(false);
  protected readonly advanced = signal(false);
  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  protected readonly protoOptions: SelectOption[] = [
    { value: 'tcp', label: 'tcp' },
    { value: 'udp', label: 'udp' },
  ];
  protected readonly restartOptions: SelectOption[] = [
    { value: 'unless-stopped', label: 'unless-stopped' },
    { value: 'always', label: 'always' },
    { value: 'on-failure', label: 'on-failure' },
    { value: 'no', label: 'no' },
  ];

  protected readonly engineOptions = computed<SelectOption[]>(() => {
    const seen = new Map<string, SelectOption>();
    for (const env of this.facade.hosts()) {
      const engine = env.agent?.engine;
      if (!engine || seen.has(engine)) continue;
      seen.set(engine, {
        value: engine,
        label: `${engine === 'podman' ? 'Podman' : 'Docker'} ${env.agent?.engineVersion ?? ''}`.trim(),
        caption: engine === 'podman' ? (env.agent?.engineRootless ? 'rootless' : 'rootful') : 'daemon',
        dot: engine === 'podman' ? '#fb923c' : '#60a5fa',
      });
    }
    return [...seen.values()];
  });
  protected readonly hostOptions = computed<SelectOption[]>(() => {
    const engine = this.value().engine;
    return this.facade
      .hosts()
      .filter(env => !engine || env.agent?.engine === engine)
      .map(env => {
        const reason = createIneligibility(env);
        const cpu = this.facade.cpu()[env.environmentId];
        return { value: env.environmentId, label: env.name, caption: reason ?? (cpu == null ? 'CPU indisponível' : `${Math.round(cpu)}% CPU`), disabled: reason !== null };
      });
  });
  protected readonly selectedHost = computed(() => (this.value().host ? this.directory.byId(this.value().host!) : undefined));
  protected readonly hostBlocked = computed(() => {
    const env = this.selectedHost();
    return env ? createIneligibility(env) : null;
  });
  protected readonly engine = computed<'docker' | 'podman'>(() => (this.selectedHost()?.agent?.engine ?? this.value().engine ?? 'docker') === 'podman' ? 'podman' : 'docker');
  protected readonly chips = computed<DrawerChip[]>(() => {
    const env = this.selectedHost();
    if (!env) return [];
    return [
      { tone: 'accent', icon: 'monitor', label: env.name },
      { tone: 'neutral', icon: 'box', label: `${env.agent?.engine === 'podman' ? 'Podman' : 'Docker'} ${env.agent?.engineVersion ?? ''}`.trim() },
    ];
  });
  protected readonly eyebrow = computed(() => `Novo container · ${stageLabel(this.stageFilter.stage())}`);
  protected readonly healthHelp = computed(() =>
    this.engine() === 'podman'
      ? 'A cada 15 s, 3 tentativas antes de reiniciar. A imagem precisa de curl ou wget.'
      : 'A cada 15 s, 3 tentativas antes de marcar como não saudável (o Docker não reinicia por health check). A imagem precisa de curl ou wget.',
  );
  protected readonly imageHelp = computed(() => {
    const parsed = parseImageReference((this.value().image ?? '').trim());
    if (!parsed) return 'Ex.: registry.empresa.com/pedidos/api:3.14.2';
    const lookup = this.facade.lookup();
    if (this.facade.lookupError()) return this.facade.lookupError();
    if (!lookup) return `Será usada como ${parsed.canonical}`;
    if (!lookup.present) return 'Não está no host: será baixada do registro ao criar.';
    const parts = ['Presente no host'];
    if (lookup.digest) parts.push(lookup.digest.slice(0, 19) + '…');
    if (lookup.sizeBytes) parts.push(formatBytes(lookup.sizeBytes));
    if (lookup.createdAt) parts.push(`publicada em ${formatDateTime(lookup.createdAt).split(' ')[0]}`);
    return parts.join(' · ');
  });
  protected readonly secretOptions = computed<SelectOption[]>(() =>
    (this.facade.options()?.secrets ?? []).map(s => ({ value: `${s.kind}:${s.name}`, label: s.name, caption: s.kind === 'podman_secret' ? 'segredo do Podman' : 'arquivo do host' })),
  );
  protected readonly request = computed(() => this.toRequest());
  protected readonly equivalent = computed(() => {
    const request = this.request();
    return request ? renderEquivalentCommand(request, this.engine()) : null;
  });
  protected readonly tracking = computed(() => this.facade.tracking());
  protected readonly primaryLabel = computed(() => {
    const tracking = this.tracking();
    if (!tracking || tracking.state === 'idle') return 'Criar e iniciar';
    return this.facade.running() ? null : 'Criar outro';
  });

  constructor() {
    void this.facade.init();
    effect(() => {
      // Pré-seleciona o primeiro host elegível assim que o diretório de ambientes carrega (board 04).
      const hosts = this.facade.hosts().filter(env => !createIneligibility(env));
      if (!this.open()) return;
      untracked(() => {
        if (!this.form.controls.host.value && hosts.length) {
          this.form.controls.engine.setValue(hosts[0].agent?.engine ?? '');
          this.form.controls.host.setValue(hosts[0].environmentId);
        }
      });
    });
    this.form.controls.host.valueChanges.subscribe(host => {
      if (host) void this.facade.selectHost(host);
      void this.facade.lookupImage(host || null, this.form.controls.image.value);
    });
    this.form.controls.engine.valueChanges.subscribe(engine => {
      const current = this.selectedHost();
      if (current && current.agent?.engine !== engine) {
        const next = this.facade.hosts().find(env => env.agent?.engine === engine && !createIneligibility(env));
        this.form.controls.host.setValue(next?.environmentId ?? '');
      }
    });
    this.form.controls.image.valueChanges.pipe(debounceTime(400)).subscribe(image => void this.facade.lookupImage(this.form.controls.host.value || null, image));
    this.form.valueChanges.subscribe(() => this.facade.formChanged());
    inject(DestroyRef).onDestroy(() => this.facade.dispose());
  }

  protected get ports(): FormArray<ReturnType<NewContainerDrawer['port']>> {
    return this.form.controls.ports;
  }

  protected get environment(): FormArray<ReturnType<NewContainerDrawer['variable']>> {
    return this.form.controls.environment;
  }

  protected port() {
    return this.fb.group({ hostPort: [''], containerPort: [''], protocol: ['tcp'] });
  }

  protected variable(secretRef = '') {
    return this.fb.group({ name: [''], value: [''], sensitive: [false], secretRef: [secretRef] });
  }

  protected addPort(): void {
    if (this.ports.length < (this.facade.options()?.limits.ports ?? 32)) this.ports.push(this.port());
  }

  protected addVariable(fromVault = false): void {
    if (this.environment.length >= (this.facade.options()?.limits.environment ?? 100)) return;
    this.environment.push(this.variable(fromVault ? this.secretOptions()[0]?.value ?? '' : ''));
    if (fromVault) this.environment.at(this.environment.length - 1).controls.sensitive.setValue(true);
  }

  /** Erros inline (validação local + erros por campo vindos da API). */
  protected readonly errors = computed(() => {
    const v = this.value();
    const show = this.submitted();
    const server = this.facade.serverErrors();
    const errors: Record<string, string | null> = {};
    const image = (v.image ?? '').trim();
    errors['image'] = (show || image) && !parseImageReference(image) ? 'Referência inválida: use registro/repositório:tag, sem credenciais ou espaços.' : server['image'] ? createFailureLabel(server['image'][0]) : null;
    const name = v.name ?? '';
    errors['name'] = (show || name) && !NAME.test(name)
      ? 'Use letras, números, ".", "_" ou "-" (até 63, começando por letra ou número).'
      : name && this.facade.nameInUse(name) ? 'Já existe um container com esse nome neste host.' : null;
    (v.ports ?? []).forEach((port, i) => {
      const host = Number(port.hostPort);
      const container = Number(port.containerPort);
      const empty = !port.hostPort && !port.containerPort;
      errors[`ports.${i}`] = empty ? null
        : !Number.isInteger(container) || container < 1 || container > 65535 ? 'Porta do container entre 1 e 65535.'
        : port.hostPort && (!Number.isInteger(host) || host < 0 || host > 65535) ? 'Porta do host entre 0 e 65535.'
        : host > 0 && this.facade.portInUse(host, port.protocol ?? 'tcp') ? `A porta ${host}/${port.protocol} já está publicada neste host.`
        : host > 0 && host < 1024 && this.selectedHost()?.agent?.engineRootless ? 'Podman rootless não publica portas abaixo de 1024.'
        : null;
    });
    const names = new Set<string>();
    (v.environment ?? []).forEach((variable, i) => {
      const n = variable.name ?? '';
      errors[`env.${i}`] = !n ? (show ? 'Informe o nome da variável.' : null)
        : !VARIABLE.test(n) ? 'Nome inválido (letras, números e _; não começa com número).'
        : names.has(n) ? 'Variável repetida.' : null;
      names.add(n);
    });
    const volume = (v.volume ?? '').trim();
    const match = VOLUME.exec(volume);
    errors['volume'] = volume && !match ? 'Use origem:destino[:ro|rw], ex.: vol-dados:/srv/dados:rw.'
      : match?.[1].startsWith('/') && !this.facade.options()?.bindMountsAllowed ? 'Este host aceita apenas volumes nomeados.' : null;
    const cpu = v.cpu ? Number(v.cpu.replace(',', '.')) : null;
    errors['cpu'] = cpu !== null && (Number.isNaN(cpu) || cpu < 0.01 || cpu > 64) ? 'Entre 0,01 e 64 vCPU.' : null;
    const memory = v.memory ? Number(v.memory) : null;
    errors['memory'] = memory !== null && (!Number.isInteger(memory) || memory < 6 || memory > 1024 * 1024) ? 'Entre 6 MiB e 1 TiB.' : null;
    errors['autoRemove'] = v.autoRemove && v.restart !== 'no' ? 'A remoção automática exige a política de reinício "no".' : null;
    return errors;
  });
  protected readonly blockingReason = computed(() => {
    if (!this.selectedHost()) return 'Selecione um host.';
    if (this.hostBlocked()) return `Não é possível criar neste host: ${this.hostBlocked()}.`;
    return null;
  });
  protected readonly canSubmit = computed(() => !this.blockingReason() && Object.values(this.errors()).every(e => !e) && !!this.request() && !this.facade.running());

  private toRequest(): ContainerCreationRequest | null {
    const v = this.value();
    const parsed = parseImageReference((v.image ?? '').trim());
    if (!parsed || !v.name) return null;
    const ports = (v.ports ?? [])
      .filter(p => p.containerPort)
      .map(p => ({ hostPort: Number(p.hostPort || 0), containerPort: Number(p.containerPort), protocol: (p.protocol === 'udp' ? 'udp' : 'tcp') as 'tcp' | 'udp' }));
    const environment = (v.environment ?? [])
      .filter(e => e.name)
      .map(e => {
        if (e.secretRef) {
          const [kind, ...rest] = e.secretRef.split(':');
          return { name: e.name!, secret: { kind: kind as 'podman_secret' | 'file', name: rest.join(':') }, sensitive: true };
        }
        return { name: e.name!, value: e.value ?? '', sensitive: !!e.sensitive };
      });
    const match = VOLUME.exec((v.volume ?? '').trim());
    const volumes = match ? [{ type: (match[1].startsWith('/') ? 'bind' : 'volume') as 'bind' | 'volume', source: match[1], target: match[2], readOnly: match[3] === 'ro' }] : [];
    const firstPort = ports[0]?.containerPort ?? 80;
    const restart = (v.restart ?? 'no') as 'no' | 'on-failure' | 'always' | 'unless-stopped';
    return {
      image: parsed.canonical,
      name: v.name,
      ports,
      environment,
      volumes,
      cpuLimitCores: v.cpu ? Number(v.cpu.replace(',', '.')) : null,
      memoryLimitBytes: v.memory ? Number(v.memory) * 1024 * 1024 : null,
      restartPolicy: { name: restart },
      healthcheck: v.health
        ? {
            test: v.healthTest?.trim() ? ['CMD-SHELL', v.healthTest.trim()] : healthPreset(firstPort),
            intervalSeconds: Number(v.healthInterval) || 15,
            timeoutSeconds: Number(v.healthTimeout) || 5,
            retries: Number(v.healthRetries) || 3,
            startPeriodSeconds: 0,
            onFailure: this.engine() === 'podman' ? 'restart' : 'none',
          }
        : null,
      autoRemove: !!v.autoRemove,
    };
  }

  protected async submit(): Promise<void> {
    const tracking = this.tracking();
    if (tracking && !this.facade.running() && tracking.state !== 'idle') {
      this.facade.reset();
      return;
    }
    this.submitted.set(true);
    const request = this.request();
    if (!this.canSubmit() || !request) return;
    await this.facade.submit(this.form.controls.host.value, request);
  }

  protected close(): void {
    this.closed.emit();
  }
}
