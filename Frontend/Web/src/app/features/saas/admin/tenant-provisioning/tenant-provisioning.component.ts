import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { DialogModule } from 'primeng/dialog';
import { SharedModule } from 'primeng/api';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { LoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';
import { StatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { TenantProvisioningService } from '../../../../core/services/tenant-provisioning.service';
import {
  ProvisioningQueueItem,
  ProvisioningMetrics,
  TenantProvisioningRequest,
  TenantProvisioningResult,
  BulkTenantItem,
  BulkProvisioningSummary
} from '../../../../core/models/tenant-provisioning.model';
import { AuthService } from '../../../../core/auth/auth.service';
import { firstValueFrom } from 'rxjs';

export interface ProvisioningStage {
  step: number;
  name: string;
  description: string;
  status: 'PENDING' | 'IN_PROGRESS' | 'COMPLETED' | 'FAILED';
}

@Component({
  selector: 'app-tenant-provisioning',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    ButtonModule,
    InputTextModule,
    PasswordModule,
    DialogModule,
    SharedModule,
    PageHeaderComponent,
    LoadingStateComponent,
    StatusBadgeComponent
  ],
  templateUrl: './tenant-provisioning.component.html',
  styleUrls: ['./tenant-provisioning.component.scss']
})
export class TenantProvisioningComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly provisioningService = inject(TenantProvisioningService);
  readonly authService = inject(AuthService);

  readonly loading = signal<boolean>(false);
  readonly provisioningInProgress = signal<boolean>(false);
  readonly errorMessage = signal<string>('');
  readonly successMessage = signal<string>('');

  readonly metrics = signal<ProvisioningMetrics>({
    pending: null,
    inProgress: null,
    failed: null,
    completed: null,
    isLive: false
  });

  readonly queueItems = signal<ProvisioningQueueItem[]>([]);
  readonly endpointAvailable = signal<boolean>(true);
  readonly endpointNotice = signal<string>('');

  // Row selection for individual & bulk operations
  readonly selectedTenantIds = signal<Set<string>>(new Set<string>());

  // Search, Filter & Sort State for Data Grid
  readonly searchQuery = signal<string>('');
  readonly statusFilter = signal<string>('ALL');
  readonly sortField = signal<string>('name');
  readonly sortAsc = signal<boolean>(true);

  // Target tenant from route parameter or input for single provisioning
  readonly selectedTenantId = signal<string>('');

  readonly selectedTenantName = computed(() => {
    const item = this.queueItems().find(i => i.id === this.selectedTenantId());
    return item?.name || 'Target Organization';
  });

  readonly selectedTenantCode = computed(() => {
    const item = this.queueItems().find(i => i.id === this.selectedTenantId());
    return item?.code || '';
  });

  readonly selectedTenantPlan = computed(() => {
    const item = this.queueItems().find(i => i.id === this.selectedTenantId());
    return item?.planName || 'Manufacturing Cloud';
  });

  // Filtered & Sorted Queue Items
  readonly filteredQueueItems = computed(() => {
    let items = [...this.queueItems()];
    const query = this.searchQuery().trim().toLowerCase();
    const status = this.statusFilter();
    const field = this.sortField();
    const asc = this.sortAsc();

    if (status !== 'ALL') {
      items = items.filter(i => i.status.toUpperCase() === status.toUpperCase());
    }

    if (query) {
      items = items.filter(i =>
        i.name.toLowerCase().includes(query) ||
        i.code.toLowerCase().includes(query) ||
        (i.planName && i.planName.toLowerCase().includes(query)) ||
        i.storageMode.toLowerCase().includes(query) ||
        i.status.toLowerCase().includes(query)
      );
    }

    items.sort((a, b) => {
      let valA: any = a[field as keyof ProvisioningQueueItem] ?? '';
      let valB: any = b[field as keyof ProvisioningQueueItem] ?? '';
      if (typeof valA === 'string') valA = valA.toLowerCase();
      if (typeof valB === 'string') valB = valB.toLowerCase();
      if (valA < valB) return asc ? -1 : 1;
      if (valA > valB) return asc ? 1 : -1;
      return 0;
    });

    return items;
  });

  setSort(field: string): void {
    if (this.sortField() === field) {
      this.sortAsc.set(!this.sortAsc());
    } else {
      this.sortField.set(field);
      this.sortAsc.set(true);
    }
  }

  // Single-Tenant Provisioning Dialog Form State
  showProvisionDialog = false;
  adminUserName = 'admin';
  adminEmail = 'admin@tenant.local';
  adminPassword = '';
  validationError = '';

  // Bulk Provisioning State
  showBulkDialog = false;
  readonly bulkItems = signal<BulkTenantItem[]>([]);
  readonly bulkSummary = signal<BulkProvisioningSummary>({
    total: 0,
    completed: 0,
    failed: 0,
    remaining: 0,
    progressPercent: 0,
    isProcessing: false,
    isFinished: false
  });
  bulkValidationError = '';

  // Actual Provisioning Result
  readonly lastResult = signal<TenantProvisioningResult | null>(null);

  // 5 conceptual stages
  readonly stages = signal<ProvisioningStage[]>([
    {
      step: 1,
      name: 'Tenant record validation',
      description: 'Verify tenant registration and active subscription record in PlatformDB.',
      status: 'PENDING'
    },
    {
      step: 2,
      name: 'Subscription and storage configuration',
      description: 'Confirm Shared storage mode policy and partition parameters.',
      status: 'PENDING'
    },
    {
      step: 3,
      name: 'Tenant database initialization',
      description: 'Allocate DomainDB on SQLSERVER and initialize tenant schema bindings.',
      status: 'PENDING'
    },
    {
      step: 4,
      name: 'Security and tenant admin bootstrap',
      description: 'Ensure Tenant Admin system role and create tenant administrator account.',
      status: 'PENDING'
    },
    {
      step: 5,
      name: 'Final verification',
      description: 'Validate database and configuration readiness for operational onboarding.',
      status: 'PENDING'
    }
  ]);

  ngOnInit(): void {
    const paramId = this.route.snapshot.paramMap.get('tenantId');
    if (paramId) {
      this.selectedTenantId.set(paramId);
    }

    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    // Load real metrics from PlatformDB
    this.provisioningService.getProvisioningMetrics().subscribe({
      next: (m) => this.metrics.set(m),
      error: () => this.metrics.set({
        pending: null,
        inProgress: null,
        failed: null,
        completed: null,
        isLive: false
      })
    });

    // Load real queue items from PlatformDB
    this.provisioningService.getProvisioningQueue().subscribe({
      next: (result) => {
        this.loading.set(false);
        this.queueItems.set(result.items);
        this.endpointAvailable.set(result.endpointAvailable);
        this.endpointNotice.set(result.message);

        // Prune any selected IDs that no longer exist or are completed
        const completedIds = new Set(result.items.filter(i => i.status === 'Completed').map(i => i.id));
        this.selectedTenantIds.update(current => {
          const next = new Set<string>();
          current.forEach(id => {
            if (!completedIds.has(id)) next.add(id);
          });
          return next;
        });
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.message || 'Failed to retrieve tenant queue from server.');
      }
    });
  }

  // ==========================================
  // Row Selection Management
  // ==========================================

  isSelected(id: string): boolean {
    return this.selectedTenantIds().has(id);
  }

  toggleSelect(item: ProvisioningQueueItem): void {
    if (item.status === 'Completed') return;
    this.selectedTenantIds.update(current => {
      const next = new Set(current);
      if (next.has(item.id)) {
        next.delete(item.id);
      } else {
        next.add(item.id);
      }
      return next;
    });
  }

  isAllEligibleSelected(): boolean {
    const eligible = this.filteredQueueItems().filter(i => i.status !== 'Completed');
    if (eligible.length === 0) return false;
    return eligible.every(i => this.selectedTenantIds().has(i.id));
  }

  toggleSelectAll(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    const eligible = this.filteredQueueItems().filter(i => i.status !== 'Completed');
    this.selectedTenantIds.update(current => {
      const next = new Set(current);
      if (checked) {
        eligible.forEach(i => next.add(i.id));
      } else {
        eligible.forEach(i => next.delete(i.id));
      }
      return next;
    });
  }

  eligiblePendingCount(): number {
    return this.filteredQueueItems().filter(i => i.status !== 'Completed').length;
  }

  selectAllAndOpenBulkDialog(): void {
    const eligible = this.filteredQueueItems().filter(i => i.status !== 'Completed');
    if (eligible.length === 0) return;
    this.selectedTenantIds.set(new Set(eligible.map(i => i.id)));
    this.openBulkDialog();
  }

  applyBulkPassword(pass: string): void {
    this.bulkItems.update(items =>
      items.map(i => ({ ...i, adminPassword: pass }))
    );
  }

  // ==========================================
  // Single Provisioning Workflow
  // ==========================================

  openProvisionDialog(tenantId?: string): void {
    if (tenantId) {
      this.selectedTenantId.set(tenantId);
      // Pre-fill email domain if known from queue
      const item = this.queueItems().find(i => i.id === tenantId);
      if (item) {
        this.adminEmail = `admin@${item.code.toLowerCase().replace(/[^a-z0-9]/g, '')}.local`;
      }
    }
    this.validationError = '';
    this.showProvisionDialog = true;
  }

  closeProvisionDialog(): void {
    this.showProvisionDialog = false;
    this.validationError = '';
  }

  executeProvisioning(): void {
    const tenantId = this.selectedTenantId().trim();
    const guidRegex = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
    if (!tenantId) {
      this.validationError = 'Please specify a Tenant Identifier (GUID).';
      return;
    }

    if (!guidRegex.test(tenantId)) {
      this.validationError = 'Invalid Tenant Identifier format. Must be a standard GUID (e.g. 00000000-0000-0000-0000-000000000000).';
      return;
    }

    if (!this.adminUserName.trim()) {
      this.validationError = 'Admin username is required.';
      return;
    }

    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!this.adminEmail.trim()) {
      this.validationError = 'Admin email address is required.';
      return;
    }

    if (!emailRegex.test(this.adminEmail.trim())) {
      this.validationError = 'Please provide a valid administrator email address.';
      return;
    }

    if (!this.adminPassword || this.adminPassword.trim().length < 3) {
      this.validationError = 'Admin password must be at least 3 characters.';
      return;
    }

    const request: TenantProvisioningRequest = {
      tenantId,
      adminUserName: this.adminUserName.trim(),
      adminEmail: this.adminEmail.trim(),
      adminPassword: this.adminPassword
    };

    // Sensitive credential safety: zero out password in component state immediately
    this.adminPassword = '';

    this.provisioningInProgress.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');
    this.validationError = '';
    this.closeProvisionDialog();

    // Mark stages as in progress
    this.stages.update(current =>
      current.map(s => ({ ...s, status: 'IN_PROGRESS' as const }))
    );

    this.provisioningService.provisionTenant(request).subscribe({
      next: (response) => {
        this.provisioningInProgress.set(false);
        if (response?.succeeded && response.data) {
          this.lastResult.set(response.data);
          this.successMessage.set(
            `Tenant ${response.data.tenantId} provisioned successfully in ${response.data.storageMode} mode.`
          );

          // Update stages to COMPLETED based on authoritative backend confirmation
          this.stages.update(current =>
            current.map(s => ({ ...s, status: 'COMPLETED' as const }))
          );

          // Refresh live queue from backend
          this.loadData();
        } else {
          this.errorMessage.set(response?.message || 'Tenant provisioning could not be completed.');
          this.stages.update(current =>
            current.map((s, idx) => ({ ...s, status: idx === 0 ? 'COMPLETED' as const : 'FAILED' as const }))
          );
        }
      },
      error: (err) => {
        this.provisioningInProgress.set(false);
        let serverError: string;
        if (err?.status === 401) {
          serverError = 'Authentication expired or invalid credentials (HTTP 401). Please re-authenticate.';
        } else if (err?.status === 403) {
          serverError = 'Access denied (HTTP 403). Provisioning requires a Platform Administrator account with TENANT.CREATE permission.';
        } else if (err?.status === 400 || err?.status === 409 || err?.status === 422) {
          serverError = err.error?.message || err.error?.error || 'Validation error: ' + (err.message || 'Tenant provisioning rejected by domain rules.');
        } else if (err?.status === 404) {
          serverError = err.error?.message || 'Provisioning endpoint or target tenant not found (HTTP 404).';
        } else if (err?.status && err.status >= 500) {
          serverError = err.error?.message || `Backend provisioning service error (HTTP ${err.status}). Check server logs.`;
        } else {
          serverError = err?.error?.message || err?.message || 'Provisioning API request failed.';
        }
        this.errorMessage.set(serverError);

        this.stages.update(current =>
          current.map((s, idx) => ({ ...s, status: idx === 0 ? 'COMPLETED' as const : 'FAILED' as const }))
        );
      }
    });
  }

  // ==========================================
  // Bulk Provisioning Workflow (Recommended UI)
  // ==========================================

  openBulkDialog(): void {
    const selected = this.selectedTenantIds();
    if (selected.size === 0) return;

    const items: BulkTenantItem[] = this.queueItems()
      .filter(q => selected.has(q.id) && q.status !== 'Completed')
      .map(q => ({
        tenantId: q.id,
        name: q.name,
        code: q.code,
        adminUserName: 'admin',
        adminEmail: `admin@${q.code.toLowerCase().replace(/[^a-z0-9]/g, '')}.local`,
        adminPassword: '',
        status: 'Pending'
      }));

    this.bulkItems.set(items);
    this.bulkSummary.set({
      total: items.length,
      completed: 0,
      failed: 0,
      remaining: items.length,
      progressPercent: 0,
      isProcessing: false,
      isFinished: false
    });
    this.bulkValidationError = '';
    this.showBulkDialog = true;
  }

  closeBulkDialog(): void {
    if (this.bulkSummary().isProcessing) return; // Prevent closing mid-batch
    this.showBulkDialog = false;
    this.bulkValidationError = '';
  }

  async executeBulkProvisioning(): Promise<void> {
    const items = this.bulkItems();
    this.bulkValidationError = '';

    // Validate distinct credentials for each tenant
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    for (const item of items) {
      if (!item.adminUserName.trim()) {
        this.bulkValidationError = `Username is required for ${item.name}.`;
        return;
      }
      if (!emailRegex.test(item.adminEmail.trim())) {
        this.bulkValidationError = `Valid email is required for ${item.name}.`;
        return;
      }
      if (!item.adminPassword || item.adminPassword.trim().length < 3) {
        this.bulkValidationError = `Password (minimum 3 characters) is required for ${item.name}.`;
        return;
      }
    }

    const total = items.length;
    let completed = 0;
    let failed = 0;

    this.bulkSummary.update(s => ({
      ...s,
      isProcessing: true,
      isFinished: false
    }));

    // Controlled sequential execution
    for (let i = 0; i < items.length; i++) {
      const item = items[i];
      if (item.status === 'Completed') continue;

      // Mark current item In Progress
      this.bulkItems.update(list => {
        const copy = [...list];
        copy[i] = { ...copy[i], status: 'In Progress' };
        return copy;
      });

      const request: TenantProvisioningRequest = {
        tenantId: item.tenantId,
        adminUserName: item.adminUserName.trim(),
        adminEmail: item.adminEmail.trim(),
        adminPassword: item.adminPassword
      };

      // Wipe password from state immediately
      item.adminPassword = '';

      try {
        const response = await firstValueFrom(this.provisioningService.provisionTenant(request));
        if (response?.succeeded && response.data) {
          completed++;
          this.bulkItems.update(list => {
            const copy = [...list];
            copy[i] = {
              ...copy[i],
              status: 'Completed',
              result: response.data,
              error: undefined
            };
            return copy;
          });
        } else {
          failed++;
          this.bulkItems.update(list => {
            const copy = [...list];
            copy[i] = {
              ...copy[i],
              status: 'Failed',
              error: response?.message || 'Provisioning rejected by server.'
            };
            return copy;
          });
        }
      } catch (err: any) {
        failed++;
        const errorMsg = err?.error?.message || err?.message || 'Network or authorization error.';
        this.bulkItems.update(list => {
          const copy = [...list];
          copy[i] = {
            ...copy[i],
            status: 'Failed',
            error: errorMsg
          };
          return copy;
        });
      }

      // Update progress metrics after each completed tenant attempt
      const attempts = completed + failed;
      const progressPercent = Math.round((attempts / total) * 100);
      const remaining = total - attempts;

      this.bulkSummary.update(s => ({
        ...s,
        completed,
        failed,
        remaining,
        progressPercent
      }));
    }

    this.bulkSummary.update(s => ({
      ...s,
      isProcessing: false,
      isFinished: true
    }));

    // Refresh live queue from backend
    this.loadData();
  }

  async retryBulkItem(item: BulkTenantItem): Promise<void> {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!item.adminUserName.trim() || !emailRegex.test(item.adminEmail.trim()) || !item.adminPassword || item.adminPassword.trim().length < 3) {
      this.bulkValidationError = `Valid username, email, and password required to retry ${item.name}.`;
      return;
    }

    const idx = this.bulkItems().findIndex(b => b.tenantId === item.tenantId);
    if (idx === -1) return;

    this.bulkItems.update(list => {
      const copy = [...list];
      copy[idx] = { ...copy[idx], status: 'In Progress', error: undefined };
      return copy;
    });

    const request: TenantProvisioningRequest = {
      tenantId: item.tenantId,
      adminUserName: item.adminUserName.trim(),
      adminEmail: item.adminEmail.trim(),
      adminPassword: item.adminPassword
    };

    item.adminPassword = '';

    try {
      const response = await firstValueFrom(this.provisioningService.provisionTenant(request));
      if (response?.succeeded && response.data) {
        this.bulkItems.update(list => {
          const copy = [...list];
          copy[idx] = { ...copy[idx], status: 'Completed', result: response.data, error: undefined };
          return copy;
        });
        this.bulkSummary.update(s => ({
          ...s,
          completed: s.completed + 1,
          failed: Math.max(0, s.failed - 1)
        }));
      } else {
        this.bulkItems.update(list => {
          const copy = [...list];
          copy[idx] = { ...copy[idx], status: 'Failed', error: response?.message || 'Retry rejected.' };
          return copy;
        });
      }
    } catch (err: any) {
      this.bulkItems.update(list => {
        const copy = [...list];
        copy[idx] = { ...copy[idx], status: 'Failed', error: err?.error?.message || err?.message || 'Retry failed.' };
        return copy;
      });
    }

    this.loadData();
  }

  resetStages(): void {
    this.stages.update(current =>
      current.map(s => ({ ...s, status: 'PENDING' as const }))
    );
    this.lastResult.set(null);
    this.successMessage.set('');
    this.errorMessage.set('');
  }
}
