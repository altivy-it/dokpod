import { HttpErrorResponse, type HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { tap } from 'rxjs';
import { SessionService } from './session.service';

/** 401 em chamadas do console (exceto a própria leitura da sessão) marca a sessão como expirada. */
export const sessionExpiryInterceptor: HttpInterceptorFn = (request, next) => {
  const session = inject(SessionService);
  return next(request).pipe(
    tap({
      error: (error: unknown) => {
        if (error instanceof HttpErrorResponse && error.status === 401 && !request.url.endsWith('bff/session')) session.expire();
      },
    }),
  );
};
