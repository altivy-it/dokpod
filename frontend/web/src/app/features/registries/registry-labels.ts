import type { RegistryOrigin } from '../../data-access/generated/control-plane';

export const ORIGIN_LABEL: Record<RegistryOrigin, string> = {
  images: 'Imagens',
  mirror: 'Espelho',
  insecure: 'Inseguro',
  search: 'De busca',
  blocked: 'Bloqueado',
  credential: 'Credencial',
};

export const POLICY_STAGE_LABEL: Record<'production' | 'staging' | 'development' | 'none', string> = {
  production: 'Produção',
  staging: 'Homologação',
  development: 'Desenvolvimento',
  none: 'Sem estágio',
};
