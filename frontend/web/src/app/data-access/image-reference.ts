/**
 * Validação de referência de imagem no cliente, espelhando a gramática OCI aplicada pela API e pelo agente
 * (spec 012 D-009). A API continua sendo a autoridade; aqui só se antecipa a mensagem ao usuário.
 */
const DOMAIN = /^(?:[a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9-]*[a-zA-Z0-9])(?:\.(?:[a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9-]*[a-zA-Z0-9]))*(?::[0-9]{1,5})?$/;
const COMPONENT = /^[a-z0-9]+(?:(?:\.|_|__|-+)[a-z0-9]+)*$/;
const TAG = /^[\w][\w.-]{0,127}$/;
const DIGEST = /^sha256:[a-f0-9]{64}$|^sha512:[a-f0-9]{128}$/;

export interface ParsedImageReference {
  registry: string;
  repository: string;
  tag: string | null;
  digest: string | null;
  canonical: string;
  mutableTag: boolean;
}

export function parseImageReference(value: string): ParsedImageReference | null {
  if (!value || value.length > 255 || /[\s?#\\]/.test(value) || /[\u0000-\u001f]/.test(value)) return null;
  let rest = value;
  let digest: string | null = null;
  const at = rest.indexOf('@');
  if (at >= 0) {
    digest = rest.slice(at + 1);
    rest = rest.slice(0, at);
    if (!DIGEST.test(digest)) return null;
  }
  let tag: string | null = null;
  const colon = rest.lastIndexOf(':');
  if (colon > rest.lastIndexOf('/')) {
    tag = rest.slice(colon + 1);
    rest = rest.slice(0, colon);
    if (!TAG.test(tag)) return null;
  }
  if (!rest) return null;
  const parts = rest.split('/');
  let registry = 'docker.io';
  let path = parts;
  if (parts.length > 1 && (parts[0].includes('.') || parts[0].includes(':') || parts[0] === 'localhost')) {
    if (!DOMAIN.test(parts[0])) return null;
    registry = parts[0].toLowerCase();
    path = parts.slice(1);
  }
  if (path.length === 0 || path.some(c => !COMPONENT.test(c))) return null;
  if (['docker.io', 'index.docker.io', 'registry-1.docker.io'].includes(registry)) {
    registry = 'docker.io';
    if (path.length === 1) path = ['library', path[0]];
  }
  if (!tag && !digest) tag = 'latest';
  const repository = path.join('/');
  const canonical = `${registry}/${repository}${tag ? ':' + tag : ''}${digest ? '@' + digest : ''}`;
  if (canonical.length > 255) return null;
  return { registry, repository, tag, digest, canonical, mutableTag: digest === null };
}

/** Mensagens legíveis para os códigos de falha do pull (contrato da spec 012). */
export const PULL_FAILURE: Record<string, string> = {
  image_not_found: 'Imagem ou tag não encontrada no registro.',
  registry_unauthorized: 'O registro recusou as credenciais configuradas no host.',
  registry_unreachable: 'O registro não respondeu a partir do host.',
  registry_not_allowed: 'O registro não é permitido para o estágio deste host.',
  insufficient_storage: 'Sem espaço em disco no host.',
  platform_mismatch: 'A imagem não tem variante para a plataforma do host.',
  invalid_reference: 'A referência da imagem é inválida.',
  deadline_exceeded: 'O download excedeu o prazo.',
  expired_command: 'O comando expirou antes de ser executado.',
  engine_unavailable: 'O engine do host não respondeu.',
  agent_disconnected: 'O agente do host está desconectado.',
  not_supported: 'O agente deste host não suporta download de imagens.',
  image_pull_failed: 'O engine não conseguiu baixar a imagem.',
  authorization_denied: 'Sem permissão para baixar imagens neste host.',
  command_id_conflict: 'A chave do pedido já foi usada com outra imagem.',
};

export function pullFailureLabel(code: string | null | undefined): string {
  return (code && PULL_FAILURE[code]) ?? (code ? `Falha: ${code}` : 'Falha no download.');
}
