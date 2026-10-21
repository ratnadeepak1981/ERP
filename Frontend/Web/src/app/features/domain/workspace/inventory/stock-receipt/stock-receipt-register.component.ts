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
  StockReceiptDto,
  StockReceiptReason,
  StockReceiptStatus,
  getStockReceiptReasonName,
  getStockReceiptReasonSeverity,
  getStockReceiptStatusSeverity
} from '../../../../../core/models/inventory.models';
import { DataTableColumn, DataTableAction } from '../../../../../shared/models/data-table-config.model';

export type StockReceiptReasonFilter = 'all' | 'opening' | 'return' | 'production' | 'misc';

@Component({
  selector: 'app-stock-receipt-register',
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
  templateUrl: './stock-receipt-register.component.html',
  styleUrl: './stock-receipt-register.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StockReceiptRegisterComponent implements OnInit, OnDestroy {
  readonly inventoryService = inject(InventoryService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);

  private readonly subs = new Subscription();

  // Loading & State
  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to access inventory stock receipt records."
  );

  // Receipts data from real API
  readonly receipts = signal<StockReceiptDto[]>([]);

  // Search & Filter
  readonly searchTerm = signal<string>('');
  readonly activeReasonFilter = signal<StockReceiptReasonFilter>('all');

  // Breadcrumbs
  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Inventory', routerLink: '/app/inventory' },
    { label: 'Stock Receipt' }
  ];

  // Reason options for filtering
  readonly reasonOptions = [
    { label: 'All Receipts', value: 'all' },
    { label: 'Opening Balance', value: 'opening' },
    { label: 'Customer Return', value: 'return' },
    { label: 'Internal Production', value: 'production' },
    { label: 'Miscellaneous Inbound', value: 'misc' }
  ];

  // Computed filtered list
  readonly filteredReceipts = computed<StockReceiptDto[]>(() => {
    let list = [...this.receipts()];

    const reasonFilter = this.activeReasonFilter();
    if (reasonFilter !== 'all') {
      list = list.filter(r => {
        if (reasonFilter === 'opening') return Number(r.reason) === StockReceiptReason.OpeningBalance;
        if (reasonFilter === 'return') return Number(r.reason) === StockReceiptReason.CustomerReturn;
        if (reasonFilter === 'production') return Number(r.reason) === StockReceiptReason.InternalProduction;
        if (reasonFilter === 'misc') return Number(r.reason) === StockReceiptReason.Miscellaneous;
        return true;
      });
    }

    const term = this.searchTerm().trim().toLowerCase();
    if (term) {
      list = list.filter(r => {
        const num = (r.receiptNumber || '').toLowerCase();
        const code = (r.productCode || '').toLowerCase();
        const name = (r.productName || '').toLowerCase();
        const wh = (r.warehouseName || '').toLowerCase();
        const ref = (r.sourceDocumentReference || '').toLowerCase();
        return (
          num.includes(term) ||
          code.includes(term) ||
          name.includes(term) ||
          wh.includes(term) ||
          ref.includes(term)
        );
      });
    }

    return list;
  });

  // Table columns
  readonly columns: DataTableColumn<StockReceiptDto>[] = [
    {
      field: 'receiptNumber',
      header: 'RECEIPT # / REF',
      sortable: true,
      format: (_, row) => `${row.receiptNumber}${row.sourceDocumentReference ? ' (' + row.sourceDocumentReference + ')' : ''}`
    },
    {
      field: 'receiptDate',
      header: 'DATE',
      sortable: true,
      align: 'center',
      format: val => (val ? new Date(val).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) : '-')
    },
    {
      field: 'reason',
      header: 'REASON',
      type: 'badge',
      sortable: true,
      align: 'center',
      format: val => getStockReceiptReasonName(val),
      badgeSeverityMap: val => getStockReceiptReasonSeverity(val)
    },
    {
      field: 'productName',
      header: 'PRODUCT / SKU',
      sortable: true,
      format: (_, row) => `${row.productCode} - ${row.productName}`
    },
    {
      field: 'warehouseName',
      header: 'WAREHOUSE / BIN',
      sortable: true,
      format: (_, row) => `${row.warehouseName}${row.locationCode ? ' / ' + row.locationCode : ''}`
    },
    {
      field: 'quantity',
      header: 'QUANTITY',
      sortable: true,
      align: 'right',
      format: (val, row) => `+${Number(val).toLocaleString()} ${row.unitOfMeasure || 'PCS'}`
    },
    {
      field: 'totalCost',
      header: 'TOTAL VALUE',
      sortable: true,
      align: 'right',
      format: val => {
        const curr = this.scopeService.selectedCompany()?.currency || 'LKR';
        const num = Number(val) || 0;
        return `${curr} ${num.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
      }
    },
    {
      field: 'status',
      header: 'STATUS',
      type: 'badge',
      sortable: true,
      align: 'center',
      format: () => 'Posted',
      badgeSeverityMap: () => 'success'
    }
  ];

  readonly tableActions: DataTableAction<StockReceiptDto>[] = [
    {
      id: 'view',
      label: 'View Details',
      icon: 'pi pi-eye',
      severity: 'secondary',
      action: (row: StockReceiptDto) => this.navigateToReceipt(row)
    },
    {
      id: 'putaway',
      label: 'Putaway',
      icon: 'pi pi-box',
      severity: 'primary',
      action: (row: StockReceiptDto) => this.navigateToPutaway(row)
    }
  ];

  canCreateReceipt(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin()) {
      return true;
    }
    return this.authService.hasPermission('PRODUCT.EDIT');
  }

  ngOnInit(): void {
    this.loadStockReceipts();
    this.subs.add(
      this.scopeService.scopeChange$.subscribe(() => {
        this.loadStockReceipts();
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  loadStockReceipts(): void {
    this.loading.set(true);
    this.error.set(null);
    this.isAccessDenied.set(false);

    this.inventoryService.getStockReceipts().subscribe({
      next: data => {
        this.receipts.set(data || []);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        if (err?.status === 403) {
          this.isAccessDenied.set(true);
        } else {
          this.error.set(err?.message || 'Failed to load stock receipt records.');
        }
      }
    });
  }

  navigateToCreate(): void {
    this.router.navigate(['/app/inventory/stock-receipts/new']);
  }

  navigateToReceipt(receipt: StockReceiptDto): void {
    this.router.navigate(['/app/inventory/stock-receipts', receipt.stockReceiptId]);
  }

  navigateToPutaway(receipt: StockReceiptDto): void {
    this.router.navigate(['/app/inventory/store-putaway/new'], {
      queryParams: {
        sourceType: 'StockReceipt',
        sourceId: receipt.stockReceiptId
      }
    });
  }
}
