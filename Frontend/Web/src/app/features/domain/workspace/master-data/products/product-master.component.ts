import {
  Component,
  OnInit,
  inject,
  signal,
  computed,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../../shared/components/data-table/data-table.component';
import { DataTableColumn, DataTableAction } from '../../../../../shared/models/data-table-config.model';
import { AppStatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppEmptyStateComponent } from '../../../../../shared/components/empty-state/empty-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import {
  ProductItem,
  CategoryItem,
  UnitOfMeasureItem,
  CategoryTreeNode
} from '../../../../../core/models/scope.models';

export interface CategoryNodeState {
  expanded: boolean;
  children: CategoryTreeNode[];
  childrenLoading: boolean;
  childrenLoaded: boolean;
  hasMoreChildren: boolean;
  childrenPage: number;
  childrenError: string | null;
  showProducts: boolean;
  products: ProductItem[];
  productsLoading: boolean;
  productsLoaded: boolean;
  hasMoreProducts: boolean;
  productsPage: number;
  productsError: string | null;
}

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
    AppDataTableComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppEmptyStateComponent,
    AppErrorStateComponent
  ],
  templateUrl: './product-master.component.html',
  styleUrl: './product-master.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProductMasterComponent implements OnInit {
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly router = inject(Router);

  readonly products = signal<ProductItem[]>([]);
  readonly categories = signal<CategoryItem[]>([]);
  readonly units = signal<UnitOfMeasureItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);

  // Filters & Search
  readonly searchTerm = signal<string>('');
  readonly activeStatusFilter = signal<'all' | 'active' | 'inactive'>('all');
  readonly purchaseableOnly = signal<boolean>(false);
  readonly sellableOnly = signal<boolean>(false);

  // View Mode: 'table' vs 'hierarchy'
  readonly viewMode = signal<'table' | 'hierarchy'>('table');

  // Hierarchy State
  readonly rootCategories = signal<CategoryTreeNode[]>([]);
  readonly rootsLoading = signal<boolean>(false);
  readonly rootsLoaded = signal<boolean>(false);
  readonly rootsError = signal<string | null>(null);
  readonly hasMoreRoots = signal<boolean>(false);
  readonly rootsPage = signal<number>(1);
  readonly loadingMoreRoots = signal<boolean>(false);

  // Per-category node state: Map<categoryId, CategoryNodeState>
  readonly nodeStates = signal<Record<string, CategoryNodeState>>({});

  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Supply Chain Masters', routerLink: '/app/master-data' },
    { label: 'Product Master', routerLink: '' }
  ];

  readonly tableColumns: DataTableColumn[] = [
    { field: 'productCode', header: 'Code', sortable: true, width: '130px' },
    { field: 'productName', header: 'Product', sortable: true },
    { field: 'unitOfMeasure', header: 'Unit', width: '90px' },
    { field: 'usageBadge', header: 'Usage', width: '220px' },
    { field: 'isActive', header: 'Status', width: '110px' }
  ];

  readonly tableActions: DataTableAction[] = [
    { id: 'view', label: 'View', icon: 'pi pi-eye', severity: 'secondary', action: (row: any) => this.viewProduct(row) },
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

  viewProduct(product: ProductItem): void {
    this.router.navigate(['/app/master-data/products', product.productId]);
  }

  openEditDialog(product: ProductItem): void {
    this.router.navigate(['/app/master-data/products', product.productId, 'edit']);
  }

  onRowAction(event: { actionId: string; row: ProductItem }): void {
    if (event.actionId === 'view') {
      this.viewProduct(event.row);
    } else if (event.actionId === 'edit') {
      this.openEditDialog(event.row);
    } else if (event.actionId === 'delete') {
      this.masterDataService.deactivateProduct(event.row.productId).subscribe({
        next: () => this.loadData(),
        error: (err) => this.error.set(err?.error?.message || 'Failed to deactivate product.')
      });
    }
  }

  onViewModeChange(mode: 'table' | 'hierarchy'): void {
    this.viewMode.set(mode);
    if (mode === 'hierarchy' && !this.rootsLoaded() && !this.rootsLoading()) {
      this.loadRoots(true);
    }
  }

  getNodeState(categoryId: string): CategoryNodeState {
    return this.nodeStates()[categoryId] || {
      expanded: false,
      children: [],
      childrenLoading: false,
      childrenLoaded: false,
      hasMoreChildren: false,
      childrenPage: 1,
      childrenError: null,
      showProducts: false,
      products: [],
      productsLoading: false,
      productsLoaded: false,
      hasMoreProducts: false,
      productsPage: 1,
      productsError: null
    };
  }

  updateNodeState(categoryId: string, partial: Partial<CategoryNodeState>): void {
    this.nodeStates.update(states => {
      const current = states[categoryId] || {
        expanded: false,
        children: [],
        childrenLoading: false,
        childrenLoaded: false,
        hasMoreChildren: false,
        childrenPage: 1,
        childrenError: null,
        showProducts: false,
        products: [],
        productsLoading: false,
        productsLoaded: false,
        hasMoreProducts: false,
        productsPage: 1,
        productsError: null
      };
      return {
        ...states,
        [categoryId]: { ...current, ...partial }
      };
    });
  }

  loadRoots(reset: boolean = false): void {
    if (this.rootsLoading()) return;

    const page = reset ? 1 : this.rootsPage();
    if (reset) {
      this.rootsLoading.set(true);
      this.rootsError.set(null);
      this.rootsPage.set(1);
    } else {
      this.loadingMoreRoots.set(true);
    }

    this.masterDataService.getRootCategories(page, 20).subscribe({
      next: (res) => {
        if (reset) {
          this.rootCategories.set(res.items);
        } else {
          const existingIds = new Set(this.rootCategories().map(r => r.categoryId));
          const newItems = res.items.filter(item => !existingIds.has(item.categoryId));
          this.rootCategories.update(current => [...current, ...newItems]);
        }
        this.hasMoreRoots.set(res.hasMore);
        this.rootsLoaded.set(true);
        this.rootsLoading.set(false);
        this.loadingMoreRoots.set(false);
      },
      error: (err) => {
        this.rootsLoading.set(false);
        this.loadingMoreRoots.set(false);
        this.rootsError.set(err?.error?.message || err?.message || 'Failed to load root categories.');
      }
    });
  }

  loadMoreRoots(): void {
    if (this.loadingMoreRoots() || !this.hasMoreRoots()) return;
    this.rootsPage.update(p => p + 1);
    this.loadRoots(false);
  }

  toggleExpandCategory(node: CategoryTreeNode, event?: Event): void {
    event?.stopPropagation();
    const state = this.getNodeState(node.categoryId);

    if (state.expanded) {
      // Collapse - cache preserved
      this.updateNodeState(node.categoryId, { expanded: false });
    } else {
      // Expand
      if (state.childrenLoaded) {
        // Reuse cached children without network request
        this.updateNodeState(node.categoryId, { expanded: true });
      } else {
        // First expansion: fetch children
        this.updateNodeState(node.categoryId, {
          expanded: true,
          childrenLoading: true,
          childrenError: null
        });
        this.loadCategoryChildren(node.categoryId, 1);
      }
    }
  }

  loadCategoryChildren(categoryId: string, page: number): void {
    this.updateNodeState(categoryId, {
      childrenLoading: true,
      childrenError: null
    });

    this.masterDataService.getCategoryChildren(categoryId, page, 20).subscribe({
      next: (res) => {
        const state = this.getNodeState(categoryId);
        const existingIds = new Set(state.children.map(c => c.categoryId));
        const newItems = res.items.filter(c => !existingIds.has(c.categoryId));
        const combined = page === 1 ? res.items : [...state.children, ...newItems];

        this.updateNodeState(categoryId, {
          children: combined,
          childrenLoaded: true,
          childrenLoading: false,
          childrenPage: page,
          hasMoreChildren: res.hasMore,
          childrenError: null
        });
      },
      error: (err) => {
        this.updateNodeState(categoryId, {
          childrenLoading: false,
          childrenError: err?.error?.message || err?.message || 'Failed to load subcategories.'
        });
      }
    });
  }

  loadMoreChildren(node: CategoryTreeNode, event?: Event): void {
    event?.stopPropagation();
    const state = this.getNodeState(node.categoryId);
    if (state.childrenLoading || !state.hasMoreChildren) return;
    this.loadCategoryChildren(node.categoryId, state.childrenPage + 1);
  }

  toggleShowProducts(node: CategoryTreeNode, event?: Event): void {
    event?.stopPropagation();
    const state = this.getNodeState(node.categoryId);

    if (state.showProducts) {
      // Hide products - cache preserved
      this.updateNodeState(node.categoryId, { showProducts: false });
    } else {
      // Show products
      if (state.productsLoaded) {
        // Reuse cached products without network request
        this.updateNodeState(node.categoryId, { showProducts: true });
      } else {
        // First reveal: fetch products
        this.updateNodeState(node.categoryId, {
          showProducts: true,
          productsLoading: true,
          productsError: null
        });
        this.loadCategoryProducts(node.categoryId, 1);
      }
    }
  }

  loadCategoryProducts(categoryId: string, page: number): void {
    this.updateNodeState(categoryId, {
      productsLoading: true,
      productsError: null
    });

    this.masterDataService.getCategoryProducts(categoryId, page, 20).subscribe({
      next: (res) => {
        const state = this.getNodeState(categoryId);
        const existingIds = new Set(state.products.map(p => p.productId));
        const newItems = res.items.filter(p => !existingIds.has(p.productId));
        const combined = page === 1 ? res.items : [...state.products, ...newItems];

        this.updateNodeState(categoryId, {
          products: combined,
          productsLoaded: true,
          productsLoading: false,
          productsPage: page,
          hasMoreProducts: res.hasMore,
          productsError: null
        });
      },
      error: (err) => {
        this.updateNodeState(categoryId, {
          productsLoading: false,
          productsError: err?.error?.message || err?.message || 'Failed to load products for category.'
        });
      }
    });
  }

  loadMoreProducts(node: CategoryTreeNode, event?: Event): void {
    event?.stopPropagation();
    const state = this.getNodeState(node.categoryId);
    if (state.productsLoading || !state.hasMoreProducts) return;
    this.loadCategoryProducts(node.categoryId, state.productsPage + 1);
  }

  refreshHierarchy(): void {
    this.masterDataService.clearHierarchyCache();
    this.nodeStates.set({});
    this.loadRoots(true);
  }
}
