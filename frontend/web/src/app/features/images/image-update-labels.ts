import type { ImageUpdateCheck } from '../../data-access/generated/control-plane';
import { formatDateTime } from '../../design-system';

/** Estado do aviso de nova versão em texto (spec 025 T017); o tom acompanha o texto, nunca o substitui. */
export const IMAGE_UPDATE_STATE_LABEL: Record<ImageUpdateCheck['state'], string> = {
  update_available: 'Nova versão disponível',
  up_to_date: 'Atualizada',
  not_checkable: 'Não verificável',
  failed: 'Falha na verificação',
  container_behind: 'Versão nova no host; o container usa a anterior',
};

export const IMAGE_UPDATE_STATE_TONE: Record<string, string> = {
  [IMAGE_UPDATE_STATE_LABEL.update_available]: 'warning',
  [IMAGE_UPDATE_STATE_LABEL.up_to_date]: 'success',
  [IMAGE_UPDATE_STATE_LABEL.not_checkable]: 'neutral',
  [IMAGE_UPDATE_STATE_LABEL.failed]: 'error',
  [IMAGE_UPDATE_STATE_LABEL.container_behind]: 'info',
  'Ainda não verificada': 'neutral',
};

/** Motivos em pt-BR (tasks.md T017); nunca a mensagem do registro. */
export const IMAGE_UPDATE_REASON_LABEL: Record<NonNullable<ImageUpdateCheck['reason']>, string> = {
  digest_reference: 'Referência fixada por digest',
  local_image: 'Imagem construída ou importada no host',
  registry_not_allowed: 'Registro fora da política do estágio',
  insecure_registry: 'Registro sem TLS',
  agent_unsupported: 'Agente sem suporte à verificação',
  registry_unavailable: 'Registro indisponível',
  rate_limited: 'Limite de requisições do registro atingido',
  unauthorized: 'Credencial recusada pelo registro',
  reference_not_found: 'Referência não encontrada no registro',
  timeout: 'O registro não respondeu a tempo',
  invalid_response: 'Resposta inválida do registro',
};

/** Rótulo curto da coluna (estado); sem verificação ainda, "Ainda não verificada". */
export function imageUpdateStateLabel(check: ImageUpdateCheck | null | undefined): string {
  return check ? IMAGE_UPDATE_STATE_LABEL[check.state] : 'Ainda não verificada';
}

/** Detalhe da coluna "Verificação": vazio quando ainda não houve verificação (o estado já diz isso). */
export function imageUpdateDetail(check: ImageUpdateCheck | null | undefined): string {
  return check ? imageUpdateDescription(check) : '';
}

/**
 * Descrição completa: estado, motivo (quando há), data da verificação e a marca de desatualizado com o host
 * desconectado. Sem verificação ainda, "Ainda não verificada".
 */
export function imageUpdateDescription(check: ImageUpdateCheck | null | undefined): string {
  if (!check) return 'Ainda não verificada';
  const parts = [IMAGE_UPDATE_STATE_LABEL[check.state]];
  if (check.reason) parts.push(IMAGE_UPDATE_REASON_LABEL[check.reason]);
  parts.push(`verificada em ${formatDateTime(check.checkedAt)}`);
  if (check.stale) parts.push('host desconectado; resultado pode estar desatualizado');
  return parts.join(' · ');
}

/**
 * "Baixar nova versão" (spec 025 T020): só com `image:pull` e só em `update_available`. Em `container_behind` a versão
 * nova já está no host; falta recriar o container, o que o Dokpod não faz (FR-004).
 */
export function canPullNewVersion(check: ImageUpdateCheck | null | undefined, canPull: boolean): boolean {
  return canPull && check?.state === 'update_available';
}

/** O aviso só existe quando a API envia o campo (composição Business com a capability). */
export function hasImageUpdateCheck(item: { updateCheck?: ImageUpdateCheck | null }): boolean {
  return 'updateCheck' in item && item.updateCheck !== undefined;
}
