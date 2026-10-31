import {
  Component,
  OnInit,
  ChangeDetectionStrategy,
  signal,
  computed,
  inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormBuilder,
  FormGroup,
  Validators,
  ReactiveFormsModule
} from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { forkJoin } from 'rxjs';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import {
  PriceCardItem,
  PriceCardLineItem,
  PriceCardType,
  CreatePriceCardDto,
  UpdatePriceCardDto,
  CreatePriceCardLineDto
} from '../../../../../core/models/price-card.models';
import {
  ProductItem,
  UnitOfMeasureItem,
  CurrencyItem
} from '../../../../../core/models/scope.models';
import {
  DataTableColumn,
  SelectOption
} from '../../../../../shared/models/data-table-config.model';
import { LookupItem } from '../../../../../shared/components/lookup-dropdown/lookup-item.model';
import { FormFieldConfig } from '../../../../../shared/components/forms/form-field-config.model';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppReactiveFormComponent } from '../../../../../shared/components/forms/reactive-form.component';
import { AppEditableDataTableComponent } from '../../../../../shared/components/data-table/editable-data-table.component';
import { AppStatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { ActionDialogComponent } from '../../../../../shared/components/action-dialog/action-dialog.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';

interface ContextMessage {
  severity: 'success' | 'warn' | 'error';
  text: string;
}

interface PendingActionConfig {
  type: 'deactivate' | 'reactivate';
  title: string;
  message: string;
  confirmLabel: string;
  severity: 'danger' | 'success';
}

@Component({
  selector: 'app-price-card-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    AppPageHeaderComponent,
    AppReactiveFormComponent,
    AppEditableDataTableComponent,
    AppStatusBadgeComponent,
    ActionDialogComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent
  ],
  templateUrl: './price-card-page.component.html',
  styleUrl: './price-card-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PriceCardPageComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly masterDataService = inject(MasterDataService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  private lineIdCounter = 1;

  // Mode and identity signals
  readonly mode = signal<'create' | 'edit' | 'view'>('create');
  readonly priceCardId = signal<string | null>(null);
  readonly currentPriceCard = signal<PriceCardItem | null>(null);

  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly actionLoading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly message = signal<ContextMessage | null>(null);
  readonly serverErrors = signal<Record<string, string>>({});

  // Lookups and reference data
  readonly products = signal<ProductItem[]>([]);
  readonly uoms = signal<UnitOfMeasureItem[]>([]);
  readonly currencies = signal<CurrencyItem[]>([]);

  readonly lines = signal<PriceCardLineItem[]>([]);

  // Confirmation dialog state
  readonly isActionDialogVisible = signal<boolean>(false);
  readonly pendingAction = signal<PendingActionConfig | null>(null);

  // Form Group
  readonly headerForm: FormGroup = this.fb.group({
    priceCardCode: ['', [Validators.required, Validators.maxLength(50)]],
    priceCardName: ['', [Validators.required, Validators.maxLength(100)]],
    priceCardType: [PriceCardType.Purchase, [Validators.required]],
    currency: ['USD', [Validators.required, Validators.maxLength(3)]],
    effectiveFrom: [new Date().toISOString().substring(0, 10), [Validators.required]],
    effectiveTo: [''],
    description: ['']
  });

  // Computed state
  readonly isReadonly = computed(() => this.mode() === 'view');

  readonly pageTitle = computed(() => {
    switch (this.mode()) {
      case 'create':
        return 'New Price Card';
      case 'edit':
        return `Edit Price Card: ${this.currentPriceCard()?.priceCardCode || ''}`;
      case 'view':
      default:
        return `Price Card: ${this.currentPriceCard()?.priceCardCode || ''}`;
    }
  });

  readonly pageSubtitle = computed(() => {
    switch (this.mode()) {
      case 'create':
        return 'Register a new master data price card for purchase or sales transactions.';
      case 'edit':
        return 'Update pricing header parameters and individual line rate entries.';
      case 'view':
      default:
        return 'Inspect active pricing conditions, validity periods, and associated item rates.';
    }
  });

  readonly breadcrumbs = computed(() => [
    { label: 'Home', routerLink: '/app' },
    { label: 'Master Data', routerLink: '/app/master-data' },
    { label: 'Price Cards', routerLink: '/app/master-data/price-cards' },
    {
      label:
        this.mode() === 'create'
          ? 'New'
          : this.currentPriceCard()?.priceCardCode || 'Details'
    }
  ]);

  readonly currentCurrency = computed(() => {
    return this.headerForm.get('currency')?.value || 'USD';
  });

  // Dynamic Form Field Configs
  headerFields: FormFieldConfig[] = [];

  // Detail Lines Table Columns
  lineColumns: DataTableColumn<PriceCardLineItem>[] = [];

  // Permissions
  canEdit(): boolean {
    return (
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin() ||
      this.authService.hasPermission('PRICE_CARD.EDIT')
    );
  }

  canManage(): boolean {
    return (
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin() ||
      this.authService.hasPermission('PRICE_CARD.MANAGE')
    );
  }

  ngOnInit(): void {
    this.determineModeAndId();
    this.initFormAndColumns();
    this.loadReferenceData();
  }

  private determineModeAndId(): void {
    const routeDataMode = this.route.snapshot.data['mode'];
    const id = this.route.snapshot.paramMap.get('id');
    const url = this.router.url;

    if (routeDataMode) {
      this.mode.set(routeDataMode);
    } else if (url.includes('/new')) {
      this.mode.set('create');
    } else if (url.includes('/edit')) {
      this.mode.set('edit');
    } else if (id) {
      this.mode.set('view');
    }

    if (id) {
      this.priceCardId.set(id);
    }
  }

  private initFormAndColumns(): void {
    const isCreate = this.mode() === 'create';
    const isReadOnly = this.mode() === 'view';

    if (!isCreate) {
      this.headerForm.get('priceCardCode')?.disable();
      this.headerForm.get('priceCardType')?.disable();
    }
    if (isReadOnly) {
      this.headerForm.disable();
    }

    this.headerFields = [
      {
        name: 'priceCardCode',
        label: 'Price Card Code',
        type: 'text',
        required: true,
        placeholder: 'e.g. PC-PUR-001',
        disabled: !isCreate,
        hint: isCreate ? 'Unique identifier for this price card' : 'Code cannot be changed after creation'
      },
      {
        name: 'priceCardName',
        label: 'Price Card Name',
        type: 'text',
        required: true,
        placeholder: 'e.g. Standard 2026 Material Pricing'
      },
      {
        name: 'priceCardType',
        label: 'Price Card Type',
        type: 'select',
        required: true,
        disabled: !isCreate,
        options: [
          { label: 'Purchase', value: PriceCardType.Purchase },
          { label: 'Sales', value: PriceCardType.Sales }
        ]
      },
      {
        name: 'currency',
        label: 'Currency',
        type: 'select',
        required: true,
        options: [
          { label: 'USD - US Dollar', value: 'USD' },
          { label: 'EUR - Euro', value: 'EUR' },
          { label: 'GBP - British Pound', value: 'GBP' },
          { label: 'CAD - Canadian Dollar', value: 'CAD' }
        ]
      },
      {
        name: 'effectiveFrom',
        label: 'Effective From',
        type: 'date',
        required: true
      },
      {
        name: 'effectiveTo',
        label: 'Effective To',
        type: 'date',
        hint: 'Optional open-ended validity'
      },
      {
        name: 'description',
        label: 'Description',
        type: 'textarea',
        colSpan: 3,
        rows: 2,
        placeholder: 'Optional notes, terms, or context...'
      }
    ];

    this.lineColumns = [
      {
        field: 'productCode',
        header: 'Product Code',
        sortable: true,
        width: '200px',
        editable: !isReadOnly,
        editType: 'lookup',
        lookupOptions: [],
        onLookupSelect: (item: LookupItem, row: PriceCardLineItem) => {
          row.productId = String(item.id);
          row.productCode = item.code;
          row.productName = item.name;

          // Default UOM from product's base UOM if available
          const product = item.meta as ProductItem;
          if (product && product.unitOfMeasureId && !row.unitOfMeasureId) {
            const matchingUom = this.uoms().find(u => u.unitOfMeasureId === product.unitOfMeasureId);
            if (matchingUom) {
              row.unitOfMeasureId = matchingUom.unitOfMeasureId;
              row.unitOfMeasureCode = matchingUom.code;
              row.unitOfMeasureName = matchingUom.name;
            }
          }
        },
        validators: [(val: any) => (val && String(val).trim() ? null : 'Product is required')]
      },
      {
        field: 'productName',
        header: 'Product Name',
        sortable: true,
        width: '240px',
        editable: false
      },
      {
        field: 'unitOfMeasureCode',
        header: 'UOM',
        sortable: true,
        width: '140px',
        editable: !isReadOnly,
        editType: 'select',
        editOptions: []
      },
      {
        field: 'unitPrice',
        header: 'Unit Price',
        sortable: true,
        width: '130px',
        align: 'right',
        type: 'currency',
        editable: !isReadOnly,
        editType: 'currency',
        validators: [
          (val: any) => {
            const num = Number(val);
            if (isNaN(num)) return 'Price must be a number';
            if (num < 0) return 'Price cannot be negative';
            return null;
          }
        ]
      },
      {
        field: 'minimumQuantity',
        header: 'Min Qty',
        sortable: true,
        width: '110px',
        align: 'right',
        editable: !isReadOnly,
        editType: 'number',
        validators: [
          (val: any) => {
            const num = Number(val);
            if (isNaN(num)) return 'Min Qty must be a number';
            if (num < 0) return 'Min Qty cannot be negative';
            return null;
          }
        ]
      },
      {
        field: 'effectiveFrom',
        header: 'Line Effective From',
        width: '140px',
        editable: !isReadOnly,
        editType: 'date'
      },
      {
        field: 'effectiveTo',
        header: 'Line Effective To',
        width: '140px',
        editable: !isReadOnly,
        editType: 'date'
      }
    ];
  }

  private loadReferenceData(): void {
    this.loading.set(true);

    forkJoin({
      products: this.masterDataService.getProducts(),
      uoms: this.masterDataService.getUnitsOfMeasure(),
      currencies: this.masterDataService.getCurrencies()
    }).subscribe({
      next: ({ products, uoms, currencies }) => {
        this.products.set(products);
        this.uoms.set(uoms);
        this.currencies.set(currencies);

        this.updateLookupOptions(products, uoms, currencies);

        // If in edit or view mode, load the specific price card record
        const id = this.priceCardId();
        if (id) {
          this.loadCardData();
        } else {
          this.loading.set(false);
        }
      },
      error: () => {
        // Fall back gracefully even if lookups fail partially
        this.loading.set(false);
        const id = this.priceCardId();
        if (id) {
          this.loadCardData();
        }
      }
    });
  }

  private updateLookupOptions(
    products: ProductItem[],
    uoms: UnitOfMeasureItem[],
    currencies: CurrencyItem[]
  ): void {
    // 1. Product Lookups
    const productLookupItems: LookupItem[] = products.map(p => ({
      id: p.productId,
      code: p.productCode,
      name: p.productName,
      description: p.productName,
      meta: p
    }));

    // 2. UOM Select Options
    const uomSelectOptions: SelectOption[] = uoms.map(u => ({
      label: `${u.code} - ${u.name}`,
      value: u.code,
      rawItem: u
    }));

    // Update lineColumns dynamically
    this.lineColumns = this.lineColumns.map(col => {
      if (col.field === 'productCode') {
        return { ...col, lookupOptions: productLookupItems };
      }
      if (col.field === 'unitOfMeasureCode') {
        return { ...col, editOptions: uomSelectOptions };
      }
      return col;
    });

    // 3. Currency Options for Header Form
    if (currencies.length > 0) {
      const currencyOptions = currencies.map(c => ({
        label: `${c.currencyCode} - ${c.currencyName}`,
        value: c.currencyCode
      }));
      this.headerFields = this.headerFields.map(f => {
        if (f.name === 'currency') {
          return { ...f, options: currencyOptions };
        }
        return f;
      });
    }
  }

  loadCardData(): void {
    const id = this.priceCardId();
    if (!id) return;

    this.loading.set(true);
    this.error.set(null);

    this.masterDataService.getPriceCard(id).subscribe({
      next: card => {
        if (!card) {
          this.error.set('Price card not found.');
          this.loading.set(false);
          return;
        }

        this.currentPriceCard.set(card);

        // Populate header form
        this.headerForm.patchValue({
          priceCardCode: card.priceCardCode,
          priceCardName: card.priceCardName,
          priceCardType: card.priceCardType,
          currency: card.currency,
          effectiveFrom: card.effectiveFrom ? card.effectiveFrom.substring(0, 10) : '',
          effectiveTo: card.effectiveTo ? card.effectiveTo.substring(0, 10) : '',
          description: card.description || ''
        });

        // Populate detail lines with tracking IDs
        const mappedLines: PriceCardLineItem[] = (card.lines || []).map(line => ({
          ...line,
          lineId: line.priceCardLineId || `line_${this.lineIdCounter++}`,
          effectiveFrom: line.effectiveFrom ? line.effectiveFrom.substring(0, 10) : null,
          effectiveTo: line.effectiveTo ? line.effectiveTo.substring(0, 10) : null
        }));

        this.lines.set(mappedLines);
        this.loading.set(false);
      },
      error: err => {
        this.error.set(err?.error?.message || err?.message || 'Failed to load price card details.');
        this.loading.set(false);
      }
    });
  }

  createBlankLine = (): Partial<PriceCardLineItem> => {
    return {
      lineId: `line_${this.lineIdCounter++}`,
      productId: '',
      productCode: '',
      productName: '',
      unitOfMeasureId: undefined,
      unitOfMeasureCode: '',
      unitPrice: 0,
      minimumQuantity: 1,
      effectiveFrom: this.headerForm.get('effectiveFrom')?.value || null,
      effectiveTo: null,
      isActive: true
    };
  };

  onLinesDataChange(updatedLines: PriceCardLineItem[]): void {
    this.lines.set(updatedLines);
  }

  onCellValueChange(event: { row: PriceCardLineItem; column: DataTableColumn<PriceCardLineItem>; value: any }): void {
    const { row, column, value } = event;

    if (column.field === 'unitOfMeasureCode') {
      const match = this.uoms().find(u => u.code === value);
      if (match) {
        row.unitOfMeasureId = match.unitOfMeasureId;
        row.unitOfMeasureCode = match.code;
        row.unitOfMeasureName = match.name;
      }
    }
  }

  saveCard(): void {
    this.serverErrors.set({});
    this.message.set(null);

    // 1. Validate Header Form
    if (this.headerForm.invalid) {
      this.headerForm.markAllAsTouched();
      this.message.set({
        severity: 'error',
        text: 'Please correct the highlighted errors in the header details.'
      });
      return;
    }

    const formVal = this.headerForm.getRawValue();

    // 2. Validate Dates consistency
    if (formVal.effectiveTo && formVal.effectiveFrom) {
      if (new Date(formVal.effectiveTo) < new Date(formVal.effectiveFrom)) {
        this.message.set({
          severity: 'error',
          text: 'Effective To date cannot be earlier than Effective From date.'
        });
        return;
      }
    }

    // 3. Validate detail lines
    const currentLines = this.lines();
    const lineDtos: CreatePriceCardLineDto[] = [];
    const seenProductUomPairs = new Set<string>();

    for (let i = 0; i < currentLines.length; i++) {
      const line = currentLines[i];

      // Skip completely empty lines if any
      if (!line.productId && !line.productCode) {
        continue;
      }

      if (!line.productId) {
        // Attempt to resolve by productCode
        const match = this.products().find(p => p.productCode === line.productCode);
        if (match) {
          line.productId = match.productId;
        } else {
          this.message.set({
            severity: 'error',
            text: `Line ${i + 1} has an unrecognised product code "${line.productCode}".`
          });
          return;
        }
      }

      const unitPrice = Number(line.unitPrice);
      if (isNaN(unitPrice) || unitPrice < 0) {
        this.message.set({
          severity: 'error',
          text: `Line ${i + 1} has an invalid or negative unit price.`
        });
        return;
      }

      const minQty = Number(line.minimumQuantity);
      if (isNaN(minQty) || minQty < 0) {
        this.message.set({
          severity: 'error',
          text: `Line ${i + 1} has an invalid or negative minimum quantity.`
        });
        return;
      }

      if (line.effectiveTo && line.effectiveFrom) {
        if (new Date(line.effectiveTo) < new Date(line.effectiveFrom)) {
          this.message.set({
            severity: 'error',
            text: `Line ${i + 1} has an Effective To date earlier than its Effective From date.`
          });
          return;
        }
      }

      // Check duplicate Product + UOM combination
      const uomKey = line.unitOfMeasureId || 'NONE';
      const pairKey = `${line.productId}::${uomKey}`;
      if (seenProductUomPairs.has(pairKey)) {
        this.message.set({
          severity: 'error',
          text: `Duplicate line detected: Product "${line.productCode || line.productName}" with the same Unit of Measure appears more than once.`
        });
        return;
      }
      seenProductUomPairs.add(pairKey);

      lineDtos.push({
        productId: line.productId,
        unitOfMeasureId: line.unitOfMeasureId || null,
        unitPrice: unitPrice,
        minimumQuantity: minQty || 1,
        effectiveFrom: line.effectiveFrom ? new Date(line.effectiveFrom).toISOString() : null,
        effectiveTo: line.effectiveTo ? new Date(line.effectiveTo).toISOString() : null
      });
    }

    this.saving.set(true);

    if (this.mode() === 'create') {
      const createDto: CreatePriceCardDto = {
        priceCardCode: String(formVal.priceCardCode).trim(),
        priceCardName: String(formVal.priceCardName).trim(),
        priceCardType: Number(formVal.priceCardType),
        currency: String(formVal.currency).trim(),
        effectiveFrom: new Date(formVal.effectiveFrom).toISOString(),
        effectiveTo: formVal.effectiveTo ? new Date(formVal.effectiveTo).toISOString() : null,
        description: formVal.description ? String(formVal.description).trim() : null,
        lines: lineDtos
      };

      this.masterDataService.createPriceCard(createDto).subscribe({
        next: created => {
          this.saving.set(false);
          this.router.navigate(['/app/master-data/price-cards', created.priceCardId]);
        },
        error: err => {
          this.saving.set(false);
          this.handleApiError(err);
        }
      });
    } else {
      const id = this.priceCardId()!;
      const updateDto: UpdatePriceCardDto = {
        priceCardName: String(formVal.priceCardName).trim(),
        currency: String(formVal.currency).trim(),
        effectiveFrom: new Date(formVal.effectiveFrom).toISOString(),
        effectiveTo: formVal.effectiveTo ? new Date(formVal.effectiveTo).toISOString() : null,
        description: formVal.description ? String(formVal.description).trim() : null,
        lines: lineDtos
      };

      this.masterDataService.updatePriceCard(id, updateDto).subscribe({
        next: updated => {
          this.saving.set(false);
          this.currentPriceCard.set(updated);
          this.mode.set('view');
          this.headerForm.disable();
          this.message.set({
            severity: 'success',
            text: 'Price card updated successfully.'
          });
        },
        error: err => {
          this.saving.set(false);
          this.handleApiError(err);
        }
      });
    }
  }

  private handleApiError(err: any): void {
    const errorMsg =
      err?.error?.message ||
      err?.error?.title ||
      err?.message ||
      'An unexpected error occurred while saving the price card.';

    this.message.set({
      severity: 'error',
      text: errorMsg
    });

    if (err?.error?.errors) {
      const mappedErrors: Record<string, string> = {};
      for (const [key, val] of Object.entries(err.error.errors)) {
        mappedErrors[key] = Array.isArray(val) ? val.join(' ') : String(val);
      }
      this.serverErrors.set(mappedErrors);
    }
  }

  navigateBack(): void {
    this.router.navigate(['/app/master-data/price-cards']);
  }

  navigateToEdit(): void {
    const id = this.priceCardId();
    if (id) {
      this.router.navigate(['/app/master-data/price-cards', id, 'edit']);
    }
  }

  promptDeactivate(): void {
    const card = this.currentPriceCard();
    if (!card) return;

    this.pendingAction.set({
      type: 'deactivate',
      title: 'Deactivate Price Card',
      message: `Are you sure you want to deactivate price card "${card.priceCardName} (${card.priceCardCode})"?`,
      confirmLabel: 'Deactivate',
      severity: 'danger'
    });
    this.isActionDialogVisible.set(true);
  }

  promptReactivate(): void {
    const card = this.currentPriceCard();
    if (!card) return;

    this.pendingAction.set({
      type: 'reactivate',
      title: 'Reactivate Price Card',
      message: `Are you sure you want to reactivate price card "${card.priceCardName} (${card.priceCardCode})"?`,
      confirmLabel: 'Reactivate',
      severity: 'success'
    });
    this.isActionDialogVisible.set(true);
  }

  executePendingAction(): void {
    const action = this.pendingAction();
    const id = this.priceCardId();
    if (!action || !id) return;

    this.actionLoading.set(true);
    const op$ =
      action.type === 'deactivate'
        ? this.masterDataService.deactivatePriceCard(id)
        : this.masterDataService.reactivatePriceCard(id);

    op$.subscribe({
      next: () => {
        this.actionLoading.set(false);
        this.isActionDialogVisible.set(false);
        this.pendingAction.set(null);
        this.message.set({
          severity: 'success',
          text: `Price card was successfully ${action.type === 'deactivate' ? 'deactivated' : 'reactivated'}.`
        });
        this.loadCardData();
      },
      error: err => {
        this.actionLoading.set(false);
        this.isActionDialogVisible.set(false);
        this.pendingAction.set(null);
        this.message.set({
          severity: 'error',
          text: err?.error?.message || err?.message || `Failed to ${action.type} price card.`
        });
      }
    });
  }

  cancelPendingAction(): void {
    this.isActionDialogVisible.set(false);
    this.pendingAction.set(null);
  }
}
