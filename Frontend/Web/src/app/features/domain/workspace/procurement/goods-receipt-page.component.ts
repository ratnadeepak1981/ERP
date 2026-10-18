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
import { AccessDeniedComponent } from '../../../../shared/components/access-denied/access-denied.component';
import { GoodsReceiptNoteService } from '../../../../core/services/goods-receipt-note.service';
import { PurchaseOrderService } from '../../../../core/services/purchase-order.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import {
  GoodsReceiptNoteDto,
  GoodsReceiptNoteStatus,
  CreateGoodsReceiptNoteRequest,
  getGrnStatusName,
  getGrnStatusSeverity
} from '../../../../core/models/goods-receipt-note.models';
import { PurchaseOrderDto, PurchaseOrderStatus } from '../../../../core/models/purchase-order.models';
import { DataTableColumn } from '../../../../shared/models/data-table-config.model';

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
  lineTotal: number; // Strictly read-only
  warehouseId?: string;
  warehouseLocationId?: string;
  remarks?: string;
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
    AccessDeniedComponent
  ],
  templateUrl: './goods-receipt-page.component.html',
  styleUrl: './goods-receipt-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GoodsReceiptPageComponent implements OnInit, OnDestroy {
  readonly grnService = inject(GoodsReceiptNoteService);
  readonly poService = inject(PurchaseOrderService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);
  readonly route = inject(ActivatedRoute);

  private readonly subs = new Subscription();

  // Mode & Route State
  readonly mode = signal<GRNPageMode>('create');
  readonly grnId = signal<string | null>(null);
  readonly purchaseOrderId = signal<string | null>(null);
  readonly currentGrn = signal<GoodsReceiptNoteDto | null>(null);
  readonly sourcePo = signal<PurchaseOrderDto | null>(null);

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
    const isView = this.mode() === 'view';
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
    const isView = this.mode() === 'view';
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
        editable: !isView,
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
    row.lineTotal = Math.round(qty * cost * 100) / 100;
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
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) {
      this.poIneligibleError.set('Operating company and branch must be selected.');
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.poIneligibleError.set(null);
    this.isAccessDenied.set(false);

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

    const lines: EditableGRNLine[] = activeItems.map(item => {
      const ordered = Number(item.quantity) || 0;
      const prevReceived = Number(item.receivedQuantity) || 0;
      const outstanding = Math.max(0, ordered - prevReceived);

      return {
        lineId: item.id || 'line-' + Math.random().toString(36).substring(2, 9),
        purchaseOrderItemId: item.id || '',
        productId: item.productId,
        productCode: item.productCode || 'ITEM',
        productName: item.productName || 'Material Item',
        unitOfMeasure: 'PCS',
        orderedQuantity: ordered,
        previouslyReceivedQuantity: prevReceived,
        outstandingQuantity: outstanding,
        receivedQuantityNow: outstanding, // Defaults to receiving full outstanding quantity
        unitCost: Number(item.unitPrice) || 0, // Server-copied unit price, strictly read-only
        lineTotal: Math.round(outstanding * (Number(item.unitPrice) || 0) * 100) / 100
      };
    });

    const receivable = lines.filter(l => l.outstandingQuantity > 0);
    if (receivable.length === 0) {
      this.poIneligibleError.set(`All items on Purchase Order ${po.orderNumber} have already been fully received.`);
    }

    this.workingLines.set(lines);
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

    const lines: EditableGRNLine[] = (grn.lines || []).map(l => ({
      lineId: l.goodsReceiptNoteLineId || 'line-' + Math.random().toString(36).substring(2, 9),
      purchaseOrderItemId: l.purchaseOrderItemId,
      productId: l.productId,
      productCode: l.productCode || 'ITEM',
      productName: l.productName || 'Material',
      unitOfMeasure: l.unitOfMeasure || 'PCS',
      orderedQuantity: l.quantityReceived,
      previouslyReceivedQuantity: 0,
      outstandingQuantity: 0,
      receivedQuantityNow: l.quantityReceived,
      unitCost: l.unitCost, // Strictly read-only
      lineTotal: l.lineTotal
    }));

    this.workingLines.set(lines);
  }

  // Save GRN Draft or Confirm
  saveReceipt(andConfirm: boolean = false): void {
    if (!this.scopeService.canOperateTransactions()) {
      this.showMessage('Please select an operating company and branch first.', 'warn');
      return;
    }

    if (this.poIneligibleError()) {
      this.showMessage(this.poIneligibleError()!, 'danger');
      return;
    }

    const company = this.scopeService.selectedCompany()!;
    const branch = this.scopeService.selectedBranch()!;
    const po = this.sourcePo();

    if (!po && this.mode() === 'create') {
      this.showMessage('Cannot save receipt without valid source Purchase Order.', 'danger');
      return;
    }

    // Filter lines that have actual received quantity > 0
    const linesToReceive = this.workingLines().filter(l => Number(l.receivedQuantityNow) > 0);
    if (linesToReceive.length === 0) {
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

    const formVal = this.headerForm.getRawValue();

    // Map request DTO - note: prices are server-authoritative and not client supplied
    const request: CreateGoodsReceiptNoteRequest = {
      purchaseOrderId: po!.id,
      receiptDate: formVal.receiptDate ? new Date(formVal.receiptDate).toISOString() : new Date().toISOString(),
      deliveryNoteNumber: formVal.deliveryNoteNumber || null,
      remarks: formVal.remarks || null,
      lines: linesToReceive.map(l => ({
        purchaseOrderItemId: l.purchaseOrderItemId,
        productId: l.productId,
        quantityReceived: Number(l.receivedQuantityNow),
        remarks: l.remarks || null
      }))
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
