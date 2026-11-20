import { Component, OnInit, OnDestroy, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { Subscription, forkJoin, of, catchError } from 'rxjs';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../shared/components/data-table/data-table.component';
import { AppLoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { AppEmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { AccessDeniedComponent } from '../../../../shared/components/access-denied/access-denied.component';
import { ConfirmationDialogComponent, ConfirmationDialogConfig, ConfirmationDialogResult } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { PurchaseRequisitionService } from '../../../../core/services/purchase-requisition.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import {
  PurchaseRequisitionDto,
  PurchaseRequisitionStatus,
  ProcurementRouteStatusDto,
  ProcurementRouteType,
  getPurchaseRequisitionStatusName,
  getPurchaseRequisitionStatusSeverity
} from '../../../../core/models/purchase-requisition.models';
import { DataTableColumn, DataTableAction } from '../../../../shared/models/data-table-config.model';

export type PRStatusFilter = 'all' | 'draft' | 'submitted' | 'approved' | 'rejected' | 'cancelled';

export interface PRConfirmModalState {
  type: 'submit' | 'delete' | 'cancel';
  requisition: PurchaseRequisitionDto;
  remarks?: string;
  error?: string | null;
}

@Component({
  selector: 'app-purchase-requisition',
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
  templateUrl: './purchase-requisition.component.html',
  styleUrl: './purchase-requisition.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PurchaseRequisitionComponent implements OnInit, OnDestroy {
  readonly prService = inject(PurchaseRequisitionService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router, { optional: true });
  private readonly route = inject(ActivatedRoute, { optional: true });
  private readonly subs = new Subscription();

  // Loading & Error States
  readonly loading = signal<boolean>(false);
  readonly actionInProgress = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to access purchase requisitions for this operating company and branch. Contact your administrator if you believe this is an error."
  );
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Data Signals
  readonly requisitions = signal<PurchaseRequisitionDto[]>([]);
  readonly routeStatusMap = signal<Map<string, ProcurementRouteStatusDto>>(new Map());

  // Search & Filter State
  readonly searchTerm = signal<string>('');
  readonly activeStatusFilter = signal<PRStatusFilter>('all');

  // Confirmation Modal State (Using Reusable Confirmation Dialog)
  readonly confirmDialogVisible = signal<boolean>(false);
  readonly confirmDialogConfig = signal<ConfirmationDialogConfig | null>(null);
  readonly confirmDialogError = signal<string | null>(null);
  readonly confirmModal = signal<PRConfirmModalState | null>(null);

  // Breadcrumbs
  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Procurement', routerLink: '/app/procurement' },
    { label: 'Purchase Requisitions' }
  ];

  // Columns for the main requisitions data-table
  readonly tableColumns = computed<DataTableColumn<PurchaseRequisitionDto>[]>(() => {
    const cols: DataTableColumn<PurchaseRequisitionDto>[] = [
      {
        field: 'requisitionNumber',
        header: 'PR Number',
        sortable: true,
        width: '160px'
      },
      {
        field: 'requisitionDate',
        header: 'Requisition Date',
        sortable: true,
        width: '140px',
        format: (val: any) => {
          if (!val) return '-';
          const d = new Date(val);
          return isNaN(d.getTime()) ? String(val) : d.toLocaleDateString();
        }
      },
      {
        field: 'requiredDate',
        header: 'Required By',
        sortable: true,
        width: '140px',
        format: (val: any) => {
          if (!val) return '-';
          const d = new Date(val);
          return isNaN(d.getTime()) ? String(val) : d.toLocaleDateString();
        }
      }
    ];

    if (this.scopeService.isAllBranches()) {
      cols.push({
        field: 'branchId',
        header: 'Branch',
        sortable: true,
        width: '160px',
        format: (val: any) => {
          const br = this.scopeService.branches().find(b => b.id.toLowerCase() === String(val).toLowerCase());
          return br ? br.name : '-';
        }
      });
    }

    cols.push(
      {
        field: 'notes',
        header: 'Justification / Notes',
        sortable: false,
        width: '220px',
        format: (val: any) => val || '-'
      },
      {
        field: 'totalAmount',
        header: 'Est. Total',
        sortable: true,
        align: 'right',
        width: '140px',
        type: 'currency'
      },
      {
        field: 'status',
        header: 'Status',
        width: '130px',
        type: 'badge',
        badgeSeverityMap: (val: any) => getPurchaseRequisitionStatusSeverity(val),
        format: (val: any) => getPurchaseRequisitionStatusName(val)
      },
      {
        field: 'id',
        header: 'Actions',
        width: '320px',
        type: 'actions'
      }
    );

    return cols;
  });

  // Row actions
  readonly tableActions: DataTableAction<PurchaseRequisitionDto>[] = [
    {
      id: 'view',
      label: 'View',
      icon: 'pi pi-eye',
      severity: 'secondary',
      action: (row: PurchaseRequisitionDto) => this.navigateToView(row)
    },
    {
      id: 'edit',
      label: 'Edit',
      icon: 'pi pi-pencil',
      severity: 'secondary',
      visible: (row: PurchaseRequisitionDto) =>
        this.isStatus(row, PurchaseRequisitionStatus.Draft) || this.isStatus(row, PurchaseRequisitionStatus.Rejected),
      disabled: () => !this.canEditRequisition(),
      action: (row: PurchaseRequisitionDto) => this.navigateToEdit(row)
    },
    {
      id: 'submit',
      label: 'Submit',
      icon: 'pi pi-send',
      severity: 'primary',
      visible: (row: PurchaseRequisitionDto) => this.isStatus(row, PurchaseRequisitionStatus.Draft),
      disabled: () => !this.canSubmitRequisition(),
      action: (row: PurchaseRequisitionDto) => this.openConfirmModal('submit', row)
    },
    {
      id: 'approve',
      label: 'Approve',
      icon: 'pi pi-check',
      severity: 'success',
      visible: (row: PurchaseRequisitionDto) => this.isStatus(row, PurchaseRequisitionStatus.Submitted),
      disabled: () => !this.canApproveRequisition(),
      action: (row: PurchaseRequisitionDto) => this.navigateToApproval(row)
    },
    {
      id: 'create-po',
      label: 'Create PO',
      icon: 'pi pi-shopping-cart',
      severity: 'primary',
      visible: (row: PurchaseRequisitionDto) => this.canShowCreatePo(row),
      disabled: (row: PurchaseRequisitionDto) => !this.canCreatePo(row),
      action: (row: PurchaseRequisitionDto) => {
        if (this.router) {
          this.router.navigate(['/app/procurement/purchase-orders/new'], {
            queryParams: {
              purchaseRequisitionId: row.id,
              branchId: row.branchId
            }
          });
        }
      }
    },
    {
      id: 'create-rfq',
      label: 'Create RFQ',
      icon: 'pi pi-file-edit',
      severity: 'secondary',
      visible: (row: PurchaseRequisitionDto) => this.canShowCreateRfq(row),
      disabled: (row: PurchaseRequisitionDto) => !this.canCreateRfq(row),
      action: (row: PurchaseRequisitionDto) => {
        if (this.router) {
          this.router.navigate(['/app/procurement/rfqs/new'], {
            queryParams: {
              purchaseRequisitionId: row.id,
              branchId: row.branchId
            }
          });
        }
      }
    },
    {
      id: 'view-rfq',
      label: 'View RFQ',
      icon: 'pi pi-external-link',
      severity: 'info',
      visible: (row: PurchaseRequisitionDto) => this.hasLinkedRfq(row),
      action: (row: PurchaseRequisitionDto) => this.navigateToLinkedRfq(row)
    },
    {
      id: 'cancel',
      label: 'Cancel',
      icon: 'pi pi-ban',
      severity: 'danger',
      visible: (row: PurchaseRequisitionDto) =>
        this.isStatus(row, PurchaseRequisitionStatus.Draft) || this.isStatus(row, PurchaseRequisitionStatus.Submitted),
      disabled: () => !this.canCancelRequisition(),
      action: (row: PurchaseRequisitionDto) => this.openConfirmModal('cancel', row)
    },
    {
      id: 'delete',
      label: 'Delete',
      icon: 'pi pi-trash',
      severity: 'danger',
      visible: (row: PurchaseRequisitionDto) => this.isStatus(row, PurchaseRequisitionStatus.Draft),
      disabled: () => !this.canDeleteRequisition(),
      action: (row: PurchaseRequisitionDto) => this.openConfirmModal('delete', row)
    }
  ];

  // Filtered requisitions list
  readonly filteredRequisitions = computed<PurchaseRequisitionDto[]>(() => {
    let list = this.requisitions();
    const filter = this.activeStatusFilter();
    if (filter !== 'all') {
      list = list.filter(r => {
        const name = getPurchaseRequisitionStatusName(r.status).toLowerCase();
        return name === filter;
      });
    }

    const term = this.searchTerm().trim().toLowerCase();
    if (term) {
      list = list.filter(r => {
        const num = (r.requisitionNumber || '').toLowerCase();
        const notes = (r.notes || '').toLowerCase();
        return num.includes(term) || notes.includes(term);
      });
    }

    return list;
  });

  ngOnInit(): void {
    if (this.route) {
      this.subs.add(
        this.route.queryParams.subscribe(params => {
          if (params['status']) {
            const st = String(params['status']).toLowerCase();
            if (['all', 'draft', 'submitted', 'approved', 'rejected', 'cancelled'].includes(st)) {
              this.activeStatusFilter.set(st as PRStatusFilter);
            }
          }
        })
      );
    }

    if (this.scopeService.hasCompany()) {
      this.loadRequisitions();
    }

    this.subs.add(
      this.scopeService.scopeChange$.subscribe(() => {
        if (this.scopeService.hasCompany()) {
          this.loadRequisitions();
        } else {
          this.requisitions.set([]);
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  // Load requisitions for active company and branch
  loadRequisitions(): void {
    const company = this.scopeService.selectedCompany();
    if (!company) {
      this.requisitions.set([]);
      return;
    }

    const branch = this.scopeService.selectedBranch();
    const branchId = branch?.id || this.scopeService.branches()[0]?.id;
    if (!branchId) {
      this.requisitions.set([]);
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.isAccessDenied.set(false);

    this.prService.getRequisitions(company.id, branchId).subscribe({
      next: data => {
        this.loading.set(false);
        this.isAccessDenied.set(false);
        this.requisitions.set(data);
        this.loadRouteStatuses(company.id, branchId, data);
      },
      error: err => {
        this.loading.set(false);
        if (this.isForbiddenError(err)) {
          this.isAccessDenied.set(true);
          this.error.set(null);
        } else {
          this.isAccessDenied.set(false);
          this.error.set(this.getCleanErrorMessage(err, 'Failed to load purchase requisitions. Please try again.'));
        }
      }
    });
  }

  private loadRouteStatuses(companyId: string, branchId: string, data: PurchaseRequisitionDto[]): void {
    const approved = (data || []).filter(r => Number(r.status) === PurchaseRequisitionStatus.Approved);
    if (approved.length === 0) {
      this.routeStatusMap.set(new Map());
      return;
    }

    const requests = approved.map(r =>
      this.prService.getRouteStatus(companyId, branchId, r.id).pipe(
        catchError(() => of(null))
      )
    );

    forkJoin(requests).subscribe(results => {
      const map = new Map<string, ProcurementRouteStatusDto>();
      for (const res of results) {
        if (res && res.requisitionId) {
          map.set(String(res.requisitionId).toLowerCase(), res);
        }
      }
      this.routeStatusMap.set(map);
    });
  }

  private isRouteDirectPo(route: ProcurementRouteType | string | number | undefined): boolean {
    const val = String(route ?? '');
    return val === '1' || val === 'DirectPO' || (route as any) === ProcurementRouteType.DirectPO;
  }

  private isRouteRfq(route: ProcurementRouteType | string | number | undefined): boolean {
    const val = String(route ?? '');
    return val === '2' || val === 'RFQ' || (route as any) === ProcurementRouteType.RFQ;
  }

  canShowCreatePo(row: PurchaseRequisitionDto): boolean {
    if (!this.isStatus(row, PurchaseRequisitionStatus.Approved)) {
      return false;
    }
    const status = this.routeStatusMap().get((row.id || '').toLowerCase());
    if (status) {
      // If RFQ has been generated, HIDE the Create PO button!
      if (this.isRouteRfq(status.selectedRoute) || status.linkedRfq != null || (status.linkedRfqs && status.linkedRfqs.length > 0)) {
        return false;
      }
      return (status.canCreateDirectPo ?? status.canCreateDirectPO ?? true);
    }
    return true;
  }

  canCreatePo(row: PurchaseRequisitionDto): boolean {
    const hasPerm = this.authService.hasPermission('PURCHASE_ORDER.CREATE') || this.authService.isTenantAdmin();
    if (!hasPerm) return false;
    const status = this.routeStatusMap().get((row.id || '').toLowerCase());
    if (status) {
      return (status.canCreateDirectPo ?? status.canCreateDirectPO ?? true);
    }
    return true;
  }

  canShowCreateRfq(row: PurchaseRequisitionDto): boolean {
    if (!this.isStatus(row, PurchaseRequisitionStatus.Approved)) {
      return false;
    }
    const status = this.routeStatusMap().get((row.id || '').toLowerCase());
    if (status) {
      // If Direct PO has been generated, HIDE the Create RFQ button!
      if (this.isRouteDirectPo(status.selectedRoute) || (status.linkedPurchaseOrders && status.linkedPurchaseOrders.length > 0)) {
        return false;
      }
      // If RFQ has already been generated, hide the Create RFQ button (use View RFQ)
      if (this.isRouteRfq(status.selectedRoute) || status.linkedRfq != null || (status.linkedRfqs && status.linkedRfqs.length > 0)) {
        return false;
      }
      return (status.canCreateRfq ?? status.canCreateRFQ ?? true);
    }
    return true;
  }

  canCreateRfq(row: PurchaseRequisitionDto): boolean {
    const hasPerm = this.authService.hasPermission('RFQ.CREATE') || this.authService.isTenantAdmin();
    if (!hasPerm) return false;
    const status = this.routeStatusMap().get((row.id || '').toLowerCase());
    if (status) {
      return (status.canCreateRfq ?? status.canCreateRFQ ?? true);
    }
    return true;
  }

  hasLinkedRfq(row: PurchaseRequisitionDto): boolean {
    if (!this.isStatus(row, PurchaseRequisitionStatus.Approved)) return false;
    const status = this.routeStatusMap().get((row.id || '').toLowerCase());
    return !!(status && (status.linkedRfq != null || (status.linkedRfqs && status.linkedRfqs.length > 0)));
  }

  navigateToLinkedRfq(row: PurchaseRequisitionDto): void {
    const status = this.routeStatusMap().get((row.id || '').toLowerCase());
    const rfqId = status?.linkedRfq?.id || status?.linkedRfqs?.[0]?.rfqId || status?.linkedRfqs?.[0]?.id;
    if (rfqId && this.router) {
      this.router.navigate(['/app/procurement/rfqs', rfqId]);
    }
  }

  // Navigation methods
  navigateToCreate(): void {
    this.router?.navigate(['/app/procurement/purchase-requisitions/new']);
  }

  navigateToView(row: PurchaseRequisitionDto): void {
    this.router?.navigate(['/app/procurement/purchase-requisitions', row.id]);
  }

  navigateToEdit(row: PurchaseRequisitionDto): void {
    this.router?.navigate(['/app/procurement/purchase-requisitions', row.id, 'edit']);
  }

  navigateToApproval(row: PurchaseRequisitionDto): void {
    this.router?.navigate(['/app/procurement/purchase-requisitions', row.id, 'approve']);
  }

  // Modal Actions
  openConfirmModal(type: 'submit' | 'delete' | 'cancel', req: PurchaseRequisitionDto): void {
    let config: ConfirmationDialogConfig;
    if (type === 'submit') {
      config = {
        action: 'submit',
        title: 'Submit Purchase Requisition',
        message: `Are you sure you want to submit requisition ${req.requisitionNumber} for management approval? Once submitted, line items cannot be modified until reviewed.`,
        confirmLabel: 'Submit for Approval',
        confirmIcon: 'pi pi-send',
        confirmSeverity: 'primary',
        commentLabel: 'Remarks',
        commentPlaceholder: 'Enter optional submission remarks...',
        commentRequired: false
      };
    } else if (type === 'delete') {
      config = {
        action: 'delete',
        title: 'Delete Draft Requisition',
        message: `Are you sure you want to delete draft requisition ${req.requisitionNumber}? This draft will be permanently deactivated.`,
        confirmLabel: 'Delete Draft',
        confirmIcon: 'pi pi-trash',
        confirmSeverity: 'danger',
        commentRequired: false
      };
    } else {
      config = {
        action: 'cancel',
        title: 'Cancel Purchase Requisition',
        message: `Are you sure you want to cancel requisition ${req.requisitionNumber}?`,
        commentLabel: 'Cancellation Reason',
        commentPlaceholder: 'Enter cancellation reason (optional)...',
        confirmLabel: 'Cancel Requisition',
        confirmIcon: 'pi pi-ban',
        confirmSeverity: 'danger',
        commentRequired: false
      };
    }

    this.confirmModal.set({
      type,
      requisition: req,
      remarks: ''
    });
    this.confirmDialogConfig.set(config);
    this.confirmDialogError.set(null);
    this.confirmDialogVisible.set(true);
  }

  closeConfirmModal(): void {
    this.confirmDialogVisible.set(false);
    this.confirmDialogError.set(null);
    this.confirmModal.set(null);
  }

  executeConfirmModal(dialogResult?: ConfirmationDialogResult): void {
    const state = this.confirmModal();
    if (!state) return;

    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    const branchId = branch?.id || state.requisition.branchId;
    if (!company || !branchId) return;

    const remarks = dialogResult ? dialogResult.comment.trim() : (state.remarks || '').trim();

    this.actionInProgress.set(true);
    this.confirmDialogError.set(null);

    if (state.type === 'submit') {
      this.prService.submitRequisition(company.id, branchId, state.requisition.id).subscribe({
        next: () => {
          this.actionInProgress.set(false);
          this.closeConfirmModal();
          this.showMessage(`Purchase requisition ${state.requisition.requisitionNumber} submitted for approval.`, 'success');
          this.loadRequisitions();
        },
        error: err => {
          this.actionInProgress.set(false);
          const errMsg = this.getCleanErrorMessage(err, 'Failed to submit requisition.');
          this.confirmDialogError.set(errMsg);
          state.error = errMsg;
          this.confirmModal.set({ ...state });
        }
      });
    } else if (state.type === 'delete') {
      this.prService.deleteRequisition(company.id, branchId, state.requisition.id).subscribe({
        next: () => {
          this.actionInProgress.set(false);
          this.closeConfirmModal();
          this.showMessage(`Purchase requisition ${state.requisition.requisitionNumber} deleted.`, 'success');
          this.loadRequisitions();
        },
        error: err => {
          this.actionInProgress.set(false);
          const errMsg = this.getCleanErrorMessage(err, 'Failed to delete requisition.');
          this.confirmDialogError.set(errMsg);
          state.error = errMsg;
          this.confirmModal.set({ ...state });
        }
      });
    } else if (state.type === 'cancel') {
      this.prService.cancelRequisition(company.id, branchId, state.requisition.id, remarks || undefined).subscribe({
        next: () => {
          this.actionInProgress.set(false);
          this.closeConfirmModal();
          this.showMessage(`Purchase requisition ${state.requisition.requisitionNumber} cancelled.`, 'success');
          this.loadRequisitions();
        },
        error: err => {
          this.actionInProgress.set(false);
          const errMsg = this.getCleanErrorMessage(err, 'Failed to cancel requisition.');
          this.confirmDialogError.set(errMsg);
          state.error = errMsg;
          this.confirmModal.set({ ...state });
        }
      });
    }
  }

  // Helpers
  isStatus(row: PurchaseRequisitionDto, status: PurchaseRequisitionStatus): boolean {
    return Number(row.status) === Number(status) ||
      String(row.status).toLowerCase() === getPurchaseRequisitionStatusName(status).toLowerCase();
  }

  canCreateRequisition(): boolean {
    return (
      this.scopeService.hasCompany() &&
      (this.authService.hasPermission('PURCHASE_REQUISITION.CREATE') ||
        this.authService.isTenantAdmin() ||
        this.authService.isPlatformAdmin())
    );
  }

  canEditRequisition(): boolean {
    return (
      this.authService.hasPermission('PURCHASE_REQUISITION.EDIT') ||
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin()
    );
  }

  canSubmitRequisition(): boolean {
    return (
      this.authService.hasPermission('PURCHASE_REQUISITION.SUBMIT') ||
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin()
    );
  }

  canApproveRequisition(): boolean {
    return (
      this.authService.hasPermission('PURCHASE_REQUISITION.APPROVE') ||
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin()
    );
  }

  canCancelRequisition(): boolean {
    return (
      this.authService.hasPermission('PURCHASE_REQUISITION.CANCEL') ||
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin()
    );
  }

  canDeleteRequisition(): boolean {
    return (
      this.authService.hasPermission('PURCHASE_REQUISITION.DELETE') ||
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin()
    );
  }

  private isForbiddenError(err: any): boolean {
    const status = err?.status;
    return status === 403;
  }

  private getCleanErrorMessage(err: any, fallback: string): string {
    const rawMsg = err?.error?.message || err?.message;
    if (typeof rawMsg === 'string' && rawMsg.trim().length > 0) {
      return rawMsg;
    }
    return fallback;
  }

  showMessage(text: string, severity: 'success' | 'info' | 'warn' | 'danger'): void {
    this.message.set({ text, severity });
    setTimeout(() => {
      if (this.message()?.text === text) {
        this.message.set(null);
      }
    }, 6000);
  }
}
