export type NavSection = 'Operação' | 'Infraestrutura' | 'Governança';

export interface NavItem {
  id: string;
  label: string;
  icon: string;
  route: string;
  section: NavSection;
  /** Item só aparece quando algum ambiente concede o scope. */
  requiredScope?: string;
  countLabel?: string;
}

export interface NavGroup {
  section: NavSection;
  items: NavItem[];
}

/** Menu do console conforme os boards (Visão geral, Containers, Detalhe, Novo container). */
export const NAV_ITEMS: NavItem[] = [
  { id: 'overview', label: 'Visão geral', icon: 'gauge', route: '/', section: 'Operação' },
  { id: 'containers', label: 'Containers', icon: 'box', route: '/containers', section: 'Operação' },
  { id: 'pods', label: 'Pods', icon: 'grid', route: '/em-breve/pods', section: 'Operação' },
  { id: 'images', label: 'Imagens', icon: 'package', route: '/imagens', section: 'Operação' },
  { id: 'volumes', label: 'Volumes', icon: 'columns', route: '/volumes', section: 'Operação' },
  { id: 'networks', label: 'Redes', icon: 'link', route: '/redes', section: 'Operação' },
  { id: 'hosts', label: 'Hosts', icon: 'monitor', route: '/hosts', section: 'Infraestrutura' },
  { id: 'registries', label: 'Registros', icon: 'inbox', route: '/registros', section: 'Infraestrutura' },
  { id: 'alerts', label: 'Alertas', icon: 'alert', route: '/alertas', section: 'Governança', countLabel: 'alertas abertos' },
  { id: 'audit', label: 'Auditoria', icon: 'shield', route: '/auditoria', section: 'Governança', requiredScope: 'audit:read' },
  { id: 'settings', label: 'Configurações', icon: 'settings', route: '/em-breve/configuracoes', section: 'Governança' },
];

export const COMING_SOON: Record<string, { label: string; description: string }> = {
  pods: { label: 'Pods', description: 'Gestão de pods do Podman ficará disponível com o adapter Libpod.' },
  configuracoes: { label: 'Configurações', description: 'Preferências da instalação Dokpod.' },
};

export function groupNavigation(items: NavItem[], scopes: ReadonlySet<string>): NavGroup[] {
  const groups: NavGroup[] = [];
  for (const item of items) {
    if (item.requiredScope && !scopes.has(item.requiredScope)) continue;
    let group = groups.find(g => g.section === item.section);
    if (!group) {
      group = { section: item.section, items: [] };
      groups.push(group);
    }
    group.items.push(item);
  }
  return groups;
}

export function activeNavId(url: string): string {
  const path = url.split(/[?#]/)[0];
  if (path === '/' || path === '') return 'overview';
  if (path.startsWith('/containers')) return 'containers';
  if (path.startsWith('/imagens')) return 'images';
  if (path.startsWith('/volumes')) return 'volumes';
  if (path.startsWith('/redes')) return 'networks';
  if (path.startsWith('/hosts')) return 'hosts';
  if (path.startsWith('/registros')) return 'registries';
  if (path.startsWith('/alertas')) return 'alerts';
  if (path.startsWith('/auditoria')) return 'audit';
  const soon = /^\/em-breve\/([^/]+)/.exec(path);
  if (soon) return NAV_ITEMS.find(item => item.route === `/em-breve/${soon[1]}`)?.id ?? '';
  return '';
}
