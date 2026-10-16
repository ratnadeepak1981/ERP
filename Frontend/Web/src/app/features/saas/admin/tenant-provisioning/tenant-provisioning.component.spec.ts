import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { TenantProvisioningComponent } from './tenant-provisioning.component';
import { TenantProvisioningService } from '../../../../core/services/tenant-provisioning.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { TenantProvisioningResult } from '../../../../core/models/tenant-provisioning.model';
import { ApiResponse } from '../../../../core/models/auth.models';

describe('TenantProvisioningComponent', () => {
  let component: TenantProvisioningComponent;
  let fixture: ComponentFixture<TenantProvisioningComponent>;
  let provisioningServiceMock: {
    provisionTenant: ReturnType<typeof vi.fn>;
    getProvisioningQueue: ReturnType<typeof vi.fn>;
    getProvisioningMetrics: ReturnType<typeof vi.fn>;
  };
  let authServiceMock: {
    roles: ReturnType<typeof vi.fn>;
    isPlatformAdmin: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    provisioningServiceMock = {
      provisionTenant: vi.fn(),
      getProvisioningQueue: vi.fn().mockReturnValue(of({
        items: [],
        endpointAvailable: false,
        message: 'Endpoint GET /api/Tenant not yet exposed'
      })),
      getProvisioningMetrics: vi.fn().mockReturnValue(of({
        pending: null,
        inProgress: null,
        failed: null,
        completed: null,
        isLive: false
      }))
    };

    authServiceMock = {
      roles: vi.fn().mockReturnValue(['Platform Admin']),
      isPlatformAdmin: vi.fn().mockReturnValue(true)
    };

    await TestBed.configureTestingModule({
      imports: [TenantProvisioningComponent],
      providers: [
        provideRouter([]),
        { provide: TenantProvisioningService, useValue: provisioningServiceMock },
        { provide: AuthService, useValue: authServiceMock },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => (key === 'tenantId' ? 'tenant-param-123' : null)
              }
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TenantProvisioningComponent);
    component = fixture.componentInstance;
  });

  it('should create component and populate tenantId from route param', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
    expect(component.selectedTenantId()).toBe('tenant-param-123');
  });

  it('should render 4 summary KPI cards showing "—" for unaggregated backend metrics', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const values = compiled.querySelectorAll('.summary-card .card-value');
    expect(values.length).toBe(4);
    values.forEach(val => {
      expect(val.textContent?.trim()).toBe('—');
    });
  });

  it('should render authentic unexposed endpoint notice in provisioning queue', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('No sample tenants or fabricated status values');
    expect(compiled.textContent).toContain('Endpoint GET /api/Tenant not yet exposed');
  });

  it('should validate required fields before executing provisioning', () => {
    component.selectedTenantId.set('');
    component.executeProvisioning();
    expect(component.validationError).toContain('Please specify a Tenant Identifier');

    // Invalid GUID format
    component.selectedTenantId.set('not-a-valid-guid');
    component.executeProvisioning();
    expect(component.validationError).toContain('Invalid Tenant Identifier format');

    // Empty username
    component.selectedTenantId.set('11111111-2222-3333-4444-555555555555');
    component.adminUserName = '';
    component.executeProvisioning();
    expect(component.validationError).toContain('Admin username is required');

    // Invalid email
    component.adminUserName = 'admin';
    component.adminEmail = 'invalid-email-format';
    component.executeProvisioning();
    expect(component.validationError).toContain('valid administrator email address');

    // Short / missing password
    component.adminEmail = 'admin@tenant.local';
    component.adminPassword = '12';
    component.executeProvisioning();
    expect(component.validationError).toContain('Admin password must be at least 3 characters');
  });

  it('should wipe sensitive password from component state immediately upon execution', () => {
    const successResult: ApiResponse<TenantProvisioningResult> = {
      succeeded: true,
      message: 'Tenant provisioned successfully.',
      data: {
        tenantId: '11111111-2222-3333-4444-555555555555',
        databaseProvisioned: true,
        configurationProvisioned: true,
        isProvisioned: true,
        storageMode: 'Shared'
      },
      errors: []
    };

    provisioningServiceMock.provisionTenant.mockReturnValue(of(successResult));

    component.selectedTenantId.set('11111111-2222-3333-4444-555555555555');
    component.adminUserName = 'admin';
    component.adminEmail = 'admin@tenant.local';
    component.adminPassword = 'SensitivePassword999!';

    component.executeProvisioning();

    // Password must be purged immediately from component memory
    expect(component.adminPassword).toBe('');
  });

  it('should update stages to COMPLETED upon successful backend provisioning', () => {
    const successResult: ApiResponse<TenantProvisioningResult> = {
      succeeded: true,
      message: 'Tenant provisioned successfully.',
      data: {
        tenantId: '11111111-2222-3333-4444-555555555555',
        databaseProvisioned: true,
        configurationProvisioned: true,
        isProvisioned: true,
        storageMode: 'Shared'
      },
      errors: []
    };

    provisioningServiceMock.provisionTenant.mockReturnValue(of(successResult));

    component.selectedTenantId.set('11111111-2222-3333-4444-555555555555');
    component.adminUserName = 'admin';
    component.adminEmail = 'admin@tenant.local';
    component.adminPassword = 'Password123!';

    component.executeProvisioning();

    expect(provisioningServiceMock.provisionTenant).toHaveBeenCalled();
    expect(component.provisioningInProgress()).toBe(false);
    expect(component.successMessage()).toContain('provisioned successfully');
    expect(component.stages().every(s => s.status === 'COMPLETED')).toBe(true);
    expect(component.lastResult()?.databaseProvisioned).toBe(true);
  });

  it('should display error message and mark stage as failed when API rejects request with 403 Forbidden', () => {
    provisioningServiceMock.provisionTenant.mockReturnValue(
      throwError(() => ({
        status: 403,
        error: { message: 'Access forbidden.' }
      }))
    );

    component.selectedTenantId.set('99999999-9999-9999-9999-999999999999');
    component.adminPassword = 'Password123!';

    component.executeProvisioning();

    expect(component.provisioningInProgress()).toBe(false);
    expect(component.errorMessage()).toContain('Access denied (HTTP 403)');
    expect(component.stages()[1].status).toBe('FAILED');
  });

  it('should display domain error message when backend returns business exception (e.g. 400)', () => {
    provisioningServiceMock.provisionTenant.mockReturnValue(
      throwError(() => ({
        status: 400,
        error: { message: 'Active subscription does not exist for the tenant.' }
      }))
    );

    component.selectedTenantId.set('99999999-9999-9999-9999-999999999999');
    component.adminPassword = 'Password123!';

    component.executeProvisioning();

    expect(component.provisioningInProgress()).toBe(false);
    expect(component.errorMessage()).toContain('Active subscription does not exist');
    expect(component.stages()[1].status).toBe('FAILED');
  });

  it('should reset stages and result panel cleanly', () => {
    component.resetStages();
    expect(component.lastResult()).toBeNull();
    expect(component.successMessage()).toBe('');
    expect(component.stages().every(s => s.status === 'PENDING')).toBe(true);
  });

  it('should manage row selection and select all eligible tenants', () => {
    const queueData: any[] = [
      { id: '11111111-1111-1111-1111-111111111111', name: 'Tenant A', code: 'TA', status: 'Pending' },
      { id: '22222222-2222-2222-2222-222222222222', name: 'Tenant B', code: 'TB', status: 'Pending' },
      { id: '33333333-3333-3333-3333-333333333333', name: 'Tenant C', code: 'TC', status: 'Completed' }
    ];

    component.queueItems.set(queueData);

    // Select individual
    component.toggleSelect(queueData[0]);
    expect(component.isSelected('11111111-1111-1111-1111-111111111111')).toBe(true);
    expect(component.selectedTenantIds().size).toBe(1);

    // Completed tenants cannot be selected
    component.toggleSelect(queueData[2]);
    expect(component.isSelected('33333333-3333-3333-3333-333333333333')).toBe(false);

    // Select all eligible
    component.toggleSelectAll({ target: { checked: true } } as any);
    expect(component.isAllEligibleSelected()).toBe(true);
    expect(component.selectedTenantIds().size).toBe(2);

    // Deselect all
    component.toggleSelectAll({ target: { checked: false } } as any);
    expect(component.selectedTenantIds().size).toBe(0);
  });

  it('should open bulk provisioning dialog and populate selected tenants', () => {
    const queueData: any[] = [
      { id: '11111111-1111-1111-1111-111111111111', name: 'Acme Corp', code: 'ACME', status: 'Pending' },
      { id: '22222222-2222-2222-2222-222222222222', name: 'Beta Ltd', code: 'BETA', status: 'Pending' }
    ];

    component.queueItems.set(queueData);
    component.selectedTenantIds.set(new Set(['11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222']));

    component.openBulkDialog();

    expect(component.showBulkDialog).toBe(true);
    expect(component.bulkItems().length).toBe(2);
    expect(component.bulkItems()[0].name).toBe('Acme Corp');
    expect(component.bulkItems()[0].adminEmail).toBe('admin@acme.local');
    expect(component.bulkSummary().total).toBe(2);
    expect(component.bulkSummary().remaining).toBe(2);
  });

  it('should execute bulk provisioning sequentially and calculate accurate progress and metrics', async () => {
    const queueData: any[] = [
      { id: '11111111-1111-1111-1111-111111111111', name: 'Acme Corp', code: 'ACME', status: 'Pending' },
      { id: '22222222-2222-2222-2222-222222222222', name: 'Beta Ltd', code: 'BETA', status: 'Pending' }
    ];

    component.queueItems.set(queueData);
    component.selectedTenantIds.set(new Set(['11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222']));
    component.openBulkDialog();

    // Set passwords for the bulk items
    component.bulkItems()[0].adminPassword = 'Password123!';
    component.bulkItems()[1].adminPassword = 'Password123!';

    // Mock first success, second failure
    provisioningServiceMock.provisionTenant.mockReturnValueOnce(of({
      succeeded: true,
      message: 'Acme provisioned',
      data: { tenantId: '11111111-1111-1111-1111-111111111111', storageMode: 'Shared' }
    })).mockReturnValueOnce(throwError(() => ({
      status: 400,
      error: { message: 'Database configuration already exists' }
    })));

    await component.executeBulkProvisioning();

    expect(component.bulkSummary().total).toBe(2);
    expect(component.bulkSummary().completed).toBe(1);
    expect(component.bulkSummary().failed).toBe(1);
    expect(component.bulkSummary().remaining).toBe(0);
    expect(component.bulkSummary().progressPercent).toBe(100);
    expect(component.bulkSummary().isFinished).toBe(true);

    expect(component.bulkItems()[0].status).toBe('Completed');
    expect(component.bulkItems()[1].status).toBe('Failed');
    expect(component.bulkItems()[1].error).toContain('Database configuration already exists');
  });

  it('should retry a failed bulk item', async () => {
    const item: any = {
      tenantId: '22222222-2222-2222-2222-222222222222',
      name: 'Beta Ltd',
      code: 'BETA',
      adminUserName: 'admin',
      adminEmail: 'admin@beta.local',
      adminPassword: 'NewSecurePassword!',
      status: 'Failed',
      error: 'Previous failure'
    };

    component.bulkItems.set([item]);
    component.bulkSummary.set({
      total: 1,
      completed: 0,
      failed: 1,
      remaining: 0,
      progressPercent: 100,
      isProcessing: false,
      isFinished: true
    });

    provisioningServiceMock.provisionTenant.mockReturnValueOnce(of({
      succeeded: true,
      message: 'Retry succeeded',
      data: { tenantId: item.tenantId, storageMode: 'Shared' }
    }));

    await component.retryBulkItem(component.bulkItems()[0]);

    expect(component.bulkItems()[0].status).toBe('Completed');
    expect(component.bulkSummary().completed).toBe(1);
    expect(component.bulkSummary().failed).toBe(0);
  });
});
