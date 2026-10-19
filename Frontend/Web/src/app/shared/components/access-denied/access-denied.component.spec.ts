import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router, ActivatedRoute } from '@angular/router';
import { Location } from '@angular/common';
import { AccessDeniedComponent } from './access-denied.component';
import { AuthService } from '../../../core/auth/auth.service';

describe('AccessDeniedComponent', () => {
  let component: AccessDeniedComponent;
  let fixture: ComponentFixture<AccessDeniedComponent>;
  let router: Router;
  let location: Location;
  let authService: AuthService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AccessDeniedComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParams: {
                attempted: '/app/procurement',
                required: 'PURCHASE_ORDER.VIEW'
              }
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AccessDeniedComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    location = TestBed.inject(Location);
    authService = TestBed.inject(AuthService);
  });

  it('should render with default title and message contract', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;

    expect(component).toBeTruthy();
    expect(compiled.querySelector('.denied-title')?.textContent).toBe('Access restricted');
    expect(compiled.querySelector('.denied-desc')?.textContent).toContain(
      "You don't have permission to access this page. Contact your administrator if you believe this is an error."
    );
  });

  it('should accept custom title and custom message inputs', () => {
    fixture.componentRef.setInput('title', 'Procurement Access Restricted');
    fixture.componentRef.setInput('message', 'Your user role cannot view purchase orders for this branch.');
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.denied-title')?.textContent).toBe('Procurement Access Restricted');
    expect(compiled.querySelector('.denied-desc')?.textContent).toBe(
      'Your user role cannot view purchase orders for this branch.'
    );
  });

  it('should conditionally hide Back and Home actions when configured', () => {
    fixture.componentRef.setInput('showBackAction', false);
    fixture.componentRef.setInput('showHomeAction', false);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const buttons = compiled.querySelectorAll('.denied-actions button');
    expect(buttons.length).toBe(0);
  });

  it('should display requested destination and required roles from route query params or inputs', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.querySelector('.destination-code')?.textContent).toBe('/app/procurement');
    expect(compiled.querySelector('.role-pill')?.textContent).toBe('PURCHASE_ORDER.VIEW');
  });

  it('should support inline rendering without full-page viewport constraint', () => {
    fixture.componentRef.setInput('inline', true);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.access-denied-inline')).toBeTruthy();
    expect(compiled.querySelector('.access-denied-page')).toBeFalsy();
  });

  it('should navigate to effective home URL on onHome()', () => {
    const navigateSpy = vi.spyOn(router, 'navigate');
    vi.spyOn(authService, 'isTenantStaff').mockReturnValue(true);
    fixture.detectChanges();

    component.onHome();
    expect(navigateSpy).toHaveBeenCalledWith(['/app/dashboard']);
  });

  it('should trigger back navigation on onBack()', () => {
    const backSpy = vi.spyOn(location, 'back');
    fixture.detectChanges();

    component.onBack();
    expect(backSpy).toHaveBeenCalled();
  });
});
