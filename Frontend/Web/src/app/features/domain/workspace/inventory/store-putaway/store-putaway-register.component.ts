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
import { TooltipModule } from 'primeng/tooltip';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../../shared/components/data-table/data-table.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AppEmptyStateComponent } from '../../../../../shared/components/empty-state/empty-state.component';
import { AccessDeniedComponent } from '../../../../../shared/components/access-denied/access-denied.component';
import { PutawayService } from '../../../../../core/services/putaway.service';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { PutawayRecordDto, PutawayStatus, getPutawayStatusSeverity } from '../../../../../core/models/putaway.models';
import { WarehouseItem } from '../../../../../core/models/scope.models';
import { DataTableColumn, DataTableAction } from '../../../../../shared/models/data-table-config.model';

export type PutawayFilterStatus = 'all' | 'draft' | 'completed';

@Component({
  selector: 'app-store-putaway-register',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    ButtonModule,
    InputTextModule,
    TooltipModule,
    AppPageHeaderComponent,
    AppDataTableComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AccessDeniedComponent
  ],
  templateUrl: './store-putaway-register.component.html',
  styleUrl: './store-putaway-register.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StorePutawayRegisterComponent implements OnInit, OnDestroy {
  readonly putawayService = inject(PutawayService);
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);

  private readonly subs = new Subscription();

  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);

  readonly putaways = signal<PutawayRecordDto[]>([]);
  readonly warehouses = signal<WarehouseItem[]>([]);
  readonly selectedWarehouseFilter = signal<string>('all');
  readonly selectedStatusFilter = signal<PutawayFilterStatus>('all');
  readonly searchTerm = signal<string>('');

  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Inventory', routerLink: '/app/inventory' },
    { label: 'Stores Putaway' }
  ];

  readonly statusOptions = [
    { label: 'All Putaways', value: 'all' as PutawayFilterStatus },
    { label: 'Draft', value: 'draft' as PutawayFilterStatus },
    { label: 'Completed', value: 'completed' as PutawayFilterStatus }
  ];

  readonly filteredPutaways = computed<PutawayRecordDto[]>(() => {
    let rows = [...this.putaways()];
    const st = this.selectedStatusFilter();
    if (st === 'draft') {
      rows = rows.filter(r => r.status === PutawayStatus.Draft);
    } else if (st === 'completed') {
      rows = rows.filter(r => r.status === PutawayStatus.Completed);
    }

    const wh = this.selectedWarehouseFilter();
    if (wh && wh !== 'all') {
      rows = rows.filter(r => r.warehouseId === wh);
    }

    const term = this.searchTerm().trim().toLowerCase();
    if (term) {
      rows = rows.filter(
        r =>
          r.putawayNumber?.toLowerCase().includes(term) ||
          r.sourceDocumentNumber?.toLowerCase().includes(term) ||
          r.warehouseName?.toLowerCase().includes(term) ||
          r.notes?.toLowerCase().includes(term)
      );
    }
    return rows;
  });

  readonly columns: DataTableColumn<PutawayRecordDto>[] = [
    {
      field: 'putawayNumber',
      header: 'PUTAWAY # / REF',
      sortable: true
    },
    {
      field: 'putawayDate',
      header: 'DATE',
      sortable: true,
      format: val => (val ? new Date(val).toLocaleDateString() : '—')
    },
    {
      field: 'sourceDocumentType',
      header: 'SOURCE TYPE',
      type: 'badge',
      sortable: true,
      align: 'center',
      format: val => (val === 'GoodsReceipt' ? 'Goods Receipt' : val === 'StockReceipt' ? 'Stock Receipt' : 'Manual'),
      badgeSeverityMap: val => (val === 'GoodsReceipt' ? 'success' : 'info')
    },
    {
      field: 'sourceDocumentNumber',
      header: 'RECEIPT # / REF',
      sortable: true,
      format: (val, row) => val || row.sourceDocumentId || '—'
    },
    {
      field: 'warehouseName',
      header: 'WAREHOUSE FACILITY',
      sortable: true,
      format: (val, row) => (val ? `${row.warehouseCode ? row.warehouseCode + ' - ' : ''}${val}` : '—')
    },
    {
      field: 'totalLines',
      header: 'LINES',
      align: 'right',
      sortable: true
    },
    {
      field: 'totalQuantity',
      header: 'TOTAL QTY',
      align: 'right',
      sortable: true,
      format: val => Number(val || 0).toLocaleString()
    },
    {
      field: 'statusName',
      header: 'STATUS',
      type: 'badge',
      sortable: true,
      align: 'center',
      badgeSeverityMap: (_val, row) => getPutawayStatusSeverity(row.status)
    }
  ];

  readonly tableActions: DataTableAction<PutawayRecordDto>[] = [
    {
      id: 'view',
      label: 'View / Process',
      icon: 'pi pi-folder-open',
      action: row => this.router.navigate(['/app/inventory/store-putaway', row.putawayId])
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

    this.masterDataService.getWarehouses().subscribe({
      next: whs => {
        this.warehouses.set(whs || []);
      },
      error: () => {
        this.warehouses.set([]);
      }
    });

    this.putawayService.getPutaways().subscribe({
      next: records => {
        this.putaways.set(records || []);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err?.message || 'Failed to load putaway records.');
      }
    });
  }

  navigateToCreate(): void {
    this.router.navigate(['/app/inventory/store-putaway/new']);
  }

  canView(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.isPlatformStaff()) {
      return true;
    }
    return this.authService.hasPermission('WAREHOUSE.VIEW') || this.authService.hasPermission('PRODUCT.VIEW');
  }

  canCreate(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin()) {
      return true;
    }
    return this.authService.hasPermission('WAREHOUSE.MANAGE_LOCATIONS') || this.authService.hasPermission('PRODUCT.EDIT');
  }
}
