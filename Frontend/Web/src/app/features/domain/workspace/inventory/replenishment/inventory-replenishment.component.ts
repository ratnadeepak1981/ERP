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
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { CheckboxModule } from 'primeng/checkbox';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppEmptyStateComponent } from '../../../../../shared/components/empty-state/empty-state.component';
import { AccessDeniedComponent } from '../../../../../shared/components/access-denied/access-denied.component';
import { InventoryService } from '../../../../../core/services/inventory.service';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import {
  ReplenishmentAssessmentRequest,
  ReplenishmentAssessmentResult,
  ReplenishmentLineAssessmentDto,
  CreateReplenishmentPrRequest,
  DraftPurchaseRequisitionResult,
  ReplenishmentSourceType
} from '../../../../../core/models/inventory.models';
import { WarehouseItem } from '../../../../../core/models/scope.models';

export interface ReplenishmentLineRow extends ReplenishmentLineAssessmentDto {
  selected: boolean;
  orderQuantity: number;
  lineTotal: number;
  hasValidationError?: boolean;
  validationMessage?: string;
}

export interface ReplenishmentErrorInfo {
  category: 'Validation' | 'Authorization' | 'Network' | 'Conflict' | 'Server';
  title: string;
  message: string;
  suggestedAction: string;
  canRetry: boolean;
  affectedItem?: string;
}

@Component({
  selector: 'app-inventory-replenishment',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterModule,
    ButtonModule,
    InputTextModule,
    DialogModule,
    CheckboxModule,
    AppPageHeaderComponent,
    AppEmptyStateComponent,
    AccessDeniedComponent
  ],
  templateUrl: './inventory-replenishment.component.html',
  styleUrl: './inventory-replenishment.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class InventoryReplenishmentComponent implements OnInit, OnDestroy {
  readonly inventoryService = inject(InventoryService);
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly subs = new Subscription();

  // State Signals
  readonly isAssessing = signal<boolean>(false);
  readonly isSubmitting = signal<boolean>(false);
  readonly error = signal<ReplenishmentErrorInfo | null>(null);
  readonly warehouses = signal<WarehouseItem[]>([]);
  readonly selectedWarehouseId = signal<string>('');
  readonly assessmentResult = signal<ReplenishmentAssessmentResult | null>(null);
  readonly lineRows = signal<ReplenishmentLineRow[]>([]);
  readonly createdRequisition = signal<DraftPurchaseRequisitionResult | null>(null);
  readonly confirmDialogVisible = signal<boolean>(false);

  // Durable Idempotency Tracking: preserved across retries
  readonly currentIdempotencyKey = signal<string>('');

  // Confirmation Form
  readonly confirmForm = this.fb.group({
    requiredDate: [
      new Date(Date.now() + 7 * 86400000).toISOString().substring(0, 10),
      Validators.required
    ],
    notes: ['Batch Inventory Replenishment Requisition']
  });

  // Computed state
  readonly hasPermission = computed(() => {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin()) return true;
    const perms = this.authService.permissions();
    return perms.includes('INVENTORY.VIEW') || perms.includes('INVENTORY.REPLENISH.CREATE');
  });

  readonly canCreatePr = computed(() => {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin()) return true;
    const perms = this.authService.permissions();
    return perms.includes('INVENTORY.REPLENISH.CREATE');
  });

  readonly selectedLines = computed(() => this.lineRows().filter(l => l.selected));
  readonly selectedLinesCount = computed(() => this.selectedLines().length);

  readonly totalSelectedQuantity = computed(() =>
    this.selectedLines().reduce((sum, l) => sum + (l.orderQuantity || 0), 0)
  );

  readonly totalEstimatedSpend = computed(() =>
    this.selectedLines().reduce((sum, l) => sum + (l.lineTotal || 0), 0)
  );

  readonly warehouseOptions = computed(() =>
    this.warehouses().map(w => ({
      label: `${w.warehouseName} (${w.warehouseCode})`,
      value: w.warehouseId
    }))
  );

  readonly isAllSelected = computed(() => {
    const rows = this.lineRows();
    return rows.length > 0 && rows.every(r => r.selected);
  });

  ngOnInit(): void {
    this.subs.add(
      this.scopeService.scopeChange$.subscribe(() => {
        this.loadWarehouses();
      })
    );
    this.loadWarehouses();
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  loadWarehouses(): void {
    const company = this.scopeService.selectedCompany();
    if (!company?.id) return;

    this.masterDataService.getWarehouses().subscribe({
      next: list => {
        const active = (list || []).filter(w => w.isActive !== false);
        this.warehouses.set(active);
        if (active.length > 0 && !this.selectedWarehouseId()) {
          this.selectedWarehouseId.set(active[0].warehouseId);
        }
      },
      error: err => {
        this.setError('Network', 'Failed to load warehouses', err?.message || 'Server error', 'Verify network connection and retry.', true);
      }
    });
  }

  onWarehouseChange(whId: string): void {
    this.selectedWarehouseId.set(whId);
    this.assessmentResult.set(null);
    this.lineRows.set([]);
    this.createdRequisition.set(null);
    this.error.set(null);
  }

  runAssessment(): void {
    const comp = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    const whId = this.selectedWarehouseId();

    if (!comp?.id || !branch?.id) {
      this.setError('Validation', 'Company Scope Missing', 'Please select an active company and branch from the workspace header.', 'Select an active branch and company.', false);
      return;
    }

    if (!whId) {
      this.setError('Validation', 'Warehouse Required', 'Please select a target warehouse facility for assessment.', 'Select a warehouse from the dropdown.', false);
      return;
    }

    this.isAssessing.set(true);
    this.error.set(null);
    this.createdRequisition.set(null);

    // Assess demand against warehouse inventory rules
    const req: ReplenishmentAssessmentRequest = {
      warehouseId: whId,
      items: [] // Server queries all active products in scope when items list is empty or evaluates master list
    };

    // First load products for assessment
    this.masterDataService.getProducts().subscribe({
      next: prods => {
        const activeProds = (prods || []).filter(p => p.isActive !== false && p.isStockTracked !== false);
        if (activeProds.length === 0) {
          this.isAssessing.set(false);
          this.lineRows.set([]);
          return;
        }

        const assessReq: ReplenishmentAssessmentRequest = {
          warehouseId: whId,
          items: activeProds.map(p => ({
            productId: p.productId,
            requiredQuantity: 10,
            unitOfMeasureId: p.unitOfMeasureId,
            remarks: `Min/Max threshold assessment for ${p.productCode}`
          }))
        };

        this.inventoryService.assessReplenishment(comp.id, branch.id, assessReq).subscribe({
          next: res => {
            this.isAssessing.set(false);
            this.assessmentResult.set(res);

            const rows: ReplenishmentLineRow[] = (res.lines || []).map(line => {
              const defaultOrderQty = line.shortageQuantity > 0 ? line.shortageQuantity : 0;
              const unitPrice = line.estimatedUnitPrice || 0;
              return {
                ...line,
                selected: line.hasShortage,
                orderQuantity: defaultOrderQty,
                lineTotal: Math.round(defaultOrderQty * unitPrice * 100) / 100
              };
            });

            this.lineRows.set(rows);
          },
          error: err => {
            this.isAssessing.set(false);
            this.handleHttpError(err, 'Stock Assessment Failed');
          }
        });
      },
      error: err => {
        this.isAssessing.set(false);
        this.handleHttpError(err, 'Product Loading Failed');
      }
    });
  }

  toggleSelectAll(): void {
    const target = !this.isAllSelected();
    this.lineRows.update(rows =>
      rows.map(r => ({
        ...r,
        selected: target
      }))
    );
  }

  toggleRow(index: number): void {
    this.lineRows.update(rows => {
      const copy = [...rows];
      copy[index] = { ...copy[index], selected: !copy[index].selected };
      return copy;
    });
  }

  onQuantityChange(index: number, newQty: number | null): void {
    const qty = Math.max(0, newQty || 0);
    this.lineRows.update(rows => {
      const copy = [...rows];
      const r = copy[index];
      const unitPrice = r.estimatedUnitPrice || 0;
      copy[index] = {
        ...r,
        orderQuantity: qty,
        lineTotal: Math.round(qty * unitPrice * 100) / 100,
        hasValidationError: qty <= 0 && r.selected,
        validationMessage: qty <= 0 && r.selected ? 'Quantity must be greater than zero.' : undefined
      };
      return copy;
    });
  }

  openConfirmDialog(): void {
    const selected = this.selectedLines();
    if (selected.length === 0) {
      this.setError('Validation', 'No Lines Selected', 'Please select at least one item line to create a purchase requisition.', 'Check the checkbox for one or more items.', false);
      return;
    }

    // Validate quantities
    const invalidLine = selected.find(l => l.orderQuantity <= 0);
    if (invalidLine) {
      this.setError('Validation', 'Invalid Quantity', `Item '${invalidLine.productCode}' has a requested quantity of 0. Please specify a positive quantity.`, 'Adjust item quantity above 0.', false, invalidLine.productCode);
      return;
    }

    // Generate or maintain stable idempotency key for this batch
    if (!this.currentIdempotencyKey()) {
      const whId = this.selectedWarehouseId();
      this.currentIdempotencyKey.set(`REPL-BATCH-${whId}-${Date.now()}`);
    }

    this.confirmDialogVisible.set(true);
  }

  submitBatchPurchaseRequisition(): void {
    const comp = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    const whId = this.selectedWarehouseId();
    const selected = this.selectedLines();

    if (!comp?.id || !branch?.id || !whId || selected.length === 0) return;

    if (this.confirmForm.invalid) {
      this.confirmForm.markAllAsTouched();
      return;
    }

    const formVal = this.confirmForm.getRawValue();
    const key = this.currentIdempotencyKey();

    this.isSubmitting.set(true);
    this.error.set(null);

    const req: CreateReplenishmentPrRequest = {
      warehouseId: whId,
      idempotencyKey: key,
      sourceType: ReplenishmentSourceType.InventoryPlanning,
      requiredDate: formVal.requiredDate || null,
      notes: formVal.notes || 'Batch Replenishment Requisition',
      items: selected.map(l => ({
        productId: l.productId,
        requestedQuantity: l.orderQuantity,
        unitOfMeasureId: l.unitOfMeasureId,
        estimatedUnitPrice: l.estimatedUnitPrice,
        remarks: `Replenishment for ${l.productCode}`
      }))
    };

    this.inventoryService.createReplenishmentPurchaseRequisition(comp.id, branch.id, req).subscribe({
      next: res => {
        this.isSubmitting.set(false);
        this.confirmDialogVisible.set(false);
        this.createdRequisition.set(res);
        // Clear idempotency key on complete success so next new batch generates a fresh key
        this.currentIdempotencyKey.set('');
      },
      error: err => {
        this.isSubmitting.set(false);
        this.handleHttpError(err, 'Draft Requisition Creation Failed');
        // NOTE: We intentionally PRESERVE this.currentIdempotencyKey() so a retry uses the exact same key!
      }
    });
  }

  retrySubmission(): void {
    this.error.set(null);
    this.submitBatchPurchaseRequisition();
  }

  inspectRequisition(requisitionId?: string): void {
    const id = requisitionId || this.createdRequisition()?.requisitionId;
    if (id) {
      this.router.navigate(['/app/procurement/purchase-requisitions', id]);
    }
  }

  resetWorkflow(): void {
    this.createdRequisition.set(null);
    this.assessmentResult.set(null);
    this.lineRows.set([]);
    this.error.set(null);
    this.currentIdempotencyKey.set('');
  }

  private handleHttpError(err: any, fallbackTitle: string): void {
    const status = err?.status;
    const msg = err?.error?.message || err?.message || 'An unexpected error occurred.';

    if (status === 403) {
      this.setError('Authorization', 'Access Forbidden', 'You do not have the required permissions (INVENTORY.REPLENISH.CREATE) to perform this replenishment operation.', 'Request permissions from your Tenant Administrator.', false);
    } else if (status === 404) {
      this.setError('Validation', 'Resource Not Found', msg, 'Verify selected warehouse and products exist and are active.', false);
    } else if (status === 409) {
      this.setError('Conflict', 'Concurrent Modification', 'Another user or process has updated stock or submitted this replenishment requisition.', 'Click retry to reconcile with latest server state.', true);
    } else if (status === 0 || err?.name === 'TimeoutError' || msg.includes('network') || msg.includes('timeout')) {
      this.setError('Network', 'Network Communication Error', 'Connection to the server timed out. If the server already processed your request, retrying will safely retrieve the original requisition without duplication.', 'Click Retry to safely resume.', true);
    } else {
      this.setError('Server', fallbackTitle, msg, 'Verify data and retry.', true);
    }
  }

  private setError(
    category: ReplenishmentErrorInfo['category'],
    title: string,
    message: string,
    suggestedAction: string,
    canRetry: boolean,
    affectedItem?: string
  ): void {
    this.error.set({
      category,
      title,
      message,
      suggestedAction,
      canRetry,
      affectedItem
    });
  }
}
