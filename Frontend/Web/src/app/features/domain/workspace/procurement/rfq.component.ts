import { Component, OnInit, OnDestroy, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../shared/components/data-table/data-table.component';
import { AppLoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { AppEmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { AccessDeniedComponent } from '../../../../shared/components/access-denied/access-denied.component';
import { ConfirmationDialogComponent, ConfirmationDialogConfig, ConfirmationDialogResult } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { RfqService } from '../../../../core/services/rfq.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { RfqHeaderDto, RequestForQuotationStatus, getRfqStatusName, getRfqStatusSeverity } from '../../../../core/models/rfq.models';
import { DataTableColumn, DataTableAction } from '../../../../shared/models/data-table-config.model';

export type RfqStatusFilter = 'all' | 'draft' | 'issued' | 'closed' | 'awarded' | 'cancelled';

export interface RfqConfirmModalState {
  type: 'close' | 'cancel';
  rfq: RfqHeaderDto;
  reason?: string;
}

@Component({
  selector: 'app-rfq',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    AppPageHeaderComponent,
    AppDataTableComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AppEmptyStateComponent,
    AccessDeniedComponent,
    ConfirmationDialogComponent
  ],
  templateUrl: './rfq.component.html',
  styleUrl: './rfq.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RfqComponent implements OnInit, OnDestroy {
  readonly rfqService = inject(RfqService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router, { optional: true });
  private readonly subs = new Subscription();

  // Loading & Error States
  readonly loading = signal<boolean>(false);
  readonly actionInProgress = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to access Request for Quotations for this operating company and branch. Contact your administrator if you believe this is an error."
  );
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Data Signals
  readonly rfqs = signal<RfqHeaderDto[]>([]);

  // Search & Filter State
  readonly searchTerm = signal<string>('');
  readonly activeStatusFilter = signal<RfqStatusFilter>('all');

  // Confirmation Modal State
  readonly confirmDialogVisible = signal<boolean>(false);
  readonly confirmDialogConfig = signal<ConfirmationDialogConfig | null>(null);
  readonly confirmDialogError = signal<string | null>(null);
  readonly confirmModal = signal<RfqConfirmModalState | null>(null);

  // Breadcrumbs
  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Procurement', routerLink: '/app/procurement' },
    { label: 'Request for Quotations' }
  ];

  // Table Columns
  readonly tableColumns = computed<DataTableColumn<RfqHeaderDto>[]>(() => [
    {
      field: 'rfqNumber',
      header: 'RFQ Number',
      sortable: true,
      width: '160px'
    },
    {
      field: 'title',
      header: 'Title',
      sortable: true,
      width: '240px'
    },
    {
      field: 'status',
      header: 'Status',
      type: 'badge',
      sortable: true,
      width: '120px',
      format: (val: any) => this.getStatusLabel(val),
      badgeSeverityMap: (val: any) => this.getStatusSeverity(val)
    },
    {
      field: 'closingDate',
      header: 'Closing Date',
      sortable: true,
      width: '130px',
      format: (val: string) => {
        if (!val) return '-';
        const d = new Date(val);
        return isNaN(d.getTime()) ? String(val) : d.toLocaleDateString();
      }
    },
    {
      field: 'lineCount',
      header: 'Items',
      width: '80px',
      align: 'right'
    },
    {
      field: 'invitedSupplierCount',
      header: 'Invited Suppliers',
      width: '130px',
      align: 'right'
    },
    {
      field: 'quotationCount',
      header: 'Quotations',
      width: '100px',
      align: 'right'
    },
    {
      field: 'createdAt',
      header: 'Created On',
      sortable: true,
      width: '130px',
      format: (val: string) => {
        if (!val) return '-';
        const d = new Date(val);
        return isNaN(d.getTime()) ? String(val) : d.toLocaleDateString();
      }
    },
    {
      field: 'id',
      header: 'Actions',
      width: '400px',
      type: 'actions'
    }
  ]);

  // Status helper methods
  isDraft(rfq: RfqHeaderDto): boolean {
    return getRfqStatusName(rfq.status) === 'Draft';
  }

  getStatusLabel(val: any): string {
    return getRfqStatusName(val);
  }

  getStatusSeverity(val: any): 'neutral' | 'info' | 'warning' | 'success' | 'danger' {
    return getRfqStatusSeverity(val);
  }

  canEditRfq(): boolean {
    return (
      this.authService.hasPermission('RFQ.EDIT') ||
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin()
    );
  }

  canManageRfqSuppliers(rfq: RfqHeaderDto): boolean {
    const status = getRfqStatusName(rfq.status);
    return (status === 'Draft' || status === 'Issued') && (
      this.authService.hasPermission('RFQ.SUPPLIER.MANAGE') ||
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin()
    );
  }

  // Row Actions
  readonly tableActions: DataTableAction<RfqHeaderDto>[] = [
    {
      id: 'view',
      label: 'View',
      icon: 'pi pi-eye',
      severity: 'secondary',
      action: (rfq: RfqHeaderDto) => this.navigateToRfq(rfq.id)
    },
    {
      id: 'invite-suppliers',
      label: 'Invite Suppliers',
      icon: 'pi pi-users',
      severity: 'secondary',
      visible: (rfq: RfqHeaderDto) => this.canManageRfqSuppliers(rfq),
      action: (rfq: RfqHeaderDto) => this.navigateToRfq(rfq.id, 'suppliers')
    },
    {
      id: 'edit',
      label: 'Edit',
      icon: 'pi pi-pencil',
      severity: 'secondary',
      visible: (rfq: RfqHeaderDto) => this.isDraft(rfq),
      disabled: () => !this.canEditRfq(),
      action: (rfq: RfqHeaderDto) => this.navigateToRfq(rfq.id)
    },
    {
      id: 'close-rfq',
      label: 'Close',
      icon: 'pi pi-lock',
      severity: 'secondary',
      visible: (rfq: RfqHeaderDto) => getRfqStatusName(rfq.status) === 'Issued',
      disabled: () => !this.canEditRfq(),
      action: (rfq: RfqHeaderDto) => this.openCloseModal(rfq)
    },
    {
      id: 'cancel-rfq',
      label: 'Cancel',
      icon: 'pi pi-ban',
      severity: 'danger',
      visible: (rfq: RfqHeaderDto) => this.isDraft(rfq) || getRfqStatusName(rfq.status) === 'Issued',
      disabled: () => !this.canEditRfq(),
      action: (rfq: RfqHeaderDto) => this.openCancelModal(rfq)
    }
  ];

  // Filtered RFQs Computed
  readonly filteredRfqs = computed(() => {
    let list = this.rfqs();
    const term = this.searchTerm().trim().toLowerCase();
    const filter = this.activeStatusFilter();

    if (filter !== 'all') {
      list = list.filter(r => getRfqStatusName(r.status).toLowerCase() === filter);
    }

    if (term) {
      list = list.filter(r =>
        (r.rfqNumber && r.rfqNumber.toLowerCase().includes(term)) ||
        (r.title && r.title.toLowerCase().includes(term))
      );
    }

    return list;
  });

  // Permission Checks
  canCreateRfq = computed(() => {
    return (
      this.scopeService.hasCompany() &&
      (this.authService.hasPermission('RFQ.CREATE') ||
        this.authService.isTenantAdmin() ||
        this.authService.isPlatformAdmin())
    );
  });

  getCurrentScope(): { companyId: string; branchId: string } | null {
    const company = this.scopeService.selectedCompany();
    if (!company) return null;
    const branch = this.scopeService.selectedBranch();
    const branchId = branch?.id || this.scopeService.branches()[0]?.id;
    if (!branchId) return null;
    return { companyId: company.id, branchId };
  }

  ngOnInit(): void {
    if (this.scopeService.hasCompany()) {
      this.loadRfqs();
    }
    this.subs.add(
      this.scopeService.scopeChange$.subscribe(() => {
        if (this.scopeService.hasCompany()) {
          this.loadRfqs();
        } else {
          this.rfqs.set([]);
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  loadRfqs(): void {
    const scope = this.getCurrentScope();
    if (!scope) return;

    this.loading.set(true);
    this.error.set(null);
    this.isAccessDenied.set(false);

    this.rfqService.getRfqs(scope.companyId, scope.branchId).subscribe({
      next: data => {
        this.rfqs.set(data);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        if (err?.status === 403) {
          this.isAccessDenied.set(true);
          this.accessDeniedMessage.set(err?.error?.message || "You don't have permission to view RFQs.");
        } else {
          this.error.set(err?.error?.message || err?.message || 'Failed to load Request for Quotations.');
        }
      }
    });
  }

  navigateToCreate(): void {
    if (this.router) {
      this.router.navigate(['/app/procurement/rfqs/new']);
    }
  }

  navigateToRfq(id: string, step?: string): void {
    if (this.router) {
      this.router.navigate(['/app/procurement/rfqs', id], {
        queryParams: step ? { step } : undefined
      });
    }
  }

  openCloseModal(rfq: RfqHeaderDto): void {
    this.confirmModal.set({ type: 'close', rfq });
    this.confirmDialogError.set(null);
    this.confirmDialogConfig.set({
      action: 'close-rfq',
      title: 'Close Request for Quotation',
      message: `Are you sure you want to close RFQ "${rfq.rfqNumber}"? Suppliers will no longer be able to submit new quotations.`,
      confirmLabel: 'Close RFQ',
      confirmSeverity: 'secondary',
      cancelLabel: 'Cancel',
      confirmIcon: 'pi pi-lock'
    });
    this.confirmDialogVisible.set(true);
  }

  openCancelModal(rfq: RfqHeaderDto): void {
    this.confirmModal.set({ type: 'cancel', rfq });
    this.confirmDialogError.set(null);
    this.confirmDialogConfig.set({
      action: 'cancel-rfq',
      title: 'Cancel Request for Quotation',
      message: `Are you sure you want to cancel RFQ "${rfq.rfqNumber}"? This action cannot be undone.`,
      confirmLabel: 'Cancel RFQ',
      confirmSeverity: 'danger',
      cancelLabel: 'Go Back',
      confirmIcon: 'pi pi-ban'
    });
    this.confirmDialogVisible.set(true);
  }

  closeConfirmModal(): void {
    this.confirmDialogVisible.set(false);
    this.confirmModal.set(null);
    this.confirmDialogError.set(null);
  }

  executeConfirmModal(result: ConfirmationDialogResult): void {
    const modal = this.confirmModal();
    if (!modal) return;

    const scope = this.getCurrentScope();
    if (!scope) return;

    this.actionInProgress.set(true);
    this.confirmDialogError.set(null);

    if (modal.type === 'close') {
      this.rfqService.closeRfq(scope.companyId, scope.branchId, modal.rfq.id).subscribe({
        next: () => {
          this.actionInProgress.set(false);
          this.closeConfirmModal();
          this.message.set({ text: `RFQ ${modal.rfq.rfqNumber} was successfully closed.`, severity: 'success' });
          this.loadRfqs();
        },
        error: err => {
          this.actionInProgress.set(false);
          this.confirmDialogError.set(err?.error?.message || err?.message || 'Failed to close RFQ.');
        }
      });
    } else if (modal.type === 'cancel') {
      this.rfqService.cancelRfq(scope.companyId, scope.branchId, modal.rfq.id, result.comment).subscribe({
        next: () => {
          this.actionInProgress.set(false);
          this.closeConfirmModal();
          this.message.set({ text: `RFQ ${modal.rfq.rfqNumber} was cancelled.`, severity: 'info' });
          this.loadRfqs();
        },
        error: err => {
          this.actionInProgress.set(false);
          this.confirmDialogError.set(err?.error?.message || err?.message || 'Failed to cancel RFQ.');
        }
      });
    }
  }
}
