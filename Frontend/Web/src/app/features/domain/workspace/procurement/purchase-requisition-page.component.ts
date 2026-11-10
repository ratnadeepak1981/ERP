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
import { Subscription, forkJoin, Observable } from 'rxjs';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppEditableDataTableComponent } from '../../../../shared/components/data-table/editable-data-table.component';
import { AppStatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { AccessDeniedComponent } from '../../../../shared/components/access-denied/access-denied.component';
import { PurchaseRequisitionService } from '../../../../core/services/purchase-requisition.service';
import { PurchaseOrderService } from '../../../../core/services/purchase-order.service';
import { MasterDataService } from '../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import {
  PurchaseRequisitionDto,
  PurchaseRequisitionItemDto,
  CreatePurchaseRequisitionRequest,
  CreatePurchaseRequisitionItemRequest,
  UpdatePurchaseRequisitionRequest,
  UpdatePurchaseRequisitionItemRequest,
  PurchaseRequisitionStatus,
  RequisitionPriority,
  getPurchaseRequisitionStatusName,
  getPurchaseRequisitionStatusSeverity,
  getPrioritySeverity,
  PurchaseRequisitionHistoryDto
} from '../../../../core/models/purchase-requisition.models';
import { PurchaseOrderStatus } from '../../../../core/models/purchase-order.models';
import { DataTableColumn } from '../../../../shared/models/data-table-config.model';
import { LookupItem } from '../../../../shared/components/lookup-dropdown/lookup-item.model';
import { Company, Branch, ProductItem, UnitOfMeasureItem, SupplierItem } from '../../../../core/models/scope.models';

export type PRPageMode = 'create' | 'view' | 'edit' | 'approval';

export interface EditablePRLine {
  lineId: string;
  id?: string;
  productId: string;
  productCode: string;
  productName: string;
  unitOfMeasureId?: string | null;
  unitOfMeasureCode?: string | null;
  quantity: number;
  estimatedUnitPrice: number;
  estimatedLineTotal: number;
  remarks?: string;
  orderedQuantity?: number;
  remainingQuantity?: number;
}

@Component({
  selector: 'app-purchase-requisition-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterModule,
    AppPageHeaderComponent,
    AppEditableDataTableComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AccessDeniedComponent
  ],
  templateUrl: './purchase-requisition-page.component.html',
  styleUrl: './purchase-requisition-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PurchaseRequisitionPageComponent implements OnInit, OnDestroy {
  readonly prService = inject(PurchaseRequisitionService);
  readonly poService = inject(PurchaseOrderService);
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);
  readonly route = inject(ActivatedRoute);

  private readonly subs = new Subscription();

  // Mode & Route State
  readonly mode = signal<PRPageMode>('create');
  readonly requisitionId = signal<string | null>(null);
  readonly currentRequisition = signal<PurchaseRequisitionDto | null>(null);
  readonly approvalHistory = signal<PurchaseRequisitionHistoryDto[]>([]);

  // Loading & Error States
  readonly loading = signal<boolean>(false);
  readonly actionInProgress = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to access purchase requisitions for this operating company and branch. Contact your administrator if you believe this is an error."
  );
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Master Data
  readonly products = signal<ProductItem[]>([]);
  readonly uoms = signal<UnitOfMeasureItem[]>([]);
  readonly suppliers = signal<SupplierItem[]>([]);

  // UOM Select Options for Line Items
  readonly uomSelectOptions = computed(() => [
    { label: '-- Select UOM --', value: '' },
    ...this.uoms().map(u => ({
      label: `${u.code} (${u.name})`,
      value: u.unitOfMeasureId
    }))
  ]);

  // Supplier Options for Header Form
  readonly supplierOptions = computed(() => [
    { label: '-- None / Optional --', value: '' },
    ...this.suppliers().map(s => ({
      label: `${s.supplierCode} - ${s.supplierName}`,
      value: s.supplierId
    }))
  ]);

  // Priority Options
  readonly priorityOptions: { label: string; value: RequisitionPriority }[] = [
    { label: 'Low', value: 'Low' },
    { label: 'Medium (Standard)', value: 'Medium' },
    { label: 'High', value: 'High' },
    { label: 'Urgent', value: 'Urgent' }
  ];

  // Scope Selection
  readonly selectedCompanyId = signal<string>('');
  readonly selectedBranchId = signal<string>('');

  // Working Line Items
  readonly workingLines = signal<EditablePRLine[]>([]);

  // PR-to-PO Fulfillment Map: itemId -> { ordered: number; remaining: number }
  readonly lineFulfillments = signal<Map<string, { ordered: number; remaining: number }>>(new Map());

  // Modal / Contextual Decision States (No PrimeNG)
  readonly decisionAction = signal<'approve' | 'reject' | 'cancel' | 'delete' | null>(null);
  readonly decisionRemarks = signal<string>('');
  readonly decisionError = signal<string | null>(null);

  // Reactive Header Form
  // Only Requisition Number and Date are mandatory on the header; Priority, Supplier, Required Date, Notes are strictly optional.
  readonly headerForm = new FormGroup({
    requisitionNumber: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    requisitionDate: new FormControl<string>(new Date().toISOString().substring(0, 10), {
      nonNullable: true,
      validators: [Validators.required]
    }),
    requiredDate: new FormControl<string>(''),
    priority: new FormControl<RequisitionPriority>('Medium', { nonNullable: true }),
    preferredSupplierId: new FormControl<string>(''),
    notes: new FormControl<string>('')
  });

  // Read-only mode check
  readonly isReadOnlyMode = computed<boolean>(() => this.mode() === 'view' || this.mode() === 'approval');

  // Total remaining and ordered quantities
  readonly totalRequestedQuantity = computed<number>(() => {
    return this.workingLines().reduce((sum, item) => sum + (Number(item.quantity) || 0), 0);
  });

  readonly totalOrderedQuantity = computed<number>(() => {
    const map = this.lineFulfillments();
    let sum = 0;
    for (const val of map.values()) {
      sum += val.ordered;
    }
    return sum;
  });

  readonly totalRemainingQuantity = computed<number>(() => {
    const map = this.lineFulfillments();
    if (map.size === 0) {
      return this.totalRequestedQuantity();
    }
    let sum = 0;
    for (const val of map.values()) {
      sum += val.remaining;
    }
    return sum;
  });

  readonly isFullyOrdered = computed<boolean>(() => {
    const req = this.currentRequisition();
    if (!req || Number(req.status) !== PurchaseRequisitionStatus.Approved) return false;
    const lines = this.workingLines();
    if (lines.length === 0) return false;
    return this.lineFulfillments().size > 0 && this.totalRemainingQuantity() === 0;
  });

  // Segregation of Duties check: Is the current logged-in user the requestor?
  readonly isSelfApprovalRestricted = computed<boolean>(() => {
    const req = this.currentRequisition();
    const currentUserId = this.authService.currentUser()?.userId;
    return !!(
      req &&
      Number(req.status) === PurchaseRequisitionStatus.Submitted &&
      currentUserId &&
      req.requestorUserId &&
      currentUserId.toLowerCase() === req.requestorUserId.toLowerCase()
    );
  });

  // Breadcrumbs
  readonly breadcrumbs = computed(() => {
    const list = [
      { label: 'Workspace', routerLink: '/app/dashboard' },
      { label: 'Procurement', routerLink: '/app/procurement' },
      { label: 'Purchase Requisitions', routerLink: '/app/procurement/purchase-requisitions' }
    ];

    switch (this.mode()) {
      case 'create':
        list.push({ label: 'New Requisition', routerLink: '' });
        break;
      case 'edit':
        list.push({ label: 'Edit Requisition', routerLink: '' });
        break;
      case 'approval':
        list.push({ label: 'Requisition Approval', routerLink: '' });
        break;
      case 'view':
      default: {
        const num = this.currentRequisition()?.requisitionNumber || 'Requisition Details';
        list.push({ label: num, routerLink: '' });
        break;
      }
    }
    return list;
  });

  // Page Title & Subtitle
  readonly pageTitle = computed(() => {
    const num = this.currentRequisition()?.requisitionNumber ? ` - ${this.currentRequisition()?.requisitionNumber}` : '';
    switch (this.mode()) {
      case 'create':
        return 'New Purchase Requisition';
      case 'edit':
        return `Edit Purchase Requisition${num}`;
      case 'approval':
        return `Purchase Requisition Approval${num}`;
      case 'view':
      default:
        return `Purchase Requisition Details${num}`;
    }
  });

  readonly pageSubtitle = computed(() => {
    switch (this.mode()) {
      case 'create':
        return 'Raise an internal purchase request with item quantities and UOM for department authorization.';
      case 'edit':
        return 'Modify requisition lines, required dates, and justification before submission.';
      case 'approval':
        return 'Review requisition lines and estimated expenditure to authorize or reject this request.';
      case 'view':
      default:
        return 'Inspect requisition status, line item specifications, fulfillment tracking, and workflow history.';
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
        unitOfMeasureId: p.unitOfMeasureId,
        unitOfMeasure: p.unitOfMeasure
      }
    }));
  });

  // Columns for the editable line items table
  readonly editableItemColumns = computed<DataTableColumn<EditablePRLine>[]>(() => {
    const isApproved = this.currentRequisition() && Number(this.currentRequisition()!.status) === PurchaseRequisitionStatus.Approved;
    const isReadOnly = this.isReadOnlyMode();

    const cols: DataTableColumn<EditablePRLine>[] = [
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
            row.estimatedUnitPrice = Number(item.meta.cost) || 0;
          }
          if (item.meta?.unitOfMeasureId) {
            row.unitOfMeasureId = item.meta.unitOfMeasureId;
          }
          if (item.meta?.unitOfMeasure) {
            row.unitOfMeasureCode = item.meta.unitOfMeasure;
          } else {
            const uom = this.uoms().find(u => u.unitOfMeasureId === item.meta?.unitOfMeasureId);
            row.unitOfMeasureCode = uom ? uom.code : null;
          }
          this.calculateRow(row);
        },
        validators: [(val: any) => (val ? null : 'Product is required')]
      },
      {
        field: 'productName',
        header: 'Description',
        sortable: true,
        width: '200px',
        editable: false
      },
      {
        field: 'unitOfMeasureId',
        header: 'UOM',
        sortable: true,
        width: '130px',
        editable: !isReadOnly,
        editType: 'select',
        editOptions: this.uomSelectOptions(),
        format: (_val: any, row?: EditablePRLine) => row?.unitOfMeasureCode || '-',
        validators: [(val: any) => (val ? null : 'UOM is required')]
      },
      {
        field: 'quantity',
        header: isApproved ? 'Requested Qty' : 'Quantity',
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
      }
    ];

    // When viewing an approved PR, display Ordered and Remaining fulfillment quantities
    if (isApproved && isReadOnly) {
      cols.push(
        {
          field: 'orderedQuantity',
          header: 'Ordered in PO',
          sortable: true,
          align: 'right',
          width: '120px',
          editable: false,
          format: (_val: any, row?: EditablePRLine) => {
            const f = row?.id ? this.lineFulfillments().get(row.id) : null;
            return f ? String(f.ordered) : '0';
          }
        },
        {
          field: 'remainingQuantity',
          header: 'Remaining Qty',
          sortable: true,
          align: 'right',
          width: '120px',
          editable: false,
          format: (_val: any, row?: EditablePRLine) => {
            const f = row?.id ? this.lineFulfillments().get(row.id) : null;
            return f ? String(f.remaining) : String(row?.quantity || 0);
          }
        }
      );
    }

    cols.push(
      {
        field: 'estimatedUnitPrice',
        header: 'Est. Unit Price',
        sortable: true,
        align: 'right',
        width: '130px',
        type: 'currency',
        editable: !isReadOnly,
        editType: 'currency',
        validators: [
          (val: any) => {
            if (val === null || val === undefined || val === '') return null; // Optional
            const num = Number(val);
            if (isNaN(num)) return 'Must be a number';
            if (num < 0) return 'Cannot be negative';
            return null;
          }
        ]
      },
      {
        field: 'estimatedLineTotal',
        header: 'Est. Line Total',
        sortable: true,
        align: 'right',
        width: '130px',
        type: 'currency',
        editable: false
      },
      {
        field: 'remarks',
        header: 'Remarks / Notes',
        sortable: false,
        width: '200px',
        editable: !isReadOnly,
        editType: 'text'
      }
    );

    return cols;
  });

  // Row calculation
  readonly calculateRow = (row: EditablePRLine): void => {
    const qty = Number(row.quantity) || 0;
    const cost = Number(row.estimatedUnitPrice) || 0;
    row.estimatedLineTotal = Math.round(qty * cost * 100) / 100;
  };

  // Blank row factory
  readonly defaultNewRow = (): Partial<EditablePRLine> => {
    const defaultUom = this.uoms().length > 0 ? this.uoms()[0] : null;
    return {
      lineId: 'line-' + Math.random().toString(36).substring(2, 9),
      productId: '',
      productCode: '',
      productName: '',
      unitOfMeasureId: defaultUom ? defaultUom.unitOfMeasureId : null,
      unitOfMeasureCode: defaultUom ? defaultUom.code : null,
      quantity: 1,
      estimatedUnitPrice: 0,
      estimatedLineTotal: 0,
      remarks: ''
    };
  };

  // Total amount
  readonly totalAmount = computed<number>(() => {
    return this.workingLines().reduce((sum, item) => sum + (Number(item.estimatedLineTotal) || 0), 0);
  });

  // Available Companies & Branches
  readonly availableCompanies = computed<Company[]>(() => this.scopeService.companies());
  readonly availableBranches = computed<Branch[]>(() => this.scopeService.branches());

  ngOnInit(): void {
    this.initScope();
    this.loadMasterData();

    this.subs.add(
      this.route.url.subscribe(segments => {
        const path = segments.map(s => s.path).join('/');
        if (path === 'new') {
          this.mode.set('create');
          this.generateRequisitionNumber();
        } else if (segments.length >= 2 && segments[1].path === 'edit') {
          this.mode.set('edit');
          this.requisitionId.set(segments[0].path);
          this.loadExistingRequisition(segments[0].path);
        } else if (segments.length >= 2 && segments[1].path === 'approve') {
          this.mode.set('approval');
          this.requisitionId.set(segments[0].path);
          this.loadExistingRequisition(segments[0].path);
        } else if (segments.length >= 1) {
          this.mode.set('view');
          this.requisitionId.set(segments[0].path);
          this.loadExistingRequisition(segments[0].path);
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  private initScope(): void {
    const currentComp = this.scopeService.selectedCompany();
    if (currentComp) {
      this.selectedCompanyId.set(currentComp.id);
    }
    const currentBr = this.scopeService.selectedBranch();
    if (currentBr) {
      this.selectedBranchId.set(currentBr.id);
    }
  }

  private loadMasterData(): void {
    this.masterDataService.getProducts().subscribe({
      next: prods => {
        this.products.set(prods);
        this.enrichLinesWithProductMetadata();
      }
    });

    this.masterDataService.getUnitsOfMeasure().subscribe({
      next: uoms => {
        this.uoms.set(uoms);
        this.enrichLinesWithProductMetadata();
      }
    });

    this.masterDataService.getSuppliers().subscribe({
      next: sups => {
        this.suppliers.set(sups);
      }
    });
  }

  private enrichLinesWithProductMetadata(): void {
    const prods = this.products();
    const uoms = this.uoms();
    if (prods.length === 0 && uoms.length === 0) return;

    const prodMap = new Map(prods.map(p => [p.productId.toLowerCase(), p]));
    const uomMap = new Map(uoms.map(u => [u.unitOfMeasureId.toLowerCase(), u]));

    this.workingLines.update(lines =>
      lines.map(line => {
        const p = prodMap.get((line.productId || '').toLowerCase());
        const uId = line.unitOfMeasureId || p?.unitOfMeasureId;
        const u = uId ? uomMap.get(uId.toLowerCase()) : null;
        return {
          ...line,
          productCode: line.productCode || p?.productCode || '',
          productName: line.productName || p?.productName || '',
          unitOfMeasureId: uId || null,
          unitOfMeasureCode: line.unitOfMeasureCode || u?.code || p?.unitOfMeasure || null
        };
      })
    );
  }

  private generateRequisitionNumber(): void {
    const yr = new Date().getFullYear();
    const rnd = Math.floor(1000 + Math.random() * 9000);
    this.headerForm.controls.requisitionNumber.setValue(`PR-${yr}-${rnd}`);
  }

  loadExistingRequisition(id: string): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    const companyId = company?.id;
    const branchId = branch?.id || this.scopeService.branches()[0]?.id;

    if (!companyId || !branchId) {
      this.error.set('Operating company and branch scope must be selected to view this requisition.');
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.isAccessDenied.set(false);

    this.prService.getRequisition(companyId, branchId, id).subscribe({
      next: req => {
        this.loading.set(false);
        if (!req) {
          this.error.set('Purchase requisition not found.');
          return;
        }

        this.currentRequisition.set(req);
        this.populateForm(req);
        this.loadApprovalHistory(companyId, branchId, id);

        // If approved, load PO fulfillment data
        if (Number(req.status) === PurchaseRequisitionStatus.Approved) {
          this.loadRequisitionFulfillment(companyId, branchId, req);
        }
      },
      error: (err: any) => {
        this.loading.set(false);
        if (err?.status === 403) {
          this.isAccessDenied.set(true);
        } else {
          this.error.set(this.getCleanErrorMessage(err, 'Failed to load purchase requisition.'));
        }
      }
    });
  }

  private loadRequisitionFulfillment(companyId: string, branchId: string, req: PurchaseRequisitionDto): void {
    this.poService.getOrders(companyId, branchId).subscribe({
      next: orders => {
        const activeOrders = (orders || []).filter(o => {
          const st = Number(o.status);
          return st !== PurchaseOrderStatus.Cancelled && st !== PurchaseOrderStatus.Rejected;
        });

        const map = new Map<string, { ordered: number; remaining: number }>();
        const reqNum = req.requisitionNumber.toLowerCase();

        for (const item of req.items || []) {
          const itemId = (item.id || '').toLowerCase();
          let orderedForLine = 0;

          for (const ord of activeOrders) {
            for (const ordItem of ord.items || []) {
              const notes = (ordItem.notes || '').toLowerCase();
              if (
                notes.includes(`line: ${itemId}`) ||
                notes.includes(`line:${itemId}`) ||
                notes.includes(itemId) ||
                (notes.includes(reqNum) && ordItem.productId.toLowerCase() === item.productId.toLowerCase())
              ) {
                orderedForLine += Number(ordItem.quantity) || 0;
              }
            }
          }

          const remaining = Math.max(0, (Number(item.quantity) || 0) - orderedForLine);
          map.set(item.id || '', { ordered: orderedForLine, remaining });
        }

        this.lineFulfillments.set(map);
      },
      error: () => {
        // If PO fetch fails, leave fulfillment map empty
      }
    });
  }

  private loadApprovalHistory(companyId: string, branchId: string, requisitionId: string): void {
    this.prService.getApprovalHistory(companyId, branchId, requisitionId).subscribe({
      next: history => this.approvalHistory.set(history),
      error: () => this.approvalHistory.set([])
    });
  }

  private populateForm(req: PurchaseRequisitionDto): void {
    let rawNotes = req.notes || '';
    let priorityVal: RequisitionPriority = 'Medium';
    const priorityMatch = rawNotes.match(/\[Priority:\s*(Low|Medium|High|Urgent)\]/i);
    if (priorityMatch) {
      priorityVal = priorityMatch[1] as RequisitionPriority;
      rawNotes = rawNotes.replace(priorityMatch[0], '').trim();
    }

    let preferredSupId = '';
    const supplierMatch = rawNotes.match(/\[Preferred Supplier:\s*([^\]]+)\]/i);
    if (supplierMatch) {
      const supName = supplierMatch[1].trim().toLowerCase();
      const sup = this.suppliers().find(s => s.supplierName.toLowerCase() === supName);
      if (sup) {
        preferredSupId = sup.supplierId;
      }
      rawNotes = rawNotes.replace(supplierMatch[0], '').trim();
    }

    this.headerForm.patchValue({
      requisitionNumber: req.requisitionNumber,
      requisitionDate: req.requisitionDate ? req.requisitionDate.substring(0, 10) : '',
      requiredDate: req.requiredDate ? req.requiredDate.substring(0, 10) : '',
      priority: priorityVal,
      preferredSupplierId: preferredSupId,
      notes: rawNotes
    });

    if (this.isReadOnlyMode()) {
      this.headerForm.disable();
    } else {
      this.headerForm.enable();
    }

    const lines: EditablePRLine[] = (req.items || []).map((item, idx) => ({
      lineId: `line-${item.id || idx}`,
      id: item.id,
      productId: item.productId,
      productCode: item.productCode || '',
      productName: item.productName || '',
      unitOfMeasureId: item.unitOfMeasureId,
      unitOfMeasureCode: item.unitOfMeasureCode,
      quantity: item.quantity,
      estimatedUnitPrice: item.estimatedUnitPrice,
      estimatedLineTotal: item.estimatedLineTotal,
      remarks: item.remarks || '',
      orderedQuantity: 0,
      remainingQuantity: item.quantity
    }));

    this.workingLines.set(lines);
    this.enrichLinesWithProductMetadata();
  }

  onLinesChange(newLines: EditablePRLine[]): void {
    this.workingLines.set(newLines);
  }

  onCellValueChange(event: { row: EditablePRLine; column?: DataTableColumn<EditablePRLine>; field?: string; value: any }): void {
    const field = event.field || event.column?.field;
    if (field === 'unitOfMeasureId') {
      event.row.unitOfMeasureId = event.value || null;
      const uom = this.uoms().find(u => u.unitOfMeasureId === event.value);
      event.row.unitOfMeasureCode = uom ? uom.code : null;
    }
    this.calculateRow(event.row);
  }

  // Save Operations
  saveRequisition(andSubmit: boolean): void {
    if (this.headerForm.invalid) {
      this.headerForm.markAllAsTouched();
      this.showMessage('Please correct the validation errors on the requisition form.', 'warn');
      return;
    }

    const companyId = this.selectedCompanyId() || this.scopeService.selectedCompany()?.id;
    const branchId = this.selectedBranchId() || this.scopeService.selectedBranch()?.id;
    if (!companyId || !branchId) {
      this.showMessage('Operating company and branch are required.', 'danger');
      return;
    }

    const lines = this.workingLines();
    if (lines.length === 0) {
      this.showMessage('At least one line item is required.', 'warn');
      return;
    }

    // Business rule: Only Product, Quantity (> 0), and UOM are mandatory
    const invalidLine = lines.find(l => !l.productId || Number(l.quantity) <= 0 || !l.unitOfMeasureId);
    if (invalidLine) {
      this.showMessage('All line items must have a valid product, quantity greater than zero, and UOM.', 'warn');
      return;
    }

    this.actionInProgress.set(true);

    const formVal = this.headerForm.getRawValue();

    // Assemble optional metadata (priority, preferred supplier) into notes cleanly
    let compiledNotes = formVal.notes?.trim() || '';
    const prefixParts: string[] = [];
    if (formVal.priority && formVal.priority !== 'Medium') {
      prefixParts.push(`[Priority: ${formVal.priority}]`);
    }
    if (formVal.preferredSupplierId) {
      const sup = this.suppliers().find(s => s.supplierId === formVal.preferredSupplierId);
      if (sup) {
        prefixParts.push(`[Preferred Supplier: ${sup.supplierName}]`);
      }
    }
    if (prefixParts.length > 0) {
      compiledNotes = prefixParts.join(' ') + (compiledNotes ? ' ' + compiledNotes : '');
    }

    if (this.mode() === 'create') {
      const request: CreatePurchaseRequisitionRequest = {
        requisitionNumber: formVal.requisitionNumber.trim(),
        requisitionDate: formVal.requisitionDate ? new Date(formVal.requisitionDate).toISOString() : undefined,
        requiredDate: formVal.requiredDate ? new Date(formVal.requiredDate).toISOString() : undefined,
        notes: compiledNotes || undefined,
        items: lines.map(l => ({
          productId: l.productId,
          unitOfMeasureId: l.unitOfMeasureId || undefined,
          quantity: Number(l.quantity),
          estimatedUnitPrice: Number(l.estimatedUnitPrice) || 0,
          remarks: l.remarks?.trim() || undefined
        }))
      };

      this.prService.createRequisition(companyId, branchId, request).subscribe({
        next: created => {
          if (andSubmit) {
            this.prService.submitRequisition(companyId, branchId, created.id).subscribe({
              next: submitted => {
                this.actionInProgress.set(false);
                this.showMessage(`Requisition ${submitted.requisitionNumber} created and submitted successfully.`, 'success');
                this.router.navigate(['/app/procurement/purchase-requisitions', submitted.id]);
              },
              error: subErr => {
                this.actionInProgress.set(false);
                this.showMessage(
                  `Requisition created as draft, but failed to submit: ${this.getCleanErrorMessage(subErr, '')}`,
                  'warn'
                );
                this.router.navigate(['/app/procurement/purchase-requisitions', created.id]);
              }
            });
          } else {
            this.actionInProgress.set(false);
            this.showMessage(`Requisition ${created.requisitionNumber} saved as Draft.`, 'success');
            this.router.navigate(['/app/procurement/purchase-requisitions', created.id]);
          }
        },
        error: (err: any) => {
          this.actionInProgress.set(false);
          this.showMessage(this.getCleanErrorMessage(err, 'Failed to create purchase requisition.'), 'danger');
        }
      });
    } else if (this.mode() === 'edit') {
      const reqId = this.requisitionId();
      if (!reqId) return;

      const updateHeader: UpdatePurchaseRequisitionRequest = {
        requisitionDate: formVal.requisitionDate ? new Date(formVal.requisitionDate).toISOString() : undefined,
        requiredDate: formVal.requiredDate ? new Date(formVal.requiredDate).toISOString() : undefined,
        notes: compiledNotes || undefined
      };

      this.prService.updateRequisition(companyId, branchId, reqId, updateHeader).subscribe({
        next: updated => {
          const newLines = lines.filter(l => !l.id);
          const addCalls = newLines.map(l =>
            this.prService.addItem(companyId, branchId, reqId, {
              productId: l.productId,
              unitOfMeasureId: l.unitOfMeasureId || undefined,
              quantity: Number(l.quantity),
              estimatedUnitPrice: Number(l.estimatedUnitPrice) || 0,
              remarks: l.remarks?.trim() || undefined
            })
          );

          const finishSave = () => {
            if (andSubmit) {
              this.prService.submitRequisition(companyId, branchId, reqId).subscribe({
                next: submitted => {
                  this.actionInProgress.set(false);
                  this.showMessage(`Requisition ${submitted.requisitionNumber} updated and submitted.`, 'success');
                  this.router.navigate(['/app/procurement/purchase-requisitions', reqId]);
                },
                error: subErr => {
                  this.actionInProgress.set(false);
                  this.showMessage(`Requisition updated, but failed to submit: ${this.getCleanErrorMessage(subErr, '')}`, 'warn');
                  this.router.navigate(['/app/procurement/purchase-requisitions', reqId]);
                }
              });
            } else {
              this.actionInProgress.set(false);
              this.showMessage(`Requisition ${updated.requisitionNumber} updated successfully.`, 'success');
              this.router.navigate(['/app/procurement/purchase-requisitions', reqId]);
            }
          };

          if (addCalls.length > 0) {
            forkJoin(addCalls).subscribe({
              next: () => finishSave(),
              error: () => finishSave()
            });
          } else {
            finishSave();
          }
        },
        error: (err: any) => {
          this.actionInProgress.set(false);
          this.showMessage(this.getCleanErrorMessage(err, 'Failed to update requisition.'), 'danger');
        }
      });
    }
  }

  // Decision Modal Actions
  openDecision(action: 'approve' | 'reject' | 'cancel' | 'delete'): void {
    this.decisionAction.set(action);
    this.decisionRemarks.set('');
    this.decisionError.set(null);
  }

  closeDecision(): void {
    this.decisionAction.set(null);
  }

  executeDecision(): void {
    const action = this.decisionAction();
    const req = this.currentRequisition();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    const companyId = company?.id;
    const branchId = branch?.id || req?.branchId;

    if (!action || !req || !companyId || !branchId) return;

    const remarks = this.decisionRemarks().trim();
    if (action === 'reject' && !remarks) {
      this.decisionError.set('Remarks are required when rejecting a requisition.');
      return;
    }

    this.actionInProgress.set(true);
    this.decisionError.set(null);

    let obs: Observable<any>;
    if (action === 'approve') {
      obs = this.prService.approveRequisition(companyId, branchId, req.id, remarks || undefined);
    } else if (action === 'reject') {
      obs = this.prService.rejectRequisition(companyId, branchId, req.id, remarks);
    } else if (action === 'cancel') {
      obs = this.prService.cancelRequisition(companyId, branchId, req.id, remarks || undefined);
    } else {
      obs = this.prService.deleteRequisition(companyId, branchId, req.id);
    }

    obs.subscribe({
      next: (_res: any) => {
        this.actionInProgress.set(false);
        this.closeDecision();
        if (action === 'delete') {
          this.showMessage(`Requisition ${req.requisitionNumber} deleted.`, 'success');
          this.router.navigate(['/app/procurement/purchase-requisitions']);
        } else {
          const verb = action === 'approve' ? 'approved' : action === 'reject' ? 'rejected' : 'cancelled';
          this.showMessage(`Requisition ${req.requisitionNumber} ${verb} successfully.`, 'success');
          this.loadExistingRequisition(req.id);
        }
      },
      error: (err: any) => {
        this.actionInProgress.set(false);
        this.decisionError.set(this.getCleanErrorMessage(err, `Failed to ${action} requisition.`));
      }
    });
  }

  // Direct Submission from View
  submitFromView(): void {
    const req = this.currentRequisition();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    const companyId = company?.id;
    const branchId = branch?.id || req?.branchId;

    if (!req || !companyId || !branchId) return;

    this.actionInProgress.set(true);
    this.prService.submitRequisition(companyId, branchId, req.id).subscribe({
      next: submitted => {
        this.actionInProgress.set(false);
        this.showMessage(`Requisition ${submitted.requisitionNumber} submitted for approval.`, 'success');
        this.loadExistingRequisition(req.id);
      },
      error: (err: any) => {
        this.actionInProgress.set(false);
        this.showMessage(this.getCleanErrorMessage(err, 'Failed to submit requisition.'), 'danger');
      }
    });
  }

  // Navigation to Create PO from Approved PR
  canCreatePurchaseOrder(): boolean {
    const req = this.currentRequisition();
    if (!req) return false;
    const isApproved = Number(req.status) === PurchaseRequisitionStatus.Approved;
    if (!isApproved) return false;
    const hasPerm = this.authService.hasPermission('PURCHASE_ORDER.CREATE') || this.authService.isTenantAdmin();
    if (!hasPerm) return false;
    return this.totalRemainingQuantity() > 0;
  }

  navigateToCreatePurchaseOrder(): void {
    const req = this.currentRequisition();
    if (req) {
      this.router.navigate(['/app/procurement/purchase-orders/new'], {
        queryParams: { purchaseRequisitionId: req.id }
      });
    }
  }

  // Navigation
  navigateBack(): void {
    this.router.navigate(['/app/procurement/purchase-requisitions']);
  }

  navigateToEdit(): void {
    const req = this.currentRequisition();
    if (req) {
      this.router.navigate(['/app/procurement/purchase-requisitions', req.id, 'edit']);
    }
  }

  // Permission & Status Checks
  canEdit(): boolean {
    const req = this.currentRequisition();
    if (!req) return false;
    const isDraftOrRejected =
      Number(req.status) === PurchaseRequisitionStatus.Draft ||
      Number(req.status) === PurchaseRequisitionStatus.Rejected;
    return isDraftOrRejected && (this.authService.hasPermission('PURCHASE_REQUISITION.EDIT') || this.authService.isTenantAdmin());
  }

  canSubmit(): boolean {
    const req = this.currentRequisition();
    if (!req) return false;
    return (
      Number(req.status) === PurchaseRequisitionStatus.Draft &&
      (this.authService.hasPermission('PURCHASE_REQUISITION.SUBMIT') || this.authService.isTenantAdmin())
    );
  }

  canApprove(): boolean {
    const req = this.currentRequisition();
    if (!req) return false;
    const isSubmitted = Number(req.status) === PurchaseRequisitionStatus.Submitted;
    if (!isSubmitted) return false;

    const hasPerm = this.authService.hasPermission('PURCHASE_REQUISITION.APPROVE') || this.authService.isTenantAdmin();
    if (!hasPerm) return false;

    // Segregation of duties: Submitter cannot approve their own requisition
    const currentUserId = this.authService.currentUser()?.userId;
    if (currentUserId && req.requestorUserId && currentUserId.toLowerCase() === req.requestorUserId.toLowerCase()) {
      return false;
    }

    return true;
  }

  canCancel(): boolean {
    const req = this.currentRequisition();
    if (!req) return false;
    const isEligible =
      Number(req.status) === PurchaseRequisitionStatus.Draft ||
      Number(req.status) === PurchaseRequisitionStatus.Submitted;
    return isEligible && (this.authService.hasPermission('PURCHASE_REQUISITION.CANCEL') || this.authService.isTenantAdmin());
  }

  canDelete(): boolean {
    const req = this.currentRequisition();
    if (!req) return false;
    return (
      Number(req.status) === PurchaseRequisitionStatus.Draft &&
      (this.authService.hasPermission('PURCHASE_REQUISITION.DELETE') || this.authService.isTenantAdmin())
    );
  }

  getStatusName(): string {
    return getPurchaseRequisitionStatusName(this.currentRequisition()?.status || PurchaseRequisitionStatus.Draft);
  }

  getStatusSeverity(): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
    return getPurchaseRequisitionStatusSeverity(this.currentRequisition()?.status || PurchaseRequisitionStatus.Draft);
  }

  getPrioritySeverityName(): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
    return getPrioritySeverity(this.headerForm.controls.priority.value);
  }

  getDecisionBadgeSeverity(decision: number | string): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
    const num = Number(decision);
    if (num === 2) return 'success'; // Approved
    if (num === 3 || num === 4) return 'danger'; // Rejected or Cancelled
    if (num === 1) return 'warning'; // Submitted
    return 'neutral';
  }

  getDecisionName(decision: number | string): string {
    const num = Number(decision);
    if (num === 1) return 'Submitted';
    if (num === 2) return 'Approved';
    if (num === 3) return 'Rejected';
    if (num === 4) return 'Cancelled';
    return String(decision);
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
