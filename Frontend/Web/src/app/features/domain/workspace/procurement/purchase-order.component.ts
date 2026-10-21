import { Component, OnInit, OnDestroy, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { TooltipModule } from 'primeng/tooltip';
import { SharedModule } from 'primeng/api';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../shared/components/data-table/data-table.component';
import { AppEditableDataTableComponent } from '../../../../shared/components/data-table/editable-data-table.component';
import { AppStatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { AppEmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { AccessDeniedComponent } from '../../../../shared/components/access-denied/access-denied.component';
import { PurchaseOrderService } from '../../../../core/services/purchase-order.service';
import { MasterDataService } from '../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import {
  PurchaseOrderDto,
  PurchaseOrderItemDto,
  CreatePurchaseOrderRequest,
  PurchaseOrderStatus,
  getPurchaseOrderStatusName,
  getPurchaseOrderStatusSeverity
} from '../../../../core/models/purchase-order.models';
import { DataTableColumn, DataTableAction } from '../../../../shared/models/data-table-config.model';
import { LookupItem } from '../../../../shared/components/lookup-dropdown/lookup-item.model';
import { SupplierItem, ProductItem } from '../../../../core/models/scope.models';

export interface EditablePOLine {
  lineId: string;
  productId: string;
  productCode: string;
  productName: string;
  unitOfMeasureId?: string | null;
  quantity: number;
  unitCost: number;
  lineTotal: number;
}

export type POStatusFilter = 'all' | 'draft' | 'submitted' | 'approved' | 'fulfilled' | 'cancelled';

@Component({
  selector: 'app-purchase-order',
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
    AppEditableDataTableComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AppEmptyStateComponent,
    AccessDeniedComponent
  ],
  templateUrl: './purchase-order.component.html',
  styleUrl: './purchase-order.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PurchaseOrderComponent implements OnInit, OnDestroy {
  readonly poService = inject(PurchaseOrderService);
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router, { optional: true });
  private readonly route = inject(ActivatedRoute, { optional: true });
  private readonly subs = new Subscription();
  private readonly pendingOrderIdToSelect = signal<string | null>(null);

  // Loading & Error States
  readonly loading = signal<boolean>(false);
  readonly actionInProgress = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to access purchase orders for this operating company and branch. Contact your administrator if you believe this is an error."
  );
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Data Signals
  readonly orders = signal<PurchaseOrderDto[]>([]);
  readonly suppliers = signal<SupplierItem[]>([]);
  readonly products = signal<ProductItem[]>([]);

  // Search & Filter State
  readonly searchTerm = signal<string>('');
  readonly activeStatusFilter = signal<POStatusFilter>('all');

  // Breadcrumbs
  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Procurement', routerLink: '/app/procurement' },
    { label: 'Purchase Orders' }
  ];

  // Dialog State: Create
  readonly createDialogVisible = signal<boolean>(false);
  readonly formOrderNumber = signal<string>('');
  readonly formSupplierId = signal<string>('');
  readonly formOrderDate = signal<string>(new Date().toISOString().substring(0, 10));
  readonly formError = signal<string | null>(null);
  readonly workingLines = signal<EditablePOLine[]>([]);

  // Dialog State: Details View
  readonly detailsDialogVisible = signal<boolean>(false);
  readonly selectedOrder = signal<PurchaseOrderDto | null>(null);

  // Dialog State: Remarks (Approve / Reject / Cancel)
  readonly remarksDialogVisible = signal<boolean>(false);
  readonly remarksAction = signal<'approve' | 'reject' | 'cancel' | null>(null);
  readonly remarksTargetOrder = signal<PurchaseOrderDto | null>(null);
  readonly remarksText = signal<string>('');
  readonly remarksError = signal<string | null>(null);

  // Lookup items for products
  readonly productLookupItems = computed<LookupItem[]>(() => {
    return this.products().map(p => ({
      id: p.productId,
      code: p.productCode,
      name: p.productName,
      category: p.categoryName || 'General',
      isEligible: p.isActive && p.isPurchasable,
      meta: {
        cost: p.standardCost,
        unitOfMeasureId: p.unitOfMeasureId
      }
    }));
  });

  // Columns for the main orders data-table
  readonly tableColumns: DataTableColumn<PurchaseOrderDto>[] = [
    {
      field: 'orderNumber',
      header: 'PO Number',
      sortable: true,
      width: '160px'
    },
    {
      field: 'supplierName',
      header: 'Supplier',
      sortable: true,
      width: '220px',
      format: (val: any) => val || 'Standard Vendor'
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
      width: '130px',
      type: 'badge',
      badgeSeverityMap: (val: any) => getPurchaseOrderStatusSeverity(val),
      format: (val: any) => getPurchaseOrderStatusName(val)
    },
    {
      field: 'id',
      header: 'Actions',
      width: '280px',
      type: 'actions'
    }
  ];

  // Row actions for each PO in the list
  readonly tableActions: DataTableAction<PurchaseOrderDto>[] = [
    {
      id: 'view',
      label: 'View',
      icon: 'pi pi-eye',
      severity: 'secondary',
      action: (row: PurchaseOrderDto) => this.navigateToView(row)
    },
    {
      id: 'amend',
      label: 'Amend',
      icon: 'pi pi-pencil',
      severity: 'secondary',
      visible: (row: PurchaseOrderDto) => this.isStatus(row, PurchaseOrderStatus.Draft),
      disabled: () => !this.canEditOrder(),
      action: (row: PurchaseOrderDto) => this.navigateToAmend(row)
    },
    {
      id: 'submit',
      label: 'Submit',
      icon: 'pi pi-send',
      severity: 'primary',
      visible: (row: PurchaseOrderDto) => this.isStatus(row, PurchaseOrderStatus.Draft),
      disabled: () => !this.canSubmitOrder(),
      action: (row: PurchaseOrderDto) => this.onSubmitOrder(row)
    },
    {
      id: 'approve',
      label: 'Approve',
      icon: 'pi pi-check',
      severity: 'success',
      visible: (row: PurchaseOrderDto) => this.isStatus(row, PurchaseOrderStatus.Submitted),
      disabled: () => !this.canApproveOrder(),
      action: (row: PurchaseOrderDto) => this.navigateToApproval(row)
    },
    {
      id: 'reject',
      label: 'Reject',
      icon: 'pi pi-times',
      severity: 'danger',
      visible: (row: PurchaseOrderDto) => this.isStatus(row, PurchaseOrderStatus.Submitted),
      disabled: () => !this.canApproveOrder(),
      action: (row: PurchaseOrderDto) => this.openRemarksDialog('reject', row)
    },
    {
      id: 'receipt',
      label: 'Receipt',
      icon: 'pi pi-box',
      severity: 'success',
      visible: (row: PurchaseOrderDto) => this.isStatus(row, PurchaseOrderStatus.Approved),
      action: (row: PurchaseOrderDto) => this.navigateToProductReceipt(row)
    },
    {
      id: 'cancel',
      label: 'Cancel',
      icon: 'pi pi-ban',
      severity: 'danger',
      visible: (row: PurchaseOrderDto) =>
        this.isStatus(row, PurchaseOrderStatus.Draft) || this.isStatus(row, PurchaseOrderStatus.Submitted),
      disabled: () => !this.canCancelOrder(),
      action: (row: PurchaseOrderDto) => this.openRemarksDialog('cancel', row)
    }
  ];

  // Columns for the editable line items table
  readonly editableItemColumns = computed<DataTableColumn<EditablePOLine>[]>(() => [
    {
      field: 'productCode',
      header: 'Product Code & Name',
      sortable: true,
      width: '250px',
      editable: true,
      editType: 'lookup',
      lookupOptions: this.productLookupItems(),
      onLookupSelect: (item, row) => {
        row.productId = String(item.id);
        row.productName = item.name;
        if (item.meta?.cost !== undefined) {
          row.unitCost = Number(item.meta.cost) || 0;
        }
        if (item.meta?.unitOfMeasureId) {
          row.unitOfMeasureId = item.meta.unitOfMeasureId;
        }
        this.calculateRow(row);
      },
      validators: [(val: any) => (val ? null : 'Product is required')]
    },
    {
      field: 'productName',
      header: 'Description',
      sortable: true,
      width: '220px',
      editable: false
    },
    {
      field: 'quantity',
      header: 'Quantity',
      sortable: true,
      align: 'right',
      width: '120px',
      editable: true,
      editType: 'number',
      validators: [
        (val: any) => {
          const num = Number(val);
          if (isNaN(num)) return 'Must be a number';
          if (num <= 0) return 'Must be > 0';
          return null;
        }
      ]
    },
    {
      field: 'unitCost',
      header: 'Unit Cost',
      sortable: true,
      align: 'right',
      width: '130px',
      type: 'currency',
      editable: true,
      editType: 'currency',
      validators: [
        (val: any) => {
          const num = Number(val);
          if (isNaN(num)) return 'Must be a valid cost';
          if (num < 0) return 'Cannot be negative';
          return null;
        }
      ]
    },
    {
      field: 'lineTotal',
      header: 'Line Total',
      sortable: true,
      align: 'right',
      width: '140px',
      type: 'currency',
      editable: false
    }
  ]);

  // Filtered orders list
  readonly filteredOrders = computed<PurchaseOrderDto[]>(() => {
    let list = this.orders();
    const filter = this.activeStatusFilter();
    if (filter !== 'all') {
      list = list.filter(o => {
        const name = getPurchaseOrderStatusName(o.status).toLowerCase();
        return name === filter;
      });
    }

    const term = this.searchTerm().trim().toLowerCase();
    if (term) {
      list = list.filter(o => {
        const num = (o.orderNumber || '').toLowerCase();
        const sup = (o.supplierName || '').toLowerCase();
        return num.includes(term) || sup.includes(term);
      });
    }

    return list;
  });

  // Calculate row lineTotal
  readonly calculateRow = (row: EditablePOLine): void => {
    const qty = Number(row.quantity) || 0;
    const cost = Number(row.unitCost) || 0;
    row.lineTotal = Math.round(qty * cost * 100) / 100;
  };

  // Generate blank new line
  readonly defaultNewRow = (): Partial<EditablePOLine> => {
    return {
      lineId: 'line-' + Math.random().toString(36).substring(2, 9),
      productId: '',
      productCode: '',
      productName: '',
      quantity: 1,
      unitCost: 0,
      lineTotal: 0
    };
  };

  // Computed total of current working lines
  readonly totalOrderAmount = computed<number>(() => {
    return this.workingLines().reduce((sum, item) => sum + (Number(item.lineTotal) || 0), 0);
  });

  ngOnInit(): void {
    this.loadMasterData();

    if (this.route) {
      this.subs.add(
        this.route.queryParams.subscribe(params => {
          if (params['status']) {
            const st = String(params['status']).toLowerCase();
            if (['all', 'draft', 'submitted', 'approved', 'fulfilled', 'cancelled'].includes(st)) {
              this.activeStatusFilter.set(st as POStatusFilter);
            }
          }
          if (params['action'] === 'create') {
            if (this.canCreateOrder()) {
              this.openCreateDialog();
            }
          }
          if (params['orderId']) {
            this.pendingOrderIdToSelect.set(params['orderId']);
          }
        })
      );
    }

    if (this.scopeService.canOperateTransactions()) {
      this.loadOrders();
    }

    this.subs.add(
      this.scopeService.scopeChange$.subscribe(() => {
        if (this.scopeService.canOperateTransactions()) {
          this.loadOrders();
        } else {
          this.orders.set([]);
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  // Master data loading
  loadMasterData(): void {
    this.masterDataService.getSuppliers().subscribe({
      next: suppliers => this.suppliers.set(suppliers),
      error: () => {}
    });

    this.masterDataService.getProducts().subscribe({
      next: products => this.products.set(products),
      error: () => {}
    });
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
      return "You don't have authorization to perform this action. Contact your administrator if you believe this is an error.";
    }
    const rawMsg = err?.error?.message;
    if (rawMsg && !rawMsg.includes('http://') && !rawMsg.includes('https://') && !rawMsg.includes('localhost')) {
      return rawMsg;
    }
    return fallback;
  }

  // Load orders for active company and branch
  loadOrders(): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) {
      this.orders.set([]);
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.isAccessDenied.set(false);

    this.poService.getOrders(company.id, branch.id).subscribe({
      next: orders => {
        this.loading.set(false);
        this.isAccessDenied.set(false);
        // Enrich order supplier names if missing
        const supplierMap = new Map(this.suppliers().map(s => [s.supplierId, s.supplierName]));
        const enriched = orders.map(o => ({
          ...o,
          supplierName: o.supplierName || (o.supplierId ? supplierMap.get(o.supplierId) : undefined) || 'Standard Vendor'
        }));
        this.orders.set(enriched);

        if (this.pendingOrderIdToSelect()) {
          const match = enriched.find(o => o.id === this.pendingOrderIdToSelect());
          if (match) {
            this.openOrderDetails(match);
          }
        }
      },
      error: err => {
        this.loading.set(false);
        if (this.isForbiddenError(err)) {
          this.isAccessDenied.set(true);
          this.error.set(null);
        } else {
          this.isAccessDenied.set(false);
          this.error.set(this.getCleanErrorMessage(err, 'Failed to load purchase orders. Please try again.'));
        }
      }
    });
  }

  // Helpers for status checks
  isStatus(row: PurchaseOrderDto, status: PurchaseOrderStatus): boolean {
    return Number(row.status) === Number(status) || String(row.status).toLowerCase() === getPurchaseOrderStatusName(status).toLowerCase();
  }

  getStatusName(status: PurchaseOrderStatus | number | string): string {
    return getPurchaseOrderStatusName(status);
  }

  getStatusSeverity(status: PurchaseOrderStatus | number | string): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
    return getPurchaseOrderStatusSeverity(status);
  }

  // Permission Checks
  canCreateOrder(): boolean {
    return (
      this.scopeService.canOperateTransactions() &&
      (this.authService.hasPermission('PURCHASE_ORDER.CREATE') ||
        this.authService.isTenantAdmin() ||
        this.authService.isPlatformAdmin() ||
        this.authService.hasRole('Tenant User') ||
        this.authService.hasRole('Branch User'))
    );
  }

  canSubmitOrder(): boolean {
    return (
      this.authService.hasPermission('PURCHASE_ORDER.SUBMIT') ||
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin() ||
      this.authService.hasRole('Tenant User') ||
      this.authService.hasRole('Branch User')
    );
  }

  canApproveOrder(): boolean {
    return (
      this.authService.hasPermission('PURCHASE_ORDER.APPROVE') ||
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin() ||
      this.authService.hasRole('Tenant Manager') ||
      this.authService.hasRole('Approver')
    );
  }

  canCancelOrder(): boolean {
    return (
      this.authService.hasPermission('PURCHASE_ORDER.CANCEL') ||
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin() ||
      this.authService.hasRole('Tenant User')
    );
  }

  canEditOrder(): boolean {
    return (
      this.authService.hasPermission('PURCHASE_ORDER.EDIT') ||
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin() ||
      this.authService.hasRole('Tenant User') ||
      this.authService.hasRole('Branch User')
    );
  }

  // Navigation methods
  navigateToCreate(): void {
    if (this.router) {
      this.router.navigate(['/app/procurement/purchase-orders/new']);
    } else {
      this.openCreateDialog();
    }
  }

  navigateToView(order: PurchaseOrderDto): void {
    if (this.router) {
      this.router.navigate(['/app/procurement/purchase-orders', order.id]);
    } else {
      this.openOrderDetails(order);
    }
  }

  navigateToAmend(order: PurchaseOrderDto): void {
    if (this.router) {
      this.router.navigate(['/app/procurement/purchase-orders', order.id, 'amend']);
    } else {
      this.openOrderDetails(order);
    }
  }

  navigateToApproval(order: PurchaseOrderDto): void {
    if (this.router) {
      this.router.navigate(['/app/procurement/purchase-orders', order.id, 'approve']);
    } else {
      this.openRemarksDialog('approve', order);
    }
  }

  navigateToProductReceipt(order: PurchaseOrderDto): void {
    if (this.router) {
      this.router.navigate(['/app/procurement/goods-receipts/new'], {
        queryParams: { purchaseOrderId: order.id }
      });
    }
  }

  // Open Create Dialog
  openCreateDialog(): void {
    const yr = new Date().getFullYear();
    const rnd = Math.floor(1000 + Math.random() * 9000);
    this.formOrderNumber.set(`PO-${yr}-${rnd}`);
    this.formSupplierId.set(this.suppliers()[0]?.supplierId || '');
    this.formOrderDate.set(new Date().toISOString().substring(0, 10));
    this.formError.set(null);

    // Initial default lines
    const initialLine: EditablePOLine = {
      lineId: 'line-' + Math.random().toString(36).substring(2, 9),
      productId: '',
      productCode: '',
      productName: '',
      quantity: 1,
      unitCost: 0,
      lineTotal: 0
    };
    this.workingLines.set([initialLine]);
    this.createDialogVisible.set(true);
  }

  onLinesChange(updated: EditablePOLine[]): void {
    this.workingLines.set([...updated]);
  }

  // Save Order (Draft or Direct Submit)
  onSaveOrder(andSubmit: boolean = false): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) {
      this.formError.set('Active company and branch selection required.');
      return;
    }

    const orderNum = this.formOrderNumber().trim();
    if (!orderNum) {
      this.formError.set('Purchase Order Number is required.');
      return;
    }

    // Filter valid lines
    const lines = this.workingLines().filter(l => l.productId || l.productCode);
    if (lines.length === 0) {
      this.formError.set('Please select at least one product line item.');
      return;
    }

    // Ensure all lines have valid quantity > 0
    for (const l of lines) {
      if (!l.productId && !l.productCode) {
        this.formError.set('Please specify a valid product for each line.');
        return;
      }
      if (Number(l.quantity) <= 0) {
        this.formError.set(`Line item for ${l.productCode || 'product'} must have quantity greater than zero.`);
        return;
      }
    }

    this.actionInProgress.set(true);
    this.formError.set(null);

    // If product lookup used code instead of id, resolve id
    const prodMap = new Map(this.products().map(p => [p.productCode, p.productId]));
    const items = lines.map(l => ({
      productId: l.productId || prodMap.get(l.productCode) || '00000000-0000-0000-0000-000000000000',
      unitOfMeasureId: l.unitOfMeasureId || null,
      quantity: Number(l.quantity),
      unitPrice: Number(l.unitCost)
    }));

    const request: CreatePurchaseOrderRequest = {
      orderNumber: orderNum,
      supplierId: this.formSupplierId() || null,
      orderDate: this.formOrderDate() ? new Date(this.formOrderDate()).toISOString() : new Date().toISOString(),
      items
    };

    this.poService.createOrder(company.id, branch.id, request).subscribe({
      next: createdOrder => {
        if (andSubmit) {
          this.poService.submitOrder(company.id, branch.id, createdOrder.id).subscribe({
            next: () => {
              this.actionInProgress.set(false);
              this.createDialogVisible.set(false);
              this.showMessage(`Purchase Order ${orderNum} created and submitted successfully.`, 'success');
              this.loadOrders();
            },
            error: subErr => {
              this.actionInProgress.set(false);
              this.createDialogVisible.set(false);
              this.showMessage(
                `Order created in Draft, but submit failed: ${this.getCleanErrorMessage(subErr, 'Permission denied.')}`,
                'warn'
              );
              this.loadOrders();
            }
          });
        } else {
          this.actionInProgress.set(false);
          this.createDialogVisible.set(false);
          this.showMessage(`Purchase Order ${orderNum} saved as Draft.`, 'success');
          this.loadOrders();
        }
      },
      error: err => {
        this.actionInProgress.set(false);
        this.formError.set(this.getCleanErrorMessage(err, 'Failed to create purchase order.'));
      }
    });
  }

  // Open Order Details
  openOrderDetails(order: PurchaseOrderDto): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) return;

    this.selectedOrder.set(order);
    this.detailsDialogVisible.set(true);

    // Fetch full order with items if needed
    this.poService.getOrder(company.id, branch.id, order.id).subscribe({
      next: fullOrder => {
        if (fullOrder) {
          this.selectedOrder.set({
            ...fullOrder,
            supplierName: fullOrder.supplierName || order.supplierName
          });
        }
      },
      error: () => {}
    });
  }

  // Direct Submit
  onSubmitOrder(order: PurchaseOrderDto): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) return;

    this.actionInProgress.set(true);
    this.poService.submitOrder(company.id, branch.id, order.id).subscribe({
      next: updated => {
        this.actionInProgress.set(false);
        this.showMessage(`Purchase Order ${order.orderNumber} submitted successfully.`, 'success');
        if (this.detailsDialogVisible() && this.selectedOrder()?.id === order.id) {
          this.selectedOrder.set(updated);
        }
        this.loadOrders();
      },
      error: err => {
        this.actionInProgress.set(false);
        this.showMessage(this.getCleanErrorMessage(err, 'Failed to submit order.'), 'danger');
      }
    });
  }

  // Open Remarks Dialog (Approve, Reject, Cancel)
  openRemarksDialog(action: 'approve' | 'reject' | 'cancel', order: PurchaseOrderDto): void {
    this.remarksAction.set(action);
    this.remarksTargetOrder.set(order);
    this.remarksText.set('');
    this.remarksError.set(null);
    this.remarksDialogVisible.set(true);
  }

  // Confirm Remarks Decision
  onConfirmRemarks(): void {
    const action = this.remarksAction();
    const order = this.remarksTargetOrder();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!action || !order || !company || !branch) return;

    const remarks = this.remarksText().trim();
    if (action === 'reject' && !remarks) {
      this.remarksError.set('Remarks are required when rejecting an order.');
      return;
    }

    this.actionInProgress.set(true);
    this.remarksError.set(null);

    let obs;
    if (action === 'approve') {
      obs = this.poService.approveOrder(company.id, branch.id, order.id, remarks);
    } else if (action === 'reject') {
      obs = this.poService.rejectOrder(company.id, branch.id, order.id, remarks);
    } else {
      obs = this.poService.cancelOrder(company.id, branch.id, order.id, remarks);
    }

    obs.subscribe({
      next: updated => {
        this.actionInProgress.set(false);
        this.remarksDialogVisible.set(false);
        const actionVerb = action === 'approve' ? 'approved' : action === 'reject' ? 'rejected' : 'cancelled';
        this.showMessage(`Purchase Order ${order.orderNumber} ${actionVerb} successfully.`, 'success');
        if (this.detailsDialogVisible() && this.selectedOrder()?.id === order.id) {
          this.selectedOrder.set(updated);
        }
        this.loadOrders();
      },
      error: err => {
        this.actionInProgress.set(false);
        this.remarksError.set(this.getCleanErrorMessage(err, `Failed to ${action} order.`));
      }
    });
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
