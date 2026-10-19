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
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { WarehouseItem, WarehouseLocationItem } from '../../../../../core/models/scope.models';
import { DataTableColumn, DataTableAction } from '../../../../../shared/models/data-table-config.model';

export interface LocationGridRow {
  locationId: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  locationCode: string;
  locationName: string;
  isReceivingLocation: boolean;
  isPickingLocation: boolean;
  isActive: boolean;
}

@Component({
  selector: 'app-store-putaway',
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
  templateUrl: './store-putaway.component.html',
  styleUrl: './store-putaway.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StorePutawayComponent implements OnInit, OnDestroy {
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);

  private readonly subs = new Subscription();

  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to access warehouse storage and putaway locations."
  );

  readonly warehouses = signal<WarehouseItem[]>([]);
  readonly selectedWarehouseFilter = signal<string>('all');
  readonly searchTerm = signal<string>('');
  readonly locationRows = signal<LocationGridRow[]>([]);

  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Inventory', routerLink: '/app/inventory' },
    { label: 'Storage & Putaway' }
  ];

  readonly totalWarehouses = computed(() => this.warehouses().length);
  readonly totalLocations = computed(() => this.locationRows().length);
  readonly receivingLocationsCount = computed(() =>
    this.locationRows().filter(l => l.isReceivingLocation).length
  );
  readonly pickingLocationsCount = computed(() =>
    this.locationRows().filter(l => l.isPickingLocation).length
  );

  readonly filteredLocations = computed<LocationGridRow[]>(() => {
    let rows = [...this.locationRows()];
    const whFilter = this.selectedWarehouseFilter();
    if (whFilter !== 'all') {
      rows = rows.filter(r => r.warehouseId === whFilter);
    }
    const term = this.searchTerm().trim().toLowerCase();
    if (term) {
      rows = rows.filter(
        r =>
          r.locationCode.toLowerCase().includes(term) ||
          r.locationName.toLowerCase().includes(term) ||
          r.warehouseName.toLowerCase().includes(term) ||
          r.warehouseCode.toLowerCase().includes(term)
      );
    }
    return rows;
  });

  readonly columns: DataTableColumn<LocationGridRow>[] = [
    {
      field: 'warehouseName',
      header: 'WAREHOUSE FACILITY',
      sortable: true,
      format: (_, row) => `${row.warehouseCode} - ${row.warehouseName}`
    },
    {
      field: 'locationCode',
      header: 'BIN / LOCATION CODE',
      sortable: true
    },
    {
      field: 'locationName',
      header: 'LOCATION NAME',
      sortable: true
    },
    {
      field: 'isReceivingLocation',
      header: 'RECEIVING STAGING',
      type: 'badge',
      sortable: true,
      align: 'center',
      format: val => (val ? 'Receiving Staging' : 'Standard'),
      badgeSeverityMap: val => (val ? 'info' : 'neutral')
    },
    {
      field: 'isPickingLocation',
      header: 'PICKING BIN',
      type: 'badge',
      sortable: true,
      align: 'center',
      format: val => (val ? 'Primary Picking' : 'Storage'),
      badgeSeverityMap: val => (val ? 'success' : 'neutral')
    },
    {
      field: 'isActive',
      header: 'STATUS',
      type: 'badge',
      sortable: true,
      align: 'center',
      format: val => (val ? 'Active' : 'Inactive'),
      badgeSeverityMap: val => (val ? 'success' : 'danger')
    }
  ];

  readonly tableActions: DataTableAction<LocationGridRow>[] = [
    {
      id: 'view',
      label: 'Manage Bins',
      icon: 'pi pi-folder-open',
      action: () => this.router.navigate(['/app/master-data'])
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
        const activeWhs = whs || [];
        this.warehouses.set(activeWhs);

        const rows: LocationGridRow[] = [];
        let completed = 0;
        if (activeWhs.length === 0) {
          this.locationRows.set([]);
          this.loading.set(false);
          return;
        }

        activeWhs.forEach(wh => {
          this.masterDataService.getWarehouseLocations(wh.warehouseId).subscribe({
            next: locs => {
              (locs || []).forEach(l => {
                rows.push({
                  locationId: l.locationId,
                  warehouseId: wh.warehouseId,
                  warehouseCode: wh.warehouseCode,
                  warehouseName: wh.warehouseName,
                  locationCode: l.locationCode,
                  locationName: l.locationName,
                  isReceivingLocation: !!l.isReceivingLocation,
                  isPickingLocation: !!l.isPickingLocation,
                  isActive: l.isActive
                });
              });
              completed++;
              if (completed === activeWhs.length) {
                this.locationRows.set(rows);
                this.loading.set(false);
              }
            },
            error: () => {
              completed++;
              if (completed === activeWhs.length) {
                this.locationRows.set(rows);
                this.loading.set(false);
              }
            }
          });
        });
      },
      error: err => {
        this.loading.set(false);
        if (err?.status === 403) {
          this.isAccessDenied.set(true);
        } else {
          this.error.set(err?.message || 'Failed to load warehouse storage data.');
        }
      }
    });
  }

  canView(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.isPlatformStaff()) {
      return true;
    }
    return this.authService.hasPermission('WAREHOUSE.VIEW') || this.authService.hasPermission('PRODUCT.VIEW');
  }
}
