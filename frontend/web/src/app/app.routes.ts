import { inject, isDevMode } from '@angular/core';
import { Router, Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./features/overview/overview.page').then(m => m.OverviewPage),
  },
  {
    path: 'hosts',
    loadComponent: () => import('./features/hosts/hosts.page').then(m => m.HostsPage),
  },
  {
    path: 'containers',
    pathMatch: 'full',
    loadComponent: () => import('./features/container-list/containers.page').then(m => m.ContainersPage),
  },
  {
    path: 'containers/:environmentId/:containerId',
    loadComponent: () => import('./features/container-detail/container-detail.page').then(m => m.ContainerDetailPage),
  },
  {
    // Rota legada (spec 010 FR-011): lista unificada filtrada pelo host.
    path: 'environments/:environmentId/containers',
    redirectTo: ({ params }) => `/containers?environmentId=${encodeURIComponent(params['environmentId'])}`,
  },
  {
    path: 'imagens',
    loadComponent: () => import('./features/images/images.page').then(m => m.ImagesPage),
  },
  {
    path: 'volumes',
    loadComponent: () => import('./features/volumes/volumes.page').then(m => m.VolumesPage),
  },
  {
    path: 'alertas',
    loadComponent: () => import('./features/alerts/alerts.page').then(m => m.AlertsPage),
  },
  {
    path: 'auditoria',
    loadComponent: () => import('./features/audit/audit.page').then(m => m.AuditPage),
  },
  {
    path: 'redes',
    loadComponent: () => import('./features/networks/networks.page').then(m => m.NetworksPage),
  },
  {
    path: 'registros',
    loadComponent: () => import('./features/registries/registries.page').then(m => m.RegistriesPage),
  },
  { path: 'em-breve/registros', redirectTo: 'registros' },
  {
    path: 'pods',
    loadComponent: () => import('./features/pods/pods.page').then(m => m.PodsPage),
  },
  { path: 'em-breve/pods', redirectTo: 'pods' },
  {
    path: 'configuracoes',
    loadComponent: () => import('./features/settings/settings.page').then(m => m.SettingsPage),
  },
  { path: 'em-breve/configuracoes', redirectTo: 'configuracoes' },
  { path: 'em-breve/volumes', redirectTo: 'volumes' },
  { path: 'em-breve/redes', redirectTo: 'redes' },
  {
    path: 'em-breve/:item',
    loadComponent: () => import('./shell/pages/coming-soon.page').then(m => m.ComingSoonPage),
  },
  {
    path: 'dev/componentes',
    canMatch: [() => isDevMode() || inject(Router).createUrlTree(['/nao-encontrado'])],
    loadComponent: () => import('./features/dev-catalog/dev-catalog.page').then(m => m.DevCatalogPage),
  },
  {
    path: '**',
    loadComponent: () => import('./shell/pages/not-found.page').then(m => m.NotFoundPage),
  },
];
