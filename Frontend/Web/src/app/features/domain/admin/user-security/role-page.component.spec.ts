import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { RolePageComponent } from './role-page.component';
import { UserSecurityService } from '../../../../core/services/user-security.service';
import { RoleDefinition, PermissionItem } from '../../../../core/models/security.models';

describe('RolePageComponent', () => {
  let component: RolePageComponent;
  let fixture: ComponentFixture<RolePageComponent>;
  let securityService: UserSecurityService;
  let router: Router;

  const mockPermissions: PermissionItem[] = [
    { id: 'p1', code: 'PRODUCT.VIEW', name: 'View Products', category: 'Master Data', description: 'Read products' },
    { id: 'p2', code: 'PRODUCT.CREATE', name: 'Create Products', category: 'Master Data', description: 'Create products' },
    { id: 'p3', code: 'INVENTORY.VIEW', name: 'View Stock', category: 'Inventory', description: 'Read inventory' }
  ];

  const mockRoleWithPerms: RoleDefinition = {
    id: 'role-custom-1',
    tenantId: 'tenant-1',
    name: 'Inventory Officer',
    description: 'Manages warehouses',
    assignmentMode: 'advanced',
    permissions: ['INVENTORY.VIEW'],
    usersCount: 2,
    isSystem: false,
    isActive: true,
    createdAt: '2026-01-01'
  };

  const mockProcurementManagerRole: RoleDefinition = {
    id: '7DD54B51-5B2E-4EB7-A1CB-87B2BFE0C12C',
    tenantId: 'DDCA51A5-246F-4EAB-A0A0-7D51C6D52896',
    name: 'Procurment Manager',
    description: 'Procurment Manager',
    assignmentMode: 'advanced',
    permissions: [], // 0 permissions initially in DB
    usersCount: 1,
    isSystem: false,
    isActive: true,
    createdAt: '2026-10-22'
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RolePageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        UserSecurityService,
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => key === 'id' ? 'role-custom-1' : null
              },
              url: [{ path: 'role-custom-1' }]
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RolePageComponent);
    component = fixture.componentInstance;
    securityService = TestBed.inject(UserSecurityService);
    router = TestBed.inject(Router);

    vi.spyOn(securityService, 'getPermissions').mockReturnValue(of(mockPermissions));
    vi.spyOn(securityService, 'getRole').mockReturnValue(of(mockRoleWithPerms));
    vi.spyOn(securityService, 'getRolePermissions').mockReturnValue(of([
      { id: 'p3', code: 'INVENTORY.VIEW', name: 'View Stock', category: 'Inventory', description: 'Read inventory' }
    ]));
  });

  it('should initialize and load role details with permissions into Dual-List', () => {
    fixture.detectChanges();
    expect(component.mode()).toBe('edit');
    expect(component.roleName).toBe('Inventory Officer');

    // Dual-list split check
    expect(component.assignedPermissionsList().length).toBe(1);
    expect(component.assignedPermissionsList()[0].code).toBe('INVENTORY.VIEW');

    expect(component.availablePermissionsList().length).toBe(2);
    expect(component.availablePermissionsList().map(p => p.code)).toEqual(['PRODUCT.VIEW', 'PRODUCT.CREATE']);
  });

  it('should load Procurement Manager role with zero permissions into Available list', () => {
    vi.spyOn(securityService, 'getRole').mockReturnValue(of(mockProcurementManagerRole));
    vi.spyOn(securityService, 'getRolePermissions').mockReturnValue(of([]));

    component.loadRoleData('7DD54B51-5B2E-4EB7-A1CB-87B2BFE0C12C');

    expect(component.roleName).toBe('Procurment Manager');
    expect(component.assignedPermissionsList().length).toBe(0);
    expect(component.availablePermissionsList().length).toBe(3);
  });

  it('should move selected permission from Available to Assigned (>)', () => {
    fixture.detectChanges();
    expect(component.assignedPermissionsList().length).toBe(1);

    component.toggleSelectAvailable('PRODUCT.VIEW');
    expect(component.selectedAvailableCodes().has('PRODUCT.VIEW')).toBe(true);

    component.moveSelectedToAssigned();
    expect(component.assignedPermissionsList().length).toBe(2);
    expect(component.assignedPermissionsList().map(p => p.code)).toContain('PRODUCT.VIEW');
    expect(component.availablePermissionsList().map(p => p.code)).not.toContain('PRODUCT.VIEW');
    expect(component.selectedAvailableCodes().size).toBe(0);
  });

  it('should move all permissions from Available to Assigned (>>)', () => {
    fixture.detectChanges();
    component.moveAllToAssigned();

    expect(component.assignedPermissionsList().length).toBe(3);
    expect(component.availablePermissionsList().length).toBe(0);
  });

  it('should move selected permission from Assigned to Available (<)', () => {
    fixture.detectChanges();
    component.toggleSelectAssigned('INVENTORY.VIEW');
    expect(component.selectedAssignedCodes().has('INVENTORY.VIEW')).toBe(true);

    component.moveSelectedToAvailable();
    expect(component.assignedPermissionsList().length).toBe(0);
    expect(component.availablePermissionsList().length).toBe(3);
    expect(component.selectedAssignedCodes().size).toBe(0);
  });

  it('should move all permissions from Assigned to Available (<<)', () => {
    fixture.detectChanges();
    component.moveAllToAvailable();

    expect(component.assignedPermissionsList().length).toBe(0);
    expect(component.availablePermissionsList().length).toBe(3);
  });

  it('should save role updates via updateRole and syncRolePermissions with dual-list contents', () => {
    const updateSpy = vi.spyOn(securityService, 'updateRole').mockReturnValue(of(mockRoleWithPerms));
    const syncSpy = vi.spyOn(securityService, 'syncRolePermissions').mockReturnValue(of({
      succeeded: true,
      data: { roleId: 'role-custom-1', assignedCount: 1, permissions: [] }
    } as any));

    fixture.detectChanges();
    component.roleName = 'Inventory Senior Officer';
    component.saveRole();

    expect(updateSpy).toHaveBeenCalledWith('role-custom-1', expect.objectContaining({
      name: 'Inventory Senior Officer'
    }));
    expect(syncSpy).toHaveBeenCalledWith('role-custom-1', ['INVENTORY.VIEW'], ['p3']);
    expect(component.successMessage()).toBe('Role definitions and assigned capabilities updated successfully.');
  });

  it('should display error message and not false success when syncRolePermissions fails', () => {
    vi.spyOn(securityService, 'updateRole').mockReturnValue(of(mockRoleWithPerms));
    vi.spyOn(securityService, 'syncRolePermissions').mockReturnValue(throwError(() => ({
      error: { message: 'Tenant administrators cannot assign platform-only permissions.' }
    })));

    fixture.detectChanges();
    component.saveRole();

    expect(component.errorMessage()).toBe('Tenant administrators cannot assign platform-only permissions.');
    expect(component.successMessage()).toBeNull();
  });

  it('should handle saving with an empty permission list (clearing all assignments)', () => {
    vi.spyOn(securityService, 'updateRole').mockReturnValue(of(mockRoleWithPerms));
    const syncSpy = vi.spyOn(securityService, 'syncRolePermissions').mockReturnValue(of({
      succeeded: true,
      data: { roleId: 'role-custom-1', assignedCount: 0, permissions: [] }
    } as any));

    fixture.detectChanges();
    component.moveAllToAvailable();
    expect(component.assignedPermissionsList().length).toBe(0);

    component.saveRole();
    expect(syncSpy).toHaveBeenCalledWith('role-custom-1', [], []);
  });

  it('should load assigned permissions when role permissions are provided as object array', () => {
    const roleWithObjectPerms: any = {
      ...mockProcurementManagerRole,
      permissions: [
        { id: 'po1', code: 'PURCHASE_ORDER.VIEW', name: 'View PO' },
        { id: 'po2', code: 'PURCHASE_ORDER.CREATE', name: 'Create PO' }
      ]
    };
    vi.spyOn(securityService, 'getRole').mockReturnValue(of(roleWithObjectPerms));
    vi.spyOn(securityService, 'getRolePermissions').mockReturnValue(of([]));

    component.loadRoleData('7DD54B51-5B2E-4EB7-A1CB-87B2BFE0C12C');

    expect(component.assignedPermissionsList().length).toBe(2);
    expect(component.assignedPermissionsList().map(p => p.code)).toEqual(['PURCHASE_ORDER.VIEW', 'PURCHASE_ORDER.CREATE']);
  });

  it('should preserve and display HTTP 404 error when role does not exist', () => {
    const httpError = new HttpErrorResponse({ status: 404, statusText: 'Not Found' });
    vi.spyOn(securityService, 'getRole').mockReturnValue(throwError(() => httpError));

    component.loadRoleData('missing-role-id');
    expect(component.errorMessage()).toBe('Role was not found in the current tenant scope.');
  });

  it('should preserve and display HTTP 403 error when access is denied', () => {
    const httpError = new HttpErrorResponse({ status: 403, statusText: 'Forbidden' });
    vi.spyOn(securityService, 'getRole').mockReturnValue(throwError(() => httpError));

    component.loadRoleData('unauthorized-role-id');
    expect(component.errorMessage()).toBe('Access denied. You do not have permission to view or manage this role.');
  });
});
