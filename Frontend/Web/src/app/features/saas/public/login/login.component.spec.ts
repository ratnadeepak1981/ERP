import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { LoginComponent } from './login.component';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
  });

  it('should create login component', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h2')?.textContent).toContain('Tenant Sign In');
  });

  it('should allow quick fill of test credentials', () => {
    component.onQuickFill('admin@platform.local', '123');
    expect(component.email).toBe('admin@platform.local');
    expect(component.password).toBe('123');
  });

  it('should display error when submitting empty fields', () => {
    component.email = '';
    component.password = '';
    component.onLogin();
    expect(component.errorMessage()).toContain('Please provide both email and password');
  });

  describe('Post-login returnUrl navigation behavior', () => {
    it('should navigate to safe returnUrl when validated', () => {
      const router = TestBed.inject(Router);
      const navigateByUrlSpy = vi.spyOn(router as any, 'navigateByUrl').mockImplementation(() => Promise.resolve(true));

      component.returnUrl.set('/inventory/items?page=2#results');
      (component as any).navigateAfterLogin();

      expect(navigateByUrlSpy).toHaveBeenCalledWith('/inventory/items?page=2#results');
    });

    it('should reject unsafe returnUrl like https://evil.example and navigate to safe default route', () => {
      const router = TestBed.inject(Router);
      const navigateByUrlSpy = vi.spyOn(router as any, 'navigateByUrl').mockImplementation(() => Promise.resolve(true));
      const navigateSpy = vi.spyOn(router as any, 'navigate').mockImplementation(() => Promise.resolve(true));

      component.returnUrl.set('https://evil.example');
      (component as any).navigateAfterLogin();

      expect(navigateByUrlSpy).not.toHaveBeenCalled();
      expect(navigateSpy).toHaveBeenCalled();
    });

    it('should reject protocol-relative returnUrl //evil.example and use safe default route', () => {
      const router = TestBed.inject(Router);
      const navigateByUrlSpy = vi.spyOn(router as any, 'navigateByUrl').mockImplementation(() => Promise.resolve(true));
      const navigateSpy = vi.spyOn(router as any, 'navigate').mockImplementation(() => Promise.resolve(true));

      component.returnUrl.set('//evil.example');
      (component as any).navigateAfterLogin();

      expect(navigateByUrlSpy).not.toHaveBeenCalled();
      expect(navigateSpy).toHaveBeenCalled();
    });

    it('should reject auth loop /login returnUrl and use safe default route', () => {
      const router = TestBed.inject(Router);
      const navigateByUrlSpy = vi.spyOn(router as any, 'navigateByUrl').mockImplementation(() => Promise.resolve(true));
      const navigateSpy = vi.spyOn(router as any, 'navigate').mockImplementation(() => Promise.resolve(true));

      component.returnUrl.set('/login?returnUrl=/dashboard');
      (component as any).navigateAfterLogin();

      expect(navigateByUrlSpy).not.toHaveBeenCalled();
      expect(navigateSpy).toHaveBeenCalled();
    });
  });
});
