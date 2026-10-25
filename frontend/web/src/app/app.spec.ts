import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should sign out through a top-level form POST to the BFF with the antiforgery token', async () => {
    const httpMock = TestBed.inject(HttpTestingController);

    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    app.ngOnInit();

    httpMock.expectOne('bff/session').flush({ authenticated: true, subject: 'user-1', name: 'User' });

    const submitSpy = vi.spyOn(HTMLFormElement.prototype, 'submit').mockImplementation(() => undefined);

    const logoutPromise = (app as any).logout();

    const antiforgeryRequest = httpMock.expectOne('bff/antiforgery');
    expect(antiforgeryRequest.request.method).toBe('GET');
    antiforgeryRequest.flush({ requestToken: 'csrf-token' });

    await logoutPromise;

    expect(submitSpy).toHaveBeenCalledTimes(1);
    const form = submitSpy.mock.contexts[0] as HTMLFormElement;
    expect(form.method).toBe('post');
    expect(form.getAttribute('action')).toBe('bff/logout');
    const token = form.querySelector<HTMLInputElement>('input[name="__RequestVerificationToken"]');
    expect(token?.value).toBe('csrf-token');

    httpMock.expectNone('bff/logout');
    submitSpy.mockRestore();
    form.remove();
    httpMock.verify();
  });
});
