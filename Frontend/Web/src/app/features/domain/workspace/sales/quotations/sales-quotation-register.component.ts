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
export type SQStatusFilter = 'all' | 'draft' | 'submitted' | 'confirmed' | 'sent' | 'rejected' | 'cancelled';

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
  readonly activeTab = signal<QuotationTab>('inbox');
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

  // Table Columns
  readonly columns: DataTableColumn[] = [
    {
      field: 'quotationNumber',
      header: 'Quotation #',
      sortable: true,
      width: '140px',
      cellTemplate: 'link'
    },
    {
      field: 'customerName',
      header: 'Customer',
      sortable: true,
      width: '200px'
    },
    {
      field: 'contactPersonName',
      header: 'Contact Person',
      width: '150px'
    },
    {
      field: 'quotationDate',
      header: 'Date',
      sortable: true,
      width: '110px'
    },
    {
      field: 'validUntilDateUtc',
      header: 'Valid Until',
      sortable: true,
      width: '130px'
    },
    {
      field: 'totalAmount',
      header: 'Total Amount',
      sortable: true,
      width: '130px',
      align: 'right'
    },
    {
      field: 'grossMarginPercentage',
      header: 'Gross Margin',
      sortable: true,
      width: '120px',
      align: 'right'
    },
    {
      field: 'status',
      header: 'Status',
      sortable: true,
      width: '150px'
    },
    {
      field: 'emailStatus',
      header: 'Email Status',
      sortable: true,
      width: '130px'
    }
  ];

  // Permissions
  readonly canCreate = computed(() => this.authService.hasPermission('SALES_QUOTATION.CREATE'));
  readonly canEdit = computed(() => this.authService.hasPermission('SALES_QUOTATION.EDIT'));
  readonly canApprove = computed(() => this.authService.hasPermission('SALES_QUOTATION.APPROVE') || this.authService.hasPermission('SALES_ORDER.CONFIRM'));

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
    this.subs.add(
      this.scopeService.selectedCompany$.subscribe(() => {
        this.loadQuotations();
      })
    );
    this.subs.add(
      this.scopeService.selectedBranch$.subscribe(() => {
        this.loadQuotations();
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
  openApproveDialog(quotation: SalesQuotationDto): void {
    this.remarksTargetQuotation.set(quotation);
    this.remarksAction.set('approve');
    this.remarksText.set('');
    this.remarksError.set(null);
    this.remarksDialogVisible.set(true);
  }

  openRejectDialog(quotation: SalesQuotationDto): void {
    this.remarksTargetQuotation.set(quotation);
    this.remarksAction.set('reject');
    this.remarksText.set('');
    this.remarksError.set(null);
    this.remarksDialogVisible.set(true);
  }

  openSendEmailDialog(quotation: SalesQuotationDto, isRetry: boolean = false): void {
    this.remarksTargetQuotation.set(quotation);
    this.remarksAction.set(isRetry ? 'retry' : 'send');
    this.remarksText.set('');
    this.remarksError.set(null);
    this.remarksDialogVisible.set(true);
  }

  openCancelDialog(quotation: SalesQuotationDto): void {
    this.remarksTargetQuotation.set(quotation);
    this.remarksAction.set('cancel');
    this.remarksText.set('');
    this.remarksError.set(null);
    this.remarksDialogVisible.set(true);
  }

  submitQuotation(quotation: SalesQuotationDto): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) return;

    this.confirmDialogConfig.set({
      title: 'Submit Quotation for Approval',
      message: `Submit commercial quotation ${quotation.QuotationNumber || quotation.quotationNumber} for managerial review? Authoring will be locked pending approval.`,
      confirmLabel: 'Submit for Approval',
      severity: 'primary',
      icon: 'pi pi-send'
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

  executeRemarksAction(): void {
    const quotation = this.remarksTargetQuotation();
    const action = this.remarksAction();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();

    if (!quotation || !action || !company || !branch) return;

    const remarks = this.remarksText().trim();
    if (action === 'reject' && !remarks) {
      this.remarksError.set('Rejection remarks are mandatory.');
      return;
    }

    this.actionInProgress.set(true);
    this.remarksError.set(null);

    if (action === 'approve') {
      this.quotationService.approveQuotation(company.id, branch.id, quotation.id, remarks).subscribe({
        next: updated => {
          this.actionInProgress.set(false);
          this.remarksDialogVisible.set(false);
          this.message.set({ text: `Quotation ${updated.quotationNumber} approved successfully. You can now issue it to the customer.`, severity: 'success' });
          this.loadQuotations();
        },
        error: err => {
          this.actionInProgress.set(false);
          this.remarksError.set(err?.error?.message || err?.message || 'Failed to approve quotation.');
        }
      });
    } else if (action === 'reject') {
      this.quotationService.rejectQuotation(company.id, branch.id, quotation.id, remarks).subscribe({
        next: updated => {
          this.actionInProgress.set(false);
          this.remarksDialogVisible.set(false);
          this.message.set({ text: `Quotation ${updated.quotationNumber} has been rejected.`, severity: 'warn' });
          this.loadQuotations();
        },
        error: err => {
          this.actionInProgress.set(false);
          this.remarksError.set(err?.error?.message || err?.message || 'Failed to reject quotation.');
        }
      });
    } else if (action === 'cancel') {
      this.quotationService.cancelQuotation(company.id, branch.id, quotation.id, remarks).subscribe({
        next: updated => {
          this.actionInProgress.set(false);
          this.remarksDialogVisible.set(false);
          this.message.set({ text: `Quotation ${updated.quotationNumber} has been cancelled.`, severity: 'info' });
          this.loadQuotations();
        },
        error: err => {
          this.actionInProgress.set(false);
          this.remarksError.set(err?.error?.message || err?.message || 'Failed to cancel quotation.');
        }
      });
    } else if (action === 'send' || action === 'retry') {
      const isRetry = action === 'retry';
      this.quotationService.sendQuotationEmail(company.id, branch.id, quotation.id, isRetry).subscribe({
        next: updated => {
          this.actionInProgress.set(false);
          this.remarksDialogVisible.set(false);
          this.message.set({ text: `Quotation ${updated.quotationNumber} sent by email to customer successfully.`, severity: 'success' });
          this.loadQuotations();
        },
        error: err => {
          this.actionInProgress.set(false);
          this.remarksError.set(err?.error?.message || err?.message || 'Failed to send quotation email.');
          this.loadQuotations();
        }
      });
    }
  }

  closeRemarksDialog(): void {
    this.remarksDialogVisible.set(false);
    this.remarksTargetQuotation.set(null);
    this.remarksAction.set(null);
    this.remarksText.set('');
    this.remarksError.set(null);
  }

  handleConfirmDialogResult(result: ConfirmationDialogResult): void {
    this.confirmDialogVisible.set(false);
    if (result === 'confirm' && this.confirmCallback) {
      this.confirmCallback();
    }
    this.confirmCallback = null;
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
      error: err => {
        this.message.set({ text: 'Failed to generate quotation PDF.', severity: 'danger' });
      }
    });
  }
}
