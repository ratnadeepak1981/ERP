import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { CustomerDashboardComponent } from './customer-dashboard.component';
import { AuthService } from '../../../../core/auth/auth.service';
import { TenantContextService } from '../../../../core/services/tenant-context.service';

describe('CustomerDashboardComponent', () => {
  let component: CustomerDashboardComponent;
  let fixture: ComponentFixture<CustomerDashboardComponent>;
  let mockAuthService: any;
  let mockTenantContext: any;
  let isCompanySignal: ReturnType<typeof signal<boolean>>;
  let isIndividualSignal: ReturnType<typeof signal<boolean>>;

  beforeEach(async () => {
    isCompanySignal = signal(false);
    isIndividualSignal = signal(true);

    mockAuthService = {
      currentUser: signal({
        email: 'retail.buyer@example.com',
        roles: ['CUSTOMER_INDIVIDUAL']
      }),
      isCustomerIndividual: isIndividualSignal,
      isCustomerCompany: isCompanySignal
    };

    mockTenantContext = {
      currentTenant: signal({
        name: 'Nova Dynamics Ltd',
        subdomain: 'nova'
      }),
      subdomain: signal('nova')
    };

    await TestBed.configureTestingModule({
      imports: [CustomerDashboardComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: mockAuthService },
        { provide: TenantContextService, useValue: mockTenantContext }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create CustomerDashboardComponent and render retail badge', () => {
    expect(component).toBeTruthy();
    expect(component.isOrganisation()).toBe(false);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.account-badge')?.textContent).toContain('Individual Retail Account');
    expect(compiled.querySelector('h1')?.textContent).toContain('retail.buyer@example.com');
  });

  it('should render corporate B2B badge when user is company customer', () => {
    isCompanySignal.set(true);
    isIndividualSignal.set(false);

    fixture.detectChanges();

    expect(component.isOrganisation()).toBe(true);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.account-badge')?.textContent).toContain('Corporate B2B Account');
  });

  it('should render recent orders table with data', () => {
    expect(component.recentOrders().length).toBe(3);
    const compiled = fixture.nativeElement as HTMLElement;
    const tableRows = compiled.querySelectorAll('.erp-table tbody tr');
    expect(tableRows.length).toBe(3);
    expect(tableRows[0].textContent).toContain('SO-2026-0891');
  });
});

