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
  UnitOfMeasureItem
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
    unitOfMeasure: new FormControl<string>('PCS', {
      nonNullable: true,
      validators: [Validators.required]
    }),
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
    description: new FormControl<string>('', { nonNullable: true }),
    isPurchasable: new FormControl<boolean>(true, { nonNullable: true }),
    isSellable: new FormControl<boolean>(true, { nonNullable: true }),
    isManufacturable: new FormControl<boolean>(false, { nonNullable: true }),
    canConsumeInProduction: new FormControl<boolean>(false, { nonNullable: true }),
    isStockTracked: new FormControl<boolean>(true, { nonNullable: true }),
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
      list.push({ label: code, routerLink: '' });
    }
    return list;
  });

  // Page Title & Subtitle
  readonly pageTitle = computed(() => {
    if (this.mode() === 'create') {
      return 'New Product Definition';
    }
    const code = this.currentProduct()?.productCode;
    return code ? `Edit Product — ${code}` : 'Edit Product';
  });

  readonly pageSubtitle = computed(() => {
    if (this.mode() === 'create') {
      return 'Register a new enterprise item definition, commercial pricing, unit of measure, and operational capabilities.';
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
        label: 'Category',
        type: 'select',
        required: true,
        placeholder: '-- Select Category --',
        options: catOptions.length > 0 ? catOptions : [{ label: 'Standard Category (CAT-01)', value: 'cat-1' }],
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'unitOfMeasure',
        label: 'Unit of Measure',
        type: 'select',
        required: true,
        placeholder: '-- Select UOM --',
        options: uomOptions,
        disabled: isView,
        colSpan: 1
      }
    ];
  });

  // Form Fields - Section 2: Valuation & Stock Thresholds
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
        label: 'Selling Price',
        type: 'number',
        placeholder: '0.00',
        prefix: '$',
        min: 0,
        step: 0.01,
        disabled: isView,
        hint: 'Default catalogue price for customer quotes and sales orders',
        colSpan: 1
      },
      {
        name: 'reorderLevel',
        label: 'Reorder Level',
        type: 'number',
        placeholder: '0',
        min: 0,
        step: 1,
        disabled: isView,
        hint: 'Minimum inventory threshold before triggering replenishment',
        colSpan: 1
      }
    ];
  });

  // Form Fields - Section 3: Operational Capabilities & Flags
  readonly capabilityFields = computed<FormFieldConfig[]>(() => {
    const isView = this.mode() === 'view';
    return [
      {
        name: 'isPurchasable',
        label: 'Purchasable',
        placeholder: 'Purchasable (Vendor Requisitions & POs)',
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
        placeholder: 'Manufacturable (Work Orders & Assembly)',
        type: 'checkbox',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'canConsumeInProduction',
        label: 'Production Consumption',
        placeholder: 'Production Consumption (Raw Material in BOM)',
        type: 'checkbox',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'isStockTracked',
        label: 'Stock Tracked',
        placeholder: 'Stock Tracked (Warehouse Balance Ledger)',
        type: 'checkbox',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'isActive',
        label: 'Active Status',
        placeholder: 'Active Item (Available across ERP modules)',
        type: 'checkbox',
        disabled: isView,
        colSpan: 1
      }
    ];
  });

  // Form Fields - Section 4: Specifications & Notes
  readonly notesFields = computed<FormFieldConfig[]>(() => {
    const isView = this.mode() === 'view';
    return [
      {
        name: 'description',
        label: 'Description & Specifications',
        type: 'textarea',
        placeholder: 'Optional item notes, specifications, handling guidelines, or usage instructions...',
        rows: 3,
        disabled: isView,
        colSpan: 1
      }
    ];
  });

  ngOnInit(): void {
    this.loadReferenceData();
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

  private loadReferenceData(): void {
    this.masterDataService.getCategories().subscribe({
      next: cats => {
        this.categories.set(cats);
        // Default category for new product if not set
        if (this.mode() === 'create' && cats.length > 0 && !this.productForm.get('categoryId')?.value) {
          this.productForm.patchValue({ categoryId: cats[0].categoryId });
        }
      },
      error: () => {}
    });

    this.masterDataService.getUnitsOfMeasure().subscribe({
      next: uoms => {
        this.units.set(uoms);
        if (this.mode() === 'create' && uoms.length > 0 && !this.productForm.get('unitOfMeasure')?.value) {
          this.productForm.patchValue({ unitOfMeasure: uoms[0].code });
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
      this.mode.set(url.includes('/edit') ? 'edit' : 'edit'); // Default to edit for master record maintenance
      this.loadProduct(id);
    } else {
      this.mode.set('create');
      this.productId.set(null);
      this.currentProduct.set(null);
      this.productForm.reset({
        productCode: '',
        productName: '',
        categoryId: this.categories()[0]?.categoryId || '',
        unitOfMeasure: this.units()[0]?.code || 'PCS',
        standardCost: 0,
        sellingPrice: 0,
        reorderLevel: 0,
        description: '',
        isPurchasable: true,
        isSellable: true,
        isManufacturable: false,
        canConsumeInProduction: false,
        isStockTracked: true,
        isActive: true
      });
      this.productForm.get('productCode')?.enable();
    }
  }

  loadProduct(id: string): void {
    this.loading.set(true);
    this.error.set(null);

    this.masterDataService.getProduct(id).subscribe({
      next: prod => {
        this.loading.set(false);
        if (!prod) {
          this.error.set('Product definition could not be found.');
          return;
        }

        this.currentProduct.set(prod);
        this.populateForm(prod);
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err?.error?.message || 'Failed to retrieve product record from server.');
      }
    });
  }

  private populateForm(prod: ProductItem): void {
    this.productForm.patchValue({
      productCode: prod.productCode,
      productName: prod.productName,
      categoryId: prod.categoryId,
      unitOfMeasure: prod.unitOfMeasure || 'PCS',
      standardCost: Number(prod.standardCost) || 0,
      sellingPrice: Number(prod.sellingPrice) || 0,
      reorderLevel: Number(prod.reorderLevel) || 0,
      description: prod.description || '',
      isPurchasable: prod.isPurchasable,
      isSellable: prod.isSellable,
      isManufacturable: prod.isManufacturable,
      canConsumeInProduction: prod.canConsumeInProduction,
      isStockTracked: prod.isStockTracked,
      isActive: prod.isActive !== false
    });

    if (this.mode() !== 'create') {
      this.productForm.get('productCode')?.disable();
    }
  }

  saveProduct(): void {
    if (this.productForm.invalid) {
      this.productForm.markAllAsTouched();
      this.showMessage('Please correct the highlighted form errors before saving.', 'danger');
      return;
    }

    const val = this.productForm.getRawValue();
    this.saving.set(true);
    this.serverErrors.set({});

    const dto: CreateProductDto = {
      productCode: val.productCode.trim(),
      productName: val.productName.trim(),
      categoryId: val.categoryId || '00000000-0000-0000-0000-000000000000',
      description: val.description?.trim() || undefined,
      unitOfMeasure: val.unitOfMeasure || 'PCS',
      standardCost: Number(val.standardCost) || 0,
      sellingPrice: Number(val.sellingPrice) || 0,
      reorderLevel: Number(val.reorderLevel) || 0,
      reorderQuantity: 0,
      isPurchasable: !!val.isPurchasable,
      isSellable: !!val.isSellable,
      isManufacturable: !!val.isManufacturable,
      canConsumeInProduction: !!val.canConsumeInProduction,
      isStockTracked: !!val.isStockTracked
    };

    if (this.mode() !== 'create' && this.productId()) {
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
            this.router.navigate(['/app/master-data/products']);
          }, 800);
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
            this.router.navigate(['/app/master-data/products']);
          }, 800);
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
    this.router.navigate(['/app/master-data/products']);
  }
}
