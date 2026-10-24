import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { UserRegisterComponent } from './user-register.component';
import { UserSecurityService } from '../../../../core/services/user-security.service';
import { UserAccount } from '../../../../core/models/security.models';

describe('UserRegisterComponent', () => {
  let component: UserRegisterComponent;
  let fixture: ComponentFixture<UserRegisterComponent>;
  let securityService: UserSecurityService;
  let router: Router;

  const mockUsers: UserAccount[] = [
    {
      id: 'usr-1',
      tenantId: 'tenant-1',
      firstName: 'Alice',
      lastName: 'Smith',
      fullName: 'Alice Smith',
      email: 'alice@example.com',
      username: 'asmith',
      accountStatus: 'Active',
      roleId: 'role-mgr',
      roleName: 'Procurement Manager',
      primaryCompanyId: 'comp-1',
      primaryCompanyName: 'Alpha Ltd',
      primaryBranchId: 'br-1',
      primaryBranchName: 'Main Plant',
      createdAt: '2026-01-01'
    },
    {
      id: 'usr-2',
      tenantId: 'tenant-1',
      firstName: 'Bob',
      lastName: 'Jones',
      fullName: 'Bob Jones',
      email: 'bob@example.com',
      username: 'bjones',
      accountStatus: 'Suspended',
      roleId: 'role-usr',
      roleName: 'Store Keeper',
      primaryCompanyId: 'comp-1',
      primaryCompanyName: 'Alpha Ltd',
      primaryBranchId: 'br-2',
      primaryBranchName: 'West Warehouse',
      createdAt: '2026-01-02'
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserRegisterComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        UserSecurityService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(UserRegisterComponent);
    component = fixture.componentInstance;
    securityService = TestBed.inject(UserSecurityService);
    router = TestBed.inject(Router);

    vi.spyOn(securityService, 'getUsers').mockReturnValue(of([...mockUsers]));
  });

  it('should initialize and load users', () => {
    fixture.detectChanges();
    expect(component.users().length).toBe(2);
    expect(component.activeUsersCount()).toBe(1);
    expect(component.suspendedUsersCount()).toBe(1);
  });

  it('should filter users by search query', () => {
    fixture.detectChanges();
    component.searchQuery.set('Alice');
    expect(component.displayedUsers().length).toBe(1);
    expect(component.displayedUsers()[0].username).toBe('asmith');

    component.searchQuery.set('Store Keeper');
    expect(component.displayedUsers().length).toBe(1);
    expect(component.displayedUsers()[0].username).toBe('bjones');
  });

  it('should filter users by status pill', () => {
    fixture.detectChanges();
    component.statusFilter.set('Active');
    expect(component.displayedUsers().length).toBe(1);
    expect(component.displayedUsers()[0].id).toBe('usr-1');

    component.statusFilter.set('Suspended');
    expect(component.displayedUsers().length).toBe(1);
    expect(component.displayedUsers()[0].id).toBe('usr-2');
  });

  it('should navigate to user creation page', () => {
    const navigateSpy = vi.spyOn(router, 'navigate');
    component.navigateToCreateUser();
    expect(navigateSpy).toHaveBeenCalledWith(['/tenant-admin/users/new']);
  });

  it('should navigate to user details page', () => {
    const navigateSpy = vi.spyOn(router, 'navigate');
    component.navigateToUserDetails(mockUsers[0]);
    expect(navigateSpy).toHaveBeenCalledWith(['/tenant-admin/users', 'usr-1']);
  });

  it('should toggle user account status via toggleUserStatus service', () => {
    const toggleSpy = vi.spyOn(securityService, 'toggleUserStatus').mockReturnValue(of(mockUsers[0]));
    fixture.detectChanges();

    component.toggleUserStatus(mockUsers[0]);
    expect(toggleSpy).toHaveBeenCalledWith('usr-1');
  });
});
