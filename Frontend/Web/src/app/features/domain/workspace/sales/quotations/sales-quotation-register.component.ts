import { Component, OnInit, OnDestroy, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';

import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../../shared/components/data-table/data-table.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AppEmptyStateComponent } from '../../../../../shared/components/empty-state/empty-state.component';
import { AccessDeniedComponent } from '../../../../../shared/components/access-denied/access-denied.component';
import { ConfirmationDialogComponent, ConfirmationDialogConfig, ConfirmationDialogResult } from '../../../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { SalesQuotationService } from '../../../../../core/services/sales-quotation.service';
import {
  SalesQuotationDto,
  SalesQuotationStatus,
  SalesQuotationType,
  getSalesQuotationStatusName,
  getSalesQuotationStatusSeverity,
  getSalesQuotationEmailStatusSeverity
} from '../../../../../core/models/sales-quotation.models';
import { DataTableColumn, DataTableAction } from '../../../../../shared/models/data-table-config.model';

export type QuotationTab = 'inbox' | 'commercial' | 'all';
export type SQStatusFilter = 'all' | 'draft' | 'submitted' | 'confirmed' | 'sent' | 'accepted' | 'converted' | 'rejected' | 'cancelled';

@Component({
  selector: 'app-sales-quotation-register',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    AppPageHeaderComponent,
    AppDataTableComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AppEmptyStateComponent,
    AccessDeniedComponent,
    ConfirmationDialogComponent
  ],
  templateUrl: './sales-quotation-register.component.html',
  styleUrl: './sales-quotation-register.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SalesQuotationRegisterComponent implements OnInit, OnDestroy {
  readonly quotationService = inject(SalesQuotationService);
  readonly scopeService: WorkspaceScopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);

  private readonly subs = new Subscription();

  // Loading & States
  readonly loading = signal<boolean>(false);
  readonly actionInProgress = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to access sales quotations for this operating company and branch. Contact your administrator if you believe this is an error."
  );
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Tab & Filters
  readonly activeTab = signal<QuotationTab>('all');
  readonly searchTerm = signal<string>('');
  readonly activeStatusFilter = signal<SQStatusFilter>('all');

  // Quotation Data
  readonly quotations = signal<SalesQuotationDto[]>([]);

  // Breadcrumbs
  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Sales', routerLink: '/app/sales' },
    { label: 'Quotations' }
  ];

  // Action Dialogs
  readonly remarksDialogVisible = signal<boolean>(false);
  readonly remarksAction = signal<'approve' | 'reject' | 'cancel' | 'send' | 'retry' | null>(null);
  readonly remarksTargetQuotation = signal<SalesQuotationDto | null>(null);
  readonly remarksText = signal<string>('');
  readonly remarksError = signal<string | null>(null);

  // Confirmation Dialog
  readonly confirmDialogVisible = signal<boolean>(false);
  readonly confirmDialogConfig = signal<ConfirmationDialogConfig | null>(null);
  private confirmCallback: (() => void) | null = null;

  // Counts computed for tab badges
  readonly inboxCount = computed<number>(() => {
    return this.quotations().filter(q => Number(q.documentType) === SalesQuotationType.CustomerRequest).length;
  });

  readonly commercialCount = computed<number>(() => {
    return this.quotations().filter(q => Number(q.documentType) !== SalesQuotationType.CustomerRequest).length;
  });

  readonly allCount = computed<number>(() => {
    return this.quotations().length;
  });

  // Filtered Quotations
  readonly filteredQuotations = computed<SalesQuotationDto[]>(() => {
    let list = this.quotations();

    // Tab filter
    if (this.activeTab() === 'inbox') {
      list = list.filter(q => Number(q.documentType) === SalesQuotationType.CustomerRequest);
    } else if (this.activeTab() === 'commercial') {
      list = list.filter(q => Number(q.documentType) !== SalesQuotationType.CustomerRequest);
    }

    // Status filter
    const status = this.activeStatusFilter();
    if (status !== 'all') {
      list = list.filter(q => {
        const s = Number(q.status);
        switch (status) {
          case 'draft':
            return s === SalesQuotationStatus.Draft;
          case 'submitted':
            return s === SalesQuotationStatus.Submitted;
          case 'confirmed':
            return s === SalesQuotationStatus.Confirmed;
          case 'sent':
            return s === SalesQuotationStatus.Sent;
          case 'accepted':
            return s === SalesQuotationStatus.Accepted;
          case 'converted':
            return s === SalesQuotationStatus.Converted;
          case 'rejected':
            return s === SalesQuotationStatus.RejectedInternal || s === SalesQuotationStatus.DeclinedByCustomer;
          case 'cancelled':
            return s === SalesQuotationStatus.Cancelled;
          default:
            return true;
        }
      });
    }

    // Search filter
    const search = this.searchTerm().toLowerCase().trim();
    if (search) {
      list = list.filter(q =>
        q.quotationNumber.toLowerCase().includes(search) ||
        (q.customerName && q.customerName.toLowerCase().includes(search)) ||
        (q.contactPersonName && q.contactPersonName.toLowerCase().includes(search)) ||
        (q.customerCode && q.customerCode.toLowerCase().includes(search))
      );
    }

    return list;
  });

  // Permissions
  readonly canCreate = computed(() => this.authService.hasPermission('SALES_QUOTATION.CREATE'));
  readonly canEdit = computed(() => this.authService.hasPermission('SALES_QUOTATION.EDIT'));
  readonly canApprove = computed(() => this.authService.hasPermission('SALES_QUOTATION.APPROVE') || this.authService.hasPermission('SALES_ORDER.CONFIRM'));
  readonly canCreateOrder = computed(() => this.authService.hasPermission('SALES_ORDER.CREATE'));

  // Table Columns (matching Sales Order register structure)
  readonly tableColumns: DataTableColumn<SalesQuotationDto>[] = [
    {
      field: 'quotationNumber',
      header: 'Quotation #',
      sortable: true,
      width: '160px'
    },
    {
      field: 'documentType',
      header: 'Type',
      sortable: true,
      width: '140px',
      format: (val: any) => Number(val) === SalesQuotationType.CustomerRequest ? 'Customer Request' : 'Commercial Offer'
    },
    {
      field: 'customerName',
      header: 'Customer',
      sortable: true,
      width: '200px',
      format: (val: any, row?: SalesQuotationDto) => val || row?.customerCode || 'Standard Customer'
    },
    {
      field: 'quotationDate',
      header: 'Date',
      sortable: true,
      width: '120px',
      format: (val: any) => {
        if (!val) return '-';
        const d = new Date(val);
        return isNaN(d.getTime()) ? String(val) : d.toLocaleDateString();
      }
    },
    {
      field: 'validUntilDateUtc',
      header: 'Valid Until',
      sortable: true,
      width: '120px',
      format: (val: any) => {
        if (!val) return '-';
        const d = new Date(val);
        return isNaN(d.getTime()) ? String(val) : d.toLocaleDateString();
      }
    },
    {
      field: 'totalAmount',
      header: 'Total Amount',
      sortable: true,
      align: 'right',
      width: '140px',
      type: 'currency'
    },
    {
      field: 'grossMarginPercentage',
      header: 'Gross Margin',
      sortable: true,
      align: 'right',
      width: '130px',
      format: (val: any) => val != null ? `${Number(val).toFixed(2)}%` : '—'
    },
    {
      field: 'status',
      header: 'Status',
      sortable: true,
      width: '170px',
      type: 'badge',
      badgeSeverityMap: (val: any) => getSalesQuotationStatusSeverity(val),
      format: (val: any) => getSalesQuotationStatusName(val)
    },
    {
      field: 'emailStatus',
      header: 'Email Status',
      sortable: true,
      width: '130px',
      type: 'badge',
      badgeSeverityMap: (val: any) => getSalesQuotationEmailStatusSeverity(val),
      format: (val: any) => val || 'Not Sent'
    },
    {
      field: 'id',
      header: 'Actions',
      width: '320px',
      type: 'actions'
    }
  ];

  // Row Actions (Matching Fluent style across ApexERP)
  readonly tableActions: DataTableAction<SalesQuotationDto>[] = [
    {
      id: 'view',
      label: 'View',
      icon: 'pi pi-eye',
      severity: 'secondary',
      action: (row: SalesQuotationDto) => this.navigateToQuotation(row)
    },
    {
      id: 'prepare',
      label: 'Prepare Offer',
      icon: 'pi pi-file-edit',
      severity: 'secondary',
      visible: (row: SalesQuotationDto) =>
        (Number(row.status) === SalesQuotationStatus.Submitted ||
         Number(row.status) === SalesQuotationStatus.DeclinedByCustomer) &&
        this.canEdit(),
      action: (row: SalesQuotationDto) => this.prepareOffer(row)
    },
    {
      id: 'approve',
      label: 'Approve',
      icon: 'pi pi-check',
      severity: 'success',
      visible: (row: SalesQuotationDto) =>
        Number(row.status) === SalesQuotationStatus.Submitted && this.canApprove(),
      action: (row: SalesQuotationDto) => this.openApproveDialog(row)
    },
    {
      id: 'send',
      label: 'Send Offer',
      icon: 'pi pi-envelope',
      severity: 'primary',
      visible: (row: SalesQuotationDto) =>
        Number(row.status) === SalesQuotationStatus.Confirmed && this.canEdit(),
      action: (row: SalesQuotationDto) => this.openSendEmailDialog(row, false)
    },
    {
      id: 'convert',
      label: 'Convert to Order',
      icon: 'pi pi-shopping-bag',
      severity: 'success',
      visible: (row: SalesQuotationDto) =>
        Number(row.status) === SalesQuotationStatus.Accepted &&
        this.canCreateOrder(),
      action: (row: SalesQuotationDto) => this.convertToOrder(row)
    },
    {
      id: 'pdf',
      label: 'PDF',
      icon: 'pi pi-download',
      severity: 'secondary',
      visible: (row: SalesQuotationDto) => Number(row.status) >= SalesQuotationStatus.Confirmed,
      action: (row: SalesQuotationDto) => this.downloadPdf(row)
    }
  ];

  // Dialog Titles
  readonly remarksDialogTitle = computed<string>(() => {
    switch (this.remarksAction()) {
      case 'approve':
        return 'Approve Quotation';
      case 'reject':
        return 'Reject Quotation';
      case 'cancel':
        return 'Cancel Quotation';
      case 'send':
        return 'Send Quotation Email';
      case 'retry':
        return 'Retry Failed Quotation Email';
      default:
        return 'Action Confirmation';
    }
  });

  ngOnInit(): void {
    if (this.scopeService.canOperateTransactions()) {
      this.loadQuotations();
    }
    this.subs.add(
      this.scopeService.scopeChange$.subscribe(() => {
        if (this.scopeService.canOperateTransactions()) {
          this.loadQuotations();
        } else {
          this.quotations.set([]);
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  loadQuotations(): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();

    if (!company || !branch) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.isAccessDenied.set(false);

    this.quotationService.getQuotations(company.id, branch.id).subscribe({
      next: data => {
        this.quotations.set(data || []);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        if (err?.status === 403) {
          this.isAccessDenied.set(true);
        } else {
          this.error.set(err?.message || 'Failed to load sales quotations.');
        }
      }
    });
  }

  setTab(tab: QuotationTab): void {
    this.activeTab.set(tab);
  }

  setStatusFilter(filter: SQStatusFilter): void {
    this.activeStatusFilter.set(filter);
  }

  navigateToCreate(): void {
    this.router.navigate(['/app/sales/quotations/new']);
  }

  navigateToQuotation(quotation: SalesQuotationDto): void {
    this.router.navigate(['/app/sales/quotations', quotation.id]);
  }

  getStatusName(status: SalesQuotationStatus | number | string): string {
    return getSalesQuotationStatusName(status);
  }

  getStatusSeverity(status: SalesQuotationStatus | number | string): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
    return getSalesQuotationStatusSeverity(status);
  }

  getEmailSeverity(status?: string | null): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
    return getSalesQuotationEmailStatusSeverity(status);
  }

  // Row Actions Handlers
  prepareOffer(quotation: SalesQuotationDto): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) return;

    if (Number(quotation.status) === SalesQuotationStatus.Draft) {
      this.navigateToQuotation(quotation);
      return;
    }

    this.actionInProgress.set(true);
    this.quotationService.returnToDraft(company.id, branch.id, quotation.id, 'Offer preparation by sales officer').subscribe({
      next: () => {
        this.actionInProgress.set(false);
        this.navigateToQuotation(quotation);
      },
      error: () => {
        this.actionInProgress.set(false);
        this.navigateToQuotation(quotation);
      }
    });
  }

  convertToOrder(quotation: SalesQuotationDto): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) return;

    if (Number(quotation.status) !== SalesQuotationStatus.Accepted) {
      this.message.set({
        text: 'Sales Order conversion is locked until formal customer acceptance has been verified.',
        severity: 'warn'
      });
      return;
    }

    this.confirmDialogConfig.set({
      action: 'convert',
      title: 'Convert Quotation to Sales Order',
      message: `Convert quotation ${quotation.quotationNumber} for customer ${quotation.customerName || 'Customer'} into an official Sales Order Draft? All line items, negotiated prices, and quantities will be automatically copied.`,
      confirmLabel: 'Convert to Sales Order',
      confirmSeverity: 'primary',
      confirmIcon: 'pi pi-shopping-bag'
    });
    this.confirmCallback = () => {
      this.actionInProgress.set(true);
      this.quotationService.convertQuotationToOrder(company.id, branch.id, quotation.id).subscribe({
        next: () => {
          this.actionInProgress.set(false);
          this.message.set({
            text: `Sales Quotation ${quotation.quotationNumber} successfully converted to Sales Order Draft.`,
            severity: 'success'
          });
          this.loadQuotations();
          this.router.navigate(['/app/sales/orders']);
        },
        error: err => {
          this.actionInProgress.set(false);
          this.message.set({
            text: err?.error?.message || err?.message || 'Failed to convert quotation to order.',
            severity: 'danger'
          });
        }
      });
    };
    this.confirmDialogVisible.set(true);
  }

  openApproveDialog(quotation: SalesQuotationDto): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) return;

    this.remarksTargetQuotation.set(quotation);
    this.remarksAction.set('approve');
    this.confirmDialogConfig.set({
      action: 'approve',
      title: 'Approve Quotation',
      message: `Approve commercial quotation ${quotation.quotationNumber} for ${quotation.customerName || 'customer'}? Authoring will be locked and the offer will be ready for formal client delivery.`,
      commentLabel: 'Approval Remarks (Optional)',
      commentPlaceholder: 'Enter any managerial approval notes or margin justifications...',
      commentRequired: false,
      confirmLabel: 'Approve Quotation',
      confirmIcon: 'pi pi-check',
      confirmSeverity: 'primary'
    });
    this.confirmDialogVisible.set(true);
  }

  openRejectDialog(quotation: SalesQuotationDto): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) return;

    this.remarksTargetQuotation.set(quotation);
    this.remarksAction.set('reject');
    this.confirmDialogConfig.set({
      action: 'reject',
      title: 'Reject Quotation',
      message: `Reject commercial quotation ${quotation.quotationNumber}? A mandatory rejection reason is required for managerial audit tracking.`,
      commentLabel: 'Rejection Reason *',
      commentPlaceholder: 'State why this commercial quotation cannot be approved...',
      commentRequired: true,
      commentMinLength: 3,
      confirmLabel: 'Reject Quotation',
      confirmIcon: 'pi pi-times',
      confirmSeverity: 'danger'
    });
    this.confirmDialogVisible.set(true);
  }

  openSendEmailDialog(quotation: SalesQuotationDto, isRetry: boolean = false): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) return;

    const action = isRetry ? 'retry' : 'send';
    this.remarksTargetQuotation.set(quotation);
    this.remarksAction.set(action);
    this.confirmDialogConfig.set({
      action,
      title: isRetry ? 'Retry Quotation Email' : 'Send Quotation Email',
      message: `Formally issue quotation ${quotation.quotationNumber} to customer ${quotation.customerName || 'Customer'} via email notification? Recipient: ${quotation.contactEmail || quotation.emailRecipient || 'Customer Primary Email'}.`,
      subMessage: 'A professional email containing commercial quotation details and a direct secure link to the Customer Workspace will be dispatched.',
      commentLabel: 'Dispatch Notes (Optional)',
      commentPlaceholder: 'Enter optional internal dispatch notes...',
      commentRequired: false,
      confirmLabel: isRetry ? 'Retry Email' : 'Send Quotation',
      confirmIcon: 'pi pi-envelope',
      confirmSeverity: 'primary'
    });
    this.confirmDialogVisible.set(true);
  }

  openCancelDialog(quotation: SalesQuotationDto): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) return;

    this.remarksTargetQuotation.set(quotation);
    this.remarksAction.set('cancel');
    this.confirmDialogConfig.set({
      action: 'cancel',
      title: 'Cancel Quotation',
      message: `Cancel quotation ${quotation.quotationNumber}? Cancelled quotations cannot be approved or converted.`,
      commentLabel: 'Cancellation Reason (Optional)',
      commentPlaceholder: 'Enter reason for cancellation...',
      commentRequired: false,
      confirmLabel: 'Cancel Quotation',
      confirmIcon: 'pi pi-ban',
      confirmSeverity: 'danger'
    });
    this.confirmDialogVisible.set(true);
  }

  submitQuotation(quotation: SalesQuotationDto): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) return;

    this.confirmDialogConfig.set({
      action: 'submit',
      title: 'Submit Quotation for Approval',
      message: `Submit commercial quotation ${quotation.quotationNumber} for managerial review? Authoring will be locked pending approval.`,
      confirmLabel: 'Submit for Approval',
      confirmSeverity: 'primary',
      confirmIcon: 'pi pi-send'
    });
    this.confirmCallback = () => {
      this.actionInProgress.set(true);
      this.quotationService.submitQuotation(company.id, branch.id, quotation.id).subscribe({
        next: updated => {
          this.actionInProgress.set(false);
          this.message.set({ text: `Quotation ${updated.quotationNumber} submitted for approval successfully.`, severity: 'success' });
          this.loadQuotations();
        },
        error: err => {
          this.actionInProgress.set(false);
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to submit quotation.', severity: 'danger' });
        }
      });
    };
    this.confirmDialogVisible.set(true);
  }

  handleConfirmDialogResult(result: ConfirmationDialogResult): void {
    this.confirmDialogVisible.set(false);
    if ((result.action === 'submit' || result.action === 'convert') && this.confirmCallback) {
      this.confirmCallback();
      this.confirmCallback = null;
      return;
    }
    this.executeRemarksActionWith(result.action, result.comment);
  }

  executeRemarksActionWith(action: string, remarks: string): void {
    const quotation = this.remarksTargetQuotation();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();

    if (!quotation || !action || !company || !branch) return;

    const trimmedRemarks = (remarks || '').trim();
    this.actionInProgress.set(true);

    if (action === 'approve') {
      this.quotationService.approveQuotation(company.id, branch.id, quotation.id, trimmedRemarks).subscribe({
        next: updated => {
          this.actionInProgress.set(false);
          this.message.set({ text: `Quotation ${updated.quotationNumber} approved successfully. You can now issue it to the customer.`, severity: 'success' });
          this.loadQuotations();
        },
        error: err => {
          this.actionInProgress.set(false);
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to approve quotation.', severity: 'danger' });
        }
      });
    } else if (action === 'reject') {
      this.quotationService.rejectQuotation(company.id, branch.id, quotation.id, trimmedRemarks).subscribe({
        next: updated => {
          this.actionInProgress.set(false);
          this.message.set({ text: `Quotation ${updated.quotationNumber} has been rejected.`, severity: 'warn' });
          this.loadQuotations();
        },
        error: err => {
          this.actionInProgress.set(false);
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to reject quotation.', severity: 'danger' });
        }
      });
    } else if (action === 'cancel') {
      this.quotationService.cancelQuotation(company.id, branch.id, quotation.id, trimmedRemarks).subscribe({
        next: updated => {
          this.actionInProgress.set(false);
          this.message.set({ text: `Quotation ${updated.quotationNumber} has been cancelled.`, severity: 'info' });
          this.loadQuotations();
        },
        error: err => {
          this.actionInProgress.set(false);
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to cancel quotation.', severity: 'danger' });
        }
      });
    } else if (action === 'send' || action === 'retry') {
      const isRetry = action === 'retry';
      this.quotationService.sendQuotationEmail(company.id, branch.id, quotation.id, isRetry).subscribe({
        next: updated => {
          this.actionInProgress.set(false);
          this.message.set({ text: `Quotation ${updated.quotationNumber} sent by email to customer successfully.`, severity: 'success' });
          this.loadQuotations();
        },
        error: err => {
          this.actionInProgress.set(false);
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to send quotation email.', severity: 'danger' });
          this.loadQuotations();
        }
      });
    }
  }

  downloadPdf(quotation: SalesQuotationDto): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) return;

    this.quotationService.downloadPdf(company.id, branch.id, quotation.id).subscribe({
      next: blob => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `Quotation_${quotation.quotationNumber}.pdf`;
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: () => {
        this.message.set({ text: 'Failed to generate quotation PDF.', severity: 'danger' });
      }
    });
  }
}
