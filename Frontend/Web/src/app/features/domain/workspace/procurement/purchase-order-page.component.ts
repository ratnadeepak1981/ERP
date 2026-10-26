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
import { SharedModule } from 'primeng/api';
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
import { Company, Branch, SupplierItem, ProductItem, TaxCodeItem } from '../../../../core/models/scope.models';

export type POPageMode = 'create' | 'view' | 'amend' | 'approval';

export interface EditablePOLine {
  lineId: string;
  productId: string;
  productCode: string;
  productName: string;
  unitOfMeasureId?: string | null;
  quantity: number;
  unitCost: number;
  discountRate?: number;
  discountAmount?: number;
  taxCodeId?: string | null;
  taxRate?: number;
  taxAmount?: number;
  subTotal?: number;
  lineTotal: number;
  isTaxInclusive?: boolean;
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
    SharedModule,
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

  readonly taxCodes = signal<TaxCodeItem[]>([]);
  readonly taxCodeLookupItems = computed<LookupItem[]>(() => {
    return this.taxCodes().map(tc => ({
      id: tc.taxCodeId,
      code: tc.code,
      name: `${tc.code} (${tc.rate}%)`,
      meta: { rate: tc.rate }
    }));
  });

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

  // Dedicated Dialog States
  readonly approveDialogVisible = signal<boolean>(false);
  readonly approveRemarks = signal<string>('');
  readonly approveError = signal<string | null>(null);

  readonly rejectDialogVisible = signal<boolean>(false);
  readonly rejectRemarks = signal<string>('');
  readonly rejectError = signal<string | null>(null);

  readonly cancelDialogVisible = signal<boolean>(false);
  readonly cancelRemarks = signal<string>('');
  readonly cancelError = signal<string | null>(null);

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

  // Read-only state for view and approval modes
  readonly isReadOnlyMode = computed<boolean>(() => this.mode() === 'view' || this.mode() === 'approval');

  // Breadcrumbs
  readonly breadcrumbs = computed(() => {
    const list = [
      { label: 'Workspace', routerLink: '/app/dashboard' },
      { label: 'Procurement', routerLink: '/app/procurement' },
      { label: 'Purchase Orders', routerLink: '/app/procurement/purchase-orders' }
    ];

    switch (this.mode()) {
      case 'create':
        list.push({ label: 'New Purchase Order', routerLink: '' });
        break;
      case 'amend':
        list.push({ label: 'Amend Purchase Order', routerLink: '' });
        break;
      case 'approval':
        list.push({ label: 'Purchase Order Approval', routerLink: '' });
        break;
      case 'view':
      default: {
        const num = this.currentOrder()?.orderNumber || 'Order Details';
        list.push({ label: num, routerLink: '' });
        break;
      }
    }
    return list;
  });

  // Page Title & Subtitle
  readonly pageTitle = computed(() => {
    const num = this.currentOrder()?.orderNumber ? ` - ${this.currentOrder()?.orderNumber}` : '';
    switch (this.mode()) {
      case 'create':
        return 'New Purchase Order';
      case 'amend':
        return `Amend Purchase Order${num}`;
      case 'approval':
        return `Purchase Order Approval${num}`;
      case 'view':
      default:
        return `Purchase Order Details${num}`;
    }
  });

  readonly pageSubtitle = computed(() => {
    switch (this.mode()) {
      case 'create':
        return 'Create a formal vendor requisition with multi-branch scope, item pricing, and approval workflow.';
      case 'amend':
        return 'Modify requisition lines, delivery timeline, and supplier information before submission.';
      case 'approval':
        return 'Review requisition lines and commercial terms to authorize or reject this purchase order.';
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
    const isReadOnly = this.isReadOnlyMode();
    const supplierOptions = [...this.suppliers().map(s => ({
      label: `${s.supplierName} (${s.supplierCode})`,
      value: s.supplierId
    }))];

    // Ensure currently selected order's supplier is in options if not present
    const currentOrd = this.currentOrder();
    if (currentOrd?.supplierId && !supplierOptions.some(o => o.value === currentOrd.supplierId)) {
      supplierOptions.unshift({
        label: currentOrd.supplierName ? `${currentOrd.supplierName}` : currentOrd.supplierId,
        value: currentOrd.supplierId
      });
    }

    return [
      {
        name: 'orderNumber',
        label: 'PO Number',
        type: 'text',
        required: true,
        placeholder: 'e.g. PO-2026-001',
        readonly: isReadOnly || this.mode() === 'amend',
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
        disabled: isReadOnly,
        colSpan: 1
      },
      {
        name: 'orderDate',
        label: 'Order Date',
        type: 'date',
        required: true,
        disabled: isReadOnly,
        colSpan: 1
      },
      {
        name: 'deliveryDate',
        label: 'Expected Delivery Date',
        type: 'date',
        disabled: isReadOnly,
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
        disabled: isReadOnly,
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
        disabled: isReadOnly,
        colSpan: 1
      },
      {
        name: 'shippingMethod',
        label: 'Shipping Method',
        type: 'text',
        placeholder: 'e.g. Standard Freight, Express',
        disabled: isReadOnly,
        colSpan: 1
      },
      {
        name: 'notes',
        label: 'Special Instructions / Notes',
        type: 'textarea',
        placeholder: 'Enter delivery gate details, packaging guidelines, or vendor terms...',
        rows: 2,
        disabled: isReadOnly,
        colSpan: 2
      }
    ];
  });

  // Columns for the Editable Line Items Table
  readonly editableItemColumns = computed<DataTableColumn<EditablePOLine>[]>(() => {
    const isReadOnly = this.isReadOnlyMode();
    return [
      {
        field: 'productCode',
        header: 'Product Code & Name',
        sortable: true,
        width: '260px',
        editable: !isReadOnly,
        editType: 'lookup',
        lookupOptions: this.productLookupItems(),
        onLookupSelect: (item, row) => {
          row.productId = String(item.id);
          row.productCode = item.code;
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
        editable: !isReadOnly,
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
        width: '120px',
        type: 'currency',
        editable: !isReadOnly,
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
        field: 'discountRate',
        header: 'Disc %',
        sortable: true,
        align: 'right',
        width: '90px',
        editable: !isReadOnly,
        editType: 'number',
        validators: [
          (val: any) => {
            const num = Number(val);
            if (isNaN(num)) return 'Must be a number';
            if (num < 0 || num > 100) return '0 - 100%';
            return null;
          }
        ]
      },
      {
        field: 'taxCodeId',
        header: 'Tax Code',
        sortable: true,
        width: '130px',
        editable: !isReadOnly,
        editType: 'lookup',
        lookupOptions: this.taxCodeLookupItems(),
        onLookupSelect: (item, row) => {
          row.taxCodeId = String(item.id);
          row.taxRate = Number(item.meta?.rate) || 0;
          this.calculateRow(row);
        }
      },
      {
        field: 'taxRate',
        header: 'Tax %',
        sortable: true,
        align: 'right',
        width: '85px',
        editable: false
      },
      {
        field: 'isTaxInclusive',
        header: 'Tax Incl.',
        sortable: true,
        align: 'center',
        width: '95px',
        type: 'boolean',
        editable: !isReadOnly
      },
      {
        field: 'lineTotal',
        header: 'Line Total',
        sortable: true,
        align: 'right',
        width: '120px',
        type: 'currency',
        editable: false
      }
    ];
  });

  // Row Calculation Callback
  readonly calculateRow = (row: EditablePOLine): void => {
    const qty = Math.round((Number(row.quantity) || 0) * 10000) / 10000;
    const cost = Math.round((Number(row.unitCost) || 0) * 100) / 100;
    const discRate = Math.round((Number(row.discountRate) || 0) * 100) / 100;
    const taxRate = Math.round((Number(row.taxRate) || 0) * 100) / 100;
    const isInclusive = !!row.isTaxInclusive;

    const gross = Math.round(qty * cost * 100) / 100;
    const discAmount = Math.round(gross * (discRate / 100) * 100) / 100;

    let taxable: number;
    let taxAmount: number;
    let lineTotal: number;

    if (isInclusive) {
      const discountedGross = gross - discAmount;
      taxable = Math.round((discountedGross / (1 + (taxRate / 100))) * 100) / 100;
      taxAmount = Math.round((discountedGross - taxable) * 100) / 100;
      lineTotal = discountedGross;
    } else {
      taxable = gross - discAmount;
      taxAmount = Math.round((taxable * (taxRate / 100)) * 100) / 100;
      lineTotal = Math.round((taxable + taxAmount) * 100) / 100;
    }

    row.quantity = qty;
    row.unitCost = cost;
    row.discountRate = discRate;
    row.discountAmount = discAmount;
    row.taxRate = taxRate;
    row.subTotal = taxable;
    row.taxAmount = taxAmount;
    row.lineTotal = lineTotal;

    const current = this.workingLines();
    if (current.some(l => l.lineId === row.lineId)) {
      this.workingLines.set(current.map(l => (l.lineId === row.lineId ? { ...l, ...row } : l)));
    }
  };

  onLinesChange(updated: EditablePOLine[]): void {
    this.workingLines.set([...updated]);
  }

  // Blank New Row Factory
  readonly defaultNewRow = (): Partial<EditablePOLine> => {
    return {
      lineId: 'line-' + Math.random().toString(36).substring(2, 9),
      productId: '',
      productCode: '',
      productName: '',
      quantity: 1,
      unitCost: 0,
      discountRate: 0,
      discountAmount: 0,
      taxCodeId: null,
      taxRate: 0,
      taxAmount: 0,
      subTotal: 0,
      lineTotal: 0,
      isTaxInclusive: false
    };
  };

  // Reactive Totals
  readonly subtotalAmount = computed<number>(() => {
    return Math.round(this.workingLines().reduce((sum, item) => sum + (Number(item.subTotal ?? (item.quantity * item.unitCost)) || 0), 0) * 100) / 100;
  });

  readonly discountTotalAmount = computed<number>(() => {
    return Math.round(this.workingLines().reduce((sum, item) => sum + (Number(item.discountAmount) || 0), 0) * 100) / 100;
  });

  readonly taxAmount = computed<number>(() => {
    return Math.round(this.workingLines().reduce((sum, item) => sum + (Number(item.taxAmount) || 0), 0) * 100) / 100;
  });

  readonly totalOrderAmount = computed<number>(() => {
    return Math.round(this.workingLines().reduce((sum, item) => sum + (Number(item.lineTotal) || 0), 0) * 100) / 100;
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

  readonly orderCompanyName = computed<string>(() => {
    const ord = this.currentOrder();
    if (ord?.companyId) {
      const match = this.availableCompanies().find(c => c.id.toLowerCase() === ord.companyId.toLowerCase());
      if (match) return `${match.name} (${match.code})`;
    }
    const current = this.scopeService.selectedCompany();
    return current ? `${current.name} (${current.code})` : 'Assigned Company';
  });

  readonly orderBranchName = computed<string>(() => {
    const ord = this.currentOrder();
    if (ord?.branchId) {
      const match = this.availableBranches().find(b => b.id.toLowerCase() === ord.branchId.toLowerCase());
      if (match) return `${match.name} (${match.code})`;
    }
    const current = this.scopeService.selectedBranch();
    return current ? `${current.name} (${current.code})` : 'Assigned Branch';
  });

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

    if (this.route.paramMap) {
      this.subs.add(
        this.route.paramMap.subscribe(() => {
          this.detectRouteMode();
        })
      );
    }
    if (this.route.queryParamMap) {
      this.subs.add(
        this.route.queryParamMap.subscribe(() => {
          this.detectRouteMode();
        })
      );
    }
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  // Detect mode: /new (create), /:id/edit or /:id/amend (amend), /:id/approve or ?mode=approval (approval), /:id (view)
  private detectRouteMode(): void {
    const url = this.router.url || '';
    const id = this.route.snapshot?.paramMap?.get('id') || null;
    const queryMode = (
      this.route.snapshot?.queryParamMap?.get('mode') ||
      (this.route.snapshot?.queryParams as any)?.['mode'] ||
      ''
    ).toLowerCase();

    if (url.includes('/purchase-orders/new') || queryMode === 'create') {
      this.mode.set('create');
      this.orderId.set(null);
      this.initCreateForm();
    } else if (id) {
      this.orderId.set(id);
      if (url.endsWith('/approve') || queryMode === 'approval' || queryMode === 'approve') {
        this.mode.set('approval');
      } else if (url.endsWith('/edit') || url.endsWith('/amend') || queryMode === 'amend' || queryMode === 'edit') {
        this.mode.set('amend');
      } else {
        this.mode.set('view');
      }
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
    if (!company) {
      return;
    }

    const branch = this.scopeService.selectedBranch();
    const branchId = this.scopeService.isAllBranches() || !branch ? null : branch.id;

    this.loading.set(true);
    this.error.set(null);
    this.isAccessDenied.set(false);

    this.poService.getOrder(company.id, branchId, orderId).subscribe({
      next: order => {
        this.loading.set(false);
        if (!order) {
          this.error.set('Order not found.');
          return;
        }

        // Status & Permission Enforcement for Amend Mode
        if (this.mode() === 'amend') {
          const st = Number(order.status);
          if (st !== PurchaseOrderStatus.Draft) {
            this.showMessage(
              `Purchase Order ${order.orderNumber} is in status '${getPurchaseOrderStatusName(order.status)}' and cannot be amended. Viewing in read-only mode.`,
              'warn'
            );
            this.mode.set('view');
          } else if (!this.canEdit()) {
            this.showMessage(
              "You don't have authorization to amend purchase orders. Viewing in read-only mode.",
              'warn'
            );
            this.mode.set('view');
          }
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

  private enrichLinesWithProductMaster(lines: EditablePOLine[]): EditablePOLine[] {
    const prodList = this.products();
    if (!prodList || prodList.length === 0) return lines;
    return lines.map(line => {
      if (line.productId && (!line.productCode || !line.productName)) {
        const found = prodList.find(p => p.productId?.toLowerCase() === line.productId.toLowerCase());
        if (found) {
          return {
            ...line,
            productCode: line.productCode || found.productCode,
            productName: line.productName || found.productName,
            unitOfMeasureId: line.unitOfMeasureId || found.unitOfMeasureId || null
          };
        }
      }
      return line;
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

    if (order.companyId) {
      this.selectedCompanyId.set(order.companyId);
    }
    if (order.branchId) {
      this.selectedBranchId.set(order.branchId);
    }

    if (order.items && order.items.length > 0) {
      const lines: EditablePOLine[] = order.items.map(item => ({
        lineId: item.id || 'line-' + Math.random().toString(36).substring(2, 9),
        productId: item.productId,
        productCode: item.productCode || '',
        productName: item.productName || '',
        unitOfMeasureId: item.unitOfMeasureId || null,
        quantity: item.quantity,
        unitCost: item.unitPrice,
        discountRate: item.discountRate || 0,
        discountAmount: item.discountAmount || 0,
        taxCodeId: item.taxCodeId || null,
        taxRate: item.taxRate || 0,
        taxAmount: item.taxAmount || 0,
        subTotal: item.subTotal || item.quantity * item.unitPrice,
        lineTotal: item.lineTotal || item.quantity * item.unitPrice,
        isTaxInclusive: item.isTaxInclusive || false
      }));
      this.workingLines.set(this.enrichLinesWithProductMaster(lines));
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

    this.masterDataService.getTaxCodes('Purchase', true).subscribe({
      next: codes => this.taxCodes.set(codes),
      error: () => {}
    });

    this.masterDataService.getProducts().subscribe({
      next: products => {
        this.products.set(products);
        const current = this.workingLines();
        if (current.some(l => l.productId && (!l.productCode || !l.productName))) {
          this.workingLines.set(this.enrichLinesWithProductMaster(current));
        }
      },
      error: () => {}
    });
  }

  // Save Order (Draft or Submit for Create / Amend)
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

    // Amend Mode: Update existing order
    if (this.mode() === 'amend' && this.orderId()) {
      const orderId = this.orderId()!;
      if (!this.canEdit()) {
        this.showMessage('Approved or non-draft purchase orders cannot be modified.', 'danger');
        return;
      }

      this.actionInProgress.set(true);

      const newLines = validLines.filter(l => l.lineId.startsWith('line-'));
      if (newLines.length > 0) {
        const addSequentially = (idx: number) => {
          if (idx >= newLines.length) {
            if (submit) {
              this.poService.submitOrder(company.id, branch.id, orderId).subscribe({
                next: () => {
                  this.actionInProgress.set(false);
                  this.showMessage(`Purchase Order ${formVal.orderNumber} updated and submitted successfully.`, 'success');
                  this.router.navigate(['/app/procurement/purchase-orders', orderId]);
                },
                error: subErr => {
                  this.actionInProgress.set(false);
                  this.showMessage(
                    `Requisition items added, but submission failed: ${this.getCleanErrorMessage(subErr, 'Permission denied.')}`,
                    'warn'
                  );
                  this.router.navigate(['/app/procurement/purchase-orders', orderId]);
                }
              });
            } else {
              this.actionInProgress.set(false);
              this.showMessage(`Purchase Order ${formVal.orderNumber} updated successfully.`, 'success');
              this.router.navigate(['/app/procurement/purchase-orders', orderId]);
            }
            return;
          }

          const line = newLines[idx];
          this.poService.addItem(company.id, branch.id, orderId, {
            productId: line.productId,
            unitOfMeasureId: line.unitOfMeasureId || null,
            quantity: Number(line.quantity),
            unitPrice: Number(line.unitCost),
            discountRate: Number(line.discountRate) || 0,
            taxCodeId: line.taxCodeId || null,
            isTaxInclusive: !!line.isTaxInclusive
          }).subscribe({
            next: () => addSequentially(idx + 1),
            error: err => {
              this.actionInProgress.set(false);
              this.showMessage(this.getCleanErrorMessage(err, 'Failed to add requisition line item.'), 'danger');
            }
          });
        };

        addSequentially(0);
      } else {
        if (submit) {
          this.poService.submitOrder(company.id, branch.id, orderId).subscribe({
            next: () => {
              this.actionInProgress.set(false);
              this.showMessage(`Purchase Order ${formVal.orderNumber} submitted successfully.`, 'success');
              this.router.navigate(['/app/procurement/purchase-orders', orderId]);
            },
            error: subErr => {
              this.actionInProgress.set(false);
              this.showMessage(this.getCleanErrorMessage(subErr, 'Failed to submit order.'), 'danger');
            }
          });
        } else {
          this.actionInProgress.set(false);
          this.showMessage(`Purchase Order ${formVal.orderNumber} saved.`, 'success');
          this.router.navigate(['/app/procurement/purchase-orders', orderId]);
        }
      }
      return;
    }

    // Create Mode: Create new purchase order
    const items = validLines.map(l => ({
      productId: l.productId,
      unitOfMeasureId: l.unitOfMeasureId || null,
      quantity: Number(l.quantity),
      unitPrice: Number(l.unitCost),
      discountRate: Number(l.discountRate) || 0,
      taxCodeId: l.taxCodeId || null,
      isTaxInclusive: !!l.isTaxInclusive
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

  // Navigation helpers for modes
  navigateToAmend(): void {
    const id = this.orderId();
    if (id) {
      this.router.navigate(['/app/procurement/purchase-orders', id, 'amend']);
    }
  }

  navigateToEdit(): void {
    this.navigateToAmend();
  }

  navigateToApproval(): void {
    const id = this.orderId();
    if (id) {
      this.router.navigate(['/app/procurement/purchase-orders', id, 'approve']);
    }
  }

  navigateToView(): void {
    const id = this.orderId();
    if (id) {
      this.router.navigate(['/app/procurement/purchase-orders', id]);
    } else {
      this.navigateBack();
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
    if (this.mode() === 'amend' && this.orderId()) {
      this.router.navigate(['/app/procurement/purchase-orders', this.orderId()]);
    } else {
      this.router.navigate(['/app/procurement/purchase-orders']);
    }
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

  openApproveDialog(): void {
    this.approveRemarks.set('');
    this.approveError.set(null);
    this.remarksAction.set('approve');
    this.remarksText.set('');
    this.remarksError.set(null);
    this.approveDialogVisible.set(true);
    this.remarksDialogVisible.set(true);
  }

  onApproveDialogHide(): void {
    this.approveDialogVisible.set(false);
    this.remarksDialogVisible.set(false);
    this.approveError.set(null);
  }

  confirmApprove(): void {
    const ord = this.currentOrder();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!ord || !company || !branch) return;

    const remarks = (this.approveRemarks().trim() || this.remarksText().trim());
    this.actionInProgress.set(true);
    this.approveError.set(null);
    this.remarksError.set(null);

    this.poService.approveOrder(company.id, branch.id, ord.id, remarks).subscribe({
      next: updated => {
        this.actionInProgress.set(false);
        this.approveDialogVisible.set(false);
        this.remarksDialogVisible.set(false);
        this.currentOrder.set(updated);
        this.showMessage(`Purchase Order ${ord.orderNumber} approved successfully.`, 'success');
      },
      error: err => {
        this.actionInProgress.set(false);
        const errMsg = this.getCleanErrorMessage(err, 'Failed to approve order.');
        this.approveError.set(errMsg);
        this.remarksError.set(errMsg);
      }
    });
  }

  openRejectDialog(): void {
    this.rejectRemarks.set('');
    this.rejectError.set(null);
    this.remarksAction.set('reject');
    this.remarksText.set('');
    this.remarksError.set(null);
    this.rejectDialogVisible.set(true);
    this.remarksDialogVisible.set(true);
  }

  onRejectDialogHide(): void {
    this.rejectDialogVisible.set(false);
    this.remarksDialogVisible.set(false);
    this.rejectError.set(null);
  }

  confirmReject(): void {
    const ord = this.currentOrder();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!ord || !company || !branch) return;

    const remarks = (this.rejectRemarks().trim() || this.remarksText().trim());
    if (!remarks) {
      const errMsg = 'Remarks are mandatory when rejecting a purchase order.';
      this.rejectError.set(errMsg);
      this.remarksError.set(errMsg);
      return;
    }

    this.actionInProgress.set(true);
    this.rejectError.set(null);
    this.remarksError.set(null);

    this.poService.rejectOrder(company.id, branch.id, ord.id, remarks).subscribe({
      next: updated => {
        this.actionInProgress.set(false);
        this.rejectDialogVisible.set(false);
        this.remarksDialogVisible.set(false);
        this.currentOrder.set(updated);
        this.showMessage(`Purchase Order ${ord.orderNumber} rejected successfully.`, 'success');
      },
      error: err => {
        this.actionInProgress.set(false);
        const errMsg = this.getCleanErrorMessage(err, 'Failed to reject order.');
        this.rejectError.set(errMsg);
        this.remarksError.set(errMsg);
      }
    });
  }

  openCancelDialog(): void {
    this.cancelRemarks.set('');
    this.cancelError.set(null);
    this.remarksAction.set('cancel');
    this.remarksText.set('');
    this.remarksError.set(null);
    this.cancelDialogVisible.set(true);
    this.remarksDialogVisible.set(true);
  }

  onCancelDialogHide(): void {
    this.cancelDialogVisible.set(false);
    this.remarksDialogVisible.set(false);
    this.cancelError.set(null);
  }

  confirmCancel(): void {
    const ord = this.currentOrder();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!ord || !company || !branch) return;

    const remarks = (this.cancelRemarks().trim() || this.remarksText().trim());
    this.actionInProgress.set(true);
    this.cancelError.set(null);
    this.remarksError.set(null);

    this.poService.cancelOrder(company.id, branch.id, ord.id, remarks).subscribe({
      next: updated => {
        this.actionInProgress.set(false);
        this.cancelDialogVisible.set(false);
        this.remarksDialogVisible.set(false);
        this.currentOrder.set(updated);
        this.showMessage(`Purchase Order ${ord.orderNumber} cancelled successfully.`, 'success');
      },
      error: err => {
        this.actionInProgress.set(false);
        const errMsg = this.getCleanErrorMessage(err, 'Failed to cancel order.');
        this.cancelError.set(errMsg);
        this.remarksError.set(errMsg);
      }
    });
  }

  openRemarksDialog(action: 'approve' | 'reject' | 'cancel'): void {
    if (action === 'approve') {
      this.openApproveDialog();
    } else if (action === 'reject') {
      this.openRejectDialog();
    } else {
      this.openCancelDialog();
    }
  }

  confirmRemarksDecision(): void {
    const action = this.remarksAction();
    if (action === 'approve') {
      this.confirmApprove();
    } else if (action === 'reject') {
      this.confirmReject();
    } else if (action === 'cancel') {
      this.confirmCancel();
    }
  }

  // Permission Checks
  canApprove(): boolean {
    const ord = this.currentOrder();
    if (ord) {
      const st = Number(ord.status);
      if (st !== PurchaseOrderStatus.Submitted) return false;
    }
    return (
      this.authService.hasPermission('PURCHASE_ORDER.APPROVE') ||
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin() ||
      this.authService.hasRole('Tenant Manager') ||
      this.authService.hasRole('Approver')
    );
  }

  canEdit(): boolean {
    const ord = this.currentOrder();
    if (!ord) return true;
    const st = Number(ord.status);
    if (st !== PurchaseOrderStatus.Draft) return false;
    return (
      this.authService.hasPermission('PURCHASE_ORDER.EDIT') ||
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin() ||
      this.authService.hasRole('Tenant User') ||
      this.authService.hasRole('Branch User')
    );
  }

  canSubmit(): boolean {
    const ord = this.currentOrder();
    if (!ord) return true;
    const st = Number(ord.status);
    if (st !== PurchaseOrderStatus.Draft) return false;
    return (
      this.authService.hasPermission('PURCHASE_ORDER.SUBMIT') ||
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin() ||
      this.authService.hasRole('Tenant User') ||
      this.authService.hasRole('Branch User')
    );
  }

  canCancel(): boolean {
    const ord = this.currentOrder();
    if (!ord) return false;
    const st = Number(ord.status);
    if (st !== PurchaseOrderStatus.Draft && st !== PurchaseOrderStatus.Submitted) return false;
    return (
      this.authService.hasPermission('PURCHASE_ORDER.CANCEL') ||
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin() ||
      this.authService.hasRole('Tenant User')
    );
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
