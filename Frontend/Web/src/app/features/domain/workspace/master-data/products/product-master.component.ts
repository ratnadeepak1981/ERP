import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../../shared/components/data-table/data-table.component';
import { AppStatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AppEmptyStateComponent } from '../../../../../shared/components/empty-state/empty-state.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import {
  ProductItem,
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
    RouterModule,
    ButtonModule,
    InputTextModule,
    AppPageHeaderComponent,
    AppStatusBadgeComponent,
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
  readonly router = inject(Router);

  readonly loading = signal<boolean>(true);
  readonly error = signal<string | null>(null);

  readonly products = signal<ProductItem[]>([]);
  readonly categories = signal<CategoryItem[]>([]);
  readonly units = signal<UnitOfMeasureItem[]>([]);

  // Search & Filter state
  readonly searchTerm = signal<string>('');
  readonly activeStatusFilter = signal<'all' | 'active' | 'inactive'>('all');
  readonly purchaseableOnly = signal<boolean>(false);
  readonly sellableOnly = signal<boolean>(false);

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
    this.router.navigate(['/app/master-data/products/new']);
  }

  openEditDialog(product: ProductItem): void {
    this.router.navigate(['/app/master-data/products', product.productId]);
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
