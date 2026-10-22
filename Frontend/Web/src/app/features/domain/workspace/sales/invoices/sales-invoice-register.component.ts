import { Component, OnInit, OnDestroy, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { TooltipModule } from 'primeng/tooltip';
import { SharedModule } from 'primeng/api';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../../shared/components/data-table/data-table.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AccessDeniedComponent } from '../../../../../shared/components/access-denied/access-denied.component';
import { SalesInvoiceService } from '../../../../../core/services/sales-invoice.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import {
  SalesInvoiceDto,
  SalesInvoiceStatus,
  getSalesInvoiceStatusName,
  getSalesInvoiceStatusSeverity
} from '../../../../../core/models/sales-invoice.models';
import { DataTableColumn, DataTableAction } from '../../../../../shared/models/data-table-config.model';

export type InvoiceStatusFilter = 'all' | 'draft' | 'confirmed' | 'cancelled' | 'paid';

@Component({
  selector: 'app-sales-invoice-register',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    InputTextModule,
    DialogModule,
    SharedModule,
    TooltipModule,
    AppPageHeaderComponent,
    AppDataTableComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AccessDeniedComponent
  ],
  templateUrl: './sales-invoice-register.component.html',
  styleUrl: './sales-invoice-register.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SalesInvoiceRegisterComponent implements OnInit, OnDestroy {
  readonly invoiceService = inject(SalesInvoiceService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);

  private readonly subs = new Subscription();

  // Loading & Error States
  readonly loading = signal<boolean>(false);
  readonly actionInProgress = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to access sales invoices for this operating company and branch."
  );
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Invoices Signal
  readonly invoices = signal<SalesInvoiceDto[]>([]);

  // Search & Filter State
  readonly searchTerm = signal<string>('');
  readonly activeStatusFilter = signal<InvoiceStatusFilter>('all');

  // Confirm Dialog State
  readonly confirmDialogVisible = signal<boolean>(false);
  readonly targetInvoice = signal<SalesInvoiceDto | null>(null);
  readonly confirmNotes = signal<string>('');

  // Breadcrumbs
  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Sales', routerLink: '/app/sales' },
    { label: 'Sales Invoices' }
  ];

  readonly statusOptions = [
    { label: 'All', value: 'all' as InvoiceStatusFilter },
    { label: 'Draft', value: 'draft' as InvoiceStatusFilter },
    { label: 'Confirmed', value: 'confirmed' as InvoiceStatusFilter },
    { label: 'Cancelled', value: 'cancelled' as InvoiceStatusFilter }
  ];

  readonly canConfirmInvoice = computed<boolean>(() => {
    return this.authService.hasPermission('SALES_INVOICE.CONFIRM') || this.authService.isTenantAdmin() || this.authService.isPlatformAdmin();
  });

  // Filtered Invoices
  readonly filteredInvoices = computed<SalesInvoiceDto[]>(() => {
    let list = this.invoices();
    const filter = this.activeStatusFilter();
    if (filter !== 'all') {
      const statusMap: Record<string, SalesInvoiceStatus> = {
        draft: SalesInvoiceStatus.Draft,
        confirmed: SalesInvoiceStatus.Confirmed,
        cancelled: SalesInvoiceStatus.Cancelled,
        paid: SalesInvoiceStatus.Paid
      };
      const target = statusMap[filter];
      list = list.filter(i => Number(i.status) === target);
    }

    const term = this.searchTerm().trim().toLowerCase();
    if (term) {
      list = list.filter(i => {
        return (
          (i.invoiceNumber && i.invoiceNumber.toLowerCase().includes(term)) ||
          (i.orderNumber && i.orderNumber.toLowerCase().includes(term)) ||
          (i.shipmentNumber && i.shipmentNumber.toLowerCase().includes(term)) ||
          (i.customerName && i.customerName.toLowerCase().includes(term))
        );
      });
    }

    return list;
  });

  readonly draftCount = computed<number>(() => {
    return this.invoices().filter(i => Number(i.status) === SalesInvoiceStatus.Draft).length;
  });

  readonly confirmedCount = computed<number>(() => {
    return this.invoices().filter(i => Number(i.status) === SalesInvoiceStatus.Confirmed).length;
  });

  readonly totalInvoiceValue = computed<number>(() => {
    return this.invoices()
      .filter(i => Number(i.status) !== SalesInvoiceStatus.Cancelled)
      .reduce((sum, i) => sum + (Number(i.totalAmount) || 0), 0);
  });

  // Table Columns
  readonly columns: DataTableColumn<SalesInvoiceDto>[] = [
    {
      field: 'invoiceNumber',
      header: 'Invoice #',
      sortable: true,
      width: '150px'
    },
    {
      field: 'orderNumber',
      header: 'Order #',
      sortable: true,
      width: '130px'
    },
    {
      field: 'shipmentNumber',
      header: 'Dispatch #',
      sortable: true,
      width: '130px'
    },
    {
      field: 'customerName',
      header: 'Customer',
      sortable: true,
      width: '200px'
    },
    {
      field: 'invoiceDate',
      header: 'Invoice Date',
      sortable: true,
      width: '120px',
      format: (val: any) => (val ? new Date(val).toLocaleDateString() : '-')
    },
    {
      field: 'totalAmount',
      header: 'Total Amount',
      sortable: true,
      width: '140px',
      align: 'right',
      type: 'currency'
    },
    {
      field: 'status',
      header: 'Status',
      sortable: true,
      width: '130px',
      type: 'badge',
      badgeSeverityMap: (val: any) => getSalesInvoiceStatusSeverity(val),
      format: (val: any) => getSalesInvoiceStatusName(val)
    },
    {
      field: 'id',
      header: 'Actions',
      width: '240px',
      type: 'actions'
    }
  ];

  // Actions
  readonly tableActions: DataTableAction<SalesInvoiceDto>[] = [
    {
      id: 'view',
      label: 'View',
      icon: 'pi pi-eye',
      severity: 'secondary',
      action: (inv: SalesInvoiceDto) => this.navigateToView(inv)
    },
    {
      id: 'edit',
      label: 'Amend Draft',
      icon: 'pi pi-pencil',
      severity: 'secondary',
      visible: (inv: SalesInvoiceDto) => Number(inv.status) === SalesInvoiceStatus.Draft,
      action: (inv: SalesInvoiceDto) => this.navigateToEdit(inv)
    },
    {
      id: 'confirm',
      label: 'Confirm Invoice',
      icon: 'pi pi-check',
      severity: 'success',
      visible: (inv: SalesInvoiceDto) => Number(inv.status) === SalesInvoiceStatus.Draft && this.canConfirmInvoice(),
      action: (inv: SalesInvoiceDto) => this.openConfirmDialog(inv)
    }
  ];

  ngOnInit(): void {
    this.subs.add(
      this.scopeService.scopeChange$.subscribe(() => {
        this.loadData();
      })
    );
    this.loadData();
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  loadData(): void {
    const comp = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!comp?.id) {
      this.invoices.set([]);
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.isAccessDenied.set(false);

    this.invoiceService.getInvoices(comp.id, branch?.id || null).subscribe({
      next: list => {
        this.invoices.set(list || []);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        if (err?.status === 403) {
          this.isAccessDenied.set(true);
        } else {
          this.error.set(err?.message || 'Failed to load sales invoices.');
        }
      }
    });
  }

  setStatusFilter(filter: InvoiceStatusFilter): void {
    this.activeStatusFilter.set(filter);
  }

  navigateToView(inv: SalesInvoiceDto): void {
    this.router.navigate(['/app/sales/invoices', inv.id]);
  }

  navigateToEdit(inv: SalesInvoiceDto): void {
    this.router.navigate(['/app/sales/invoices', inv.id, 'edit']);
  }

  openConfirmDialog(inv: SalesInvoiceDto): void {
    this.targetInvoice.set(inv);
    this.confirmNotes.set('');
    this.confirmDialogVisible.set(true);
  }

  closeConfirmDialog(): void {
    this.confirmDialogVisible.set(false);
    this.targetInvoice.set(null);
  }

  executeConfirm(): void {
    const inv = this.targetInvoice();
    const comp = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!inv || !comp?.id || !branch?.id) return;

    this.actionInProgress.set(true);
    this.invoiceService
      .confirmInvoice(comp.id, branch.id, inv.id, this.confirmNotes().trim() || undefined)
      .subscribe({
        next: updated => {
          this.actionInProgress.set(false);
          this.closeConfirmDialog();
          this.message.set({
            text: `Sales invoice ${updated.invoiceNumber} confirmed successfully.`,
            severity: 'success'
          });
          this.loadData();
        },
        error: err => {
          this.actionInProgress.set(false);
          this.message.set({
            text: err?.error?.message || err?.message || 'Failed to confirm sales invoice.',
            severity: 'danger'
          });
        }
      });
  }
}
