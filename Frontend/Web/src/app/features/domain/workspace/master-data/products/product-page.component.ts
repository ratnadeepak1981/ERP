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
import { FormGroup, FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Subscription, forkJoin } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppReactiveFormComponent } from '../../../../../shared/components/forms/reactive-form.component';
import { FormFieldConfig } from '../../../../../shared/components/forms/form-field-config.model';
import { AppStatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import {
  ProductItem,
  CreateProductDto,
  UpdateProductDto,
  CategoryItem,
  UnitOfMeasureItem,
  BrandItem,
  ProductPackagingItem,
  ProductType,
  TrackingMode,
  ValuationMethod
} from '../../../../../core/models/scope.models';

export type ProductPageMode = 'create' | 'edit' | 'view';

@Component({
  selector: 'app-product-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    ButtonModule,
    AppPageHeaderComponent,
    AppReactiveFormComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent
  ],
  templateUrl: './product-page.component.html',
  styleUrl: './product-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProductPageComponent implements OnInit, OnDestroy {
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly router = inject(Router);
  readonly route = inject(ActivatedRoute);

  private readonly subs = new Subscription();

  // Mode and identity
  readonly mode = signal<ProductPageMode>('create');
  readonly productId = signal<string | null>(null);
  readonly currentProduct = signal<ProductItem | null>(null);

  // Reference master data
  readonly categories = signal<CategoryItem[]>([]);
  readonly units = signal<UnitOfMeasureItem[]>([]);
  readonly brands = signal<BrandItem[]>([]);
  readonly packagings = signal<ProductPackagingItem[]>([]);

  // Page State
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly serverErrors = signal<Record<string, string>>({});
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Reactive Form
  readonly productForm = new FormGroup({
    productCode: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(2)]
    }),
    productName: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(2)]
    }),
    categoryId: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required]
    }),
    brandId: new FormControl<string | null>(null),
    productType: new FormControl<number>(ProductType.StockItem, {
      nonNullable: true,
      validators: [Validators.required]
    }),
    unitOfMeasure: new FormControl<string>('PCS', {
      nonNullable: true,
      validators: [Validators.required]
    }),
    unitOfMeasureId: new FormControl<string | null>(null),
    isStockTracked: new FormControl<boolean>(true, { nonNullable: true }),
    trackingMode: new FormControl<number>(TrackingMode.None, { nonNullable: true }),
    valuationMethod: new FormControl<number>(ValuationMethod.FIFO, { nonNullable: true }),
    standardCost: new FormControl<number>(0, {
      nonNullable: true,
      validators: [Validators.min(0)]
    }),
    sellingPrice: new FormControl<number>(0, {
      nonNullable: true,
      validators: [Validators.min(0)]
    }),
    reorderLevel: new FormControl<number>(0, {
      nonNullable: true,
      validators: [Validators.min(0)]
    }),
    reorderQuantity: new FormControl<number>(0, {
      nonNullable: true,
      validators: [Validators.min(0)]
    }),
    minStockQuantity: new FormControl<number | null>(null, {
      validators: [Validators.min(0)]
    }),
    maxStockQuantity: new FormControl<number | null>(null, {
      validators: [Validators.min(0)]
    }),
    safetyStockQuantity: new FormControl<number | null>(null, {
      validators: [Validators.min(0)]
    }),
    leadTimeDays: new FormControl<number | null>(null, {
      validators: [Validators.min(0)]
    }),
    description: new FormControl<string>('', { nonNullable: true }),
    isPurchasable: new FormControl<boolean>(true, { nonNullable: true }),
    isSellable: new FormControl<boolean>(true, { nonNullable: true }),
    isManufacturable: new FormControl<boolean>(false, { nonNullable: true }),
    canConsumeInProduction: new FormControl<boolean>(false, { nonNullable: true }),
    canConsumeInMaintenance: new FormControl<boolean>(false, { nonNullable: true }),
    isActive: new FormControl<boolean>(true, { nonNullable: true })
  });

  // Breadcrumbs
  readonly breadcrumbs = computed(() => {
    const list = [
      { label: 'Workspace', routerLink: '/app/dashboard' },
      { label: 'Supply Chain Masters', routerLink: '/app/master-data' },
      { label: 'Products', routerLink: '/app/master-data/products' }
    ];

    if (this.mode() === 'create') {
      list.push({ label: 'New Product Definition', routerLink: '' });
    } else {
      const code = this.currentProduct()?.productCode || 'Product Details';
      if (this.mode() === 'edit') {
        list.push({ label: code, routerLink: `/app/master-data/products/${this.productId()}` });
        list.push({ label: 'Edit', routerLink: '' });
      } else {
        list.push({ label: code, routerLink: '' });
      }
    }
    return list;
  });

  // Page Title & Subtitle
  readonly pageTitle = computed(() => {
    if (this.mode() === 'create') {
      return 'New Product Definition';
    }
    const code = this.currentProduct()?.productCode;
    if (this.mode() === 'view') {
      return code ? `View Product — ${code}` : 'View Product';
    }
    return code ? `Edit Product — ${code}` : 'Edit Product';
  });

  readonly pageSubtitle = computed(() => {
    if (this.mode() === 'create') {
      return 'Register a new enterprise item definition, commercial pricing, unit of measure, and operational capabilities.';
    }
    if (this.mode() === 'view') {
      return 'View product specifications, commercial valuation, category classification, inventory thresholds, and operational flags in read-only mode.';
    }
    return 'Update item specifications, commercial valuation, category classification, and operational flags.';
  });

  // Form Fields - Section 1: Identification & Taxonomy
  readonly identificationFields = computed<FormFieldConfig[]>(() => {
    const isEdit = this.mode() !== 'create';
    const isView = this.mode() === 'view';

    const catOptions = this.categories().map(c => ({
      label: `${c.categoryName} (${c.categoryCode})`,
      value: c.categoryId
    }));

    const brandOptions = [
      { label: 'None (Unbranded)', value: null },
      ...this.brands().map(b => ({
        label: `${b.brandName} (${b.brandCode})`,
        value: b.brandId
      }))
    ];

    const typeOptions = [
      { label: 'Stock Item (Physical balance tracked in warehouse)', value: ProductType.StockItem },
      { label: 'Non-Stock Item (Expense / consumable / direct procurement)', value: ProductType.NonStockItem },
      { label: 'Service Item (Labor / maintenance / service contracts)', value: ProductType.Service }
    ];

    return [
      {
        name: 'productCode',
        label: 'Product Code',
        type: 'text',
        required: true,
        placeholder: 'e.g. PRD-001',
        readonly: isEdit || isView,
        tooltip: 'Unique system identifier for the product',
        colSpan: 1
      },
      {
        name: 'productName',
        label: 'Product Name',
        type: 'text',
        required: true,
        placeholder: 'e.g. Standard Item A',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'categoryId',
        label: 'Category (Taxonomy Group)',
        type: 'select',
        required: true,
        placeholder: '-- Select Category --',
        options: catOptions,
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'brandId',
        label: 'Brand / Manufacturer',
        type: 'select',
        placeholder: '-- Select Brand --',
        options: brandOptions,
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'productType',
        label: 'Product Type',
        type: 'select',
        required: true,
        placeholder: '-- Select Product Type --',
        options: typeOptions,
        disabled: isView,
        colSpan: 2
      }
    ];
  });

  // Form Fields - Section 2: Inventory, UOM & Tracking
  readonly inventoryTrackingFields = computed<FormFieldConfig[]>(() => {
    const isView = this.mode() === 'view';

    const uomOptions = this.units().length > 0
      ? this.units().map(u => ({ label: `${u.name} (${u.code})`, value: u.code }))
      : [
          { label: 'Pieces (PCS)', value: 'PCS' },
          { label: 'Each (EA)', value: 'EA' },
          { label: 'Kilograms (KG)', value: 'KG' },
          { label: 'Meters (MTR)', value: 'MTR' },
          { label: 'Boxes (BOX)', value: 'BOX' },
          { label: 'Litres (LTR)', value: 'LTR' },
          { label: 'Hours (HRS)', value: 'HRS' }
        ];

    const trackingOptions = [
      { label: 'None (Standard count tracking)', value: TrackingMode.None },
      { label: 'Batch / Lot Tracked (Batch expiry & lot quarantine)', value: TrackingMode.Batch },
      { label: 'Serial Tracked (Unique individual serial numbers)', value: TrackingMode.Serial },
      { label: 'Batch & Serial Tracked', value: TrackingMode.BatchAndSerial }
    ];

    const valuationOptions = [
      { label: 'FIFO (First In, First Out)', value: ValuationMethod.FIFO },
      { label: 'Weighted Average Cost', value: ValuationMethod.WeightedAverage },
      { label: 'Standard Costing', value: ValuationMethod.StandardCost },
      { label: 'LIFO (Last In, First Out)', value: ValuationMethod.LIFO }
    ];

    const isNonStock = Number(this.productForm.get('productType')?.value) !== ProductType.StockItem;
    const isStockTracked = !!this.productForm.get('isStockTracked')?.value;

    return [
      {
        name: 'unitOfMeasure',
        label: 'Base Unit of Measure',
        type: 'select',
        required: true,
        placeholder: '-- Select Base UOM --',
        options: uomOptions,
        disabled: isView,
        hint: 'Authoritative unit for stock balance ledger and standard costing',
        colSpan: 1
      },
      {
        name: 'isStockTracked',
        label: 'Stock Ledger Tracking',
        placeholder: 'Maintain perpetual balance in warehouse ledger',
        type: 'checkbox',
        disabled: isView || isNonStock,
        colSpan: 1
      },
      {
        name: 'trackingMode',
        label: 'Traceability Mode',
        type: 'select',
        placeholder: '-- Select Tracking Mode --',
        options: trackingOptions,
        disabled: isView || !isStockTracked,
        hint: 'Controls batch/serial requirements on Goods Receipt and Dispatch',
        colSpan: 1
      },
      {
        name: 'valuationMethod',
        label: 'Valuation Method',
        type: 'select',
        placeholder: '-- Select Valuation Method --',
        options: valuationOptions,
        disabled: isView || !isStockTracked,
        hint: 'Financial inventory valuation and cost of goods sold model',
        colSpan: 1
      }
    ];
  });

  // Form Fields - Section 3: Commercial Valuation & Pricing
  readonly financialFields = computed<FormFieldConfig[]>(() => {
    const isView = this.mode() === 'view';
    return [
      {
        name: 'standardCost',
        label: 'Standard Cost',
        type: 'number',
        placeholder: '0.00',
        prefix: '$',
        min: 0,
        step: 0.01,
        disabled: isView,
        hint: 'Base inventory receipt and valuation unit cost',
        colSpan: 1
      },
      {
        name: 'sellingPrice',
        label: 'List Selling Price',
        type: 'number',
        placeholder: '0.00',
        prefix: '$',
        min: 0,
        step: 0.01,
        disabled: isView,
        hint: 'Default catalogue price for customer quotes and sales orders',
        colSpan: 1
      }
    ];
  });

  // Form Fields - Section 4: Inventory Planning & Replenishment Thresholds
  readonly planningFields = computed<FormFieldConfig[]>(() => {
    const isView = this.mode() === 'view';
    return [
      {
        name: 'reorderLevel',
        label: 'Reorder Point (Level)',
        type: 'number',
        placeholder: '0',
        min: 0,
        step: 1,
        disabled: isView,
        hint: 'Inventory threshold triggering replenishment alerts',
        colSpan: 1
      },
      {
        name: 'reorderQuantity',
        label: 'Reorder Batch Quantity',
        type: 'number',
        placeholder: '0',
        min: 0,
        step: 1,
        disabled: isView,
        hint: 'Standard order batch size suggested on purchase requisitions',
        colSpan: 1
      },
      {
        name: 'leadTimeDays',
        label: 'Supplier Lead Time (Days)',
        type: 'number',
        placeholder: '0',
        min: 0,
        step: 1,
        disabled: isView,
        hint: 'Estimated procurement/replenishment duration in days',
        colSpan: 1
      },
      {
        name: 'minStockQuantity',
        label: 'Minimum Stock Floor',
        type: 'number',
        placeholder: 'Optional minimum',
        min: 0,
        step: 1,
        disabled: isView,
        hint: 'Lower operational bound for warehouse stock alerts',
        colSpan: 1
      },
      {
        name: 'safetyStockQuantity',
        label: 'Safety Stock Buffer',
        type: 'number',
        placeholder: 'Optional buffer',
        min: 0,
        step: 1,
        disabled: isView,
        hint: 'Emergency buffer reserve (must be <= Min Stock & <= Max Stock)',
        colSpan: 1
      },
      {
        name: 'maxStockQuantity',
        label: 'Maximum Stock Ceiling',
        type: 'number',
        placeholder: 'Optional ceiling',
        min: 0,
        step: 1,
        disabled: isView,
        hint: 'Capacity limit threshold to prevent over-purchasing',
        colSpan: 1
      }
    ];
  });

  // Form Fields - Section 5: Item Capabilities & Operational Flags
  readonly capabilityFields = computed<FormFieldConfig[]>(() => {
    const isView = this.mode() === 'view';
    return [
      {
        name: 'isPurchasable',
        label: 'Purchasable',
        placeholder: 'Purchasable (Vendor Requisitions & Purchase Orders)',
        type: 'checkbox',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'isSellable',
        label: 'Sellable',
        placeholder: 'Sellable (Customer Sales Orders & Invoicing)',
        type: 'checkbox',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'isManufacturable',
        label: 'Manufacturable',
        placeholder: 'Manufacturable (Work Orders & Assembly BOM)',
        type: 'checkbox',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'canConsumeInProduction',
        label: 'Production BOM Raw Material',
        placeholder: 'Can consume in production work orders',
        type: 'checkbox',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'canConsumeInMaintenance',
        label: 'Maintenance & MRO Spare Part',
        placeholder: 'Can consume in plant maintenance / repair orders',
        type: 'checkbox',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'isActive',
        label: 'Active Status',
        placeholder: 'Active Item (Available across ERP operational modules)',
        type: 'checkbox',
        disabled: isView,
        colSpan: 1
      }
    ];
  });

  // Form Fields - Section 6: Specifications & Notes
  readonly notesFields = computed<FormFieldConfig[]>(() => {
    const isView = this.mode() === 'view';
    return [
      {
        name: 'description',
        label: 'Description & Technical Specifications',
        type: 'textarea',
        placeholder: 'Optional item notes, specifications, material standards, handling guidelines, or usage instructions...',
        rows: 3,
        disabled: isView,
        colSpan: 1
      }
    ];
  });

  ngOnInit(): void {
    this.setupConditionalLogic();
    this.loadReferenceData();

    // Listen to URL changes to detect create vs view vs edit mode seamlessly
    this.subs.add(
      this.route.url.subscribe(() => {
        this.detectRoute();
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  private setupConditionalLogic(): void {
    // 1. When ProductType changes, restrict stock tracking
    this.subs.add(
      this.productForm.get('productType')!.valueChanges.subscribe(type => {
        if (this.mode() === 'view') return;
        this.applyConditionalRestrictions();
      })
    );

    // 2. When isStockTracked changes, restrict trackingMode & valuationMethod
    this.subs.add(
      this.productForm.get('isStockTracked')!.valueChanges.subscribe(tracked => {
        if (this.mode() === 'view') return;
        this.applyConditionalRestrictions();
      })
    );

    // 3. When unitOfMeasure code changes, resolve corresponding unitOfMeasureId
    this.subs.add(
      this.productForm.get('unitOfMeasure')!.valueChanges.subscribe(uomCode => {
        const matched = this.units().find(u => u.code === uomCode);
        this.productForm.get('unitOfMeasureId')?.setValue(matched ? matched.unitOfMeasureId : null, { emitEvent: false });
      })
    );
  }

  private applyConditionalRestrictions(): void {
    if (this.mode() === 'view') {
      this.productForm.disable({ emitEvent: false });
      return;
    }

    const type = Number(this.productForm.get('productType')?.value);
    const isStockTrackedCtrl = this.productForm.get('isStockTracked')!;
    const trackingModeCtrl = this.productForm.get('trackingMode')!;
    const valuationMethodCtrl = this.productForm.get('valuationMethod')!;

    if (type === ProductType.Service || type === ProductType.NonStockItem) {
      isStockTrackedCtrl.setValue(false, { emitEvent: false });
      isStockTrackedCtrl.disable({ emitEvent: false });
      trackingModeCtrl.setValue(TrackingMode.None, { emitEvent: false });
      trackingModeCtrl.disable({ emitEvent: false });
      valuationMethodCtrl.disable({ emitEvent: false });
    } else {
      isStockTrackedCtrl.enable({ emitEvent: false });
      if (!isStockTrackedCtrl.value) {
        trackingModeCtrl.setValue(TrackingMode.None, { emitEvent: false });
        trackingModeCtrl.disable({ emitEvent: false });
      } else {
        trackingModeCtrl.enable({ emitEvent: false });
        valuationMethodCtrl.enable({ emitEvent: false });
      }
    }
  }

  private loadReferenceData(): void {
    forkJoin([
      this.masterDataService.getCategories(),
      this.masterDataService.getUnitsOfMeasure(),
      this.masterDataService.getBrands()
    ]).subscribe({
      next: ([cats, uoms, brands]) => {
        this.categories.set(cats);
        this.units.set(uoms);
        this.brands.set(brands);

        // Default category & UOM for new product if not set
        if (this.mode() === 'create') {
          if (cats.length > 0 && !this.productForm.get('categoryId')?.value) {
            this.productForm.patchValue({ categoryId: cats[0].categoryId });
          }
          if (uoms.length > 0 && !this.productForm.get('unitOfMeasure')?.value) {
            this.productForm.patchValue({
              unitOfMeasure: uoms[0].code,
              unitOfMeasureId: uoms[0].unitOfMeasureId
            });
          }
        }
      },
      error: () => {}
    });
  }

  private detectRoute(): void {
    const url = this.router.url;
    const id = this.route.snapshot.paramMap.get('id');

    if (id) {
      this.productId.set(id);
      const isEdit = url.endsWith('/edit') || url.includes('/edit?') || url.includes('/edit#');
      const targetMode: ProductPageMode = isEdit ? 'edit' : 'view';
      this.mode.set(targetMode);
      this.loadProduct(id);
    } else {
      this.mode.set('create');
      this.productId.set(null);
      this.currentProduct.set(null);
      this.packagings.set([]);
      this.resetCreateForm();
    }
  }

  private resetCreateForm(): void {
    const defaultCat = this.categories()[0]?.categoryId || '';
    const defaultUom = this.units()[0]?.code || 'PCS';
    const defaultUomId = this.units()[0]?.unitOfMeasureId || null;

    this.productForm.reset({
      productCode: '',
      productName: '',
      categoryId: defaultCat,
      brandId: null,
      productType: ProductType.StockItem,
      unitOfMeasure: defaultUom,
      unitOfMeasureId: defaultUomId,
      isStockTracked: true,
      trackingMode: TrackingMode.None,
      valuationMethod: ValuationMethod.FIFO,
      standardCost: 0,
      sellingPrice: 0,
      reorderLevel: 0,
      reorderQuantity: 0,
      minStockQuantity: null,
      maxStockQuantity: null,
      safetyStockQuantity: null,
      leadTimeDays: null,
      description: '',
      isPurchasable: true,
      isSellable: true,
      isManufacturable: false,
      canConsumeInProduction: false,
      canConsumeInMaintenance: false,
      isActive: true
    });
    this.productForm.enable({ emitEvent: false });
    this.applyConditionalRestrictions();
  }

  loadProduct(id: string): void {
    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      product: this.masterDataService.getProduct(id),
      packagings: this.masterDataService.getProductPackagings(id)
    }).subscribe({
      next: ({ product, packagings }) => {
        this.loading.set(false);
        if (!product) {
          this.error.set('Product definition could not be found.');
          return;
        }

        this.currentProduct.set(product);
        this.packagings.set(packagings || []);
        this.populateForm(product);
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err?.error?.message || 'Failed to retrieve product record from server.');
      }
    });
  }

  private populateForm(prod: ProductItem): void {
    let uomId = prod.unitOfMeasureId || null;
    let uomCode = prod.unitOfMeasure || 'PCS';
    if (!uomId && uomCode) {
      const matched = this.units().find(u => u.code === uomCode);
      if (matched) uomId = matched.unitOfMeasureId;
    }

    this.productForm.patchValue({
      productCode: prod.productCode,
      productName: prod.productName,
      categoryId: prod.categoryId,
      brandId: prod.brandId || null,
      productType: Number(prod.productType) || ProductType.StockItem,
      unitOfMeasure: uomCode,
      unitOfMeasureId: uomId,
      isStockTracked: prod.isStockTracked !== false,
      trackingMode: Number(prod.trackingMode) || TrackingMode.None,
      valuationMethod: Number(prod.valuationMethod) || ValuationMethod.FIFO,
      standardCost: Number(prod.standardCost) || 0,
      sellingPrice: Number(prod.sellingPrice) || 0,
      reorderLevel: Number(prod.reorderLevel) || 0,
      reorderQuantity: Number(prod.reorderQuantity) || 0,
      minStockQuantity: prod.minStockQuantity != null ? Number(prod.minStockQuantity) : null,
      maxStockQuantity: prod.maxStockQuantity != null ? Number(prod.maxStockQuantity) : null,
      safetyStockQuantity: prod.safetyStockQuantity != null ? Number(prod.safetyStockQuantity) : null,
      leadTimeDays: prod.leadTimeDays != null ? Number(prod.leadTimeDays) : null,
      description: prod.description || '',
      isPurchasable: prod.isPurchasable,
      isSellable: prod.isSellable,
      isManufacturable: prod.isManufacturable,
      canConsumeInProduction: prod.canConsumeInProduction,
      canConsumeInMaintenance: !!prod.canConsumeInMaintenance,
      isActive: prod.isActive !== false
    });

    if (this.mode() === 'view') {
      this.productForm.disable({ emitEvent: false });
    } else {
      this.productForm.enable({ emitEvent: false });
      if (this.mode() === 'edit') {
        // Product code is immutable in edit mode
        this.productForm.get('productCode')?.disable({ emitEvent: false });
      }
      this.applyConditionalRestrictions();
    }
  }

  switchToEdit(): void {
    if (this.productId()) {
      this.router.navigate(['/app/master-data/products', this.productId(), 'edit']);
    }
  }

  switchToView(): void {
    if (this.productId()) {
      this.router.navigate(['/app/master-data/products', this.productId()]);
    }
  }

  saveProduct(): void {
    if (this.mode() === 'view') {
      return;
    }

    if (this.productForm.invalid) {
      this.productForm.markAllAsTouched();
      this.showMessage('Please correct the highlighted form errors before saving.', 'danger');
      return;
    }

    const val = this.productForm.getRawValue();

    // Validate Planning Hierarchy: SafetyStock <= MinStock <= MaxStock
    const minStock = val.minStockQuantity != null && (val.minStockQuantity as any) !== '' ? Number(val.minStockQuantity) : null;
    const maxStock = val.maxStockQuantity != null && (val.maxStockQuantity as any) !== '' ? Number(val.maxStockQuantity) : null;
    const safetyStock = val.safetyStockQuantity != null && (val.safetyStockQuantity as any) !== '' ? Number(val.safetyStockQuantity) : null;

    if (minStock != null && maxStock != null && minStock > maxStock) {
      this.showMessage('Minimum stock quantity cannot be greater than maximum stock quantity.', 'danger');
      return;
    }
    if (safetyStock != null && maxStock != null && safetyStock > maxStock) {
      this.showMessage('Safety stock quantity cannot be greater than maximum stock quantity.', 'danger');
      return;
    }
    if (safetyStock != null && minStock != null && safetyStock > minStock) {
      this.showMessage('Safety stock quantity cannot be greater than minimum stock quantity.', 'danger');
      return;
    }

    this.saving.set(true);
    this.serverErrors.set({});

    // Resolve unitOfMeasureId if missing
    let uomId = val.unitOfMeasureId || null;
    if (!uomId && val.unitOfMeasure) {
      const matched = this.units().find(u => u.code === val.unitOfMeasure);
      if (matched) uomId = matched.unitOfMeasureId;
    }

    // Normalize dependent values based on ProductType and StockTracking
    const normalizedProductType = Number(val.productType) || ProductType.StockItem;
    const isPhysicalStock = normalizedProductType === ProductType.StockItem;
    const normalizedIsStockTracked = isPhysicalStock && !!val.isStockTracked;
    const normalizedTrackingMode = normalizedIsStockTracked ? (Number(val.trackingMode) || TrackingMode.None) : TrackingMode.None;

    const dto: CreateProductDto = {
      productCode: val.productCode.trim(),
      productName: val.productName.trim(),
      categoryId: val.categoryId,
      brandId: val.brandId || null,
      productType: normalizedProductType,
      description: val.description?.trim() || undefined,
      unitOfMeasure: val.unitOfMeasure || 'PCS',
      unitOfMeasureId: uomId,
      isStockTracked: normalizedIsStockTracked,
      trackingMode: normalizedTrackingMode,
      valuationMethod: Number(val.valuationMethod) || ValuationMethod.FIFO,
      standardCost: Number(val.standardCost) || 0,
      sellingPrice: Number(val.sellingPrice) || 0,
      reorderLevel: Number(val.reorderLevel) || 0,
      reorderQuantity: Number(val.reorderQuantity) || 0,
      minStockQuantity: minStock,
      maxStockQuantity: maxStock,
      safetyStockQuantity: safetyStock,
      leadTimeDays: val.leadTimeDays != null && (val.leadTimeDays as any) !== '' ? Number(val.leadTimeDays) : null,
      isPurchasable: !!val.isPurchasable,
      isSellable: !!val.isSellable,
      isManufacturable: !!val.isManufacturable,
      canConsumeInProduction: !!val.canConsumeInProduction,
      canConsumeInMaintenance: !!val.canConsumeInMaintenance
    };

    if (this.mode() === 'edit' && this.productId()) {
      const updateDto: UpdateProductDto = {
        ...dto,
        isActive: !!val.isActive
      };

      this.masterDataService.updateProduct(this.productId()!, updateDto).subscribe({
        next: updated => {
          this.saving.set(false);
          this.currentProduct.set(updated || { ...this.currentProduct()!, ...updateDto });
          this.showMessage(`Product '${val.productName}' updated successfully.`, 'success');
          setTimeout(() => {
            this.switchToView();
          }, 600);
        },
        error: err => {
          this.saving.set(false);
          this.handleSaveError(err, 'Failed to update product.');
        }
      });
    } else {
      this.masterDataService.createProduct(dto).subscribe({
        next: created => {
          this.saving.set(false);
          this.showMessage(`Product '${val.productName}' created successfully.`, 'success');
          setTimeout(() => {
            if (created?.productId) {
              this.router.navigate(['/app/master-data/products', created.productId]);
            } else {
              this.router.navigate(['/app/master-data/products']);
            }
          }, 600);
        },
        error: err => {
          this.saving.set(false);
          this.handleSaveError(err, 'Failed to create product.');
        }
      });
    }
  }

  private handleSaveError(err: any, fallbackMessage: string): void {
    if (err?.error?.errors && typeof err.error.errors === 'object') {
      const fieldErrors: Record<string, string> = {};
      for (const [key, msgs] of Object.entries(err.error.errors)) {
        const fieldName = key.charAt(0).toLowerCase() + key.slice(1);
        fieldErrors[fieldName] = Array.isArray(msgs) ? msgs.join(' ') : String(msgs);
      }
      this.serverErrors.set(fieldErrors);
      this.showMessage('Validation error occurred while saving the product.', 'danger');
    } else {
      const msg = err?.error?.message || err?.message || fallbackMessage;
      this.showMessage(msg, 'danger');
    }
  }

  showMessage(text: string, severity: 'success' | 'info' | 'warn' | 'danger'): void {
    this.message.set({ text, severity });
  }

  navigateBack(): void {
    if (this.mode() === 'edit' && this.productId()) {
      this.switchToView();
    } else {
      this.router.navigate(['/app/master-data/products']);
    }
  }
}
