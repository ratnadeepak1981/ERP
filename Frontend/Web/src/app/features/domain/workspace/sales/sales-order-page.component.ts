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
import { Subscription, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ActionDialogComponent } from '../../../../shared/components/action-dialog/action-dialog.component';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppReactiveFormComponent } from '../../../../shared/components/forms/reactive-form.component';
import { FormFieldConfig } from '../../../../shared/components/forms/form-field-config.model';
import { AppEditableDataTableComponent } from '../../../../shared/components/data-table/editable-data-table.component';
import { AppStatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { AccessDeniedComponent } from '../../../../shared/components/access-denied/access-denied.component';
import { SalesOrderService } from '../../../../core/services/sales-order.service';
import { MasterDataService } from '../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import {
  SalesOrderDto,
  SalesOrderItemDto,
  CreateSalesOrderRequest,
  CreateSalesOrderItemRequest,
  SalesOrderStatus,
  getSalesOrderStatusName,
  getSalesOrderStatusSeverity
} from '../../../../core/models/sales-order.models';
import { DataTableColumn } from '../../../../shared/models/data-table-config.model';
import { LookupItem } from '../../../../shared/components/lookup-dropdown/lookup-item.model';
import { CustomerItem, ProductItem, CustomerAddressItem, TaxCodeItem } from '../../../../core/models/scope.models';

export type SOPageMode = 'create' | 'view' | 'amend' | 'confirm';

export interface EditableSOLine {
  lineId: string;
  productId: string;
  productCode: string;
  productName: string;
  unitOfMeasureId?: string | null;
  quantity: number;
  unitPrice: number;
  discountRate: number;
  discountAmount: number;
  taxCodeId?: string | null;
  taxRate: number;
  taxAmount: number;
  subTotal: number;
  lineTotal: number;
  isTaxInclusive?: boolean;
  notes?: string | null;
}

@Component({
  selector: 'app-sales-order-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterModule,
    ActionDialogComponent,
    AppPageHeaderComponent,
    AppReactiveFormComponent,
    AppEditableDataTableComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AccessDeniedComponent
  ],
  templateUrl: './sales-order-page.component.html',
  styleUrl: './sales-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SalesOrderPageComponent implements OnInit, OnDestroy {
  readonly salesOrderService = inject(SalesOrderService);
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
  readonly mode = signal<SOPageMode>('create');
  readonly orderId = signal<string | null>(null);
  readonly currentOrder = signal<SalesOrderDto | null>(null);

  // Loading & Error States
  readonly loading = signal<boolean>(false);
  readonly actionInProgress = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to access sales orders for this operating company and branch. Contact your administrator if you believe this is an error."
  );
  readonly serverErrors = signal<Record<string, string>>({});
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Master Data
  readonly customers = signal<CustomerItem[]>([]);
  readonly products = signal<ProductItem[]>([]);
  readonly customerAddresses = signal<CustomerAddressItem[]>([]);

  // Order Lines
  readonly workingLines = signal<EditableSOLine[]>([]);

  // Dialog State: Remarks (Confirm / Reject / Cancel)
  readonly remarksDialogVisible = signal<boolean>(false);
  readonly remarksAction = signal<'confirm' | 'reject' | 'cancel' | null>(null);
  readonly remarksText = signal<string>('');
  readonly remarksError = signal<string | null>(null);

  // Reactive Header Form
  readonly headerForm = new FormGroup({
    orderNumber: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    customerId: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    orderDate: new FormControl<string>(new Date().toISOString().substring(0, 10), {
      nonNullable: true,
      validators: [Validators.required]
    }),
    deliveryDate: new FormControl<string>(''),
    currency: new FormControl<string>('USD', { nonNullable: true }),
    paymentTerms: new FormControl<string>('Net 30'),
    deliveryAddressId: new FormControl<string | null>(null),
    notes: new FormControl<string>('')
  });

  // Read-only state for view and confirm modes
  readonly isReadOnlyMode = computed<boolean>(() => this.mode() === 'view' || this.mode() === 'confirm');

  // Active currency
  readonly activeCurrency = computed<string>(() => {
    return this.headerForm.get('currency')?.value || this.scopeService.selectedCompany()?.currency || 'USD';
  });

  // Breadcrumbs
  readonly breadcrumbs = computed(() => {
    const list = [
      { label: 'Workspace', routerLink: '/app/dashboard' },
      { label: 'Sales', routerLink: '/app/sales' },
      { label: 'Sales Orders', routerLink: '/app/sales/orders' }
    ];

    switch (this.mode()) {
      case 'create':
        list.push({ label: 'New Sales Order', routerLink: '' });
        break;
      case 'amend':
        list.push({ label: 'Amend Sales Order', routerLink: '' });
        break;
      case 'confirm':
        list.push({ label: 'Order Confirmation', routerLink: '' });
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
        return 'New Sales Order';
      case 'amend':
        return `Amend Sales Order${num}`;
      case 'confirm':
        return `Sales Order Confirmation${num}`;
      case 'view':
      default:
        return `Sales Order Details${num}`;
    }
  });

  readonly pageSubtitle = computed(() => {
    switch (this.mode()) {
      case 'create':
        return 'Create a commercial customer order with line pricing, discounts, tax, and delivery dates.';
      case 'amend':
        return 'Add new items or modify line requisitions before confirming this order.';
      case 'confirm':
        return 'Review customer order details, pricing, and commercial terms to authorize order fulfillment.';
      case 'view':
      default:
        return 'Inspect order lines, total value, confirmation status, and warehouse fulfillment progress.';
    }
  });

  // Scope Info
  readonly orderCompanyName = computed<string>(() => {
    const ord = this.currentOrder();
    if (ord?.companyId) {
      const match = this.scopeService.companies().find(c => c.id.toLowerCase() === ord.companyId.toLowerCase());
      if (match) return `${match.name} (${match.code})`;
    }
    const current = this.scopeService.selectedCompany();
    return current ? `${current.name} (${current.code})` : 'Assigned Company';
  });

  readonly orderBranchName = computed<string>(() => {
    const ord = this.currentOrder();
    if (ord?.branchId) {
      const match = this.scopeService.branches().find(b => b.id.toLowerCase() === ord.branchId.toLowerCase());
      if (match) return `${match.name} (${match.code})`;
    }
    const current = this.scopeService.selectedBranch();
    return current ? `${current.name} (${current.code})` : 'Assigned Branch';
  });

  readonly orderStatusName = computed<string>(() => {
    const ord = this.currentOrder();
    if (!ord) return 'Draft';
    return getSalesOrderStatusName(ord.status);
  });

  readonly orderStatusSeverity = computed<'success' | 'warning' | 'danger' | 'info' | 'neutral'>(() => {
    const ord = this.currentOrder();
    if (!ord) return 'info';
    return getSalesOrderStatusSeverity(ord.status);
  });

  // Product Lookup Items for Editable Table
  readonly productLookupItems = computed<LookupItem[]>(() => {
    return this.products().map(p => ({
      id: p.productId,
      code: p.productCode,
      name: p.productName,
      category: p.categoryName || 'General',
      isEligible: p.isActive && (p.isSellable ?? true),
      meta: {
        sellingPrice: p.sellingPrice || 0,
        unitOfMeasureId: p.unitOfMeasureId
      }
    }));
  });

  // Form Field Configurations
  readonly formFields = computed<FormFieldConfig[]>(() => {
    const isReadOnly = this.isReadOnlyMode();
    const addressOptions = this.customerAddresses().map(ca => {
      const addr = ca.address;
      const label = addr ? `${addr.addressName} — ${addr.addressLine1}, ${addr.city}` : ca.addressId;
      return { label: ca.isDefault ? `★ ${label} (Default)` : label, value: ca.addressId };
    });
    const currentOrderAddrId = this.currentOrder()?.deliveryAddressId;
    if (currentOrderAddrId && !addressOptions.some(a => a.value === currentOrderAddrId)) {
      const snap = this.currentOrder()?.deliveryAddressSnapshot;
      addressOptions.unshift({ label: snap || currentOrderAddrId, value: currentOrderAddrId });
    }
    const customerOptions = this.customers().map(c => ({
      label: `${c.customerName} (${c.customerCode})`,
      value: c.customerId
    }));

    const currentOrd = this.currentOrder();
    if (currentOrd?.customerId && !customerOptions.some(o => o.value === currentOrd.customerId)) {
      customerOptions.unshift({
        label: currentOrd.customerName ? currentOrd.customerName : currentOrd.customerId,
        value: currentOrd.customerId
      });
    }

    return [
      {
        name: 'orderNumber',
        label: 'Sales Order #',
        type: 'text',
        required: true,
        placeholder: 'e.g. SO-2026-001',
        readonly: isReadOnly || this.mode() === 'amend',
        tooltip: 'Unique identifier for the customer sales order',
        colSpan: 1
      },
      {
        name: 'customerId',
        label: 'Customer Account',
        type: 'select',
        required: true,
        placeholder: '-- Select Customer --',
        options: customerOptions,
        disabled: isReadOnly || this.mode() === 'amend',
        colSpan: 1
      },
      {
        name: 'deliveryAddressId',
        label: 'Delivery Destination / Site',
        type: 'select',
        placeholder: addressOptions.length > 0 ? '-- Select Delivery Destination --' : '-- Default Customer Address --',
        options: addressOptions,
        disabled: isReadOnly || this.mode() === 'amend',
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
        label: 'Requested Delivery Date',
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
          { label: 'EUR (â‚¬)', value: 'EUR' },
          { label: 'GBP (Â£)', value: 'GBP' },
          { label: 'LKR (Rs)', value: 'LKR' }
        ],
        disabled: isReadOnly || this.mode() === 'amend',
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
        name: 'notes',
        label: 'Commercial Notes & Instructions',
        type: 'textarea',
        placeholder: 'Enter special delivery requirements, customer PO ref, packaging instructions...',
        rows: 2,
        disabled: isReadOnly,
        colSpan: 2
      }
    ];
  });

  // Editable Columns
  readonly editableItemColumns = computed<DataTableColumn<EditableSOLine>[]>(() => {
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
          if (item.meta?.sellingPrice !== undefined) {
            row.unitPrice = Number(item.meta.sellingPrice) || 0;
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
        width: '110px',
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
        field: 'unitPrice',
        header: 'Unit Price',
        sortable: true,
        align: 'right',
        width: '130px',
        type: 'currency',
        editable: !isReadOnly,
        editType: 'currency',
        validators: [
          (val: any) => {
            const num = Number(val);
            if (isNaN(num)) return 'Must be valid price';
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
        width: '100px',
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
        width: '130px',
        type: 'currency',
        editable: false
      }
    ];
  });

  // Calculate Row Amounts matching SalesOrder.RecalculateTotals backend rules
  readonly calculateRow = (row: EditableSOLine): void => {
    const qty = Math.round((Number(row.quantity) || 0) * 10000) / 10000;
    const price = Math.round((Number(row.unitPrice) || 0) * 100) / 100;
    const discRate = Math.round((Number(row.discountRate) || 0) * 100) / 100;
    const taxRate = Math.round((Number(row.taxRate) || 0) * 100) / 100;
    const isInclusive = !!row.isTaxInclusive;

    const gross = Math.round(qty * price * 100) / 100;
    const discount = Math.round(gross * (discRate / 100) * 100) / 100;

    let taxable: number;
    let tax: number;
    let lineTotal: number;

    if (isInclusive) {
      const discountedGross = gross - discount;
      taxable = Math.round((discountedGross / (1 + (taxRate / 100))) * 100) / 100;
      tax = Math.round((discountedGross - taxable) * 100) / 100;
      lineTotal = discountedGross;
    } else {
      taxable = gross - discount;
      tax = Math.round((taxable * (taxRate / 100)) * 100) / 100;
      lineTotal = Math.round((taxable + tax) * 100) / 100;
    }

    row.quantity = qty;
    row.unitPrice = price;
    row.discountRate = discRate;
    row.taxRate = taxRate;
    row.subTotal = taxable;
    row.discountAmount = discount;
    row.taxAmount = tax;
    row.lineTotal = lineTotal;

    const current = this.workingLines();
    if (current.some(l => l.lineId === row.lineId)) {
      this.workingLines.set(current.map(l => (l.lineId === row.lineId ? { ...l, ...row } : l)));
    }
  };

  onLinesChange(updated: EditableSOLine[]): void {
    this.workingLines.set([...updated]);
  }

  // Blank New Row Factory
  readonly defaultNewRow = (): Partial<EditableSOLine> => {
    return {
      lineId: 'line-' + Math.random().toString(36).substring(2, 9),
      productId: '',
      productCode: '',
      productName: '',
      quantity: 1,
      unitPrice: 0,
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
    return this.workingLines().reduce((sum, item) => sum + (Number(item.subTotal) || 0), 0);
  });

  readonly discountAmount = computed<number>(() => {
    return this.workingLines().reduce((sum, item) => sum + (Number(item.discountAmount) || 0), 0);
  });

  readonly taxAmount = computed<number>(() => {
    return this.workingLines().reduce((sum, item) => sum + (Number(item.taxAmount) || 0), 0);
  });

  readonly totalOrderAmount = computed<number>(() => {
    return this.workingLines().reduce((sum, item) => sum + (Number(item.lineTotal) || 0), 0);
  });

  // Cancel Action Visibility
  readonly isCancelVisible = computed<boolean>(() => {
    const ord = this.currentOrder();
    if (!ord) return false;
    const status = Number(ord.status);
    return status !== SalesOrderStatus.Fulfilled && status !== SalesOrderStatus.Cancelled && status !== SalesOrderStatus.Rejected;
  });

  // Dialog Computeds
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
        return 'Enter optional confirmation remarks or customer purchase order notes...';
      case 'reject':
        return 'Please explain the business reason for rejecting this customer order...';
      case 'cancel':
        return 'Enter reason for cancelling this sales order...';
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

  ngOnInit(): void {
    this.detectRouteMode();
    this.loadMasterData();
    this.subs.add(
      this.headerForm.get('customerId')!.valueChanges.subscribe(cid => {
        if (cid) {
          this.loadCustomerAddressesForOrder(cid);
        } else {
          this.customerAddresses.set([]);
          this.headerForm.get('deliveryAddressId')?.setValue(null);
        }
      })
    );

    if (this.route.paramMap) {
      this.subs.add(this.route.paramMap.subscribe(() => this.detectRouteMode()));
    }
    if (this.route.queryParamMap) {
      this.subs.add(this.route.queryParamMap.subscribe(() => this.detectRouteMode()));
    }
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  private detectRouteMode(): void {
    const url = this.router.url || '';
    const id = this.route.snapshot?.paramMap?.get('id') || null;
    const queryAction = (this.route.snapshot?.queryParamMap?.get('action') || '').toLowerCase();

    if (url.includes('/sales/orders/new') || queryAction === 'create') {
      this.mode.set('create');
      this.orderId.set(null);
      this.initCreateForm();
    } else if (id) {
      this.orderId.set(id);
      if (url.endsWith('/confirm')) {
        this.mode.set('confirm');
      } else if (url.endsWith('/edit')) {
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

  initCreateForm(): void {
    this.headerForm.reset({
      orderNumber: this.generateOrderNumber(),
      customerId: '',
      orderDate: new Date().toISOString().substring(0, 10),
      deliveryDate: '',
      currency: this.scopeService.selectedCompany()?.currency || 'USD',
      paymentTerms: 'Net 30',
      notes: ''
    });
    this.workingLines.set([this.defaultNewRow() as EditableSOLine]);
    this.currentOrder.set(null);
    this.error.set(null);
    this.isAccessDenied.set(false);
  }

  private generateOrderNumber(): string {
    const year = new Date().getFullYear();
    const rand = Math.floor(1000 + Math.random() * 9000);
    return `SO-${year}-${rand}`;
  }

  private loadCustomerAddressesForOrder(customerId: string): void {
    forkJoin({
      linked: this.masterDataService.getCustomerAddresses(customerId).pipe(catchError(() => of([]))),
      all: this.masterDataService.getAddresses().pipe(catchError(() => of([])))
    }).subscribe({
      next: ({ linked, all }) => {
        const list = (linked || []).map(l => ({
          ...l,
          address: (all || []).find(a => a.addressId === l.addressId)
        }));
        this.customerAddresses.set(list);
        if (!this.headerForm.get('deliveryAddressId')?.value && list.length > 0) {
          const def = list.find(l => l.isDefault) || list[0];
          if (def) {
            this.headerForm.get('deliveryAddressId')?.setValue(def.addressId);
          }
        }
      }
    });
  }

  loadMasterData(): void {
    forkJoin({
      customers: this.masterDataService.getCustomers().pipe(catchError(() => of([] as CustomerItem[]))),
      products: this.masterDataService.getProducts().pipe(catchError(() => of([] as ProductItem[]))),
      taxCodes: this.masterDataService.getTaxCodes('Sales', true).pipe(catchError(() => of([] as TaxCodeItem[])))
    }).subscribe({
      next: ({ customers, products, taxCodes }) => {
        this.customers.set(customers || []);
        this.products.set(products || []);
        this.taxCodes.set(taxCodes || []);
        const current = this.workingLines();
        if (current.some(l => l.productId && (!l.productCode || !l.productName))) {
          this.workingLines.set(this.enrichLinesWithProductMaster(current));
        }
      }
    });
  }

  private enrichLinesWithProductMaster(lines: EditableSOLine[]): EditableSOLine[] {
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

  loadExistingOrder(id: string): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) return;

    this.loading.set(true);
    this.error.set(null);
    this.isAccessDenied.set(false);

    this.salesOrderService.getOrder(company.id, branch.id, id).subscribe({
      next: order => {
        this.loading.set(false);
        if (!order) {
          this.error.set('Sales order not found in the authorized scope.');
          return;
        }

        // Amend Mode Guard
        if (this.mode() === 'amend') {
          const st = Number(order.status);
          if (st !== SalesOrderStatus.Draft) {
            this.showMessage(
              `Sales Order ${order.orderNumber} is in status '${getSalesOrderStatusName(order.status)}' and cannot be amended. Viewing in read-only mode.`,
              'warn'
            );
            this.mode.set('view');
          } else if (!this.canEditOrder()) {
            this.showMessage("You don't have authorization to amend sales orders. Viewing in read-only mode.", 'warn');
            this.mode.set('view');
          }
        }

        this.populateOrderData(order);
        if (order.customerId) { this.loadCustomerAddressesForOrder(order.customerId); }
      },
      error: err => {
        this.loading.set(false);
        if (this.isForbiddenError(err)) {
          this.isAccessDenied.set(true);
        } else {
          this.error.set(this.getCleanErrorMessage(err, 'Failed to load sales order details.'));
        }
      }
    });
  }

  private populateOrderData(order: SalesOrderDto): void {
    this.currentOrder.set(order);
    this.headerForm.patchValue({
      orderNumber: order.orderNumber,
      customerId: order.customerId || '',
      orderDate: order.orderDate ? order.orderDate.substring(0, 10) : new Date().toISOString().substring(0, 10),
      deliveryDate: order.deliveryDate ? order.deliveryDate.substring(0, 10) : '',
      currency: order.currency || 'USD',
      paymentTerms: order.paymentTerms || 'Net 30',
      deliveryAddressId: order.deliveryAddressId || null,
      notes: order.notes || ''
    });

    if (order.items && order.items.length > 0) {
      const lines: EditableSOLine[] = order.items.map(item => ({
        lineId: item.id || 'line-' + Math.random().toString(36).substring(2, 9),
        productId: item.productId,
        productCode: item.productCode || '',
        productName: item.productName || '',
        unitOfMeasureId: item.unitOfMeasureId || null,
        quantity: item.quantity,
        unitPrice: item.unitPrice,
        discountRate: item.discountRate || 0,
        discountAmount: item.discountAmount || 0,
        taxCodeId: item.taxCodeId || null,
        taxRate: item.taxRate || 0,
        taxAmount: item.taxAmount || 0,
        subTotal: item.subTotal || item.quantity * item.unitPrice,
        lineTotal: item.lineTotal,
        isTaxInclusive: item.isTaxInclusive || false,
        notes: item.notes || null
      }));
      this.workingLines.set(this.enrichLinesWithProductMaster(lines));
    } else {
      this.workingLines.set([this.defaultNewRow() as EditableSOLine]);
    }
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

    // Amend Mode: Append new lines to draft
    if (this.mode() === 'amend' && this.orderId()) {
      const orderId = this.orderId()!;
      if (!this.canEditOrder()) {
        this.showMessage('You do not have permission to edit sales orders.', 'danger');
        return;
      }

      this.actionInProgress.set(true);

      const newLines = validLines.filter(l => l.lineId.startsWith('line-'));
      if (newLines.length > 0) {
        const addSequentially = (idx: number) => {
          if (idx >= newLines.length) {
            if (submit) {
              this.salesOrderService.submitOrder(company.id, branch.id, orderId).subscribe({
                next: () => {
                  this.actionInProgress.set(false);
                  this.showMessage(`Sales Order ${formVal.orderNumber} updated and submitted.`, 'success');
                  this.router.navigate(['/app/sales/orders', orderId]);
                },
                error: subErr => {
                  this.actionInProgress.set(false);
                  this.showMessage(
                    `Items added, but submission failed: ${this.getCleanErrorMessage(subErr, 'Permission denied.')}`,
                    'warn'
                  );
                  this.router.navigate(['/app/sales/orders', orderId]);
                }
              });
            } else {
              this.actionInProgress.set(false);
              this.showMessage(`Sales Order ${formVal.orderNumber} updated successfully.`, 'success');
              this.router.navigate(['/app/sales/orders', orderId]);
            }
            return;
          }

          const line = newLines[idx];
          const itemReq: CreateSalesOrderItemRequest = {
            productId: line.productId,
            unitOfMeasureId: line.unitOfMeasureId || null,
            quantity: Number(line.quantity),
            unitPrice: Number(line.unitPrice),
            discountRate: Number(line.discountRate) || 0,
            taxCodeId: line.taxCodeId || null,
            taxRate: Number(line.taxRate) || 0,
            isTaxInclusive: !!line.isTaxInclusive,
            notes: line.notes || null
          };

          this.salesOrderService.addItem(company.id, branch.id, orderId, itemReq).subscribe({
            next: () => addSequentially(idx + 1),
            error: err => {
              this.actionInProgress.set(false);
              this.showMessage(this.getCleanErrorMessage(err, 'Failed to add line item.'), 'danger');
            }
          });
        };

        addSequentially(0);
      } else {
        if (submit) {
          this.salesOrderService.submitOrder(company.id, branch.id, orderId).subscribe({
            next: () => {
              this.actionInProgress.set(false);
              this.showMessage(`Sales Order ${formVal.orderNumber} submitted successfully.`, 'success');
              this.router.navigate(['/app/sales/orders', orderId]);
            },
            error: subErr => {
              this.actionInProgress.set(false);
              this.showMessage(this.getCleanErrorMessage(subErr, 'Failed to submit order.'), 'danger');
            }
          });
        } else {
          this.actionInProgress.set(false);
          this.showMessage(`Sales Order ${formVal.orderNumber} saved.`, 'success');
          this.router.navigate(['/app/sales/orders', orderId]);
        }
      }
      return;
    }

    // Create Mode: New Sales Order
    const items: CreateSalesOrderItemRequest[] = validLines.map(l => ({
      productId: l.productId,
      unitOfMeasureId: l.unitOfMeasureId || null,
      quantity: Number(l.quantity),
      unitPrice: Number(l.unitPrice),
      discountRate: Number(l.discountRate) || 0,
      taxCodeId: l.taxCodeId || null,
      taxRate: Number(l.taxRate) || 0,
      isTaxInclusive: !!l.isTaxInclusive,
      notes: l.notes || null
    }));

    const request: CreateSalesOrderRequest = {
      orderNumber: formVal.orderNumber,
      customerId: formVal.customerId,
      orderDate: formVal.orderDate ? new Date(formVal.orderDate).toISOString() : new Date().toISOString(),
      deliveryDate: formVal.deliveryDate ? new Date(formVal.deliveryDate).toISOString() : null,
      currency: formVal.currency || 'USD',
      paymentTerms: formVal.paymentTerms || null,
      deliveryAddressId: formVal.deliveryAddressId || null,
      notes: formVal.notes || null,
      items
    };

    this.actionInProgress.set(true);
    this.serverErrors.set({});

    this.salesOrderService.createOrder(company.id, branch.id, request).subscribe({
      next: createdOrder => {
        if (submit) {
          this.salesOrderService.submitOrder(company.id, branch.id, createdOrder.id).subscribe({
            next: () => {
              this.actionInProgress.set(false);
              this.showMessage(`Sales Order ${createdOrder.orderNumber} created and submitted.`, 'success');
              this.router.navigate(['/app/sales/orders', createdOrder.id]);
            },
            error: subErr => {
              this.actionInProgress.set(false);
              this.showMessage(
                `Sales Order ${createdOrder.orderNumber} created as draft, but submit failed: ${this.getCleanErrorMessage(subErr, '')}`,
                'warn'
              );
              this.router.navigate(['/app/sales/orders', createdOrder.id]);
            }
          });
        } else {
          this.actionInProgress.set(false);
          this.showMessage(`Sales Order ${createdOrder.orderNumber} saved as draft.`, 'success');
          this.router.navigate(['/app/sales/orders', createdOrder.id]);
        }
      },
      error: err => {
        this.actionInProgress.set(false);
        this.showMessage(this.getCleanErrorMessage(err, 'Failed to create sales order.'), 'danger');
      }
    });
  }

  // Submit from View Mode
  submitCurrentOrder(): void {
    const ord = this.currentOrder();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!ord || !company || !branch) return;

    this.actionInProgress.set(true);
    this.salesOrderService.submitOrder(company.id, branch.id, ord.id).subscribe({
      next: updated => {
        this.actionInProgress.set(false);
        this.showMessage(`Sales Order ${ord.orderNumber} submitted successfully.`, 'success');
        this.populateOrderData(updated);
      },
      error: err => {
        this.actionInProgress.set(false);
        this.showMessage(this.getCleanErrorMessage(err, 'Failed to submit sales order.'), 'danger');
      }
    });
  }

  // Remarks Dialog Controls
  openRemarksDialog(action: 'confirm' | 'reject' | 'cancel'): void {
    this.remarksAction.set(action);
    this.remarksText.set('');
    this.remarksError.set(null);
    this.remarksDialogVisible.set(true);
  }

  closeRemarksDialog(): void {
    if (this.actionInProgress()) return;
    this.remarksDialogVisible.set(false);
    this.remarksAction.set(null);
    this.remarksText.set('');
    this.remarksError.set(null);
  }

  submitRemarksAction(): void {
    const action = this.remarksAction();
    const ord = this.currentOrder();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();

    if (!action || !ord || !company || !branch) return;

    const remarks = this.remarksText().trim();
    if (action === 'reject' && !remarks) {
      this.remarksError.set('Rejection remarks are required.');
      return;
    }

    this.actionInProgress.set(true);
    this.remarksError.set(null);

    let obs;
    if (action === 'confirm') {
      obs = this.salesOrderService.confirmOrder(company.id, branch.id, ord.id, remarks || undefined);
    } else if (action === 'reject') {
      obs = this.salesOrderService.rejectOrder(company.id, branch.id, ord.id, remarks);
    } else {
      obs = this.salesOrderService.cancelOrder(company.id, branch.id, ord.id, remarks || undefined);
    }

    obs.subscribe({
      next: updated => {
        this.actionInProgress.set(false);
        const actionPast = action === 'confirm' ? 'confirmed' : action === 'reject' ? 'rejected' : 'cancelled';
        this.showMessage(`Sales Order ${ord.orderNumber} ${actionPast} successfully.`, 'success');
        this.closeRemarksDialog();
        this.populateOrderData(updated);
      },
      error: err => {
        this.actionInProgress.set(false);
        this.remarksError.set(this.getCleanErrorMessage(err, `Failed to ${action} sales order.`));
      }
    });
  }

  // Navigation Handlers
  navigateBack(): void {
    this.router.navigate(['/app/sales/orders']);
  }

  navigateToAmendMode(): void {
    if (this.orderId()) {
      this.router.navigate(['/app/sales/orders', this.orderId(), 'edit']);
    }
  }

  navigateToDispatch(): void {
    const ord = this.currentOrder();
    if (!ord) return;
    this.router.navigate(['/app/sales/dispatches/new'], {
      queryParams: {
        orderId: ord.id
      }
    });
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
