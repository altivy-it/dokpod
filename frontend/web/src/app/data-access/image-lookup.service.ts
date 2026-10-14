import { inject, Injectable } from '@angular/core';
import { ControlPlaneApiService } from './control-plane-api.service';
import type { ImageLookup } from './generated/control-plane';
import { parseImageReference } from './image-reference';

/**
 * Consulta "a imagem já está no host?" para a criação de container (spec 012 T012 → spec 013).
 * Resultados ficam em cache curto por host e referência canônica; nunca consulta o registro.
 */
@Injectable({ providedIn: 'root' })
export class ImageLookupService {
  private readonly api = inject(ControlPlaneApiService);
  private readonly cache = new Map<string, { at: number; value: Promise<ImageLookup> }>();
  private static readonly ttlMs = 15_000;

  lookup(environmentId: string, reference: string): Promise<ImageLookup> | null {
    const parsed = parseImageReference(reference.trim());
    if (!parsed) return null;
    const key = `${environmentId}|${parsed.canonical}`;
    const cached = this.cache.get(key);
    if (cached && Date.now() - cached.at < ImageLookupService.ttlMs) return cached.value;
    const value = this.api.lookupImage(environmentId, parsed.canonical);
    value.catch(() => this.cache.delete(key));
    this.cache.set(key, { at: Date.now(), value });
    return value;
  }

  invalidate(environmentId?: string): void {
    for (const key of [...this.cache.keys()]) {
      if (!environmentId || key.startsWith(`${environmentId}|`)) this.cache.delete(key);
    }
  }
}
