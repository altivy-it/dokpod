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

/** Spec 027: situação de cada destino de exportação (data-model.md). */
export const AUDIT_EXPORT_STATE: Record<
  string,
  { label: string; tone: 'success' | 'warning' | 'error' | 'neutral' }
> = {
  disabled: { label: 'Desativado', tone: 'neutral' },
  ok: { label: 'Em dia', tone: 'success' },
  catching_up: { label: 'Recuperando atraso', tone: 'warning' },
  failing: { label: 'Falhando', tone: 'error' },
  gap: { label: 'Lacuna recente', tone: 'error' },
};

/** Spec 027: códigos de falha de envio, sem detalhe do servidor. */
export const AUDIT_EXPORT_ERROR: Record<string, string> = {
  tls_error: 'Falha de TLS (certificado, CA ou nome do servidor)',
  connection_refused: 'Conexão recusada ou host não encontrado',
  timeout: 'Tempo limite esgotado',
  secret_missing: 'Secret ausente ou inválido',
  rejected: 'Recusado pelo destino',
  address_blocked: 'Endereço não permitido',
};

/** Mensagem da recusa ao salvar, excluir ou testar um destino (contracts/api.md). */
export function auditExportFailureMessage(code: string | null, status: number): string {
  switch (code) {
    case 'audit_export_limit_reached':
      return 'Já existem 5 destinos. Exclua um antes de criar outro.';
    case 'audit_export_name_taken':
      return 'Já existe um destino com esse nome.';
    case 'audit_export_version_conflict':
      return 'O destino mudou desde a leitura. A lista foi atualizada; tente de novo.';
    case 'audit_export_address_blocked':
      return 'Esse host resolve para um endereço não permitido (loopback, link-local, metadados ou rede do plano de controle).';
    case 'audit_export_invalid_secret_ref':
      return 'O secret aceita só letras minúsculas, números e hífen (até 64).';
    case 'audit_export_invalid_destination':
      return 'Revise nome, host, porta e categorias.';
    case 'audit_export_not_found':
      return 'O destino não existe mais.';
    case 'rate_limited':
      return 'Limite de 5 testes por minuto atingido. Tente de novo em instantes.';
    case 'authorization_denied':
      return 'Gerenciar a exportação exige a permissão audit-export:manage.';
    default:
      if (code?.startsWith('license_'))
        return 'A licença não permite criar, editar, reativar ou testar destinos agora.';
      return status === 503
        ? 'A autorização está indisponível. Tente de novo.'
        : 'Não foi possível concluir a operação.';
  }
}
