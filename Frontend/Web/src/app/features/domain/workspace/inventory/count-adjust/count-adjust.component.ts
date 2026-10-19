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
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../../shared/components/data-table/data-table.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AppEmptyStateComponent } from '../../../../../shared/components/empty-state/empty-state.component';
import { AccessDeniedComponent } from '../../../../../shared/components/access-denied/access-denied.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { InventoryService } from '../../../../../core/services/inventory.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { ProductItem } from '../../../../../core/models/scope.models';
import { DataTableColumn, DataTableAction } from '../../../../../shared/models/data-table-config.model';

@Component({
  selector: 'app-count-adjust',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    ButtonModule,
    InputTextModule,
    AppPageHeaderComponent,
    AppDataTableComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AppEmptyStateComponent,
    AccessDeniedComponent
  ],
  templateUrl: './count-adjust.component.html',
  styleUrl: './count-adjust.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CountAdjustComponent implements OnInit, OnDestroy {
  readonly masterDataService = inject(MasterDataService);
  readonly inventoryService = inject(InventoryService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);

  private readonly subs = new Subscription();

  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to access physical stock counts and adjustments."
  );

  readonly products = signal<ProductItem[]>([]);
  readonly searchTerm = signal<string>('');
  readonly statusFilter = signal<'all' | 'low-stock' | 'healthy'>('all');

  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Inventory', routerLink: '/app/inventory' },
    { label: 'Stock Count & Adjustment' }
  ];

  readonly totalProducts = computed(() => this.products().length);
  readonly lowStockCount = computed(() =>
    this.products().filter(p => (p.reorderLevel || 0) > 0).length
  );
  readonly healthyCount = computed(() =>
    this.products().filter(p => (p.reorderLevel || 0) <= 0).length
  );

  readonly filteredProducts = computed<ProductItem[]>(() => {
    let rows = [...this.products()];
    const filter = this.statusFilter();
    if (filter === 'low-stock') {
      rows = rows.filter(p => (p.reorderLevel || 0) > 0);
    } else if (filter === 'healthy') {
      rows = rows.filter(p => (p.reorderLevel || 0) <= 0);
    }
    const term = this.searchTerm().trim().toLowerCase();
    if (term) {
      rows = rows.filter(
        p =>
          p.productCode.toLowerCase().includes(term) ||
          p.productName.toLowerCase().includes(term) ||
          (p.categoryName && p.categoryName.toLowerCase().includes(term))
      );
    }
    return rows;
  });

  readonly columns: DataTableColumn<ProductItem>[] = [
    {
      field: 'productCode',
      header: 'SKU / PRODUCT CODE',
      sortable: true
    },
    {
      field: 'productName',
      header: 'PRODUCT DESCRIPTION',
      sortable: true
    },
    {
      field: 'unitOfMeasure',
      header: 'UOM',
      sortable: true,
      align: 'center'
    },
    {
      field: 'reorderLevel',
      header: 'REORDER THRESHOLD',
      sortable: true,
      align: 'right',
      format: (val, row) => `${Number(val || 0).toLocaleString()} ${row.unitOfMeasure || 'PCS'}`
    },
    {
      field: 'standardCost',
      header: 'STANDARD COST',
      sortable: true,
      align: 'right',
      format: val => Number(val || 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
    },
    {
      field: 'isStockTracked',
      header: 'TRACKING STATUS',
      type: 'badge',
      sortable: true,
      align: 'center',
      format: val => (val ? 'Tracked Inventory' : 'Non-Stocked'),
      badgeSeverityMap: val => (val ? 'info' : 'neutral')
    },
    {
      field: 'isActive',
      header: 'STATUS',
      type: 'badge',
      sortable: true,
      align: 'center',
      format: (_, row) => ((row.reorderLevel || 0) > 0 ? 'Review Needed' : 'Normal'),
      badgeSeverityMap: (_, row) => ((row.reorderLevel || 0) > 0 ? 'warning' : 'success')
    }
  ];

  readonly tableActions: DataTableAction<ProductItem>[] = [
    {
      id: 'issue',
      label: 'Issue Stock',
      icon: 'pi pi-arrow-circle-up',
      action: () => this.router.navigate(['/app/inventory/issue-stock/new'])
    }
  ];

  ngOnInit(): void {
    this.loadData();
    this.subs.add(
      this.scopeService.scopeChange$.subscribe(() => {
        this.loadData();
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  loadData(): void {
    if (!this.canView()) {
      this.isAccessDenied.set(true);
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.masterDataService.getProducts().subscribe({
      next: prods => {
        this.products.set(prods || []);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        if (err?.status === 403) {
          this.isAccessDenied.set(true);
        } else {
          this.error.set(err?.message || 'Failed to load stock count catalogue.');
        }
      }
    });
  }

  canView(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.isPlatformStaff()) {
      return true;
    }
    return this.authService.hasPermission('PRODUCT.VIEW');
  }
}
