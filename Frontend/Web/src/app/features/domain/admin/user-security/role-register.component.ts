import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { MenuItem } from 'primeng/api';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppStatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { UserSecurityService } from '../../../../core/services/user-security.service';
import { RoleDefinition } from '../../../../core/models/security.models';

@Component({
  selector: 'app-role-register',
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
    AppLoadingStateComponent,
    AppErrorStateComponent
  ],
  templateUrl: './role-register.component.html',
  styleUrl: './role-register.component.scss'
})
export class RoleRegisterComponent implements OnInit {
  private readonly router = inject(Router);
  readonly securityService = inject(UserSecurityService);

  readonly breadcrumbs: MenuItem[] = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Administration' },
    { label: 'Roles & Policies' }
  ];

  readonly loading = signal<boolean>(true);
  readonly error = signal<string | null>(null);
  readonly actionMessage = signal<{ severity: 'success' | 'danger' | 'info'; text: string } | null>(null);

  readonly roles = signal<RoleDefinition[]>([]);
  readonly searchQuery = signal<string>('');
  readonly typeFilter = signal<'all' | 'custom' | 'system'>('all');

  readonly systemRolesCount = computed(() => this.roles().filter(r => r.isSystem).length);
  readonly customRolesCount = computed(() => this.roles().filter(r => !r.isSystem).length);

  readonly displayedRoles = computed(() => {
    const q = this.searchQuery().toLowerCase().trim();
    const type = this.typeFilter();
    let list = this.roles();

    if (type === 'system') {
      list = list.filter(r => r.isSystem);
    } else if (type === 'custom') {
      list = list.filter(r => !r.isSystem);
    }

    if (!q) return list;

    return list.filter(r =>
      (r.name || '').toLowerCase().includes(q) ||
      (r.description || '').toLowerCase().includes(q)
    );
  });

  ngOnInit(): void {
    this.loadRoles();
  }

  loadRoles(): void {
    this.loading.set(true);
    this.error.set(null);

    this.securityService.getRoles().subscribe({
      next: data => {
        this.roles.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load capability roles from server.');
        this.loading.set(false);
      }
    });
  }

  navigateToCreateRole(): void {
    this.router.navigate(['/tenant-admin/roles/new']);
  }

  navigateToRoleDetails(role: RoleDefinition): void {
    this.router.navigate(['/tenant-admin/roles', role.id]);
  }

  toggleRoleStatus(role: RoleDefinition, event: Event): void {
    event.stopPropagation();
    if (role.isSystem) return;

    this.securityService.toggleRoleStatus(role.id).subscribe({
      next: updated => {
        const nextStatus = role.isActive !== false ? 'deactivated' : 'activated';
        this.actionMessage.set({
          severity: 'success',
          text: `Role "${role.name}" has been ${nextStatus} successfully.`
        });
        this.loadRoles();
      },
      error: err => {
        this.actionMessage.set({
          severity: 'danger',
          text: err?.error?.message || `Failed to update status for role "${role.name}".`
        });
      }
    });
  }
}
