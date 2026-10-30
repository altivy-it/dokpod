import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  getSettingsInstallation,
  listActionJustificationPolicies,
  listRegistryPolicies,
  listSettingsParameters,
  type JustificationPolicy,
  type RegistryPolicy,
  type SettingsInstallation,
  type SettingsParameter,
} from '../../data-access/generated/control-plane';

/** Versão do console: a mesma do backend e do `package.json` (conferida pelo teste de arquitetura da spec 022). */
export const CONSOLE_VERSION = '0.1.1'; // x-release-please-version

/** API de Configurações (spec 022), fora do bundle inicial. */
@Injectable({ providedIn: 'root' })
export class SettingsApiService {
  private readonly http = inject(HttpClient);

  async installation(): Promise<SettingsInstallation> {
    return (await getSettingsInstallation({ throwOnError: true })).data;
  }

  async parameters(): Promise<SettingsParameter[]> {
    return (await listSettingsParameters({ throwOnError: true })).data.parameters;
  }

  async justificationPolicies(): Promise<JustificationPolicy[]> {
    return (await listActionJustificationPolicies({ throwOnError: true })).data.policies;
  }

  async registryPolicies(): Promise<RegistryPolicy[]> {
    return (await listRegistryPolicies({ throwOnError: true })).data.policies;
  }

  /** Versão do BFF; nula se indisponível (a tela mostra "indisponível" sem quebrar as demais). */
  async bffVersion(): Promise<string | null> {
    try {
      return (await firstValueFrom(this.http.get<{ version: string }>('bff/version'))).version;
    } catch {
      return null;
    }
  }
}
