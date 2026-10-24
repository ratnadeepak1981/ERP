import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { MenuItem } from 'primeng/api';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppStatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';
import { UserSecurityService } from '../../../../core/services/user-security.service';
import { RoleDefinition, PermissionItem, PermissionTemplate, CreateRoleDto, UpdateRoleDto } from '../../../../core/models/security.models';

export type RolePageMode = 'create' | 'edit' | 'view';

@Component({
  selector: 'app-role-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    ButtonModule,
    InputTextModule,
    TooltipModule,
    AppPageHeaderComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent
  ],
  templateUrl: './role-page.component.html',
  styleUrl: './role-page.component.scss'
})
export class RolePageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly securityService = inject(UserSecurityService);

  readonly mode = signal<RolePageMode>('create');
  readonly roleId = signal<string | null>(null);
  readonly role = signal<RoleDefinition | null>(null);

  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  // Form Fields
  roleName = '';
  roleDescription = '';
  assignmentMode: 'simple' | 'advanced' = 'advanced';
  selectedTemplateId = '';

  // All catalog permissions
  readonly allPermissions = signal<PermissionItem[]>([]);

  // Dual-List State
  readonly availablePermissionsList = signal<PermissionItem[]>([]);
  readonly assignedPermissionsList = signal<PermissionItem[]>([]);
  readonly selectedAvailableCodes = signal<Set<string>>(new Set<string>());
  readonly selectedAssignedCodes = signal<Set<string>>(new Set<string>());
  readonly availableSearchQuery = signal<string>('');
  readonly assignedSearchQuery = signal<string>('');

  readonly templates = signal<PermissionTemplate[]>(this.securityService.permissionTemplates);

  readonly breadcrumbs = computed<MenuItem[]>(() => [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Administration' },
    { label: 'Roles & Policies', routerLink: '/tenant-admin/roles' },
    { label: this.mode() === 'create' ? 'New Role' : (this.role()?.name || 'Role Details') }
  ]);

  readonly pageTitle = computed(() => {
    if (this.mode() === 'create') return 'Create Custom Role';
    return `Role Policy: ${this.role()?.name || 'Details'}`;
  });

  readonly isSystemRole = computed(() => !!this.role()?.isSystem);

  // Filtered available permissions
  readonly filteredAvailablePermissions = computed(() => {
    const q = this.availableSearchQuery().toLowerCase().trim();
    const list = this.availablePermissionsList();
    if (!q) return list;
    return list.filter(p =>
      p.name.toLowerCase().includes(q) ||
      p.code.toLowerCase().includes(q) ||
      p.category.toLowerCase().includes(q)
    );
  });

  // Filtered assigned permissions
  readonly filteredAssignedPermissions = computed(() => {
    const q = this.assignedSearchQuery().toLowerCase().trim();
    const list = this.assignedPermissionsList();
    if (!q) return list;
    return list.filter(p =>
      p.name.toLowerCase().includes(q) ||
      p.code.toLowerCase().includes(q) ||
      p.category.toLowerCase().includes(q)
    );
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    const isNew = this.route.snapshot.url.some(s => s.path === 'new') || !id;

    if (isNew) {
      this.mode.set('create');
      this.loadPermissionsCatalog();
    } else {
      this.mode.set('edit');
      this.roleId.set(id);
      this.loadRoleData(id!);
    }
  }

  loadPermissionsCatalog(): void {
    this.loading.set(true);
    this.securityService.getPermissions().subscribe({
      next: (perms) => {
        this.allPermissions.set(perms);
        this.initDualList(perms, []);
        this.loading.set(false);
      },
      error: (err) => {
        this.handleHttpError(err, 'Failed to load permissions catalog from server.');
        this.loading.set(false);
      }
    });
  }

  loadRoleData(id: string): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    // 1. Fetch all permissions catalog
    this.securityService.getPermissions().subscribe({
      next: (allPerms) => {
        this.allPermissions.set(allPerms);

        // 2. Fetch role details
        this.securityService.getRole(id).subscribe({
          next: (roleData) => {
            this.role.set(roleData);
            this.roleName = roleData.name || '';
            this.roleDescription = roleData.description || '';

            // Also retrieve assigned permissions via role-permission API to ensure completeness
            this.securityService.getRolePermissions(id).subscribe({
              next: (rolePermItems) => {
                const assignedCodes = new Set<string>();
                if (Array.isArray(rolePermItems)) {
                  for (const rp of rolePermItems) {
                    if (rp?.code) assignedCodes.add(rp.code.toUpperCase());
                  }
                }
                if (Array.isArray(roleData.permissions)) {
                  for (const p of roleData.permissions) {
                    if (typeof p === 'string' && p.trim()) {
                      assignedCodes.add(p.trim().toUpperCase());
                    } else if (p && typeof p === 'object' && (p as any).code) {
                      assignedCodes.add((p as any).code.toUpperCase());
                    }
                  }
                }

                this.initDualList(allPerms, Array.from(assignedCodes));
                this.loading.set(false);
              },
              error: () => {
                // Fallback to roleData.permissions if getRolePermissions fails
                const fallbackCodes = (roleData.permissions || []).map(p =>
                  typeof p === 'string' ? p.trim().toUpperCase() : ((p as any)?.code || '').toUpperCase()
                ).filter(Boolean);
                this.initDualList(allPerms, fallbackCodes);
                this.loading.set(false);
              }
            });
          },
          error: (err) => {
            this.handleHttpError(err, 'Role was not found or is inaccessible.');
            this.loading.set(false);
          }
        });
      },
      error: (err) => {
        this.handleHttpError(err, 'Failed to load permissions catalog.');
        this.loading.set(false);
      }
    });
  }

  initDualList(allPerms: PermissionItem[], assignedCodes: string[]): void {
    const assignedSet = new Set(assignedCodes.map(c => c.toUpperCase()));
    const assigned: PermissionItem[] = [];
    const available: PermissionItem[] = [];

    for (const p of allPerms) {
      if (assignedSet.has(p.code.toUpperCase())) {
        assigned.push(p);
      } else {
        available.push(p);
      }
    }

    // Preserve any assigned code that was not part of the initial catalog list
    for (const code of assignedCodes) {
      if (!allPerms.some(p => p.code.toUpperCase() === code.toUpperCase())) {
        assigned.push({
          id: code,
          category: 'Assigned Capabilities',
          code: code,
          name: code,
          description: 'Assigned capability permission'
        });
      }
    }

    this.assignedPermissionsList.set(assigned);
    this.availablePermissionsList.set(available);
    this.selectedAvailableCodes.set(new Set());
    this.selectedAssignedCodes.set(new Set());
    this.availableSearchQuery.set('');
    this.assignedSearchQuery.set('');
  }

  onRoleAssignmentModeChange(mode: 'simple' | 'advanced'): void {
    this.assignmentMode = mode;
    if (mode === 'advanced') {
      this.selectedAvailableCodes.set(new Set());
      this.selectedAssignedCodes.set(new Set());
    }
  }

  onTemplateChange(tmplId: string): void {
    const tmpl = this.templates().find(t => t.id === tmplId);
    if (!tmpl) return;

    this.initDualList(this.allPermissions(), tmpl.permissionCodes);
  }

  // --- Dual-List Item Selection ---
  toggleSelectAvailable(code: string): void {
    if (this.isSystemRole()) return;
    const current = new Set(this.selectedAvailableCodes());
    if (current.has(code)) {
      current.delete(code);
    } else {
      current.add(code);
    }
    this.selectedAvailableCodes.set(current);
  }

  toggleSelectAssigned(code: string): void {
    if (this.isSystemRole()) return;
    const current = new Set(this.selectedAssignedCodes());
    if (current.has(code)) {
      current.delete(code);
    } else {
      current.add(code);
    }
    this.selectedAssignedCodes.set(current);
  }

  // --- Dual-List Transfer Controls ---
  // Move selected from Available -> Assigned (>)
  moveSelectedToAssigned(): void {
    if (this.isSystemRole()) return;
    const toMove = this.selectedAvailableCodes();
    if (toMove.size === 0) return;

    const currentAvail = this.availablePermissionsList();
    const currentAssigned = this.assignedPermissionsList();

    const moving = currentAvail.filter(p => toMove.has(p.code));
    const remaining = currentAvail.filter(p => !toMove.has(p.code));

    this.availablePermissionsList.set(remaining);
    this.assignedPermissionsList.set([...currentAssigned, ...moving]);
    this.selectedAvailableCodes.set(new Set());
  }

  // Move ALL from Available -> Assigned (>>)
  moveAllToAssigned(): void {
    if (this.isSystemRole()) return;
    const currentAvail = this.availablePermissionsList();
    if (currentAvail.length === 0) return;

    const currentAssigned = this.assignedPermissionsList();
    this.assignedPermissionsList.set([...currentAssigned, ...currentAvail]);
    this.availablePermissionsList.set([]);
    this.selectedAvailableCodes.set(new Set());
  }

  // Move selected from Assigned -> Available (<)
  moveSelectedToAvailable(): void {
    if (this.isSystemRole()) return;
    const toMove = this.selectedAssignedCodes();
    if (toMove.size === 0) return;

    const currentAssigned = this.assignedPermissionsList();
    const currentAvail = this.availablePermissionsList();

    const moving = currentAssigned.filter(p => toMove.has(p.code));
    const remaining = currentAssigned.filter(p => !toMove.has(p.code));

    this.assignedPermissionsList.set(remaining);
    this.availablePermissionsList.set([...currentAvail, ...moving]);
    this.selectedAssignedCodes.set(new Set());
  }

  // Move ALL from Assigned -> Available (<<)
  moveAllToAvailable(): void {
    if (this.isSystemRole()) return;
    const currentAssigned = this.assignedPermissionsList();
    if (currentAssigned.length === 0) return;

    const currentAvail = this.availablePermissionsList();
    this.availablePermissionsList.set([...currentAvail, ...currentAssigned]);
    this.assignedPermissionsList.set([]);
    this.selectedAssignedCodes.set(new Set());
  }

  saveRole(): void {
    if (this.isSystemRole()) return;

    if (!this.roleName.trim()) {
      this.errorMessage.set('Please provide a valid role name.');
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const assignedCodes = this.assignedPermissionsList().map(p => p.code);
    const assignedIds = this.assignedPermissionsList().map(p => p.id);

    if (this.mode() === 'create') {
      const createDto: CreateRoleDto = {
        name: this.roleName.trim(),
        description: this.roleDescription.trim(),
        assignmentMode: this.assignmentMode,
        templateId: this.selectedTemplateId || undefined,
        permissions: assignedCodes
      };

      this.securityService.createRole(createDto).subscribe({
        next: (newRole) => {
          // Sync permissions to role in backend
          this.securityService.syncRolePermissions(newRole.id, assignedCodes, assignedIds).subscribe({
            next: (syncRes) => {
              if (syncRes && syncRes.succeeded === false) {
                this.saving.set(false);
                this.errorMessage.set(syncRes.message || 'Role created but failed to synchronize permissions.');
                return;
              }
              this.saving.set(false);
              this.successMessage.set(`Role "${newRole.name}" created and permissions synchronized successfully.`);
              setTimeout(() => this.router.navigate(['/tenant-admin/roles']), 1200);
            },
            error: (syncErr) => {
              this.saving.set(false);
              this.errorMessage.set(syncErr?.error?.message || syncErr?.message || 'Role created but failed to synchronize permissions.');
            }
          });
        },
        error: (err) => {
          this.saving.set(false);
          this.handleHttpError(err, 'Failed to create role.');
        }
      });
    } else {
      const id = this.roleId()!;
      const updateDto: UpdateRoleDto = {
        name: this.roleName.trim(),
        description: this.roleDescription.trim()
      };

      this.securityService.updateRole(id, updateDto).subscribe({
        next: () => {
          this.securityService.syncRolePermissions(id, assignedCodes, assignedIds).subscribe({
            next: (syncRes) => {
              if (syncRes && syncRes.succeeded === false) {
                this.saving.set(false);
                this.errorMessage.set(syncRes.message || 'Permission synchronization failed.');
                return;
              }
              this.saving.set(false);
              this.successMessage.set('Role definitions and assigned capabilities updated successfully.');
              this.loadRoleData(id);
            },
            error: (syncErr) => {
              this.saving.set(false);
              this.errorMessage.set(syncErr?.error?.message || syncErr?.message || 'Role metadata updated but permission synchronization failed.');
            }
          });
        },
        error: (err) => {
          this.saving.set(false);
          this.handleHttpError(err, 'Failed to update role.');
        }
      });
    }
  }

  toggleRoleStatus(): void {
    if (this.isSystemRole() || !this.roleId()) return;

    this.securityService.toggleRoleStatus(this.roleId()!).subscribe({
      next: (res) => {
        if (res) {
          const newStatus = res.isActive ? 'activated' : 'deactivated';
          this.successMessage.set(`Role has been ${newStatus} successfully.`);
          this.loadRoleData(this.roleId()!);
        }
      },
      error: (err) => {
        this.handleHttpError(err, 'Failed to toggle role status.');
      }
    });
  }

  navigateBack(): void {
    this.router.navigate(['/tenant-admin/roles']);
  }

  private handleHttpError(err: any, fallbackMessage: string): void {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 404) {
        this.errorMessage.set('Role was not found in the current tenant scope.');
      } else if (err.status === 403) {
        this.errorMessage.set('Access denied. You do not have permission to view or manage this role.');
      } else if (err.status === 401) {
        this.errorMessage.set('Authentication session expired. Please sign in again.');
      } else if (err.status === 0) {
        this.errorMessage.set('Unable to connect to backend service (port 5182). Please ensure the API is running.');
      } else {
        this.errorMessage.set(err.error?.message || err.error?.errors?.[0] || fallbackMessage);
      }
    } else {
      this.errorMessage.set(err?.message || fallbackMessage);
    }
  }
}
