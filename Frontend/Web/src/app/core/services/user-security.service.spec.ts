import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { UserSecurityService } from './user-security.service';
import { CompanyService } from './company.service';

describe('UserSecurityService', () => {
  let service: UserSecurityService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        UserSecurityService,
        CompanyService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(UserSecurityService);
  });

  it('should initialize with system permissions and predefined templates', () => {
    expect(service.systemPermissions.length).toBeGreaterThan(15);
    expect(service.permissionTemplates.length).toBeGreaterThan(3);
  });

  it('should return initial seeded roles', () => {
    service.getRoles().subscribe(roles => {
      expect(roles.length).toBeGreaterThan(0);
      const adminRole = roles.find(r => r.name === 'Tenant Administrator');
      expect(adminRole).toBeDefined();
    });
  });

  it('should create a custom role with permissions', () => {
    service.createRole({
      name: 'Custom Safety Inspector',
      description: 'Oversees safety and compliance',
      assignmentMode: 'advanced',
      permissions: ['scope.switch', 'warehouses.view', 'reports.export']
    }).subscribe(role => {
      expect(role.id).toBeDefined();
      expect(role.name).toBe('Custom Safety Inspector');
      expect(role.permissions).toContain('warehouses.view');
    });
  });

  it('should return initial seeded users and create new users with primary scope', () => {
    service.getUsers().subscribe(users => {
      expect(users.length).toBeGreaterThan(0);
    });

    service.createUser(
      {
        firstName: 'Jane',
        lastName: 'Watson',
        email: 'jane.watson@alphamfg.com',
        username: 'jwatson',
        accountStatus: 'Active',
        roleId: 'role-procurement',
        companyId: 'comp_test',
        branchId: 'br_test'
      },
      'Procurement Officer',
      'Test Company',
      'Test Branch'
    ).subscribe(newUser => {
      expect(newUser.fullName).toBe('Jane Watson');
      expect(newUser.primaryCompanyName).toBe('Test Company');

      // Verify that primary scope was automatically registered
      service.getUserScopes(newUser.id).subscribe(scopes => {
        expect(scopes.length).toBe(1);
        expect(scopes[0].isPrimary).toBe(true);
      });
    });
  });

  it('should toggle user account status between Active and Suspended', () => {
    service.getUsers().subscribe(users => {
      const firstUser = users[0];
      const initialStatus = firstUser.accountStatus;

      service.toggleUserStatus(firstUser.id).subscribe(updated => {
        expect(updated).not.toBeNull();
        expect(updated?.accountStatus).not.toBe(initialStatus);
      });
    });
  });

  it('should assign and revoke additional company/branch scope', () => {
    service.assignUserScope(
      {
        userId: 'usr-proc-02',
        companyId: 'comp_extra',
        branchId: 'br_extra',
        notes: 'Temporary plant assignment'
      },
      'Sarah Connor',
      's.connor@alphamfg.com',
      'Extra Company Ltd',
      'Extra Branch Plant'
    ).subscribe(assignedScope => {
      expect(assignedScope.id).toBeDefined();
      expect(assignedScope.isPrimary).toBe(false);

      // Verify revoke
      service.revokeUserScope(assignedScope.id).subscribe(success => {
        expect(success).toBe(true);
      });
    });
  });
});
