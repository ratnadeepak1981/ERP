import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../../shared/components/data-table/data-table.component';
import { AppStatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { AppCheckboxComponent } from '../../../../../shared/components/checkbox/checkbox.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AppEmptyStateComponent } from '../../../../../shared/components/empty-state/empty-state.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import {
  ProductItem,
  CreateProductDto,
  CategoryItem,
  UnitOfMeasureItem
} from '../../../../../core/models/scope.models';
import { DataTableColumn, DataTableAction } from '../../../../../shared/models/data-table-config.model';

@Component({
  selector: 'app-product-master',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    InputTextModule,
    DialogModule,
    AppPageHeaderComponent,
    AppStatusBadgeComponent,
    AppCheckboxComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AppEmptyStateComponent
  ],
  templateUrl: './product-master.component.html',
  styleUrl: './product-master.component.scss'
})
export class ProductMasterComponent implements OnInit {
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);

  readonly loading = signal<boolean>(true);
  readonly saving = signal<boolean>(false);
  readonly error = signal<string | null>(null);

  readonly products = signal<ProductItem[]>([]);
  readonly categories = signal<CategoryItem[]>([]);
  readonly units = signal<UnitOfMeasureItem[]>([]);

  // Search & Filter state
  readonly searchTerm = signal<string>('');
  readonly activeStatusFilter = signal<'all' | 'active' | 'inactive'>('all');
  readonly purchaseableOnly = signal<boolean>(false);
  readonly sellableOnly = signal<boolean>(false);

  // Dialog State
  readonly dialogVisible = signal<boolean>(false);
  readonly isEditing = signal<boolean>(false);
  readonly editingProductId = signal<string | null>(null);
  readonly formError = signal<string | null>(null);

  // Form Fields
  formCode: string = '';
  formName: string = '';
  formCategoryId: string = '';
  formDescription: string = '';
  formUnitOfMeasure: string = 'PCS';
  formStandardCost: number = 0;
  formSellingPrice: number = 0;
  formReorderLevel: number = 0;
  formIsPurchasable: boolean = true;
  formIsSellable: boolean = true;
  formIsManufacturable: boolean = false;
  formCanConsumeInProduction: boolean = false;
  formIsStockTracked: boolean = true;

  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Supply Chain Masters', routerLink: '/app/master-data' },
    { label: 'Product Master' }
  ];

  readonly tableColumns: DataTableColumn[] = [
    { field: 'productCode', header: 'Code', sortable: true, width: '130px' },
    { field: 'productName', header: 'Product', sortable: true },
    { field: 'unitOfMeasure', header: 'Unit', width: '90px' },
    { field: 'usageBadge', header: 'Usage', width: '220px' },
    { field: 'isActive', header: 'Status', width: '110px' }
  ];

  readonly tableActions: DataTableAction[] = [
    { id: 'edit', label: 'Edit', icon: 'pi pi-pencil', severity: 'info', action: (row: any) => this.openEditDialog(row) },
    { id: 'delete', label: 'Deactivate', icon: 'pi pi-trash', severity: 'danger', action: (row: any) => this.onRowAction({ actionId: 'delete', row }) }
  ];

  readonly filteredProducts = computed(() => {
    let list = this.products();
    const q = this.searchTerm().trim().toLowerCase();
    if (q) {
      list = list.filter(p =>
        p.productCode.toLowerCase().includes(q) ||
        p.productName.toLowerCase().includes(q)
      );
    }
    const status = this.activeStatusFilter();
    if (status === 'active') {
      list = list.filter(p => p.isActive);
    } else if (status === 'inactive') {
      list = list.filter(p => !p.isActive);
    }
    if (this.purchaseableOnly()) {
      list = list.filter(p => p.isPurchasable);
    }
    if (this.sellableOnly()) {
      list = list.filter(p => p.isSellable);
    }
    return list;
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.error.set(null);

    this.masterDataService.getProducts().subscribe({
      next: (prods) => {
        this.products.set(prods);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.message || 'Failed to retrieve products from server.');
      }
    });

    this.masterDataService.getCategories().subscribe({
      next: (cats) => this.categories.set(cats),
      error: () => {}
    });

    this.masterDataService.getUnitsOfMeasure().subscribe({
      next: (uoms) => this.units.set(uoms),
      error: () => {}
    });
  }

  getUsageText(product: ProductItem): string {
    const usages: string[] = [];
    if (product.isPurchasable) usages.push('Purchase');
    if (product.isSellable) usages.push('Sell');
    if (product.isManufacturable) usages.push('Manufacture');
    if (product.canConsumeInProduction) usages.push('Consume');
    return usages.length > 0 ? usages.join(', ') : 'Stock';
  }

  openCreateDialog(): void {
    this.isEditing.set(false);
    this.editingProductId.set(null);
    this.formError.set(null);

    this.formCode = '';
    this.formName = '';
    this.formCategoryId = this.categories()[0]?.categoryId || '';
    this.formDescription = '';
    this.formUnitOfMeasure = this.units()[0]?.code || 'PCS';
    this.formStandardCost = 0;
    this.formSellingPrice = 0;
    this.formReorderLevel = 0;
    this.formIsPurchasable = true;
    this.formIsSellable = true;
    this.formIsManufacturable = false;
    this.formCanConsumeInProduction = false;
    this.formIsStockTracked = true;

    this.dialogVisible.set(true);
  }

  openEditDialog(product: ProductItem): void {
    this.isEditing.set(true);
    this.editingProductId.set(product.productId);
    this.formError.set(null);

    this.formCode = product.productCode;
    this.formName = product.productName;
    this.formCategoryId = product.categoryId;
    this.formDescription = product.description || '';
    this.formUnitOfMeasure = product.unitOfMeasure || 'PCS';
    this.formStandardCost = product.standardCost || 0;
    this.formSellingPrice = product.sellingPrice || 0;
    this.formReorderLevel = product.reorderLevel || 0;
    this.formIsPurchasable = product.isPurchasable;
    this.formIsSellable = product.isSellable;
    this.formIsManufacturable = product.isManufacturable;
    this.formCanConsumeInProduction = product.canConsumeInProduction;
    this.formIsStockTracked = product.isStockTracked;

    this.dialogVisible.set(true);
  }

  onSaveProduct(): void {
    if (!this.formCode.trim()) {
      this.formError.set('Product code is required.');
      return;
    }
    if (!this.formName.trim()) {
      this.formError.set('Product name is required.');
      return;
    }

    this.saving.set(true);
    this.formError.set(null);

    const dto: CreateProductDto = {
      productCode: this.formCode.trim(),
      productName: this.formName.trim(),
      categoryId: this.formCategoryId || '00000000-0000-0000-0000-000000000000',
      description: this.formDescription?.trim(),
      unitOfMeasure: this.formUnitOfMeasure,
      standardCost: Number(this.formStandardCost) || 0,
      sellingPrice: Number(this.formSellingPrice) || 0,
      reorderLevel: Number(this.formReorderLevel) || 0,
      reorderQuantity: 0,
      isPurchasable: this.formIsPurchasable,
      isSellable: this.formIsSellable,
      isManufacturable: this.formIsManufacturable,
      canConsumeInProduction: this.formCanConsumeInProduction,
      isStockTracked: this.formIsStockTracked
    };

    if (this.isEditing() && this.editingProductId()) {
      this.masterDataService.updateProduct(this.editingProductId()!, dto).subscribe({
        next: () => {
          this.saving.set(false);
          this.dialogVisible.set(false);
          this.loadData();
        },
        error: (err) => {
          this.saving.set(false);
          this.formError.set(err?.error?.message || 'Failed to update product.');
        }
      });
    } else {
      this.masterDataService.createProduct(dto).subscribe({
        next: () => {
          this.saving.set(false);
          this.dialogVisible.set(false);
          this.loadData();
        },
        error: (err) => {
          this.saving.set(false);
          this.formError.set(err?.error?.message || 'Failed to create product.');
        }
      });
    }
  }

  onRowAction(event: { actionId: string; row: ProductItem }): void {
    if (event.actionId === 'edit') {
      this.openEditDialog(event.row);
    } else if (event.actionId === 'delete') {
      if (confirm(`Deactivate product ${event.row.productCode}?`)) {
        this.masterDataService.deactivateProduct(event.row.productId).subscribe({
          next: () => this.loadData(),
          error: (err) => alert(err?.error?.message || 'Failed to deactivate product.')
        });
      }
    }
  }
}
