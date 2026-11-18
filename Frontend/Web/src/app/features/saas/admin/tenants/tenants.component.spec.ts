import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { TenantsComponent } from './tenants.component';
import { TenantProvisioningService } from '../../../../core/services/tenant-provisioning.service';
import { TenantDirectoryRecord, TenantUserRecord } from '../../../../core/models/tenant-provisioning.model';

describe('TenantsComponent', () => {
  let component: TenantsComponent;
  let fixture: ComponentFixture<TenantsComponent>;

  const mockTenants: TenantDirectoryRecord[] = [
    {
      id: '7d61559d-1b1c-4d2c-a8ee-dbf4f16abc57',
      code: 'TEN-7D61559D',
      name: 'ABC Company PVT LTD',
      legalName: 'ABC Company Private Limited',
      subdomain: 'ten-7d61559d',
      taxId: 'TAX-998877',
      city: 'Colombo',
      countryCode: 'LK',
      isActive: true,
      status: 'Completed',
      createdAt: '2026-10-07T08:51:00Z',
      modifiedAt: '2026-10-10T12:00:00Z',
      planName: 'Free Manufacturing',
      planCode: 'FREE',
      planIsActive: true,
      subscriptionStatus: 'Active',
      storageMode: 'SharedDatabase',
      subscriptionStartDate: '2026-10-07T08:51:00Z',
      isProvisioned: true,
      databaseName: 'ERP_DB',
      databaseServer: 'SQLSERVER',
      databaseIsActive: true,
      provisioningDate: '2026-10-07T08:55:00Z',
      configurationExists: true,
      companyEnabled: true,
      branchEnabled: true,
      warehouseZoneEnabled: true,
      warehouseBinEnabled: true,
      procurementApprovalRequired: true,
      procurementApprovalMode: 1,
      specialUomConversionEnabled: true,
      adminUsername: 'abcadmin',
      adminEmail: 'abcadmin.erp.lk',
      tenantAdminUsernames: ['abcadmin.erp.lk'],
      tenantAdminEmails: ['abcadmin.erp.lk'],
      tenantAdminCount: 1,
      tenantAdminAccountStatus: 'Active',
      totalUserCount: 12,
      activeUserCount: 12,
      inactiveUserCount: 0,
      companyCount: 2,
      branchCount: 3,
      primaryCompanyName: 'ABC Main Plant',
      primaryCompanyStatus: 'Active'
    },
    {
      id: '8ff8ec5e-a9d6-4d82-8fba-e3d9ac9c4d2c',
      code: 'TEN-8FF8EC5E',
      name: 'Apex Flow Mart Ltd',
      legalName: 'Apex Flow Mart Limited',
      subdomain: 'apxflow',
      isActive: true,
      status: 'Completed',
      createdAt: '2026-11-12T07:37:26Z',
      planName: 'Dedicated',
      planCode: 'DEDICATED',
      planIsActive: true,
      subscriptionStatus: 'Active',
      storageMode: 'SharedDatabase',
      isProvisioned: true,
      databaseName: 'DomainDB',
      databaseServer: 'SQLSERVER',
      databaseIsActive: true,
      configurationExists: true,
      companyEnabled: true,
      branchEnabled: true,
      tenantAdminUsernames: ['APEXFLOWADM', 'apxflowadmin'],
      tenantAdminEmails: ['apexflowadm@apexflow.local', 'flowadmin@apxflow.local'],
      tenantAdminCount: 2,
      tenantAdminAccountStatus: 'Active',
      totalUserCount: 8,
      activeUserCount: 8,
      inactiveUserCount: 0,
      companyCount: 1,
      branchCount: 1,
      primaryCompanyName: 'Apex Flow Mart',
      primaryCompanyStatus: 'Active'
    }
  ];

  const mockUsers: TenantUserRecord[] = [
    {
      id: 'u-1',
      tenantId: '7d61559d-1b1c-4d2c-a8ee-dbf4f16abc57',
      username: 'abcadmin.erp.lk',
      email: 'abcadmin.erp.lk',
      fullName: 'abcadmin.erp.lk',
      accountStatus: 'Active',
      roles: [{ roleId: 'r-1', roleName: 'Tenant Admin', scopes: [] }],
      createdAt: '2026-10-07T08:52:00Z',
      lastActivityAt: '2026-11-17T10:00:00Z'
    }
  ];

  const mockProvisioningService = {
    getTenantDirectory: vi.fn().mockReturnValue(of(mockTenants)),
    getTenantUsers: vi.fn().mockReturnValue(of(mockUsers)),
    getTenantDetails: vi.fn().mockReturnValue(of({ data: mockTenants[0] })),
    getTenantStatus: vi.fn().mockReturnValue(of({ data: { isProvisioned: true } }))
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TenantsComponent],
      providers: [
        provideRouter([]),
        { provide: TenantProvisioningService, useValue: mockProvisioningService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TenantsComponent);
    component = fixture.componentInstance;
  });

  it('should create tenants component and render real database-backed tenant rows', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
    expect(component.tenants().length).toBe(2);
    expect(component.metrics().total).toBe(2);
    expect(component.metrics().active).toBe(2);
    expect(component.metrics().totalUsers).toBe(20);
    expect(component.metrics().totalAdmins).toBe(3);
  });

  it('should filter tenants by global search query', () => {
    fixture.detectChanges();
    component.globalSearch.set('Flow Mart');
    expect(component.filteredTenants().length).toBe(1);
    expect(component.filteredTenants()[0].code).toBe('TEN-8FF8EC5E');

    component.globalSearch.set('NonExistentKeyword');
    expect(component.filteredTenants().length).toBe(0);

    component.clearFilters();
    expect(component.filteredTenants().length).toBe(2);
  });

  it('should open and close details dialog with comprehensive tenant profile', () => {
    fixture.detectChanges();
    component.viewTenantDetails(mockTenants[0]);
    expect(component.showDetailsModal()).toBe(true);
    expect(component.selectedTenant()?.code).toBe('TEN-7D61559D');
    expect(component.selectedTenant()?.databaseName).toBe('ERP_DB');

    component.closeDetailsModal();
    expect(component.showDetailsModal()).toBe(false);
    expect(component.selectedTenant()).toBeNull();
  });

  it('should open users dialog and fetch scoped tenant users', () => {
    fixture.detectChanges();
    component.viewTenantUsers(mockTenants[0]);
    expect(component.showUsersModal()).toBe(true);
    expect(component.usersModalTenant()?.code).toBe('TEN-7D61559D');
    expect(mockProvisioningService.getTenantUsers).toHaveBeenCalledWith('7d61559d-1b1c-4d2c-a8ee-dbf4f16abc57');
    expect(component.tenantUsers().length).toBe(1);
    expect(component.tenantUsers()[0].username).toBe('abcadmin.erp.lk');

    component.closeUsersModal();
    expect(component.showUsersModal()).toBe(false);
    expect(component.usersModalTenant()).toBeNull();
  });
});
