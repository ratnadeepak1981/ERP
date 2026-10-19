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
import { InventoryService } from '../../../../../core/services/inventory.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import {
  InventoryRecentTransactionDto,
  getMovementTypeDisplayName,
  getMovementTypeBadgeSeverity
} from '../../../../../core/models/inventory.models';
import { DataTableColumn, DataTableAction } from '../../../../../shared/models/data-table-config.model';

@Component({
  selector: 'app-transfer-stock',
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
  templateUrl: './transfer-stock.component.html',
  styleUrl: './transfer-stock.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TransferStockComponent implements OnInit, OnDestroy {
  readonly inventoryService = inject(InventoryService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);

  private readonly subs = new Subscription();

  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to access inventory stock transfer records."
  );

  readonly transactions = signal<InventoryRecentTransactionDto[]>([]);
  readonly searchTerm = signal<string>('');
  readonly movementFilter = signal<string>('all');

  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Inventory', routerLink: '/app/inventory' },
    { label: 'Stock Transfers' }
  ];

  readonly totalTransfers = computed(() => this.transactions().length);
  readonly internalMovements = computed(() =>
    this.transactions().filter(t => t.movementType.toLowerCase().includes('adjustment') || t.movementType.toLowerCase().includes('transfer')).length
  );
  readonly totalQuantityMoved = computed(() =>
    this.transactions().reduce((sum, t) => sum + Math.abs(Number(t.quantity) || 0), 0)
  );

  readonly filteredTransactions = computed<InventoryRecentTransactionDto[]>(() => {
    let rows = [...this.transactions()];
    const filter = this.movementFilter();
    if (filter !== 'all') {
      rows = rows.filter(r => r.movementType.toLowerCase().includes(filter.toLowerCase()));
    }
    const term = this.searchTerm().trim().toLowerCase();
    if (term) {
      rows = rows.filter(
        r =>
          r.transactionNumber.toLowerCase().includes(term) ||
          r.productCode.toLowerCase().includes(term) ||
          r.productName.toLowerCase().includes(term) ||
          r.warehouseName.toLowerCase().includes(term) ||
          (r.locationCode && r.locationCode.toLowerCase().includes(term))
      );
    }
    return rows;
  });

  readonly columns: DataTableColumn<InventoryRecentTransactionDto>[] = [
    {
      field: 'transactionNumber',
      header: 'TRANSFER / DOC #',
      sortable: true
    },
    {
      field: 'transactionDate',
      header: 'DATE',
      sortable: true,
      align: 'center',
      format: val => (val ? new Date(val).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) : '-')
    },
    {
      field: 'productName',
      header: 'PRODUCT / SKU',
      sortable: true,
      format: (_, row) => `${row.productCode} - ${row.productName}`
    },
    {
      field: 'warehouseName',
      header: 'WAREHOUSE / STORAGE BIN',
      sortable: true,
      format: (_, row) => `${row.warehouseName}${row.locationCode ? ' / ' + row.locationCode : ''}`
    },
    {
      field: 'movementType',
      header: 'MOVEMENT TYPE',
      type: 'badge',
      sortable: true,
      align: 'center',
      format: val => getMovementTypeDisplayName(val),
      badgeSeverityMap: val => getMovementTypeBadgeSeverity(val)
    },
    {
      field: 'quantity',
      header: 'QUANTITY',
      sortable: true,
      align: 'right',
      format: (val, row) => `${Number(val) > 0 ? '+' : ''}${Number(val).toLocaleString()} ${row.unitOfMeasure || 'PCS'}`
    }
  ];

  readonly tableActions: DataTableAction<InventoryRecentTransactionDto>[] = [
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

    this.inventoryService.getRecentTransactions(50).subscribe({
      next: txs => {
        this.transactions.set(txs || []);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        if (err?.status === 403) {
          this.isAccessDenied.set(true);
        } else {
          this.error.set(err?.message || 'Failed to load transfer movements.');
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
