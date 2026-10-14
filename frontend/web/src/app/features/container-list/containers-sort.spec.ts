import { describe, expect, it } from 'vitest';
import { gridSortFromServer } from './containers.page';

// Spec 015: a ordenação da URL (cards de CPU e memória) aparece no cabeçalho do grid.
describe('ordenação da lista vinda da URL', () => {
  it('reflete consumo e data nas duas direções', () => {
    expect(gridSortFromServer('-cpu')).toEqual({ field: 'cpu', dir: 'desc' });
    expect(gridSortFromServer('cpu')).toEqual({ field: 'cpu', dir: 'asc' });
    expect(gridSortFromServer('-memory')).toEqual({ field: 'mem', dir: 'desc' });
    expect(gridSortFromServer('-createdAt')).toEqual({ field: 'created', dir: 'desc' });
  });

  it('não força cabeçalho para a ordem padrão por nome nem para chaves ambíguas', () => {
    expect(gridSortFromServer(undefined)).toBeNull();
    expect(gridSortFromServer('name')).toBeNull();
    expect(gridSortFromServer('displayStatus')).toBeNull();
  });
});
