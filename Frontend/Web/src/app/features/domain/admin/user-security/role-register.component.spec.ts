import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { RoleRegisterComponent } from './role-register.component';
import { UserSecurityService } from '../../../../core/services/user-security.service';
import { RoleDefinition } from '../../../../core/models/security.models';

describe('RoleRegisterComponent', () => {
  let component: RoleRegisterComponent;
  let fixture: ComponentFixture<RoleRegisterComponent>;
  let securityService: UserSecurityService;
  let router: Router;

  const mockRoles: RoleDefinition[] = [
    {
      id: 'role-sys',
      tenantId: 'tenant-1',
      name: 'Tenant Admin',
      description: 'System-defined tenant administrator role',
      assignmentMode: 'advanced',
      permissions: ['ALL'],
      usersCount: 2,
      isSystem: true,
      isActive: true,
      createdAt: '2026-01-01'
    },
    {
      id: 'role-custom',
      tenantId: 'tenant-1',
      name: 'Procurement Specialist',
      description: 'Handles purchase orders and RFQs',
      assignmentMode: 'advanced',
      permissions: ['PURCHASE.VIEW', 'PURCHASE.CREATE'],
      usersCount: 5,
      isSystem: false,
      isActive: true,
      createdAt: '2026-01-02'
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RoleRegisterComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        UserSecurityService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RoleRegisterComponent);
    component = fixture.componentInstance;
    securityService = TestBed.inject(UserSecurityService);
    router = TestBed.inject(Router);

    vi.spyOn(securityService, 'getRoles').mockReturnValue(of([...mockRoles]));
  });

  it('should initialize and load roles with computed metrics', () => {
    fixture.detectChanges();
    expect(component.roles().length).toBe(2);
    expect(component.systemRolesCount()).toBe(1);
    expect(component.customRolesCount()).toBe(1);
  });

  it('should filter roles by search query', () => {
    fixture.detectChanges();
    component.searchQuery.set('Procurement');
    expect(component.displayedRoles().length).toBe(1);
    expect(component.displayedRoles()[0].name).toBe('Procurement Specialist');
  });

  it('should filter roles by type (custom vs system)', () => {
    fixture.detectChanges();
    component.typeFilter.set('custom');
    expect(component.displayedRoles().length).toBe(1);
    expect(component.displayedRoles()[0].id).toBe('role-custom');

    component.typeFilter.set('system');
    expect(component.displayedRoles().length).toBe(1);
    expect(component.displayedRoles()[0].id).toBe('role-sys');
  });

  it('should prevent deactivation of system roles', () => {
    const toggleSpy = vi.spyOn(securityService, 'toggleRoleStatus');
    fixture.detectChanges();

    const dummyEvent = new MouseEvent('click');
    component.toggleRoleStatus(mockRoles[0], dummyEvent); // System role
    expect(toggleSpy).not.toHaveBeenCalled();
  });

  it('should toggle status of custom roles via toggleRoleStatus service', () => {
    const toggleSpy = vi.spyOn(securityService, 'toggleRoleStatus').mockReturnValue(of({
      ...mockRoles[1],
      isActive: false
    }));
    fixture.detectChanges();

    const dummyEvent = new MouseEvent('click');
    component.toggleRoleStatus(mockRoles[1], dummyEvent);
    expect(toggleSpy).toHaveBeenCalledWith('role-custom');
  });

  it('should navigate to create role page', () => {
    const navSpy = vi.spyOn(router, 'navigate');
    component.navigateToCreateRole();
    expect(navSpy).toHaveBeenCalledWith(['/tenant-admin/roles/new']);
  });
});
