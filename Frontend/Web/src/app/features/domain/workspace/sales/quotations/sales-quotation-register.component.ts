import { Component, OnInit, OnDestroy, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
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
  getSalesQuotationApprovalStatusLabel,
  getSalesQuotationApprovalSeverity,
  getSalesQuotationCustomerStatusLabel,
  getSalesQuotationCustomerStatusSeverity,
  getSalesQuotationEmailStatusLabel,
  getSalesQuotationEmailStatusSeverity
} from '../../../../../core/models/sales-quotation.models';
import { DataTableColumn, DataTableAction } from '../../../../../shared/models/data-table-config.model';

export type QuotationTab = 'requests' | 'quotations';
export type RequestFilter = 'all' | 'submitted' | 'under_review' | 'quotation_prepared' | 'closed';
export type QuotationFilter = 'all' | 'draft' | 'submitted' | 'confirmed' | 'rejected_internal' | 'sent' | 'accepted' | 'declined' | 'expired' | 'converted';

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
  readonly route = inject(ActivatedRoute);

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

  // Tabs & Filters (Tab A: Customer Requests, Tab B: Sales Quotations)
  readonly activeTab = signal<QuotationTab>('requests');
  readonly requestFilter = signal<RequestFilter>('all');
  readonly quotationFilter = signal<QuotationFilter>('all');
  readonly searchTerm = signal<string>('');

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

  // Confirmation Dialog
  readonly confirmDialogVisible = signal<boolean>(false);
  readonly confirmDialogConfig = signal<ConfirmationDialogConfig | null>(null);
  private confirmCallback: (() => void) | null = null;

  // Counts computed for tab badges
  readonly requestsCount = computed<number>(() => {
    return this.quotations().filter(q => Number(q.documentType) === SalesQuotationType.CustomerRequest).length;
  });

  readonly quotationsCount = computed<number>(() => {
    return this.quotations().filter(q => Number(q.documentType) !== SalesQuotationType.CustomerRequest).length;
  });

  // Filtered Quotations
  readonly filteredQuotations = computed<SalesQuotationDto[]>(() => {
    let list = this.quotations();

    if (this.activeTab() === 'requests') {
      list = list.filter(q => Number(q.documentType) === SalesQuotationType.CustomerRequest);

      const filter = this.requestFilter();
      if (filter !== 'all') {
        list = list.filter(q => {
          const revStatus = q.requestReviewStatus || '';
          const s = Number(q.status);
          switch (filter) {
            case 'submitted':
              return revStatus === 'Submitted' || (!revStatus && s === SalesQuotationStatus.Submitted);
            case 'under_review':
              return revStatus === 'UnderReview' || (!revStatus && s === SalesQuotationStatus.Draft);
            case 'quotation_prepared':
              return revStatus === 'QuotationPrepared' || !!q.linkedCommercialQuotationId;
            case 'closed':
              return revStatus === 'Closed' || s === SalesQuotationStatus.Cancelled;
            default:
              return true;
          }
        });
      }
    } else {
      list = list.filter(q => Number(q.documentType) !== SalesQuotationType.CustomerRequest);

      const filter = this.quotationFilter();
      if (filter !== 'all') {
        list = list.filter(q => {
          const s = Number(q.status);
          switch (filter) {
            case 'draft':
              return s === SalesQuotationStatus.Draft;
            case 'submitted':
              return s === SalesQuotationStatus.Submitted;
            case 'confirmed':
              return s === SalesQuotationStatus.Confirmed;
            case 'rejected_internal':
              return s === SalesQuotationStatus.RejectedInternal;
            case 'sent':
              return s === SalesQuotationStatus.Sent;
            case 'accepted':
              return s === SalesQuotationStatus.Accepted;
            case 'declined':
              return s === SalesQuotationStatus.DeclinedByCustomer;
            case 'expired':
              return s === SalesQuotationStatus.Expired || q.isExpired;
            case 'converted':
              return s === SalesQuotationStatus.Converted;
            default:
              return true;
          }
        });
      }
    }

    const search = this.searchTerm().toLowerCase().trim();
    if (search) {
      list = list.filter(q =>
        q.quotationNumber.toLowerCase().includes(search) ||
        (q.customerName && q.customerName.toLowerCase().includes(search)) ||
        (q.contactPersonName && q.contactPersonName.toLowerCase().includes(search)) ||
        (q.customerCode && q.customerCode.toLowerCase().includes(search)) ||
        (q.linkedCustomerRequestNumber && q.linkedCustomerRequestNumber.toLowerCase().includes(search)) ||
        (q.linkedCommercialQuotationNumber && q.linkedCommercialQuotationNumber.toLowerCase().includes(search))
      );
    }

    return list;
  });

  // Permissions
  readonly canCreate = computed(() => this.authService.hasPermission('SALES_QUOTATION.CREATE'));
  readonly canEdit = computed(() => this.authService.hasPermission('SALES_QUOTATION.EDIT'));
  readonly canApprove = computed(() =>
    this.authService.hasPermission('SALES_QUOTATION.CONFIRM') ||
    this.authService.hasPermission('SALES_QUOTATION.APPROVE')
  );
  readonly canCreateOrder = computed(() => this.authService.hasPermission('SALES_ORDER.CREATE'));

  // Tab A: Customer Requests Columns
  readonly requestColumns: DataTableColumn<SalesQuotationDto>[] = [
    {
      field: 'quotationNumber',
      header: 'Request #',
      sortable: true,
      width: '150px'
    },
    {
      field: 'customerName',
      header: 'Customer',
      sortable: true,
      width: '200px',
      format: (val: any, row?: SalesQuotationDto) => val || row?.customerCode || 'B2B Customer'
    },
    {
      field: 'quotationDate',
      header: 'Submitted Date',
      sortable: true,
      width: '130px',
      format: (val: any) => {
        if (!val) return '-';
        const d = new Date(val);
        return isNaN(d.getTime()) ? String(val) : d.toLocaleDateString();
      }
    },
    {
      field: 'deliveryDate',
      header: 'Required Delivery',
      sortable: true,
      width: '140px',
      format: (val: any) => {
        if (!val) return '—';
        const d = new Date(val);
        return isNaN(d.getTime()) ? String(val) : d.toLocaleDateString();
      }
    },
    {
      field: 'requestReviewStatus',
      header: 'Review Status',
      sortable: true,
      width: '160px',
      type: 'badge',
      badgeSeverityMap: (val: any) => {
        switch (val) {
          case 'QuotationPrepared': return 'success';
          case 'UnderReview': return 'info';
          case 'Closed': return 'danger';
          case 'Submitted':
          default: return 'warning';
        }
      },
      format: (val: any, row?: SalesQuotationDto) => {
        if (row?.linkedCommercialQuotationId || val === 'QuotationPrepared') return 'Quotation Prepared';
        if (val === 'UnderReview') return 'Under Review';
        if (val === 'Closed') return 'Closed';
        return 'Submitted';
      }
    },
    {
      field: 'linkedCommercialQuotationNumber',
      header: 'Linked Offer #',
      sortable: true,
      width: '160px',
      format: (val: any) => val || '—'
    },
    {
      field: 'id',
      header: 'Actions',
      width: '320px',
      type: 'actions'
    }
  ];

  // Tab B: Commercial Quotations Columns
  readonly quotationColumns: DataTableColumn<SalesQuotationDto>[] = [
    {
      field: 'quotationNumber',
      header: 'Quotation #',
      sortable: true,
      width: '150px'
    },
    {
      field: 'linkedCustomerRequestNumber',
      header: 'From Request #',
      sortable: true,
      width: '140px',
      format: (val: any) => val || 'Direct Offer'
    },
    {
      field: 'customerName',
      header: 'Customer',
      sortable: true,
      width: '180px',
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
      width: '120px',
      format: (val: any) => val != null ? `${Number(val).toFixed(2)}%` : '—'
    },
    {
      field: 'approvalStatus',
      header: 'Approval Status',
      sortable: true,
      width: '160px',
      type: 'badge',
      badgeSeverityMap: (_val: any, row?: SalesQuotationDto) => getSalesQuotationApprovalSeverity(row),
      format: (_val: any, row?: SalesQuotationDto) => getSalesQuotationApprovalStatusLabel(row)
    },
    {
      field: 'customerStatus',
      header: 'Customer Progress',
      sortable: true,
      width: '170px',
      type: 'badge',
      badgeSeverityMap: (_val: any, row?: SalesQuotationDto) => getSalesQuotationCustomerStatusSeverity(row),
      format: (_val: any, row?: SalesQuotationDto) => getSalesQuotationCustomerStatusLabel(row)
    },
    {
      field: 'emailStatus',
      header: 'Email Status',
      sortable: true,
      width: '130px',
      type: 'badge',
      badgeSeverityMap: (val: any) => getSalesQuotationEmailStatusSeverity(val),
      format: (val: any) => getSalesQuotationEmailStatusLabel(val)
    },
    {
      field: 'id',
      header: 'Actions',
      width: '320px',
      type: 'actions'
    }
  ];

  // Tab A: Customer Requests Actions
  readonly requestActions: DataTableAction<SalesQuotationDto>[] = [
    {
      id: 'view',
      label: 'View Request',
      icon: 'pi pi-eye',
      severity: 'secondary',
      action: (row: SalesQuotationDto) => this.navigateToQuotation(row)
    },
    {
      id: 'prepare',
      label: 'Prepare Commercial Quotation',
      icon: 'pi pi-file-edit',
      severity: 'primary',
      visible: (row: SalesQuotationDto) => this.canCreate() && !row.linkedCommercialQuotationId,
      action: (row: SalesQuotationDto) => this.prepareCommercialQuotation(row)
    },
    {
      id: 'view_linked',
      label: 'View Linked Quotation',
      icon: 'pi pi-external-link',
      severity: 'secondary',
      visible: (row: SalesQuotationDto) => !!row.linkedCommercialQuotationId,
      action: (row: SalesQuotationDto) => this.navigateToLinkedQuotation(row.linkedCommercialQuotationId!)
    }
  ];

  // Tab B: Commercial Quotations Actions
  readonly quotationActions: DataTableAction<SalesQuotationDto>[] = [
    {
      id: 'view',
      label: 'View',
      icon: 'pi pi-eye',
      severity: 'secondary',
      action: (row: SalesQuotationDto) => this.navigateToQuotation(row)
    },
    {
      id: 'edit',
      label: 'Edit Draft',
      icon: 'pi pi-pencil',
      severity: 'secondary',
      visible: (row: SalesQuotationDto) =>
        Number(row.status) === SalesQuotationStatus.Draft && this.canEdit(),
      action: (row: SalesQuotationDto) => this.navigateToQuotation(row)
    },
    {
      id: 'submit',
      label: 'Submit for Approval',
      icon: 'pi pi-send',
      severity: 'primary',
      visible: (row: SalesQuotationDto) =>
        Number(row.status) === SalesQuotationStatus.Draft && this.canEdit(),
      action: (row: SalesQuotationDto) => this.submitQuotation(row)
    },
    {
      id: 'approve',
      label: 'Approve',
      icon: 'pi pi-check',
      severity: 'success',
      visible: (row: SalesQuotationDto) =>
        Number(row.status) === SalesQuotationStatus.Submitted && this.canApprove(),
      action: (row: SalesQuotationDto) => this.approveQuotationDirectly(row)
    },
    {
      id: 'reject',
      label: 'Reject',
      icon: 'pi pi-times',
      severity: 'danger',
      visible: (row: SalesQuotationDto) =>
        Number(row.status) === SalesQuotationStatus.Submitted && this.canApprove(),
      action: (row: SalesQuotationDto) => this.openRejectDialog(row)
    },
    {
      id: 'revise',
      label: 'Revise',
      icon: 'pi pi-replay',
      severity: 'secondary',
      visible: (row: SalesQuotationDto) =>
        (Number(row.status) === SalesQuotationStatus.RejectedInternal ||
         Number(row.status) === SalesQuotationStatus.DeclinedByCustomer) &&
        this.canEdit(),
      action: (row: SalesQuotationDto) => this.openReviseDialog(row)
    },
    {
      id: 'send',
      label: 'Send Quotation',
      icon: 'pi pi-envelope',
      severity: 'primary',
      visible: (row: SalesQuotationDto) =>
        Number(row.status) === SalesQuotationStatus.Confirmed && this.canEdit(),
      action: (row: SalesQuotationDto) => this.openSendEmailDialog(row, false)
    },
    {
      id: 'retry',
      label: 'Retry Dispatch',
      icon: 'pi pi-refresh',
      severity: 'danger',
      visible: (row: SalesQuotationDto) =>
        row.emailStatus === 'Failed' && this.canEdit(),
      action: (row: SalesQuotationDto) => this.openSendEmailDialog(row, true)
    },
    {
      id: 'convert',
      label: 'Convert to Sales Order',
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
      visible: (row: SalesQuotationDto) => {
        const s = Number(row.status);
        return s === SalesQuotationStatus.Confirmed ||
               s === SalesQuotationStatus.Sent ||
               s === SalesQuotationStatus.Accepted ||
               s === SalesQuotationStatus.Converted;
      },
      action: (row: SalesQuotationDto) => this.downloadPdf(row)
    }
  ];

  readonly currentColumns = computed<DataTableColumn<SalesQuotationDto>[]>(() => {
    return this.activeTab() === 'requests' ? this.requestColumns : this.quotationColumns;
  });

  readonly currentActions = computed<DataTableAction<SalesQuotationDto>[]>(() => {
    return this.activeTab() === 'requests' ? this.requestActions : this.quotationActions;
  });

  ngOnInit(): void {
    this.subs.add(
      this.route.queryParams.subscribe(params => {
        const tab = params['tab'];
        if (tab === 'quotations' || tab === 'requests') {
          this.activeTab.set(tab);
        }
      })
    );
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

  setRequestFilter(filter: RequestFilter): void {
    this.requestFilter.set(filter);
  }

  setQuotationFilter(filter: QuotationFilter): void {
    this.quotationFilter.set(filter);
  }

  navigateToCreate(): void {
    this.router.navigate(['/app/sales/quotations/new']);
  }

  navigateToQuotation(quotation: SalesQuotationDto): void {
    this.router.navigate(['/app/sales/quotations', quotation.id]);
  }

  navigateToLinkedQuotation(linkedQuotationId: string): void {
    this.router.navigate(['/app/sales/quotations', linkedQuotationId]);
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

  private getEffectiveScope(quotation?: SalesQuotationDto | null): { companyId: string; branchId: string } | null {
    const comp = this.scopeService.selectedCompany();
    const br = this.scopeService.selectedBranch();
    const companyId = comp?.id || quotation?.companyId;
    const branchId = br?.id || quotation?.branchId;
    if (!companyId || !branchId) {
      this.message.set({
        text: 'Operating scope is missing. Please ensure an Operating Company and Branch are active.',
        severity: 'warn'
      });
      return null;
    }
    return { companyId, branchId };
  }

  prepareCommercialQuotation(quotation: SalesQuotationDto): void {
    const scope = this.getEffectiveScope(quotation);
    if (!scope) return;

    this.actionInProgress.set(true);
    this.quotationService.prepareQuotationFromRequest(scope.companyId, scope.branchId, quotation.id).subscribe({
      next: created => {
        this.actionInProgress.set(false);
        this.message.set({
          text: `Formal Commercial Quotation ${created.quotationNumber} prepared from Customer Request ${quotation.quotationNumber}.`,
          severity: 'success'
        });
        this.loadQuotations();
        this.router.navigate(['/app/sales/quotations', created.id]);
      },
      error: err => {
        this.actionInProgress.set(false);
        this.message.set({
          text: err?.error?.message || err?.message || 'Failed to prepare commercial quotation from request.',
          severity: 'danger'
        });
      }
    });
  }

  openReviseDialog(quotation: SalesQuotationDto): void {
    const scope = this.getEffectiveScope(quotation);
    if (!scope) return;

    this.confirmDialogConfig.set({
      action: 'revise',
      title: 'Revise Commercial Quotation',
      message: `Return quotation ${quotation.quotationNumber} to Draft for commercial revisions and resubmission?`,
      commentLabel: 'Revision Remarks (Optional)',
      commentPlaceholder: 'Enter revision notes...',
      commentRequired: false,
      confirmLabel: 'Revise Quotation',
      confirmIcon: 'pi pi-replay',
      confirmSeverity: 'primary'
    });
    this.confirmCallback = () => {
      this.actionInProgress.set(true);
      this.quotationService.returnToDraft(scope.companyId, scope.branchId, quotation.id, 'Revision by sales officer').subscribe({
        next: updated => {
          this.actionInProgress.set(false);
          this.message.set({ text: `Quotation ${updated.quotationNumber} returned to Draft for revision.`, severity: 'info' });
          this.loadQuotations();
          this.router.navigate(['/app/sales/quotations', quotation.id]);
        },
        error: err => {
          this.actionInProgress.set(false);
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to return quotation to Draft.', severity: 'danger' });
        }
      });
    };
    this.confirmDialogVisible.set(true);
  }

  convertToOrder(quotation: SalesQuotationDto): void {
    const scope = this.getEffectiveScope(quotation);
    if (!scope) return;

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
      this.quotationService.convertQuotationToOrder(scope.companyId, scope.branchId, quotation.id).subscribe({
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

  approveQuotationDirectly(quotation: SalesQuotationDto): void {
    const scope = this.getEffectiveScope(quotation);
    if (!scope) return;

    this.actionInProgress.set(true);
    this.quotationService.approveQuotation(scope.companyId, scope.branchId, quotation.id, '').subscribe({
      next: updated => {
        this.actionInProgress.set(false);
        this.quotations.update(list => list.map(q => q.id === updated.id ? updated : q));
        this.message.set({
          text: `Quotation ${updated.quotationNumber} approved successfully. You can now issue it to the customer.`,
          severity: 'success'
        });
        this.loadQuotations();
      },
      error: err => {
        this.actionInProgress.set(false);
        this.message.set({
          text: err?.error?.message || err?.message || 'Failed to approve quotation.',
          severity: 'danger'
        });
      }
    });
  }

  openApproveDialog(quotation: SalesQuotationDto): void {
    const scope = this.getEffectiveScope(quotation);
    if (!scope) return;

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
    const scope = this.getEffectiveScope(quotation);
    if (!scope) return;

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
    const scope = this.getEffectiveScope(quotation);
    if (!scope) return;

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
    const scope = this.getEffectiveScope(quotation);
    if (!scope) return;

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
    const scope = this.getEffectiveScope(quotation);
    if (!scope) return;

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
      this.quotationService.submitQuotation(scope.companyId, scope.branchId, quotation.id).subscribe({
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
    if ((result.action === 'submit' || result.action === 'convert' || result.action === 'revise') && this.confirmCallback) {
      this.confirmCallback();
      this.confirmCallback = null;
      return;
    }
    this.executeRemarksActionWith(result.action, result.comment);
  }

  executeRemarksActionWith(action: string, remarks: string): void {
    const quotation = this.remarksTargetQuotation();
    if (!quotation || !action) return;

    const scope = this.getEffectiveScope(quotation);
    if (!scope) return;

    const trimmedRemarks = (remarks || '').trim();
    this.actionInProgress.set(true);

    if (action === 'approve') {
      this.quotationService.approveQuotation(scope.companyId, scope.branchId, quotation.id, trimmedRemarks).subscribe({
        next: updated => {
          this.actionInProgress.set(false);
          this.quotations.update(list => list.map(q => q.id === updated.id ? updated : q));
          this.message.set({ text: `Quotation ${updated.quotationNumber} approved successfully. You can now issue it to the customer.`, severity: 'success' });
          this.loadQuotations();
        },
        error: err => {
          this.actionInProgress.set(false);
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to approve quotation.', severity: 'danger' });
        }
      });
    } else if (action === 'reject') {
      this.quotationService.rejectQuotation(scope.companyId, scope.branchId, quotation.id, trimmedRemarks).subscribe({
        next: updated => {
          this.actionInProgress.set(false);
          this.quotations.update(list => list.map(q => q.id === updated.id ? updated : q));
          this.message.set({ text: `Quotation ${updated.quotationNumber} has been rejected.`, severity: 'warn' });
          this.loadQuotations();
        },
        error: err => {
          this.actionInProgress.set(false);
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to reject quotation.', severity: 'danger' });
        }
      });
    } else if (action === 'cancel') {
      this.quotationService.cancelQuotation(scope.companyId, scope.branchId, quotation.id, trimmedRemarks).subscribe({
        next: updated => {
          this.actionInProgress.set(false);
          this.quotations.update(list => list.map(q => q.id === updated.id ? updated : q));
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
      this.quotationService.sendQuotationEmail(scope.companyId, scope.branchId, quotation.id, isRetry).subscribe({
        next: updated => {
          this.actionInProgress.set(false);
          this.quotations.update(list => list.map(q => q.id === updated.id ? updated : q));
          this.message.set({ text: `Quotation ${updated.quotationNumber} sent to customer successfully.`, severity: 'success' });
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
    const scope = this.getEffectiveScope(quotation);
    if (!scope) return;

    this.quotationService.downloadPdf(scope.companyId, scope.branchId, quotation.id).subscribe({
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
