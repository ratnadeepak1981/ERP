import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { UserSecurityService } from './user-security.service';
import { CompanyService } from './company.service';

describe('UserSecurityService', () => {
  let service: UserSecurityService;
  let httpMock: HttpTestingController;
  let companyService: CompanyService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        UserSecurityService,
        CompanyService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(UserSecurityService);
    httpMock = TestBed.inject(HttpTestingController);
    companyService = TestBed.inject(CompanyService);

    vi.spyOn(companyService, 'getCompanies').mockReturnValue(of([
      { id: 'comp-1', tenantId: 't1', name: 'XYZ Manufacturing Ltd', code: 'XYZ', isActive: true }
    ]));
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should initialize with system permissions and predefined templates', () => {
    expect(service.systemPermissions.length).toBeGreaterThan(15);
    expect(service.permissionTemplates.length).toBeGreaterThan(3);
  });

  it('should retrieve roles from backend API without mock data', () => {
    service.getRoles().subscribe(roles => {
      expect(roles.length).toBe(1);
      expect(roles[0].name).toBe('Tenant Administrator');
      expect(roles[0].isSystem).toBe(true);
    });

    const req = httpMock.expectOne(r => r.url.includes('/Role') && r.method === 'GET');
    req.flush({
      data: [
        {
          id: 'role-admin',
          name: 'Tenant Administrator',
          description: 'Tenant Administrator role',
          isSystem: true,
          usersCount: 1
        }
      ]
    });
  });

  it('should create a custom role via backend API', () => {
    service.createRole({
      name: 'Custom Safety Inspector',
      description: 'Oversees safety and compliance',
      assignmentMode: 'advanced',
      permissions: ['scope.switch', 'warehouses.view', 'reports.export']
    }).subscribe(role => {
      expect(role.id).toBe('role-99');
      expect(role.name).toBe('Custom Safety Inspector');
      expect(role.permissions).toContain('warehouses.view');
    });

    const req = httpMock.expectOne(r => r.url.includes('/Role') && r.method === 'POST');
    expect(req.request.body).toEqual({
      name: 'Custom Safety Inspector',
      description: 'Oversees safety and compliance'
    });

    req.flush({
      data: {
        roleId: 'role-99',
        name: 'Custom Safety Inspector',
        description: 'Oversees safety and compliance'
      }
    });
  });

  it('should retrieve users from backend API and create new user via tenant-user endpoint', () => {
    service.getUsers().subscribe(users => {
      expect(users.length).toBe(1);
      expect(users[0].username).toBe('xyzadmin');
      expect(users[0].primaryCompanyName).toBe('XYZ Manufacturing Ltd');
    });

    const getReq = httpMock.expectOne(r => r.url.includes('/User') && r.method === 'GET');
    getReq.flush({
      data: [
        {
          id: 'u-1',
          username: 'xyzadmin',
          email: 'admin@xyz.lk',
          isActive: true,
          roles: [
            {
              roleId: 'r-admin',
              roleName: 'Tenant Administrator',
              scopes: [{ companyId: 'comp-1', branchId: 'br-1' }]
            }
          ]
        }
      ]
    });

    service.createUser(
      {
        firstName: 'Jane',
        lastName: 'Watson',
        email: 'jane.watson@xyz.lk',
        username: 'jwatson',
        accountStatus: 'Active',
        roleId: 'r-proc',
        companyId: 'comp-1',
        branchId: 'br-1'
      },
      'Procurement Officer',
      'XYZ Manufacturing Ltd',
      'Colombo Plant'
    ).subscribe(newUser => {
      expect(newUser.fullName).toBe('Jane Watson');
      expect(newUser.primaryCompanyName).toBe('XYZ Manufacturing Ltd');
      expect(newUser.id).toBe('u-created-01');
    });

    const postReq = httpMock.expectOne(r => r.url.includes('/User/tenant-user') && r.method === 'POST');
    expect(postReq.request.body.email).toBe('jane.watson@xyz.lk');
    postReq.flush({
      data: {
        userId: 'u-created-01',
        username: 'jwatson',
        roleName: 'Procurement Officer'
      }
    });
  });

  it('should toggle user account status via backend API', () => {
    service.toggleUserStatus('u-1').subscribe(res => {
      expect(res).not.toBeNull();
      expect(res?.accountStatus).toBe('Suspended');
    });

    const req = httpMock.expectOne(r => r.url.includes('/User/u-1/toggle-status') && r.method === 'PUT');
    req.flush({
      data: {
        userId: 'u-1',
        isActive: false,
        accountStatus: 'Suspended'
      }
    });
  });

  it('should retrieve user scopes and assign new user scope via backend API', () => {
    service.getUserScopes().subscribe(scopes => {
      expect(scopes.length).toBe(1);
      expect(scopes[0].companyName).toBe('XYZ Manufacturing Ltd');
      expect(scopes[0].status).toBe('Authorized');
    });

    const getReq = httpMock.expectOne(r => r.url.includes('/User/scopes') && r.method === 'GET');
    getReq.flush({
      data: [
        {
          id: 'scp-1',
          userId: 'u-1',
          userName: 'xyzadmin',
          companyId: 'comp-1',
          branchId: 'br-1',
          isActive: true
        }
      ]
    });

    service.assignUserScope(
      {
        userId: 'u-1',
        companyId: 'comp-1',
        branchId: 'br-2',
        notes: 'Central plant assignment'
      },
      'xyzadmin',
      'admin@xyz.lk',
      'XYZ Manufacturing Ltd',
      'Kandy Assembly'
    ).subscribe(assignedScope => {
      expect(assignedScope.id).toBe('scp-new-99');
      expect(assignedScope.isPrimary).toBe(false);
      expect(assignedScope.branchName).toBe('Kandy Assembly');
    });

    const postReq = httpMock.expectOne(r => r.url.includes('/User/scope') && r.method === 'POST');
    expect(postReq.request.body.branchId).toBe('br-2');
    postReq.flush({
      data: {
        scopeId: 'scp-new-99'
      }
    });
  });
});
