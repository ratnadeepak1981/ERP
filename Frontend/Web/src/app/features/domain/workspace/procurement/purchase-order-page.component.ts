import {
  Component,
  OnInit,
  OnDestroy,
  inject,
  signal,
  computed,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { TooltipModule } from 'primeng/tooltip';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppReactiveFormComponent } from '../../../../shared/components/forms/reactive-form.component';
import { FormFieldConfig } from '../../../../shared/components/forms/form-field-config.model';
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
import { DataTableColumn } from '../../../../shared/models/data-table-config.model';
import { LookupItem } from '../../../../shared/components/lookup-dropdown/lookup-item.model';
import { Company, Branch, SupplierItem, ProductItem } from '../../../../core/models/scope.models';

export type POPageMode = 'create' | 'edit' | 'view';

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

@Component({
  selector: 'app-purchase-order-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterModule,
    ButtonModule,
    InputTextModule,
    DialogModule,
    TooltipModule,
    AppPageHeaderComponent,
    AppReactiveFormComponent,
    AppEditableDataTableComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AccessDeniedComponent
  ],
  templateUrl: './purchase-order-page.component.html',
  styleUrl: './purchase-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PurchaseOrderPageComponent implements OnInit, OnDestroy {
  readonly poService = inject(PurchaseOrderService);
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);
  readonly route = inject(ActivatedRoute);

  private readonly subs = new Subscription();

  // Mode & Route State
  readonly mode = signal<POPageMode>('create');
  readonly orderId = signal<string | null>(null);
  readonly currentOrder = signal<PurchaseOrderDto | null>(null);

  // Loading & Error States
  readonly loading = signal<boolean>(false);
  readonly actionInProgress = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to access purchase orders for this operating company and branch. Contact your administrator if you believe this is an error."
  );
  readonly serverErrors = signal<Record<string, string>>({});
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Master Data
  readonly suppliers = signal<SupplierItem[]>([]);
  readonly products = signal<ProductItem[]>([]);

  // Operating Scope Selection
  readonly selectedCompanyId = signal<string>('');
  readonly selectedBranchId = signal<string>('');
  readonly pendingCompanyChange = signal<Company | null>(null);
  readonly confirmScopeChangeDialog = signal<boolean>(false);

  // Order Lines
  readonly workingLines = signal<EditablePOLine[]>([]);

  // Dialog State: Remarks (Approve / Reject / Cancel)
  readonly remarksDialogVisible = signal<boolean>(false);
  readonly remarksAction = signal<'approve' | 'reject' | 'cancel' | null>(null);
  readonly remarksText = signal<string>('');
  readonly remarksError = signal<string | null>(null);

  // Reactive Header Form
  readonly headerForm = new FormGroup({
    orderNumber: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    supplierId: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    orderDate: new FormControl<string>(new Date().toISOString().substring(0, 10), {
      nonNullable: true,
      validators: [Validators.required]
    }),
    deliveryDate: new FormControl<string>(''),
    currency: new FormControl<string>('USD', { nonNullable: true }),
    paymentTerms: new FormControl<string>('Net 30'),
    shippingMethod: new FormControl<string>('Standard Ground'),
    notes: new FormControl<string>('')
  });

  // Breadcrumbs
  readonly breadcrumbs = computed(() => {
    const list = [
      { label: 'Workspace', routerLink: '/app/dashboard' },
      { label: 'Procurement', routerLink: '/app/procurement' },
      { label: 'Purchase Orders', routerLink: '/app/procurement/purchase-orders' }
    ];

    if (this.mode() === 'create') {
      list.push({ label: 'New Purchase Order', routerLink: '' });
    } else {
      const num = this.currentOrder()?.orderNumber || 'Order Details';
      list.push({ label: num, routerLink: '' });
    }
    return list;
  });

  // Page Title & Subtitle
  readonly pageTitle = computed(() => {
    switch (this.mode()) {
      case 'create':
        return 'New Purchase Order';
      case 'edit':
        return `Edit Purchase Order ${this.currentOrder()?.orderNumber || ''}`.trim();
      case 'view':
      default:
        return `Purchase Order ${this.currentOrder()?.orderNumber || ''}`.trim();
    }
  });

  readonly pageSubtitle = computed(() => {
    switch (this.mode()) {
      case 'create':
        return 'Create a formal vendor requisition with multi-branch scope, item pricing, and approval workflow.';
      case 'edit':
        return 'Modify requisition lines, delivery timeline, and supplier information before submission.';
      case 'view':
      default:
        return 'Inspect order fulfillment status, line requisitions, and audit history.';
    }
  });

  // Product Lookup Items for Editable Table
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

  // Form Field Configurations for Reusable Form
  readonly formFields = computed<FormFieldConfig[]>(() => {
    const isView = this.mode() === 'view';
    const supplierOptions = this.suppliers().map(s => ({
      label: `${s.supplierName} (${s.supplierCode})`,
      value: s.supplierId
    }));

    return [
      {
        name: 'orderNumber',
        label: 'PO Number',
        type: 'text',
        required: true,
        placeholder: 'e.g. PO-2026-001',
        readonly: isView || this.mode() === 'edit',
        tooltip: 'Unique identifier for the purchase order',
        colSpan: 1
      },
      {
        name: 'supplierId',
        label: 'Supplier / Vendor',
        type: 'select',
        required: true,
        placeholder: '-- Select Supplier --',
        options: supplierOptions,
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'orderDate',
        label: 'Order Date',
        type: 'date',
        required: true,
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'deliveryDate',
        label: 'Expected Delivery Date',
        type: 'date',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'currency',
        label: 'Currency',
        type: 'select',
        options: [
          { label: 'USD ($)', value: 'USD' },
          { label: 'EUR (€)', value: 'EUR' },
          { label: 'GBP (£)', value: 'GBP' },
          { label: 'LKR (Rs)', value: 'LKR' }
        ],
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'paymentTerms',
        label: 'Payment Terms',
        type: 'select',
        options: [
          { label: 'Net 30 Days', value: 'Net 30' },
          { label: 'Net 60 Days', value: 'Net 60' },
          { label: 'Due Upon Receipt', value: 'Due on Receipt' },
          { label: 'Advance Payment', value: 'Advance' }
        ],
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'shippingMethod',
        label: 'Shipping Method',
        type: 'text',
        placeholder: 'e.g. Standard Freight, Express',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'notes',
        label: 'Special Instructions / Notes',
        type: 'textarea',
        placeholder: 'Enter delivery gate details, packaging guidelines, or vendor terms...',
        rows: 2,
        disabled: isView,
        colSpan: 2
      }
    ];
  });

  // Columns for the Editable Line Items Table
  readonly editableItemColumns = computed<DataTableColumn<EditablePOLine>[]>(() => {
    const isView = this.mode() === 'view';
    return [
      {
        field: 'productCode',
        header: 'Product Code & Name',
        sortable: true,
        width: '260px',
        editable: !isView,
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
        width: '240px',
        editable: false
      },
      {
        field: 'quantity',
        header: 'Quantity',
        sortable: true,
        align: 'right',
        width: '120px',
        editable: !isView,
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
        editable: !isView,
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
    ];
  });

  // Row Calculation Callback
  readonly calculateRow = (row: EditablePOLine): void => {
    const qty = Number(row.quantity) || 0;
    const cost = Number(row.unitCost) || 0;
    row.lineTotal = Math.round(qty * cost * 100) / 100;
    this.workingLines.set([...this.workingLines()]);
  };

  // Blank New Row Factory
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

  // Reactive Totals
  readonly subtotalAmount = computed<number>(() => {
    return this.workingLines().reduce((sum, item) => sum + (Number(item.lineTotal) || 0), 0);
  });

  readonly taxAmount = computed<number>(() => {
    // 0% default or calculate standard 0
    return 0;
  });

  readonly totalOrderAmount = computed<number>(() => {
    return this.subtotalAmount() + this.taxAmount();
  });

  // Current Order Status Name & Severity
  readonly orderStatusName = computed<string>(() => {
    const ord = this.currentOrder();
    if (!ord) return 'Draft';
    return getPurchaseOrderStatusName(ord.status);
  });

  readonly orderStatusSeverity = computed<'success' | 'warning' | 'danger' | 'info' | 'neutral'>(() => {
    const ord = this.currentOrder();
    if (!ord) return 'info';
    return getPurchaseOrderStatusSeverity(ord.status);
  });

  // Scope selection helpers
  readonly availableCompanies = computed<Company[]>(() => this.scopeService.companies());
  readonly availableBranches = computed<Branch[]>(() => this.scopeService.branches());

  readonly currentOperatingScopeText = computed<string>(() => {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company) return 'No company selected';
    if (!branch) return `${company.name} / All Permitted Branches`;
    return `${company.name} / ${branch.name}`;
  });

  ngOnInit(): void {
    this.detectRouteMode();
    this.initScope();
    this.loadMasterData();

    this.subs.add(
      this.route.paramMap.subscribe(() => {
        this.detectRouteMode();
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  // Detect mode: /new (create), /:id/edit (edit), /:id (view)
  private detectRouteMode(): void {
    const url = this.router.url;
    const id = this.route.snapshot.paramMap.get('id');

    if (url.includes('/purchase-orders/new')) {
      this.mode.set('create');
      this.orderId.set(null);
      this.initCreateForm();
    } else if (url.endsWith('/edit') && id) {
      this.mode.set('edit');
      this.orderId.set(id);
      this.loadExistingOrder(id);
    } else if (id) {
      this.mode.set('view');
      this.orderId.set(id);
      this.loadExistingOrder(id);
    } else {
      this.mode.set('create');
      this.initCreateForm();
    }
  }

  // Initialize scope based on WorkspaceScopeService
  private initScope(): void {
    const comp = this.scopeService.selectedCompany();
    const br = this.scopeService.selectedBranch();

    if (comp) {
      this.selectedCompanyId.set(comp.id);
    }
    if (br) {
      this.selectedBranchId.set(br.id);
    }

    // React to changes in workspace scope service
    this.subs.add(
      this.scopeService.scopeChange$.subscribe(scope => {
        if (scope.company) {
          this.selectedCompanyId.set(scope.company.id);
        }
        if (scope.branch) {
          this.selectedBranchId.set(scope.branch.id);
        }
      })
    );
  }

  // Handle user changing company in Step 1
  onCompanyChange(companyId: string): void {
    const targetComp = this.availableCompanies().find(c => c.id === companyId) || null;
    if (!targetComp) return;

    // Check if user has active line items entered
    const hasDirtyLines = this.workingLines().some(l => !!l.productId || l.unitCost > 0);
    if (hasDirtyLines) {
      this.pendingCompanyChange.set(targetComp);
      this.confirmScopeChangeDialog.set(true);
    } else {
      this.applyCompanyChange(targetComp);
    }
  }

  confirmCompanyChange(): void {
    const comp = this.pendingCompanyChange();
    if (comp) {
      this.applyCompanyChange(comp);
      // Reset lines
      this.workingLines.set([this.defaultNewRow() as EditablePOLine]);
    }
    this.confirmScopeChangeDialog.set(false);
    this.pendingCompanyChange.set(null);
  }

  cancelCompanyChange(): void {
    // Revert dropdown selection to current company
    const current = this.scopeService.selectedCompany();
    if (current) {
      this.selectedCompanyId.set(current.id);
    }
    this.confirmScopeChangeDialog.set(false);
    this.pendingCompanyChange.set(null);
  }

  private applyCompanyChange(company: Company): void {
    this.selectedCompanyId.set(company.id);
    this.scopeService.selectCompany(company);
    // Auto-select first branch when branches are populated
    setTimeout(() => {
      const branches = this.scopeService.branches();
      if (branches.length > 0) {
        this.selectedBranchId.set(branches[0].id);
        this.scopeService.selectBranch(branches[0]);
      } else {
        this.selectedBranchId.set('');
      }
    }, 100);
  }

  // Handle user changing branch in Step 1
  onBranchChange(branchId: string): void {
    this.selectedBranchId.set(branchId);
    const br = this.availableBranches().find(b => b.id === branchId) || null;
    this.scopeService.selectBranch(br);
  }

  // Initialize Create Form
  private initCreateForm(): void {
    this.headerForm.reset({
      orderNumber: this.generateOrderNumber(),
      supplierId: '',
      orderDate: new Date().toISOString().substring(0, 10),
      deliveryDate: '',
      currency: 'USD',
      paymentTerms: 'Net 30',
      shippingMethod: 'Standard Ground',
      notes: ''
    });
    this.workingLines.set([this.defaultNewRow() as EditablePOLine]);
    this.currentOrder.set(null);
    this.error.set(null);
    this.isAccessDenied.set(false);
  }

  private generateOrderNumber(): string {
    const year = new Date().getFullYear();
    const rand = Math.floor(1000 + Math.random() * 9000);
    return `PO-${year}-${rand}`;
  }

  // Load Existing Order for Edit or View
  loadExistingOrder(orderId: string): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.isAccessDenied.set(false);

    this.poService.getOrder(company.id, branch.id, orderId).subscribe({
      next: order => {
        this.loading.set(false);
        if (!order) {
          this.error.set('Order not found.');
          return;
        }
        this.populateOrderData(order);
      },
      error: err => {
        this.loading.set(false);
        if (this.isForbiddenError(err)) {
          this.isAccessDenied.set(true);
        } else {
          this.error.set(this.getCleanErrorMessage(err, 'Failed to load purchase order details.'));
        }
      }
    });
  }

  private populateOrderData(order: PurchaseOrderDto): void {
    this.currentOrder.set(order);
    this.headerForm.patchValue({
      orderNumber: order.orderNumber,
      supplierId: order.supplierId || '',
      orderDate: order.orderDate ? order.orderDate.substring(0, 10) : new Date().toISOString().substring(0, 10),
      notes: ''
    });

    if (order.items && order.items.length > 0) {
      const lines: EditablePOLine[] = order.items.map(item => ({
        lineId: item.id || 'line-' + Math.random().toString(36).substring(2, 9),
        productId: item.productId,
        productCode: item.productCode || '',
        productName: item.productName || '',
        unitOfMeasureId: item.unitOfMeasureId || null,
        quantity: item.quantity,
        unitCost: item.unitPrice,
        lineTotal: item.lineTotal || item.quantity * item.unitPrice
      }));
      this.workingLines.set(lines);
    } else {
      this.workingLines.set([this.defaultNewRow() as EditablePOLine]);
    }
  }

  // Load Master Data: Suppliers and Products
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

  // Save Order (Draft or Submit)
  saveOrder(submit: boolean): void {
    if (!this.scopeService.canOperateTransactions()) {
      this.showMessage('Please select an operating company and branch first.', 'warn');
      return;
    }

    if (this.headerForm.invalid) {
      this.headerForm.markAllAsTouched();
      this.showMessage('Please correct required fields in the header details.', 'danger');
      return;
    }

    const validLines = this.workingLines().filter(l => !!l.productId && Number(l.quantity) > 0);
    if (validLines.length === 0) {
      this.showMessage('At least one valid line item with product and quantity is required.', 'danger');
      return;
    }

    const company = this.scopeService.selectedCompany()!;
    const branch = this.scopeService.selectedBranch()!;
    const formVal = this.headerForm.getRawValue();

    const items = validLines.map(l => ({
      productId: l.productId,
      unitOfMeasureId: l.unitOfMeasureId || null,
      quantity: Number(l.quantity),
      unitPrice: Number(l.unitCost)
    }));

    const request: CreatePurchaseOrderRequest = {
      orderNumber: formVal.orderNumber,
      supplierId: formVal.supplierId || null,
      orderDate: formVal.orderDate ? new Date(formVal.orderDate).toISOString() : new Date().toISOString(),
      items
    };

    this.actionInProgress.set(true);
    this.serverErrors.set({});

    this.poService.createOrder(company.id, branch.id, request).subscribe({
      next: createdOrder => {
        if (submit) {
          this.poService.submitOrder(company.id, branch.id, createdOrder.id).subscribe({
            next: () => {
              this.actionInProgress.set(false);
              this.showMessage(`Purchase Order ${formVal.orderNumber} created and submitted successfully.`, 'success');
              this.router.navigate(['/app/procurement/purchase-orders']);
            },
            error: subErr => {
              this.actionInProgress.set(false);
              this.showMessage(
                `Order created as Draft, but submission failed: ${this.getCleanErrorMessage(subErr, 'Permission denied.')}`,
                'warn'
              );
              this.router.navigate(['/app/procurement/purchase-orders']);
            }
          });
        } else {
          this.actionInProgress.set(false);
          this.showMessage(`Purchase Order ${formVal.orderNumber} saved as Draft.`, 'success');
          this.router.navigate(['/app/procurement/purchase-orders']);
        }
      },
      error: err => {
        this.actionInProgress.set(false);
        const cleanMsg = this.getCleanErrorMessage(err, 'Failed to save purchase order.');
        this.showMessage(cleanMsg, 'danger');
        if (err?.error?.errors) {
          this.serverErrors.set(err.error.errors);
        }
      }
    });
  }

  // View Mode Workflow Actions
  navigateToEdit(): void {
    const id = this.orderId();
    if (id) {
      this.router.navigate(['/app/procurement/purchase-orders', id, 'edit']);
    }
  }

  navigateToProductReceipt(): void {
    const ord = this.currentOrder();
    if (ord) {
      this.router.navigate(['/app/procurement/goods-receipts/new'], {
        queryParams: { purchaseOrderId: ord.id }
      });
    }
  }

  isApproved(): boolean {
    const ord = this.currentOrder();
    if (!ord) return false;
    const st = Number(ord.status);
    return st === PurchaseOrderStatus.Approved || String(ord.status).toLowerCase() === 'approved';
  }

  navigateBack(): void {
    this.router.navigate(['/app/procurement/purchase-orders']);
  }

  submitCurrentOrder(): void {
    const ord = this.currentOrder();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!ord || !company || !branch) return;

    this.actionInProgress.set(true);
    this.poService.submitOrder(company.id, branch.id, ord.id).subscribe({
      next: updated => {
        this.actionInProgress.set(false);
        this.currentOrder.set(updated);
        this.showMessage(`Purchase Order ${ord.orderNumber} submitted for approval.`, 'success');
      },
      error: err => {
        this.actionInProgress.set(false);
        this.showMessage(this.getCleanErrorMessage(err, 'Failed to submit order.'), 'danger');
      }
    });
  }

  openRemarksDialog(action: 'approve' | 'reject' | 'cancel'): void {
    this.remarksAction.set(action);
    this.remarksText.set('');
    this.remarksError.set(null);
    this.remarksDialogVisible.set(true);
  }

  confirmRemarksDecision(): void {
    const action = this.remarksAction();
    const ord = this.currentOrder();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!action || !ord || !company || !branch) return;

    const remarks = this.remarksText().trim();
    if (action === 'reject' && !remarks) {
      this.remarksError.set('Remarks are mandatory when rejecting a purchase order.');
      return;
    }

    this.actionInProgress.set(true);
    this.remarksError.set(null);

    let obs;
    if (action === 'approve') {
      obs = this.poService.approveOrder(company.id, branch.id, ord.id, remarks);
    } else if (action === 'reject') {
      obs = this.poService.rejectOrder(company.id, branch.id, ord.id, remarks);
    } else {
      obs = this.poService.cancelOrder(company.id, branch.id, ord.id, remarks);
    }

    obs.subscribe({
      next: updated => {
        this.actionInProgress.set(false);
        this.remarksDialogVisible.set(false);
        this.currentOrder.set(updated);
        const actionVerb = action === 'approve' ? 'approved' : action === 'reject' ? 'rejected' : 'cancelled';
        this.showMessage(`Purchase Order ${ord.orderNumber} ${actionVerb} successfully.`, 'success');
      },
      error: err => {
        this.actionInProgress.set(false);
        this.remarksError.set(this.getCleanErrorMessage(err, `Failed to ${action} order.`));
      }
    });
  }

  // Permission Checks
  canApprove(): boolean {
    return this.authService.hasRole('TenantAdmin') || this.authService.hasRole('Approver');
  }

  canEdit(): boolean {
    const ord = this.currentOrder();
    if (!ord) return true;
    const st = Number(ord.status);
    return st === PurchaseOrderStatus.Draft;
  }

  canSubmit(): boolean {
    const ord = this.currentOrder();
    if (!ord) return true;
    const st = Number(ord.status);
    return st === PurchaseOrderStatus.Draft;
  }

  canCancel(): boolean {
    const ord = this.currentOrder();
    if (!ord) return false;
    const st = Number(ord.status);
    return st === PurchaseOrderStatus.Draft || st === PurchaseOrderStatus.Submitted;
  }

  // Error Sanitization
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

  showMessage(text: string, severity: 'success' | 'info' | 'warn' | 'danger'): void {
    this.message.set({ text, severity });
    setTimeout(() => {
      if (this.message()?.text === text) {
        this.message.set(null);
      }
    }, 6000);
  }
}
