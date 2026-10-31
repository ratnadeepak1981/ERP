import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { UserPageComponent } from './user-page.component';
import { UserSecurityService } from '../../../../core/services/user-security.service';
import { CompanyService } from '../../../../core/services/company.service';
import { UserAccount, RoleDefinition, UserScopeAssignment } from '../../../../core/models/security.models';

describe('UserPageComponent', () => {
  let component: UserPageComponent;
  let fixture: ComponentFixture<UserPageComponent>;
  let securityService: UserSecurityService;
  let companyService: CompanyService;
  let router: Router;

  const mockUser: UserAccount = {
    id: 'usr-100',
    tenantId: 'tenant-1',
    firstName: 'John',
    lastName: 'Doe',
    fullName: 'John Doe',
    email: 'johndoe@erp.lk',
    username: 'johndoe',
    phone: '+94771234567',
    accountStatus: 'Active',
    roleId: 'role-mgr',
    roleName: 'Procurement Manager',
    primaryCompanyId: 'comp-1',
    primaryCompanyName: 'Alpha Ltd',
    primaryBranchId: 'br-1',
    primaryBranchName: 'Main Plant',
    createdAt: '2026-01-01'
  };

  const mockRoles: RoleDefinition[] = [
    {
      id: 'role-mgr',
      tenantId: 'tenant-1',
      name: 'Procurement Manager',
      description: 'Manages POs and receipts',
      assignmentMode: 'advanced',
      permissions: ['INVENTORY.VIEW'],
      usersCount: 1,
      isSystem: false,
      isActive: true,
      createdAt: '2026-01-01'
    }
  ];

  const mockScopes: UserScopeAssignment[] = [
    {
      id: 'scp-1',
      userId: 'usr-100',
      userName: 'John Doe',
      userEmail: 'johndoe@erp.lk',
      companyId: 'comp-1',
      companyName: 'Alpha Ltd',
      branchId: 'br-1',
      branchName: 'Main Plant',
      isAllBranches: false,
      isPrimary: false,
      status: 'Authorized',
      grantedAt: '2026-01-01',
      grantedBy: 'Tenant Admin'
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserPageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        UserSecurityService,
        CompanyService,
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => key === 'id' ? 'usr-100' : null
              },
              url: [{ path: 'usr-100' }]
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(UserPageComponent);
    component = fixture.componentInstance;
    securityService = TestBed.inject(UserSecurityService);
    companyService = TestBed.inject(CompanyService);
    router = TestBed.inject(Router);

    vi.spyOn(securityService, 'getRoles').mockReturnValue(of(mockRoles));
    vi.spyOn(companyService, 'getCompanies').mockReturnValue(of([
      { id: 'comp-1', tenantId: 'tenant-1', name: 'Alpha Ltd', code: 'ALP', isActive: true }
    ]));
    vi.spyOn(companyService, 'getBranches').mockReturnValue(of([
      { id: 'br-1', companyId: 'comp-1', name: 'Main Plant', code: 'MP', isActive: true }
    ]));
    vi.spyOn(securityService, 'getUser').mockReturnValue(of(mockUser));
    vi.spyOn(securityService, 'getUserScopes').mockReturnValue(of(mockScopes));
  });

  it('should load user details and scopes in edit mode', () => {
    fixture.detectChanges();
    expect(component.mode()).toBe('edit');
    expect(component.user()?.id).toBe('usr-100');
    expect(component.firstName).toBe('John');
    expect(component.email).toBe('johndoe@erp.lk');
    expect(component.userScopes().length).toBe(1);
  });

  it('should toggle view mode', () => {
    fixture.detectChanges();
    expect(component.mode()).toBe('edit');
    component.mode.set('view');
    expect(component.mode()).toBe('view');
  });

  it('should save user updates via updateUser', () => {
    const updateSpy = vi.spyOn(securityService, 'updateUser').mockReturnValue(of(mockUser));
    fixture.detectChanges();
    component.mode.set('edit');
    component.username = 'johndoe_updated';
    component.saveUser();

    expect(updateSpy).toHaveBeenCalledWith('usr-100', expect.objectContaining({
      username: 'johndoe_updated'
    }));
  });

  it('should toggle user account status via toggleUserStatus', () => {
    const toggleSpy = vi.spyOn(securityService, 'toggleUserStatus').mockReturnValue(of(mockUser));
    fixture.detectChanges();
    component.toggleAccountStatus();

    expect(toggleSpy).toHaveBeenCalledWith('usr-100');
  });

  it('should revoke a user scope via revokeUserScope', () => {
    const revokeSpy = vi.spyOn(securityService, 'revokeUserScope').mockReturnValue(of(true));
    fixture.detectChanges();
    component.revokeScope(mockScopes[0]);

    expect(revokeSpy).toHaveBeenCalledWith('scp-1');
  });
});
