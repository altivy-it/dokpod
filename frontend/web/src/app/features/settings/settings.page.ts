import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { JustificationPolicyService } from '../../core/justification-policy.service';
import type {
  JustificationPolicy,
  RegistryPolicy,
  SettingsInstallation,
  SettingsParameter,
  Stage,
} from '../../data-access/generated/control-plane';
import {
  DokButton,
  DokCheckbox,
  DokEmptyState,
  DokKeyValue,
  DokStatusChip,
  DokTabs,
  formatDateTime,
  type KeyValueItem,
  type TabItem,
  ToastService,
} from '../../design-system';
import { ShellContextService } from '../../shell/shell-context.service';
import { toLoadError, type LoadError } from '../overview/overview.facade';
import { RegistriesApiService } from '../registries/registries-api.service';
import { RegistryPolicyDrawer } from '../registries/registry-policy.drawer';
import { CONSOLE_VERSION, SettingsApiService } from './settings-api.service';
import { DEPENDENCY_LABEL, DEPENDENCY_STATUS, migrationLabel, PARAMETER_GROUPS, SOURCE_LABEL, STAGES } from './settings-labels';

type View = 'politicas' | 'instalacao' | 'limites';
const VIEWS: readonly View[] = ['politicas', 'instalacao', 'limites'];

export interface StagePolicy {
  stage: Stage;
  label: string;
  justification: JustificationPolicy | null;
  registries: RegistryPolicy | null;
}

/** Junta as duas políticas por estágio (spec 022 FR-002); estágio sem uma delas mostra só a outra. */
export function stagePolicies(justifications: readonly JustificationPolicy[], registries: readonly RegistryPolicy[]): StagePolicy[] {
  return STAGES.map(stage => ({
    stage: stage.id,
    label: stage.label,
    justification: justifications.find(p => p.stage === stage.id) ?? null,
    registries: registries.find(p => p.stage === stage.id) ?? null,
  }));
}

/** "Alterada por X em data" ou o valor inicial (nunca em branco). */
export function changedBy(updatedBy: string | null | undefined, updatedAt: string | null | undefined, initial: string): string {
  return updatedBy && updatedAt ? `Alterada por ${updatedBy} em ${formatDateTime(updatedAt)}` : initial;
}

/** Governança → Configurações (spec 022): políticas reunidas, instalação e retenções e limites. */
@Component({
  selector: 'app-settings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokButton, DokCheckbox, DokEmptyState, DokKeyValue, DokStatusChip, DokTabs, FormsModule, RegistryPolicyDrawer, RouterLink],
  styleUrl: './settings.page.css',
  templateUrl: './settings.page.html',
})
export class SettingsPage {
  readonly vista = input<string | undefined>();

  private readonly api = inject(SettingsApiService);
  private readonly registriesApi = inject(RegistriesApiService);
  private readonly justification = inject(JustificationPolicyService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly view = computed<View>(() => (VIEWS.includes(this.vista() as View) ? (this.vista() as View) : 'politicas'));
  protected readonly tabs: TabItem[] = [
    { id: 'politicas', label: 'Políticas', icon: 'shield' },
    { id: 'instalacao', label: 'Instalação', icon: 'monitor' },
    { id: 'limites', label: 'Retenções e limites', icon: 'clock' },
  ];

  protected readonly loading = signal(false);
  protected readonly error = signal<LoadError | null>(null);
  protected readonly policies = signal<StagePolicy[] | null>(null);
  protected readonly installation = signal<SettingsInstallation | null>(null);
  protected readonly bffVersion = signal<string | null>(null);
  protected readonly parameters = signal<SettingsParameter[] | null>(null);
  protected readonly justificationBusy = signal<Stage | null>(null);
  protected readonly editing = signal<RegistryPolicy | null>(null);
  protected readonly saving = signal(false);

  protected readonly dependencyLabel = DEPENDENCY_LABEL;
  protected readonly dependencyStatus = DEPENDENCY_STATUS;
  protected readonly sourceLabel = SOURCE_LABEL;
  protected readonly formatDateTime = formatDateTime;
  protected readonly changedBy = changedBy;

  protected readonly versions = computed<KeyValueItem[]>(() => {
    const info = this.installation();
    if (!info) return [];
    return [
      { label: 'Plano de controle', value: info.controlPlane.version, mono: true },
      { label: 'BFF', value: this.bffVersion() ?? 'indisponível', mono: true },
      { label: 'Console', value: CONSOLE_VERSION, mono: true },
      { label: 'Protocolo do agente', value: `v${info.protocolVersion}`, mono: true },
      {
        label: 'Banco de dados',
        value: !info.database.configured
          ? 'não configurado'
          : `${migrationLabel(info.database.lastMigration)}${info.database.pendingMigrations ? ` · ${info.database.pendingMigrations} atualização(ões) pendente(s)` : ' · atualizado'}`,
      },
    ];
  });

  protected readonly parameterGroups = computed(() => {
    const parameters = this.parameters() ?? [];
    return PARAMETER_GROUPS.map(group => ({ ...group, items: parameters.filter(p => p.group === group.id) })).filter(g => g.items.length);
  });

  constructor() {
    inject(ShellContextService).set('Configurações', ['Governança', 'Configurações']);
    effect(() => {
      const view = this.view();
      untracked(() => void this.load(view));
    });
  }

  protected selectView(view: string): void {
    void this.router.navigate([], { queryParams: { vista: view === 'politicas' ? null : view }, queryParamsHandling: 'merge', replaceUrl: true });
  }

  protected reload(): void {
    void this.load(this.view());
  }

  /** Em caso de falha, devolve o marcador ao valor salvo sem recriar o cartão (mantém o foco de quem usa teclado). */
  protected async setJustification(stage: Stage, required: boolean, box?: DokCheckbox): Promise<void> {
    this.justificationBusy.set(stage);
    try {
      await this.justification.set(stage, required);
      this.toast.success(`${STAGES.find(s => s.id === stage)?.label}: justificativa ${required ? 'obrigatória' : 'opcional'} para parar, pausar e remover.`);
    } catch (error: unknown) {
      this.toast.error(this.policyError(error));
      box?.writeValue(!required);
    } finally {
      this.justificationBusy.set(null);
      await this.load('politicas');
    }
  }

  protected async saveRegistries(stage: RegistryPolicy['stage'], registries: string[]): Promise<void> {
    this.saving.set(true);
    try {
      await this.registriesApi.savePolicy(stage, registries);
      this.editing.set(null);
      this.toast.success(`Registros permitidos salvos: ${registries.length ? registries.join(', ') : 'qualquer registro'}.`);
      await this.load('politicas');
    } catch (error: unknown) {
      const problem = error as { code?: string } | null;
      this.toast.error(problem?.code === 'registry_invalid' ? 'Há registro inválido na lista.' : this.policyError(error));
    } finally {
      this.saving.set(false);
    }
  }

  private policyError(error: unknown): string {
    return (error as { code?: string } | null)?.code === 'authorization_denied'
      ? 'Alterar a política exige administração de todos os hosts do estágio.'
      : 'Não foi possível salvar a política.';
  }

  private async load(view: View): Promise<void> {
    this.loading.set(true);
    try {
      if (view === 'politicas') {
        const [justifications, registries] = await Promise.all([this.api.justificationPolicies(), this.api.registryPolicies()]);
        this.policies.set(stagePolicies(justifications, registries));
      } else if (view === 'instalacao') {
        const [installation, bff] = await Promise.all([this.api.installation(), this.api.bffVersion()]);
        this.installation.set(installation);
        this.bffVersion.set(bff);
      } else {
        this.parameters.set(await this.api.parameters());
      }
      this.error.set(null);
    } catch (error: unknown) {
      this.error.set(toLoadError(error));
    } finally {
      this.loading.set(false);
    }
  }
}
