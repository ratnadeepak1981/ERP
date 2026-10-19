import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { UserSecurityComponent } from './user-security.component';
import { UserSecurityService } from '../../../../core/services/user-security.service';
import { CompanyService } from '../../../../core/services/company.service';
import { of } from 'rxjs';

describe('UserSecurityComponent', () => {
  let component: UserSecurityComponent;
  let fixture: ComponentFixture<UserSecurityComponent>;
  let securityService: UserSecurityService;
  let companyService: CompanyService;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [UserSecurityComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        UserSecurityService,
        CompanyService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(UserSecurityComponent);
    component = fixture.componentInstance;
    securityService = TestBed.inject(UserSecurityService);
    companyService = TestBed.inject(CompanyService);

    // Mock initial companies
    vi.spyOn(companyService, 'getCompanies').mockReturnValue(of([
      { id: 'comp-1', tenantId: 't1', name: 'Alpha Industries', code: 'ALP-01', isActive: true }
    ]));
    vi.spyOn(companyService, 'getBranches').mockReturnValue(of([
      { id: 'br-1', companyId: 'comp-1', name: 'Colombo HQ Plant', code: 'CMB-01', isActive: true },
      { id: 'br-2', companyId: 'comp-1', name: 'Kandy Assembly', code: 'KDY-01', isActive: true }
    ]));

    fixture.detectChanges();
  });

  it('should create user security component and render title and navigation', () => {
    expect(component).toBeTruthy();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h2')?.textContent).toContain('Security Governance');
    expect(compiled.querySelectorAll('.step-card').length).toBe(4);
  });

  it('should switch navigation tabs between Users, Roles, Permissions, and User Scope', () => {
    expect(component.activeTab()).toBe('users');

    component.switchTab('roles');
    expect(component.activeTab()).toBe('roles');

    component.switchTab('permissions');
    expect(component.activeTab()).toBe('permissions');

    component.switchTab('user-scope');
    expect(component.activeTab()).toBe('user-scope');
  });

  it('should validate and create a new user with company and branch scope', () => {
    component.openCreateUserModal();
    expect(component.showCreateUserModal()).toBe(true);

    // Test validation
    component.newUserFirstName = '';
    component.newUserLastName = '';
    component.saveUser();
    expect(component.formError()).toContain('First Name and Last Name are required');

    // Fill valid data
    component.newUserFirstName = 'James';
    component.newUserLastName = 'Kirk';
    component.newUserEmail = 'j.kirk@alphamfg.com';
    component.newUserUsername = 'jkirk';
    component.newUserRoleId = component.roles()[0].id;
    component.newUserCompanyId = 'comp-1';
    component.newUserBranchId = 'br-1';

    component.saveUser();
    expect(component.showCreateUserModal()).toBe(false);

    // Verify user was added to list
    const added = component.users().find(u => u.email === 'j.kirk@alphamfg.com');
    expect(added).toBeDefined();
    expect(added?.fullName).toBe('James Kirk');
  });

  it('should support Advanced Role creation using 2 lists and arrow buttons to transfer permissions', () => {
    component.openCreateRoleModal();
    expect(component.showCreateRoleModal()).toBe(true);

    // Switch to Advanced Mode
    component.onRoleAssignmentModeChange('advanced');
    expect(component.newRoleAssignmentMode).toBe('advanced');

    const initialAvailableCount = component.availablePermissionsList().length;
    expect(initialAvailableCount).toBeGreaterThan(0);
    expect(component.assignedPermissionsList().length).toBe(0);

    // Select first permission in available list
    const firstPerm = component.availablePermissionsList()[0];
    component.toggleSelectAvailable(firstPerm.code);
    expect(component.selectedAvailableCodes().has(firstPerm.code)).toBe(true);

    // Move selected to Assigned (>)
    component.moveSelectedToAssigned();
    expect(component.assignedPermissionsList().length).toBe(1);
    expect(component.assignedPermissionsList()[0].code).toBe(firstPerm.code);
    expect(component.availablePermissionsList().length).toBe(initialAvailableCount - 1);
    expect(component.selectedAvailableCodes().size).toBe(0);

    // Move All remaining to Assigned (>>)
    component.moveAllToAssigned();
    expect(component.availablePermissionsList().length).toBe(0);
    expect(component.assignedPermissionsList().length).toBe(initialAvailableCount);

    // Select a permission in assigned list and move back to Available (<)
    component.toggleSelectAssigned(firstPerm.code);
    component.moveSelectedToAvailable();
    expect(component.availablePermissionsList().length).toBe(1);
    expect(component.assignedPermissionsList().length).toBe(initialAvailableCount - 1);

    // Move All back to Available (<<)
    component.moveAllToAvailable();
    expect(component.assignedPermissionsList().length).toBe(0);
    expect(component.availablePermissionsList().length).toBe(initialAvailableCount);

    // Re-add one permission and save role
    component.toggleSelectAvailable(firstPerm.code);
    component.moveSelectedToAssigned();

    component.newRoleName = 'Logistics Inspector';
    component.newRoleDescription = 'Validates dispatch and receipt lines';
    component.saveRole();

    expect(component.showCreateRoleModal()).toBe(false);
    const createdRole = component.roles().find(r => r.name === 'Logistics Inspector');
    expect(createdRole).toBeDefined();
    expect(createdRole?.permissions).toContain(firstPerm.code);
  });

  it('should govern User Scope in dedicated view with additional company/branch assignment', () => {
    component.switchTab('user-scope');
    expect(component.activeTab()).toBe('user-scope');

    // Open Assign Scope Modal
    component.openAssignScopeModal('usr-proc-02');
    expect(component.showAssignScopeModal()).toBe(true);
    expect(component.assignScopeUserId).toBe('usr-proc-02');

    // Fill valid company and branch
    component.assignScopeCompanyId = 'comp-1';
    component.assignScopeBranchId = 'br-2';
    component.assignScopeNotes = 'Regional procurement jurisdiction';

    component.saveScopeAssignment();
    expect(component.showAssignScopeModal()).toBe(false);

    // Verify the scope assignment was added
    const userScopes = component.userScopes();
    const newScope = userScopes.find(s => s.userId === 'usr-proc-02' && s.branchId === 'br-2');
    expect(newScope).toBeDefined();
    expect(newScope?.isPrimary).toBe(false);

    // Test revoking additional scope
    if (newScope) {
      component.revokeScope(newScope);
      const remaining = component.userScopes().find(s => s.id === newScope.id);
      expect(remaining).toBeUndefined();
    }
  });

  it('should navigate from Users table action Manage Scope directly into User Scope view filtered to that user', () => {
    const user = component.users()[0];
    component.manageUserScope(user);

    expect(component.activeTab()).toBe('user-scope');
    expect(component.selectedScopeUserFilter()).toBe(user.id);
  });
});
