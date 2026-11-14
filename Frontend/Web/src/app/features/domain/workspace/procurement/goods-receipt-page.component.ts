import {
  Component,
  OnInit,
  OnDestroy,
  inject,
  signal,
  computed,
  ChangeDetectionStrategy,
  ViewChild,
  TemplateRef
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
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
import { AccessDeniedComponent } from '../../../../shared/components/access-denied/access-denied.component';
import { GoodsReceiptNoteService } from '../../../../core/services/goods-receipt-note.service';
import { PurchaseOrderService } from '../../../../core/services/purchase-order.service';
import { MasterDataService } from '../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import {
  GoodsReceiptNoteDto,
  GoodsReceiptNoteStatus,
  CreateGoodsReceiptNoteRequest,
  CreateGoodsReceiptNoteLineRequest,
  UpdateGoodsReceiptNoteRequest,
  BatchSerialAllocation,
  getGrnStatusName,
  getGrnStatusSeverity
} from '../../../../core/models/goods-receipt-note.models';
import { PurchaseOrderDto, PurchaseOrderStatus } from '../../../../core/models/purchase-order.models';
import { TrackingMode, ProductItem, Branch } from '../../../../core/models/scope.models';
import { DataTableColumn } from '../../../../shared/models/data-table-config.model';
import {
  BatchSerialTrackingDialogComponent,
  BatchSerialDialogData
} from './batch-serial-dialog/batch-serial-dialog.component';

export type GRNPageMode = 'create' | 'view';

export interface EditableGRNLine {
  lineId: string;
  purchaseOrderItemId: string;
  productId: string;
  productCode: string;
  productName: string;
  unitOfMeasure: string;
  orderedQuantity: number;
  previouslyReceivedQuantity: number;
  outstandingQuantity: number;
  receivedQuantityNow: number;
  unitCost: number; // Strictly read-only
  discountRate?: number;
  netLineAmount?: number;
  lineTotal: number; // Strictly read-only
  warehouseId?: string;
  warehouseLocationId?: string;
  unitOfMeasureId?: string | null;
  remarks?: string;
  trackingMode?: number;
  trackingAllocations?: BatchSerialAllocation[];
  batchNumber?: string | null;
  serialNumber?: string | null;
  manufacturingDate?: string | null;
  expiryDate?: string | null;
  isQuarantine?: boolean;
}

@Component({
  selector: 'app-goods-receipt-page',
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
    AccessDeniedComponent,
    MatDialogModule
  ],
  templateUrl: './goods-receipt-page.component.html',
  styleUrl: './goods-receipt-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GoodsReceiptPageComponent implements OnInit, OnDestroy {
  @ViewChild('trackingCellTemplate', { static: true }) trackingCellTemplate!: TemplateRef<any>;

  readonly grnService = inject(GoodsReceiptNoteService);
  readonly poService = inject(PurchaseOrderService);
  readonly masterDataService = inject(MasterDataService);
  readonly matDialog = inject(MatDialog);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);
  readonly route = inject(ActivatedRoute);

  private readonly subs = new Subscription();
  private readonly productMasterCache = new Map<string, ProductItem>();
  private enrichmentToken = 0;

  // Mode & Route State
  readonly mode = signal<GRNPageMode>('create');
  readonly grnId = signal<string | null>(null);
  readonly purchaseOrderId = signal<string | null>(null);
  readonly currentGrn = signal<GoodsReceiptNoteDto | null>(null);
  readonly sourcePo = signal<PurchaseOrderDto | null>(null);
  readonly editingDraft = signal<boolean>(false);
  readonly confirmDeleteDraft = signal<boolean>(false);

  readonly isFullyReceived = computed<boolean>(() => {
    if (this.mode() !== 'create') return false;
    const po = this.sourcePo();
    const lines = this.workingLines();
    if (!po || lines.length === 0) return false;
    return lines.every(l => (Number(l.outstandingQuantity) || 0) <= 0);
  });

  readonly canEditDraft = computed(() =>
    (this.mode() === 'create' && !this.isFullyReceived()) ||
    (this.mode() === 'view' &&
      this.editingDraft() &&
      Number(this.currentGrn()?.status) === GoodsReceiptNoteStatus.Draft)
  );
  readonly isDraft = computed(() =>
    Number(this.currentGrn()?.status) === GoodsReceiptNoteStatus.Draft
  );

  // Loading & Error States
  readonly loading = signal<boolean>(false);
  readonly actionInProgress = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly poIneligibleError = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to access goods receipts for this operating company and branch."
  );
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Line items
  readonly workingLines = signal<EditableGRNLine[]>([]);

  // Header Form
  readonly headerForm = new FormGroup({
    grnNumber: new FormControl<string>('Auto-generated on save', { nonNullable: true }),
    receiptDate: new FormControl<string>(new Date().toISOString().substring(0, 10), {
      nonNullable: true,
      validators: [Validators.required]
    }),
    purchaseOrderNumber: new FormControl<string>('', { nonNullable: true }),
    supplierName: new FormControl<string>('', { nonNullable: true }),
    deliveryNoteNumber: new FormControl<string>(''),
    remarks: new FormControl<string>('')
  });

  // Breadcrumbs
  readonly breadcrumbs = computed(() => {
    const list = [
      { label: 'Workspace', routerLink: '/app/dashboard' },
      { label: 'Procurement', routerLink: '/app/procurement' },
      { label: 'Goods Receipts', routerLink: '/app/procurement/goods-receipts' }
    ];

    if (this.mode() === 'create') {
      list.push({ label: 'New Product Receipt', routerLink: '' });
    } else {
      const num = this.currentGrn()?.grnNumber || 'Receipt Details';
      list.push({ label: num, routerLink: '' });
    }
    return list;
  });

  // Page Title & Subtitle
  readonly pageTitle = computed(() => {
    if (this.mode() === 'create') {
      const poNum = this.sourcePo()?.orderNumber;
      return poNum ? `Product Receipt for ${poNum}` : 'New Goods Receipt Note';
    }
    return `Goods Receipt ${this.currentGrn()?.grnNumber || ''}`.trim();
  });

  readonly pageSubtitle = computed(() => {
    if (this.mode() === 'create') {
      return 'Inspect vendor delivered items, record actual quantities received, and update inventory.';
    }
    return 'Inspect verified goods receipt quantities, audit logs, and status.';
  });

  // Header Form Field Configuration for Reusable Form
  readonly formFields = computed<FormFieldConfig[]>(() => {
    const isView = !this.canEditDraft();
    return [
      {
        name: 'grnNumber',
        label: 'GRN Number',
        type: 'text',
        readonly: true,
        tooltip: 'Assigned uniquely by the server',
        colSpan: 1
      },
      {
        name: 'receiptDate',
        label: 'Receipt Date',
        type: 'date',
        required: true,
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'purchaseOrderNumber',
        label: 'Purchase Order',
        type: 'text',
        readonly: true,
        tooltip: 'Source purchase order reference',
        colSpan: 1
      },
      {
        name: 'supplierName',
        label: 'Supplier / Vendor',
        type: 'text',
        readonly: true,
        tooltip: 'Vendor loaded from the source purchase order',
        colSpan: 1
      },
      {
        name: 'deliveryNoteNumber',
        label: 'Delivery Note Ref',
        type: 'text',
        placeholder: 'e.g. DN-98412',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'remarks',
        label: 'Receipt Notes',
        type: 'textarea',
        placeholder: 'Enter dock inspection comments, carrier tracking, or package condition...',
        rows: 2,
        disabled: isView,
        colSpan: 2
      }
    ];
  });

  // Columns for the Received Items Table
  readonly itemColumns = computed<DataTableColumn<EditableGRNLine>[]>(() => {
    const isView = !this.canEditDraft();
    return [
      {
        field: 'productCode',
        header: 'Item Code',
        sortable: true,
        width: '140px',
        editable: false
      },
      {
        field: 'productName',
        header: 'Item Description',
        sortable: true,
        width: '220px',
        editable: false
      },
      {
        field: 'unitOfMeasure',
        header: 'UOM',
        width: '90px',
        editable: false
      },
      {
        field: 'orderedQuantity',
        header: 'PO Ordered',
        sortable: true,
        align: 'right',
        width: '110px',
        editable: false
      },
      {
        field: 'previouslyReceivedQuantity',
        header: 'Prev. Received',
        sortable: true,
        align: 'right',
        width: '120px',
        editable: false
      },
      {
        field: 'outstandingQuantity',
        header: 'Outstanding',
        sortable: true,
        align: 'right',
        width: '120px',
        editable: false
      },
      {
        field: 'receivedQuantityNow',
        header: 'Received Now',
        sortable: true,
        align: 'right',
        width: '130px',
        editable: !isView && !this.isFullyReceived(),
        editType: 'number',
        validators: [
          (val: any, row?: EditableGRNLine) => {
            const num = Number(val);
            if (isNaN(num)) return 'Must be a number';
            if (num < 0) return 'Cannot be negative';
            if (row && num > row.outstandingQuantity) {
              return `Cannot exceed outstanding (${row.outstandingQuantity})`;
            }
            return null;
          }
        ]
      },
      {
        field: 'trackingMode',
        header: 'Tracking Details',
        width: '180px',
        align: 'center',
        editable: false,
        cellTemplate: this.trackingCellTemplate
      },
      {
        field: 'unitCost',
        header: 'Unit Price',
        sortable: true,
        align: 'right',
        width: '120px',
        type: 'currency',
        editable: false // STRICTLY READ-ONLY!
      },
      {
        field: 'lineTotal',
        header: 'Line Total',
        sortable: true,
        align: 'right',
        width: '130px',
        type: 'currency',
        editable: false // STRICTLY READ-ONLY!
      }
    ];
  });

  // Row Calculation Callback
  readonly calculateRow = (row: EditableGRNLine): void => {
    const qty = Number(row.receivedQuantityNow) || 0;
    const cost = Number(row.unitCost) || 0;
    row.lineTotal = row.netLineAmount !== undefined && row.orderedQuantity > 0
      ? Math.round(row.netLineAmount * qty / row.orderedQuantity * 100) / 100
      : Math.round(qty * cost * (1 - (Number(row.discountRate) || 0) / 100) * 100) / 100;
    this.workingLines.set([...this.workingLines()]);
  };

  // Reactive Totals
  readonly totalReceivedQuantity = computed<number>(() => {
    return this.workingLines().reduce((sum, item) => sum + (Number(item.receivedQuantityNow) || 0), 0);
  });

  readonly totalReceiptAmount = computed<number>(() => {
    return this.workingLines().reduce((sum, item) => sum + (Number(item.lineTotal) || 0), 0);
  });

  // Status computation for View Mode
  readonly grnStatusName = computed<string>(() => {
    const grn = this.currentGrn();
    if (!grn) return 'Draft';
    return getGrnStatusName(grn.status);
  });

  readonly grnStatusSeverity = computed<'success' | 'warning' | 'danger' | 'info' | 'neutral'>(() => {
    const grn = this.currentGrn();
    if (!grn) return 'info';
    return getGrnStatusSeverity(grn.status);
  });

  // Operating Scope Description
  readonly currentOperatingScopeText = computed<string>(() => {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company) return 'No company selected';
    if (!branch) return `${company.name} / All Permitted Branches`;
    return `${company.name} / ${branch.name}`;
  });

  ngOnInit(): void {
    this.detectRoute();

    this.subs.add(
      this.route.paramMap.subscribe(() => {
        this.detectRoute();
      })
    );

    // Prefetch product master data for catalogue enrichment
    this.subs.add(
      this.masterDataService.getProducts().subscribe({
        next: prods => {
          for (const p of prods) {
            if (p.productId) {
              this.productMasterCache.set(p.productId.toLowerCase(), p);
            }
          }
          if (this.workingLines().length > 0) {
            this.enrichWorkingLines(prods);
          }
        },
        error: () => {}
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  private detectRoute(): void {
    const url = this.router.url;
    const id = this.route.snapshot.paramMap.get('id');
    const poParam = this.route.snapshot.queryParamMap.get('purchaseOrderId');

    if (id) {
      this.mode.set('view');
      this.grnId.set(id);
      this.loadSavedGrn(id);
    } else if (url.includes('/goods-receipts/new') || poParam) {
      this.mode.set('create');
      this.purchaseOrderId.set(poParam);
      if (poParam) {
        this.loadSourcePurchaseOrder(poParam);
      } else {
        this.poIneligibleError.set('No Purchase Order specified. Goods receipts must be initiated from an approved Purchase Order.');
      }
    }
  }

  // Load source PO and validate receipt eligibility
  loadSourcePurchaseOrder(poId: string): void {
    const company = this.scopeService.selectedCompany();
    let branch = this.scopeService.selectedBranch();
    if (!company) {
      this.poIneligibleError.set('Operating company and branch must be selected.');
      return;
    }

    if (!branch) {
      const queryBranchId = this.route.snapshot?.queryParamMap?.get('branchId') ||
        (this.route.snapshot?.queryParams as any)?.['branchId'];
      const branches = this.scopeService.branches();
      let targetBranch = queryBranchId ? branches.find(b => b.id === queryBranchId) : null;
      if (!targetBranch && queryBranchId) {
        targetBranch = { id: queryBranchId, name: 'Branch', companyId: company.id } as Branch;
      } else if (!targetBranch && branches.length === 1) {
        targetBranch = branches[0];
      }

      if (targetBranch) {
        this.scopeService.selectBranch(targetBranch);
        branch = targetBranch;
      }
    }

    if (!branch) {
      this.poIneligibleError.set('Operating company and branch must be selected.');
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.poIneligibleError.set(null);
    this.isAccessDenied.set(false);
    this.enrichmentToken++;

    this.poService.getOrder(company.id, branch.id, poId).subscribe({
      next: po => {
        this.loading.set(false);
        if (!po) {
          this.poIneligibleError.set('Source Purchase Order could not be found in the current operating scope.');
          return;
        }

        this.sourcePo.set(po);
        this.validateAndPopulateFromPo(po);
      },
      error: err => {
        this.loading.set(false);
        if (this.isForbiddenError(err)) {
          this.isAccessDenied.set(true);
        } else {
          this.poIneligibleError.set(this.getCleanErrorMessage(err, 'Failed to load source purchase order.'));
        }
      }
    });
  }

  // Validate PO status and populate receivable lines
  private validateAndPopulateFromPo(po: PurchaseOrderDto): void {
    const st = Number(po.status);
    const isApproved = st === PurchaseOrderStatus.Approved || String(po.status).toLowerCase() === 'approved';

    if (!isApproved) {
      this.poIneligibleError.set(
        `Purchase Order ${po.orderNumber} is in status '${po.status}'. Only Approved Purchase Orders can be received.`
      );
      return;
    }

    // Prefill Header Form
    this.headerForm.patchValue({
      grnNumber: 'Auto-generated on save',
      receiptDate: new Date().toISOString().substring(0, 10),
      purchaseOrderNumber: po.orderNumber,
      supplierName: po.supplierName || 'Standard Vendor',
      deliveryNoteNumber: '',
      remarks: ''
    });

    const activeItems = (po.items || []).filter(i => i.isActive !== false);
    if (activeItems.length === 0) {
      this.poIneligibleError.set(`Purchase Order ${po.orderNumber} does not have any items.`);
      return;
    }

    const allFullyReceived = activeItems.every(item => {
      const ord = Number(item.quantity) || 0;
      const prevRec = Number(item.receivedQuantity) || 0;
      return Math.max(0, ord - prevRec) <= 0;
    });

    const lines: EditableGRNLine[] = activeItems.map((item, index) => {
      const ordered = Number(item.quantity) || 0;
      const prevReceived = Number(item.receivedQuantity) || 0;
      const outstanding = Math.max(0, ordered - prevReceived);
      const receivedNow = allFullyReceived ? 0 : outstanding;

      // Deterministic unique line identity per PO line
      const lineId = item.id ? item.id : `po-line-${po.id || 'order'}-${index}`;
      const purchaseOrderItemId = item.id || lineId;

      const cachedProd = item.productId ? this.productMasterCache.get(item.productId.toLowerCase()) : undefined;
      const isGenericCode = !item.productCode || item.productCode === 'ITEM';
      const isGenericName = !item.productName || item.productName === 'Material Item' || item.productName === 'Material';

      const productCode = !isGenericCode
        ? item.productCode!
        : (cachedProd?.productCode || item.productId || 'ITEM');

      const productName = !isGenericName
        ? item.productName!
        : (cachedProd?.productName || (cachedProd?.productCode ? cachedProd.productName : (item.productId ? `Product (${item.productId})` : 'Product')));

      const unitOfMeasure = item.unitOfMeasureCode || cachedProd?.unitOfMeasure || '';
      const unitOfMeasureId = item.unitOfMeasureId ?? cachedProd?.unitOfMeasureId ?? null;

      const unitCost = Number(item.unitPrice) || 0;
      const discountRate = Number(item.discountRate) || 0;
      const netLineAmount =
        Number(item.subTotal ?? (ordered * unitCost)) - Number(item.discountAmount || 0);
      const lineTotal = Math.round(
        (netLineAmount * receivedNow / (ordered || 1)) * 100
      ) / 100;

      return {
        lineId,
        purchaseOrderItemId,
        productId: item.productId,
        productCode,
        productName,
        unitOfMeasure,
        unitOfMeasureId,
        orderedQuantity: ordered,
        previouslyReceivedQuantity: prevReceived,
        outstandingQuantity: outstanding,
        receivedQuantityNow: receivedNow, // 0 if fully received, otherwise full outstanding
        unitCost, // Server-copied unit price, strictly read-only
        discountRate,
        netLineAmount,
        lineTotal
      };
    });

    // Fully received POs retain all lines for historical reference in read-only mode
    this.workingLines.set(lines);
    this.loadProductTrackingModes(lines, po.id, null);
  }

  // Load Product Master to configure authoritative Tracking Modes and reconcile product details
  private loadProductTrackingModes(lines: EditableGRNLine[], targetPoId?: string | null, targetGrnId?: string | null): void {
    if (!lines || lines.length === 0) return;

    const token = ++this.enrichmentToken;
    const currentMode = this.mode();
    const expectedPoId = targetPoId ?? this.purchaseOrderId();
    const expectedGrnId = targetGrnId ?? this.grnId();

    this.masterDataService.getProducts().subscribe({
      next: products => {
        // Prevent stale asynchronous responses from overwriting a newer PO or receipt
        if (token !== this.enrichmentToken) return;
        if (currentMode === 'create' && this.purchaseOrderId() !== expectedPoId) return;
        if (currentMode === 'view' && this.grnId() !== expectedGrnId) return;

        for (const p of products) {
          if (p.productId) {
            this.productMasterCache.set(p.productId.toLowerCase(), p);
          }
        }

        this.enrichWorkingLines(products);
      },
      error: () => {
        // Fallback: If bulk product load encounters issue, lines preserve current values
      }
    });
  }

  private enrichWorkingLines(products: ProductItem[]): void {
    if (!products || products.length === 0) return;
    const prodMap = new Map<string, ProductItem>();
    for (const p of products) {
      if (p.productId) {
        prodMap.set(p.productId.toLowerCase(), p);
      }
    }

    this.workingLines.update(currLines =>
      currLines.map(l => {
        const prod = l.productId ? prodMap.get(l.productId.toLowerCase()) : undefined;
        const mode = prod ? Number(prod.trackingMode) || TrackingMode.None : (l.trackingMode ?? TrackingMode.None);

        const isGenericCode = !l.productCode || l.productCode === 'ITEM' || (l.productId && l.productCode.toLowerCase() === l.productId.toLowerCase());
        const isGenericName = !l.productName || l.productName === 'Material Item' || l.productName === 'Material' || l.productName.startsWith('Product (');

        const enrichedCode = isGenericCode
          ? (prod?.productCode || l.productCode || l.productId)
          : l.productCode;

        const enrichedName = isGenericName
          ? (prod?.productName || (prod?.productCode ? prod.productName : (l.productId ? `Product (${l.productId})` : l.productName)))
          : l.productName;

        const enrichedUom = (!l.unitOfMeasure && prod?.unitOfMeasure)
          ? prod.unitOfMeasure
          : (l.unitOfMeasure || '');

        const enrichedUomId = l.unitOfMeasureId ?? prod?.unitOfMeasureId ?? null;

        return {
          ...l,
          productCode: enrichedCode,
          productName: enrichedName,
          unitOfMeasure: enrichedUom,
          unitOfMeasureId: enrichedUomId,
          trackingMode: mode
        };
      })
    );
  }

  // Tracking Helpers & Validation
  isLineTrackingComplete(row: EditableGRNLine): boolean {
    const mode = Number(row.trackingMode) || TrackingMode.None;
    if (mode === TrackingMode.None) return true;

    const receivedQty = Number(row.receivedQuantityNow) || 0;
    if (receivedQty <= 0) return true;

    const allocs = row.trackingAllocations || [];
    if (allocs.length === 0) return false;

    const totalAlloc = allocs.reduce((sum, a) => sum + (Number(a.quantity) || 0), 0);
    if (Math.abs(totalAlloc - receivedQty) > 0.0001) return false;

    for (const a of allocs) {
      if ((mode === TrackingMode.Batch || mode === TrackingMode.BatchAndSerial) && (!a.batchNumber || !a.batchNumber.trim())) {
        return false;
      }
      if ((mode === TrackingMode.Serial || mode === TrackingMode.BatchAndSerial) && (!a.serialNumber || !a.serialNumber.trim())) {
        return false;
      }
    }
    return true;
  }

  getLineAllocatedQuantity(row: EditableGRNLine): number {
    return (row.trackingAllocations || []).reduce((sum, a) => sum + (Number(a.quantity) || 0), 0);
  }

  getLineTrackingSummary(row: EditableGRNLine): string {
    const mode = Number(row.trackingMode) || TrackingMode.None;
    if (mode === TrackingMode.None) return 'Untracked';

    const allocs = row.trackingAllocations || [];
    const allocated = allocs.reduce((sum, a) => sum + (Number(a.quantity) || 0), 0);
    const target = Number(row.receivedQuantityNow) || 0;

    if (allocated === target && target > 0) {
      return mode === TrackingMode.Batch
        ? `Batches (${allocs.length}) ✓`
        : `Serials (${allocs.length}) ✓`;
    }
    return `Configure (${allocated}/${target})`;
  }

  openTrackingDialog(row: EditableGRNLine, event?: Event): void {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }

    const mode = Number(row.trackingMode) || TrackingMode.None;
    if (mode === TrackingMode.None) {
      this.showMessage(`Product '${row.productCode}' does not track batches or serial numbers.`, 'info');
      return;
    }

    const targetQty = Number(row.receivedQuantityNow) || 0;
    if (targetQty <= 0) {
      this.showMessage(`Specify a received quantity greater than 0 before allocating batch or serial details.`, 'warn');
      return;
    }

    const dialogRef = this.matDialog.open<BatchSerialTrackingDialogComponent, BatchSerialDialogData, BatchSerialAllocation[]>(
      BatchSerialTrackingDialogComponent,
      {
        width: '920px',
        maxWidth: '96vw',
        disableClose: true,
        data: {
          productCode: row.productCode,
          productName: row.productName,
          unitOfMeasure: row.unitOfMeasure,
          trackingMode: mode,
          targetQuantity: targetQty,
          readOnly: !this.canEditDraft(),
          allocations: row.trackingAllocations || []
        }
      }
    );

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        row.trackingAllocations = result;
        this.workingLines.set([...this.workingLines()]);
        this.showMessage(`Tracking details recorded for ${row.productCode}.`, 'success');
      }
    });
  }

  // Load Saved GRN for Reopening / View Mode
  loadSavedGrn(id: string): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.isAccessDenied.set(false);

    this.grnService.getGrn(company.id, branch.id, id).subscribe({
      next: grn => {
        this.loading.set(false);
        if (!grn) {
          this.error.set('Goods Receipt Note not found in the current operating scope.');
          return;
        }

        this.currentGrn.set(grn);
        this.populateFromSavedGrn(grn);
        this.loadPurchaseOrderForDraft(grn);
      },
      error: err => {
        this.loading.set(false);
        if (this.isForbiddenError(err)) {
          this.isAccessDenied.set(true);
        } else {
          this.error.set(this.getCleanErrorMessage(err, 'Failed to load goods receipt note.'));
        }
      }
    });
  }

  private populateFromSavedGrn(grn: GoodsReceiptNoteDto): void {
    this.headerForm.patchValue({
      grnNumber: grn.grnNumber,
      receiptDate: grn.receiptDate ? grn.receiptDate.substring(0, 10) : '',
      purchaseOrderNumber: 'PO Reference',
      supplierName: grn.supplierName || 'Vendor',
      deliveryNoteNumber: grn.deliveryNoteNumber || '',
      remarks: grn.remarks || ''
    });

    const groupedMap = new Map<string, { headerLine: EditableGRNLine; allocations: BatchSerialAllocation[] }>();

    for (const l of grn.lines || []) {
      const alloc: BatchSerialAllocation = {
        id: l.goodsReceiptNoteLineId || `alloc-${l.purchaseOrderItemId || 'line'}-${groupedMap.size}`,
        batchNumber: l.batchNumber || '',
        serialNumber: l.serialNumber || '',
        quantity: Number(l.quantityReceived) || 1,
        manufacturingDate: l.manufacturingDate ? l.manufacturingDate.substring(0, 10) : '',
        expiryDate: l.expiryDate ? l.expiryDate.substring(0, 10) : '',
        isQuarantine: l.isQuarantine || false,
        remarks: l.remarks || ''
      };

      const existing = groupedMap.get(l.purchaseOrderItemId);
      if (!existing) {
        const cachedProd = l.productId ? this.productMasterCache.get(l.productId.toLowerCase()) : undefined;
        const isGenericCode = !l.productCode || l.productCode === 'ITEM';
        const isGenericName = !l.productName || l.productName === 'Material' || l.productName === 'Material Item';

        const productCode = !isGenericCode
          ? l.productCode!
          : (cachedProd?.productCode || l.productId || 'ITEM');

        const productName = !isGenericName
          ? l.productName!
          : (cachedProd?.productName || (cachedProd?.productCode ? cachedProd.productName : (l.productId ? `Product (${l.productId})` : 'Material')));

        const unitOfMeasure = l.unitOfMeasure || cachedProd?.unitOfMeasure || '';
        const unitOfMeasureId = l.unitOfMeasureId ?? cachedProd?.unitOfMeasureId ?? null;

        const editableLine: EditableGRNLine = {
          lineId: l.goodsReceiptNoteLineId || `line-${l.purchaseOrderItemId || 'saved'}-${groupedMap.size}`,
          purchaseOrderItemId: l.purchaseOrderItemId,
          productId: l.productId,
          productCode,
          productName,
          unitOfMeasure,
          orderedQuantity: Number(l.quantityReceived) || 0,
          previouslyReceivedQuantity: 0,
          outstandingQuantity: Number(l.quantityReceived) || 0,
          receivedQuantityNow: Number(l.quantityReceived) || 0,
          unitCost: l.unitCost, // Strictly read-only
          discountRate: 0,
          netLineAmount: undefined,
          lineTotal: l.lineTotal,
          warehouseId: l.warehouseId,
          warehouseLocationId: l.warehouseLocationId,
          unitOfMeasureId,
          remarks: l.remarks ?? undefined,
          trackingAllocations: (l.batchNumber || l.serialNumber) ? [alloc] : []
        };
        groupedMap.set(l.purchaseOrderItemId, {
          headerLine: editableLine,
          allocations: (l.batchNumber || l.serialNumber) ? [alloc] : []
        });
      } else {
        existing.headerLine.receivedQuantityNow += Number(l.quantityReceived) || 0;
        existing.headerLine.lineTotal += l.lineTotal;
        existing.headerLine.orderedQuantity += Number(l.quantityReceived) || 0;
        existing.headerLine.outstandingQuantity += Number(l.quantityReceived) || 0;
        if (l.batchNumber || l.serialNumber) {
          existing.allocations.push(alloc);
          existing.headerLine.trackingAllocations = existing.allocations;
        }
      }
    }

    const lines = Array.from(groupedMap.values()).map(g => g.headerLine);
    this.workingLines.set(lines);
    this.loadProductTrackingModes(lines, null, grn.goodsReceiptNoteId);
  }

  private loadPurchaseOrderForDraft(grn: GoodsReceiptNoteDto): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) return;

    this.poService.getOrder(company.id, branch.id, grn.purchaseOrderId).subscribe({
      next: po => {
        if (!po) return;
        this.sourcePo.set(po);
        const poItems = new Map((po.items || []).map(item => [item.id, item]));
        this.workingLines.update(lines => lines.map(line => {
          const item = poItems.get(line.purchaseOrderItemId);
          if (!item) return line;
          const ordered = Number(item.quantity) || 0;
          const previouslyReceived = Number(item.receivedQuantity) || 0;
          const usesPurchaseOrderUom =
            !line.unitOfMeasureId || line.unitOfMeasureId === item.unitOfMeasureId;
          return {
            ...line,
            orderedQuantity: ordered,
            previouslyReceivedQuantity: previouslyReceived,
            outstandingQuantity: Math.max(0, ordered - previouslyReceived),
            unitOfMeasure: line.unitOfMeasure || item.unitOfMeasureCode || '',
            unitCost: usesPurchaseOrderUom ? Number(item.unitPrice) || line.unitCost : line.unitCost,
            discountRate: usesPurchaseOrderUom ? Number(item.discountRate) || 0 : 0,
            netLineAmount: usesPurchaseOrderUom
              ? Number(item.subTotal ?? (Number(item.quantity) * Number(item.unitPrice))) - Number(item.discountAmount || 0)
              : undefined,
            unitOfMeasureId: line.unitOfMeasureId ?? item.unitOfMeasureId ?? null
          };
        }));
      },
      error: err => {
        this.showMessage(
          this.getCleanErrorMessage(err, 'Could not load the source PO. Draft changes remain available, but PO validations require server confirmation.'),
          'warn'
        );
      }
    });
  }

  editCurrentDraft(): void {
    if (this.isDraft()) {
      this.confirmDeleteDraft.set(false);
      this.editingDraft.set(true);
    }
  }

  discardDraftEdits(): void {
    const grn = this.currentGrn();
    if (!grn) return;
    this.populateFromSavedGrn(grn);
    this.editingDraft.set(false);
  }

  onDraftLinesChange(lines: EditableGRNLine[]): void {
    this.workingLines.set(lines);
  }

  // Save GRN Draft or Confirm
  saveReceipt(andConfirm: boolean = false): void {
    if (!this.scopeService.canOperateTransactions()) {
      this.showMessage('Please select an operating company and branch first.', 'warn');
      return;
    }

    if (this.isFullyReceived()) {
      this.showMessage(
        `Cannot create a receipt. All items on Purchase Order ${this.sourcePo()?.orderNumber} have already been fully received.`,
        'danger'
      );
      return;
    }

    if (this.poIneligibleError()) {
      this.showMessage(this.poIneligibleError()!, 'danger');
      return;
    }

    const company = this.scopeService.selectedCompany()!;
    const branch = this.scopeService.selectedBranch()!;
    const po = this.sourcePo();
    const currentGrn = this.currentGrn();

    if (!po && this.mode() === 'create') {
      this.showMessage('Cannot save receipt without valid source Purchase Order.', 'danger');
      return;
    }

    // Filter lines that have actual received quantity > 0
    const linesToReceive = this.workingLines().filter(l => Number(l.receivedQuantityNow) > 0);
    if (linesToReceive.length === 0 && this.mode() === 'create') {
      this.showMessage('At least one item line must have a received quantity greater than zero.', 'danger');
      return;
    }

    // Zero-tolerance over-receipt validation
    for (const line of linesToReceive) {
      if (line.receivedQuantityNow > line.outstandingQuantity) {
        this.showMessage(
          `Over-receipt rejected for ${line.productCode}. Attempted: ${line.receivedQuantityNow}, Outstanding: ${line.outstandingQuantity}.`,
          'danger'
        );
        return;
      }
    }

    // Product Master-driven tracking completeness validation
    for (const line of linesToReceive) {
      const mode = Number(line.trackingMode) || TrackingMode.None;
      if (mode !== TrackingMode.None) {
        if (!this.isLineTrackingComplete(line)) {
          this.showMessage(
            `Tracking details are incomplete for line '${line.productCode}'. Received quantity is ${line.receivedQuantityNow}, but allocated tracking quantity is ${this.getLineAllocatedQuantity(line)}. Click 'Configure' in the Tracking Details column.`,
            'danger'
          );
          return;
        }
      }
    }

    const formVal = this.headerForm.getRawValue();

    // Map request DTO - expands tracking allocations into individual line items for server
    const requestLines: CreateGoodsReceiptNoteLineRequest[] = [];
    for (const l of linesToReceive) {
      const mode = Number(l.trackingMode) || TrackingMode.None;
      if (mode !== TrackingMode.None && l.trackingAllocations && l.trackingAllocations.length > 0) {
        for (const alloc of l.trackingAllocations) {
          requestLines.push({
            purchaseOrderItemId: l.purchaseOrderItemId,
            productId: l.productId,
            warehouseId: l.warehouseId,
            warehouseLocationId: l.warehouseLocationId,
            quantityReceived: Number(alloc.quantity),
            unitOfMeasureId: l.unitOfMeasureId ?? null,
            batchNumber: alloc.batchNumber?.trim() || null,
            serialNumber: alloc.serialNumber?.trim() || null,
            manufacturingDate: alloc.manufacturingDate ? new Date(alloc.manufacturingDate).toISOString() : null,
            expiryDate: alloc.expiryDate ? new Date(alloc.expiryDate).toISOString() : null,
            isQuarantine: alloc.isQuarantine || false,
            remarks: alloc.remarks || l.remarks || null
          });
        }
      } else {
        requestLines.push({
          purchaseOrderItemId: l.purchaseOrderItemId,
          productId: l.productId,
          warehouseId: l.warehouseId,
          warehouseLocationId: l.warehouseLocationId,
          quantityReceived: Number(l.receivedQuantityNow),
          unitOfMeasureId: l.unitOfMeasureId ?? null,
          batchNumber: null,
          serialNumber: null,
          manufacturingDate: null,
          expiryDate: null,
          isQuarantine: false,
          remarks: l.remarks || null
        });
      }
    }

    const receiptDate = formVal.receiptDate
      ? new Date(formVal.receiptDate).toISOString()
      : new Date().toISOString();

    if (this.mode() === 'view' && currentGrn && this.editingDraft()) {
      const request: UpdateGoodsReceiptNoteRequest = {
        receiptDate,
        deliveryNoteNumber: formVal.deliveryNoteNumber || null,
        remarks: formVal.remarks || null,
        lines: requestLines
      };

      this.actionInProgress.set(true);
      this.grnService.updateDraft(company.id, branch.id, currentGrn.goodsReceiptNoteId, request).subscribe({
        next: updatedGrn => {
          this.currentGrn.set(updatedGrn);
          this.populateFromSavedGrn(updatedGrn);
          if (andConfirm) {
            this.grnService.confirmGrn(company.id, branch.id, updatedGrn.goodsReceiptNoteId).subscribe({
              next: confirmedGrn => {
                this.actionInProgress.set(false);
                this.currentGrn.set(confirmedGrn);
                this.populateFromSavedGrn(confirmedGrn);
                this.editingDraft.set(false);
                this.showMessage(`Goods Receipt ${confirmedGrn.grnNumber} confirmed and stock posted successfully.`, 'success');
              },
              error: confErr => {
                this.actionInProgress.set(false);
                this.editingDraft.set(false);
                this.showMessage(
                  `Draft saved, but confirmation failed: ${this.getCleanErrorMessage(confErr, 'Failed to confirm.')}`,
                  'warn'
                );
              }
            });
          } else {
            this.actionInProgress.set(false);
            this.editingDraft.set(false);
            this.showMessage(`Goods Receipt ${updatedGrn.grnNumber} draft updated.`, 'success');
          }
        },
        error: err => {
          this.actionInProgress.set(false);
          this.showMessage(this.getCleanErrorMessage(err, 'Failed to update Goods Receipt Note draft.'), 'danger');
        }
      });
      return;
    }

    const request: CreateGoodsReceiptNoteRequest = {
      purchaseOrderId: po!.id,
      receiptDate,
      deliveryNoteNumber: formVal.deliveryNoteNumber || null,
      remarks: formVal.remarks || null,
      lines: requestLines
    };

    this.actionInProgress.set(true);

    this.grnService.createManualDraft(company.id, branch.id, request).subscribe({
      next: createdGrn => {
        if (andConfirm) {
          this.grnService.confirmGrn(company.id, branch.id, createdGrn.goodsReceiptNoteId).subscribe({
            next: confirmedGrn => {
              this.actionInProgress.set(false);
              this.showMessage(`Goods Receipt ${confirmedGrn.grnNumber} confirmed and stock posted successfully.`, 'success');
              this.router.navigate(['/app/procurement/goods-receipts', confirmedGrn.goodsReceiptNoteId]);
            },
            error: confErr => {
              this.actionInProgress.set(false);
              this.showMessage(
                `Receipt saved as Draft ${createdGrn.grnNumber}, but confirmation failed: ${this.getCleanErrorMessage(confErr, 'Failed to confirm.')}`,
                'warn'
              );
              this.router.navigate(['/app/procurement/goods-receipts', createdGrn.goodsReceiptNoteId]);
            }
          });
        } else {
          this.actionInProgress.set(false);
          this.showMessage(`Goods Receipt ${createdGrn.grnNumber} saved as Draft.`, 'success');
          this.router.navigate(['/app/procurement/goods-receipts', createdGrn.goodsReceiptNoteId]);
        }
      },
      error: err => {
        this.actionInProgress.set(false);
        this.showMessage(this.getCleanErrorMessage(err, 'Failed to save Goods Receipt Note.'), 'danger');
      }
    });
  }

  // Confirm existing Draft in View mode
  confirmCurrentDraft(): void {
    const grn = this.currentGrn();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!grn || !company || !branch) return;

    // Validate that any tracked lines have complete tracking details
    for (const line of this.workingLines()) {
      const mode = Number(line.trackingMode) || TrackingMode.None;
      if (mode !== TrackingMode.None && !this.isLineTrackingComplete(line)) {
        this.showMessage(
          `Cannot confirm receipt. Tracking details are incomplete for line '${line.productCode}'. Click 'Edit Draft' to configure batch/serial tracking.`,
          'danger'
        );
        return;
      }
    }

    this.actionInProgress.set(true);
    this.grnService.confirmGrn(company.id, branch.id, grn.goodsReceiptNoteId).subscribe({
      next: updated => {
        this.actionInProgress.set(false);
        this.currentGrn.set(updated);
        this.showMessage(`Goods Receipt ${grn.grnNumber} confirmed and inventory updated.`, 'success');
      },
      error: err => {
        this.actionInProgress.set(false);
        this.showMessage(this.getCleanErrorMessage(err, 'Failed to confirm goods receipt.'), 'danger');
      }
    });
  }

  // Cancel existing Draft in View mode
  cancelCurrentDraft(): void {
    const grn = this.currentGrn();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!grn || !company || !branch) return;

    this.actionInProgress.set(true);
    this.grnService.cancelGrn(company.id, branch.id, grn.goodsReceiptNoteId).subscribe({
      next: updated => {
        this.actionInProgress.set(false);
        this.currentGrn.set(updated);
        this.showMessage(`Goods Receipt ${grn.grnNumber} cancelled.`, 'success');
      },
      error: err => {
        this.actionInProgress.set(false);
        this.showMessage(this.getCleanErrorMessage(err, 'Failed to cancel goods receipt.'), 'danger');
      }
    });
  }

  deleteCurrentDraft(): void {
    const grn = this.currentGrn();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!grn || !company || !branch || !this.isDraft()) return;

    this.actionInProgress.set(true);
    this.grnService.deleteDraft(company.id, branch.id, grn.goodsReceiptNoteId).subscribe({
      next: () => {
        this.actionInProgress.set(false);
        this.showMessage(`Draft ${grn.grnNumber} deleted.`, 'success');
        this.router.navigate(['/app/procurement/goods-receipts']);
      },
      error: err => {
        this.actionInProgress.set(false);
        this.showMessage(this.getCleanErrorMessage(err, 'Failed to delete goods receipt draft.'), 'danger');
      }
    });
  }

  canPutaway(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin()) {
      return true;
    }
    return this.authService.hasPermission('PUTAWAY.POST');
  }

  createPutawayFromGrn(): void {
    const grn = this.currentGrn();
    if (!grn) return;
    this.router.navigate(['/app/inventory/store-putaway/new'], {
      queryParams: {
        sourceType: 'GoodsReceipt',
        sourceId: grn.goodsReceiptNoteId
      }
    });
  }

  navigateBack(): void {
    if (this.sourcePo()) {
      this.router.navigate(['/app/procurement/purchase-orders', this.sourcePo()!.id]);
    } else {
      this.router.navigate(['/app/procurement/goods-receipts']);
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
