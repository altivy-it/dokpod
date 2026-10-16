import type { SettingsDependency, SettingsParameter, Stage } from '../../data-access/generated/control-plane';

export const STAGES: { id: Stage; label: string }[] = [
  { id: 'production', label: 'Produção' },
  { id: 'staging', label: 'Homologação' },
  { id: 'development', label: 'Desenvolvimento' },
];

export const DEPENDENCY_LABEL: Record<string, string> = {
  'controlplane-api': 'Plano de controle',
  postgresql: 'Banco de dados (PostgreSQL)',
  keycloak: 'Provedor de identidade (Keycloak)',
};

export const DEPENDENCY_STATUS: Record<SettingsDependency['status'], { label: string; tone: 'success' | 'warning' | 'error' }> = {
  healthy: { label: 'Saudável', tone: 'success' },
  degraded: { label: 'Degradado', tone: 'warning' },
  unhealthy: { label: 'Indisponível', tone: 'error' },
};

export const PARAMETER_GROUPS: { id: SettingsParameter['group']; title: string; description: string }[] = [
  { id: 'retention', title: 'Retenções', description: 'Por quanto tempo cada dado fica guardado no plano de controle.' },
  { id: 'limits', title: 'Limites operacionais', description: 'Limites do produto aplicados pela API e pelo console.' },
  { id: 'terminal', title: 'Terminal', description: 'Sessões de terminal nos containers.' },
];

export const SOURCE_LABEL: Record<SettingsParameter['source'], string> = {
  default: 'Padrão do Dokpod',
  installation: 'Configurado na instalação',
};

/** Nome legível da migration EF (`202610160001_PodInventory` → `PodInventory (2026-10-16)`). */
export function migrationLabel(id: string | null | undefined): string {
  if (!id) return 'desconhecido';
  const match = /^(\d{4})(\d{2})(\d{2})\d{4}_(.+)$/.exec(id);
  return match ? `${match[4]} (${match[1]}-${match[2]}-${match[3]})` : id;
}
