import { Component, OnInit, OnDestroy, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { TooltipModule } from 'primeng/tooltip';
import { SharedModule } from 'primeng/api';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../shared/components/data-table/data-table.component';
import { AppLoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { AppEmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { AccessDeniedComponent } from '../../../../shared/components/access-denied/access-denied.component';
import { SalesOrderService } from '../../../../core/services/sales-order.service';
import { MasterDataService } from '../../../../core/services/master-data.service';
import { ActionDialogComponent } from '../../../../shared/components/action-dialog/action-dialog.component';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import {
  SalesOrderDto,
  SalesOrderStatus,
  getSalesOrderStatusName,
  getSalesOrderStatusSeverity
} from '../../../../core/models/sales-order.models';
import { CustomerItem } from '../../../../core/models/scope.models';
import { DataTableColumn, DataTableAction } from '../../../../shared/models/data-table-config.model';

export type SOStatusFilter = 'all' | 'draft' | 'submitted' | 'confirmed' | 'fulfilled' | 'cancelled';

@Component({
  selector: 'app-sales-order-register',
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
    AppEmptyStateComponent,
    AccessDeniedComponent,
    ActionDialogComponent
  ],
  templateUrl: './sales-order-register.component.html',
  styleUrl: './sales-order-register.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SalesOrderRegisterComponent implements OnInit, OnDestroy {
  readonly salesOrderService = inject(SalesOrderService);
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService: WorkspaceScopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);

  private readonly subs = new Subscription();

  // Loading & Error States
  readonly loading = signal<boolean>(false);
  readonly actionInProgress = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to access sales orders for this operating company and branch. Contact your administrator if you believe this is an error."
  );
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Data Signals
  readonly orders = signal<SalesOrderDto[]>([]);
  readonly customers = signal<CustomerItem[]>([]);

  // Search & Filter State
  readonly searchTerm = signal<string>('');
  readonly activeStatusFilter = signal<SOStatusFilter>('all');

  // Breadcrumbs
  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Sales', routerLink: '/app/sales' },
    { label: 'Sales Orders' }
  ];

  // Dialog State: Remarks (Confirm / Reject / Cancel)
  readonly remarksDialogVisible = signal<boolean>(false);
  readonly remarksAction = signal<'confirm' | 'reject' | 'cancel' | null>(null);
  readonly remarksTargetOrder = signal<SalesOrderDto | null>(null);
  readonly remarksText = signal<string>('');
  readonly remarksError = signal<string | null>(null);

  // Dialog Computed Properties
  readonly remarksDialogTitle = computed<string>(() => {
    switch (this.remarksAction()) {
      case 'confirm':
        return 'Confirm Sales Order';
      case 'reject':
        return 'Reject Sales Order';
      case 'cancel':
        return 'Cancel Sales Order';
      default:
        return 'Order Action';
    }
  });

  readonly remarksPlaceholder = computed<string>(() => {
    switch (this.remarksAction()) {
      case 'confirm':
        return 'Enter optional confirmation notes or customer reference...';
      case 'reject':
        return 'Please explain the reason for rejecting this customer order...';
      case 'cancel':
        return 'Enter optional cancellation reason or notes...';
      default:
        return 'Enter remarks...';
    }
  });

  readonly remarksSubmitLabel = computed<string>(() => {
    switch (this.remarksAction()) {
      case 'confirm':
        return 'Confirm Order';
      case 'reject':
        return 'Reject Order';
      case 'cancel':
        return 'Cancel Order';
      default:
        return 'Submit';
    }
  });

  readonly remarksSubmitIcon = computed<string>(() => {
    switch (this.remarksAction()) {
      case 'confirm':
        return 'pi pi-check';
      case 'reject':
        return 'pi pi-times';
      case 'cancel':
        return 'pi pi-ban';
      default:
        return 'pi pi-check';
    }
  });

  readonly remarksSubmitSeverity = computed<'success' | 'danger' | 'secondary'>(() => {
    switch (this.remarksAction()) {
      case 'confirm':
        return 'success';
      case 'reject':
      case 'cancel':
        return 'danger';
      default:
        return 'secondary';
    }
  });

  // Filtered Orders Computed
  readonly filteredOrders = computed<SalesOrderDto[]>(() => {
    let list = [...this.orders()];

    const status = this.activeStatusFilter();
    if (status !== 'all') {
      list = list.filter(o => {
        const num = Number(o.status);
        if (status === 'draft') return num === SalesOrderStatus.Draft;
        if (status === 'submitted') return num === SalesOrderStatus.Submitted;
        if (status === 'confirmed') return num === SalesOrderStatus.Confirmed;
        if (status === 'fulfilled') return num === SalesOrderStatus.Fulfilled;
        if (status === 'cancelled') return num === SalesOrderStatus.Cancelled || num === SalesOrderStatus.Rejected;
        return true;
      });
    }

    const term = this.searchTerm().trim().toLowerCase();
    if (term) {
      list = list.filter(o => {
        const num = (o.orderNumber || '').toLowerCase();
        const cust = (o.customerName || '').toLowerCase();
        return num.includes(term) || cust.includes(term);
      });
    }

    return list;
  });

  // Columns for the main orders data-table
  readonly tableColumns: DataTableColumn<SalesOrderDto>[] = [
    {
      field: 'orderNumber',
      header: 'SO Number',
      sortable: true,
      width: '160px'
    },
    {
      field: 'customerName',
      header: 'Customer',
      sortable: true,
      width: '220px',
      format: (val: any) => val || 'Standard Customer'
    },
    {
      field: 'orderDate',
      header: 'Order Date',
      sortable: true,
      width: '130px',
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
      width: '150px',
      type: 'currency'
    },
    {
      field: 'status',
      header: 'Status',
      width: '160px',
      type: 'badge',
      badgeSeverityMap: (val: any) => getSalesOrderStatusSeverity(val),
      format: (val: any) => getSalesOrderStatusName(val)
    },
    {
      field: 'id',
      header: 'Actions',
      width: '280px',
      type: 'actions'
    }
  ];

  // Row actions for each Sales Order
  readonly tableActions: DataTableAction<SalesOrderDto>[] = [
    {
      id: 'view',
      label: 'View',
      icon: 'pi pi-eye',
      severity: 'secondary',
      action: (row: SalesOrderDto) => this.navigateToView(row)
    },
    {
      id: 'amend',
      label: 'Amend',
      icon: 'pi pi-pencil',
      severity: 'secondary',
      visible: (row: SalesOrderDto) => this.isStatus(row, SalesOrderStatus.Draft),
      disabled: () => !this.canEditOrder(),
      action: (row: SalesOrderDto) => this.navigateToAmend(row)
    },
    {
      id: 'submit',
      label: 'Submit',
      icon: 'pi pi-send',
      severity: 'primary',
      visible: (row: SalesOrderDto) => this.isStatus(row, SalesOrderStatus.Draft),
      disabled: () => !this.canSubmitOrder(),
      action: (row: SalesOrderDto) => this.onSubmitOrder(row)
    },
    {
      id: 'confirm',
      label: 'Confirm',
      icon: 'pi pi-check',
      severity: 'success',
      visible: (row: SalesOrderDto) => this.isStatus(row, SalesOrderStatus.Submitted),
      disabled: () => !this.canConfirmOrder(),
      action: (row: SalesOrderDto) => this.openRemarksDialog('confirm', row)
    },
    {
      id: 'dispatch',
      label: 'Issue Stock',
      icon: 'pi pi-box',
      severity: 'success',
      visible: (row: SalesOrderDto) => this.isStatus(row, SalesOrderStatus.Confirmed),
      action: (row: SalesOrderDto) => this.navigateToDispatch(row)
    },
    {
      id: 'reject',
      label: 'Reject',
      icon: 'pi pi-times',
      severity: 'danger',
      visible: (row: SalesOrderDto) => this.isStatus(row, SalesOrderStatus.Submitted),
      disabled: () => !this.canConfirmOrder(),
      action: (row: SalesOrderDto) => this.openRemarksDialog('reject', row)
    },
    {
      id: 'cancel',
      label: 'Cancel',
      icon: 'pi pi-ban',
      severity: 'danger',
      visible: (row: SalesOrderDto) =>
        !this.isStatus(row, SalesOrderStatus.Fulfilled) &&
        !this.isStatus(row, SalesOrderStatus.Cancelled) &&
        !this.isStatus(row, SalesOrderStatus.Rejected),
      disabled: () => !this.canCancelOrder(),
      action: (row: SalesOrderDto) => this.openRemarksDialog('cancel', row)
    }
  ];

  ngOnInit(): void {
    if (this.scopeService.canOperateTransactions()) {
      this.loadOrders();
    }

    this.subs.add(
      this.scopeService.scopeChange$.subscribe(() => {
        if (this.scopeService.canOperateTransactions()) {
          this.loadOrders();
        } else {
          this.orders.set([]);
          this.customers.set([]);
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  loadOrders(): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) {
      this.orders.set([]);
      this.customers.set([]);
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.isAccessDenied.set(false);

    forkJoin({
      orders: this.salesOrderService.getOrders(company.id, branch.id),
      customers: this.masterDataService.getCustomers().pipe(catchError(() => of([] as CustomerItem[])))
    }).subscribe({
      next: ({ orders, customers }) => {
        this.loading.set(false);
        this.customers.set(customers || []);

        const enriched = (orders || []).map(order => {
          const match = (customers || []).find(c => (c.customerId || (c as any).id) === order.customerId);
          return {
            ...order,
            customerName:
              order.customerName ||
              match?.customerName ||
              (order.customerId ? `Customer (${order.customerId.substring(0, 8)})` : 'Standard Customer')
          };
        });

        this.orders.set(enriched);
      },
      error: err => {
        this.loading.set(false);
        if (this.isForbiddenError(err)) {
          this.isAccessDenied.set(true);
          this.error.set(null);
        } else {
          this.error.set(this.getCleanErrorMessage(err, 'Failed to load sales orders.'));
        }
      }
    });
  }

  isStatus(row: SalesOrderDto, status: SalesOrderStatus): boolean {
    return Number(row.status) === status;
  }

  // Permissions
  canCreateOrder(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin()) return true;
    return this.authService.hasPermission('SALES_ORDER.CREATE');
  }

  canEditOrder(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin()) return true;
    return this.authService.hasPermission('SALES_ORDER.EDIT');
  }

  canSubmitOrder(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin()) return true;
    return this.authService.hasPermission('SALES_ORDER.SUBMIT');
  }

  canConfirmOrder(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin()) return true;
    return this.authService.hasPermission('SALES_ORDER.CONFIRM');
  }

  canCancelOrder(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin()) return true;
    return this.authService.hasPermission('SALES_ORDER.CANCEL');
  }

  // Navigations
  navigateToCreate(): void {
    this.router.navigate(['/app/sales/orders/new']);
  }

  navigateToView(order: SalesOrderDto): void {
    this.router.navigate(['/app/sales/orders', order.id]);
  }

  navigateToAmend(order: SalesOrderDto): void {
    this.router.navigate(['/app/sales/orders', order.id, 'edit']);
  }

  navigateToDispatch(order: SalesOrderDto): void {
    this.router.navigate(['/app/inventory/issue-stock/new'], {
      queryParams: {
        purpose: 'sales',
        referenceNumber: order.orderNumber,
        salesOrderId: order.id
      }
    });
  }

  // Direct Submit Action from Register Row
  onSubmitOrder(order: SalesOrderDto): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) return;

    this.actionInProgress.set(true);
    this.salesOrderService.submitOrder(company.id, branch.id, order.id).subscribe({
      next: updated => {
        this.actionInProgress.set(false);
        this.showMessage(`Sales Order ${order.orderNumber} submitted successfully.`, 'success');
        this.loadOrders();
      },
      error: err => {
        this.actionInProgress.set(false);
        this.showMessage(this.getCleanErrorMessage(err, 'Failed to submit sales order.'), 'danger');
      }
    });
  }

  // Remarks Dialog Controls
  openRemarksDialog(action: 'confirm' | 'reject' | 'cancel', order: SalesOrderDto): void {
    this.remarksAction.set(action);
    this.remarksTargetOrder.set(order);
    this.remarksText.set('');
    this.remarksError.set(null);
    this.remarksDialogVisible.set(true);
  }

  closeRemarksDialog(): void {
    if (this.actionInProgress()) return;
    this.remarksDialogVisible.set(false);
    this.remarksTargetOrder.set(null);
    this.remarksAction.set(null);
    this.remarksText.set('');
    this.remarksError.set(null);
  }

  submitRemarksAction(): void {
    const action = this.remarksAction();
    const order = this.remarksTargetOrder();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();

    if (!action || !order || !company || !branch) return;

    const remarks = this.remarksText().trim();
    if (action === 'reject' && !remarks) {
      this.remarksError.set('Rejection remarks are required.');
      return;
    }

    this.actionInProgress.set(true);
    this.remarksError.set(null);

    let obs;
    if (action === 'confirm') {
      obs = this.salesOrderService.confirmOrder(company.id, branch.id, order.id, remarks || undefined);
    } else if (action === 'reject') {
      obs = this.salesOrderService.rejectOrder(company.id, branch.id, order.id, remarks);
    } else {
      obs = this.salesOrderService.cancelOrder(company.id, branch.id, order.id, remarks || undefined);
    }

    obs.subscribe({
      next: () => {
        this.actionInProgress.set(false);
        const actionPast = action === 'confirm' ? 'confirmed' : action === 'reject' ? 'rejected' : 'cancelled';
        this.showMessage(`Sales Order ${order.orderNumber} ${actionPast} successfully.`, 'success');
        this.closeRemarksDialog();
        this.loadOrders();
      },
      error: err => {
        this.actionInProgress.set(false);
        this.remarksError.set(this.getCleanErrorMessage(err, `Failed to ${action} sales order.`));
      }
    });
  }

  showMessage(text: string, severity: 'success' | 'info' | 'warn' | 'danger'): void {
    this.message.set({ text, severity });
    if (severity === 'success' || severity === 'info') {
      setTimeout(() => {
        if (this.message()?.text === text) {
          this.message.set(null);
        }
      }, 5000);
    }
  }

  private isForbiddenError(err: any): boolean {
    if (!err) return false;
    if (err.status === 403 || err.statusText === 'Forbidden') return true;
    if (err?.error?.status === 403) return true;
    const msg = String(err?.message || err?.error?.message || '');
    return msg.includes('403') || msg.toLowerCase().includes('forbidden');
  }

  private getCleanErrorMessage(err: any, fallback: string): string {
    if (this.isForbiddenError(err)) {
      return "You don't have authorization for this operation. Contact your administrator if you believe this is an error.";
    }
    const raw = err?.error?.message;
    if (raw && !raw.includes('http://') && !raw.includes('localhost')) {
      return raw;
    }
    return fallback;
  }
}
