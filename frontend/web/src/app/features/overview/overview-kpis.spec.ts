import { describe, expect, it } from 'vitest';
import { KPI_LINKS } from './overview.page';

// Spec 015: cards da Visão geral navegáveis para a tela Containers.
describe('cards navegáveis da Visão geral', () => {
  it('leva cada card de situação ao filtro correspondente, sem ordenação residual', () => {
    expect(KPI_LINKS.running.params).toEqual({ status: ['running'], sort: null });
    expect(KPI_LINKS.stopped.params).toEqual({ status: ['stopped', 'paused'], sort: null });
    expect(KPI_LINKS.failing.params).toEqual({ status: ['failed', 'restarting'], sort: null });
  });

  it('leva CPU e memória à lista em execução ordenada por consumo decrescente', () => {
    expect(KPI_LINKS.cpu.params).toEqual({ status: ['running'], sort: '-cpu' });
    expect(KPI_LINKS.memory.params).toEqual({ status: ['running'], sort: '-memory' });
  });

  it('descreve o destino de cada card para o leitor de tela', () => {
    expect(KPI_LINKS.running.destination).toBe('Abrir a lista filtrada');
    expect(KPI_LINKS.cpu.destination).toBe('Abrir os containers ordenados por CPU');
    expect(KPI_LINKS.memory.destination).toBe('Abrir os containers ordenados por memória');
  });
});
