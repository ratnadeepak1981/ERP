import { Component, OnInit, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router, ActivatedRoute } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppStatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AppEmptyStateComponent } from '../../../../../shared/components/empty-state/empty-state.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import {
  WarehouseItem,
  WarehouseZoneItem,
  WarehouseLocationItem,
  LocationTypeItem,
  CreateWarehouseDto,
  CreateWarehouseZoneDto,
  CreateWarehouseLocationDto
} from '../../../../../core/models/scope.models';

@Component({
  selector: 'app-warehouse-master',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    ButtonModule,
    InputTextModule,
    TooltipModule,
    AppPageHeaderComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AppEmptyStateComponent
  ],
  templateUrl: './warehouse-master.component.html',
  styleUrl: './warehouse-master.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class WarehouseMasterComponent implements OnInit {
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);
  readonly route = inject(ActivatedRoute);

  // Loading & Global Status
  readonly loading = signal<boolean>(true);
  readonly locationsLoading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  // Active view tab: 'locations' | 'zones' | 'warehouses' | 'policies'
  readonly activeTab = signal<'locations' | 'zones' | 'warehouses' | 'policies'>('locations');

  // Breadcrumbs
  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Supply Chain Masters', routerLink: '/app/master-data' },
    { label: 'Warehouses & Storage Locations' }
  ];

  // Master Data collections
  readonly warehouses = signal<WarehouseItem[]>([]);
  readonly selectedWarehouseId = signal<string | null>(null);
  readonly zones = signal<WarehouseZoneItem[]>([]);
  readonly locations = signal<WarehouseLocationItem[]>([]);
  readonly locationTypes = signal<LocationTypeItem[]>([]);

  // Selected warehouse computed
  readonly selectedWarehouse = computed<WarehouseItem | null>(() => {
    const id = this.selectedWarehouseId();
    if (!id) return null;
    return this.warehouses().find(w => w.warehouseId === id) ?? null;
  });

  // Filter signals for Locations
  readonly selectedZoneFilter = signal<string>('all');
  readonly locationSearch = signal<string>('');
  readonly locationStatusFilter = signal<'all' | 'active' | 'inactive'>('active');
  readonly locationCapabilityFilter = signal<'all' | 'store' | 'pick' | 'putaway'>('all');

  // Filter signals for Zones
  readonly zoneSearch = signal<string>('');

  // Modals visibility (kept for backward-compatibility aliases)
  readonly showCreateWarehouseModal = signal<boolean>(false);
  readonly showEditWarehouseModal = signal<boolean>(false);
  readonly showCreateZoneModal = signal<boolean>(false);
  readonly showEditZoneModal = signal<boolean>(false);
  readonly showCreateLocationModal = signal<boolean>(false);
  readonly showEditLocationModal = signal<boolean>(false);
  readonly modalError = signal<string | null>(null);

  // Backward compatibility state
  editingWarehouseId: string | null = null;
  editingZoneId: string | null = null;
  editingLocationId: string | null = null;
  warehouseForm: CreateWarehouseDto = { warehouseCode: '', warehouseName: '', city: '' };
  zoneForm: CreateWarehouseZoneDto = { zoneCode: '', zoneName: '' };
  locationForm: CreateWarehouseLocationDto = { locationCode: '', locationName: '' };

  // Computed filtered locations
  readonly filteredLocations = computed<WarehouseLocationItem[]>(() => {
    let list = this.locations();

    // Zone filter
    const zoneFilter = this.selectedZoneFilter();
    if (zoneFilter !== 'all') {
      if (zoneFilter === 'unzoned') {
        list = list.filter(l => !l.warehouseZoneId && !l.zoneId);
      } else {
        list = list.filter(l => (l.warehouseZoneId ?? l.zoneId) === zoneFilter);
      }
    }

    // Status filter
    const status = this.locationStatusFilter();
    if (status === 'active') {
      list = list.filter(l => l.isActive);
    } else if (status === 'inactive') {
      list = list.filter(l => !l.isActive);
    }

    // Capability filter
    const cap = this.locationCapabilityFilter();
    if (cap === 'store') {
      list = list.filter(l => l.canStoreInventory ?? true);
    } else if (cap === 'pick') {
      list = list.filter(l => l.canPick ?? true);
    } else if (cap === 'putaway') {
      list = list.filter(l => l.canPutAway ?? true);
    }

    // Search query
    const q = this.locationSearch().trim().toLowerCase();
    if (q) {
      list = list.filter(l =>
        l.locationCode.toLowerCase().includes(q) ||
        l.locationName.toLowerCase().includes(q) ||
        (l.hierarchyPath && l.hierarchyPath.toLowerCase().includes(q)) ||
        (l.zoneName && l.zoneName.toLowerCase().includes(q)) ||
        (l.locationTypeName && l.locationTypeName.toLowerCase().includes(q))
      );
    }

    return list;
  });

  // Computed filtered zones
  readonly filteredZones = computed<WarehouseZoneItem[]>(() => {
    let list = this.zones();
    const q = this.zoneSearch().trim().toLowerCase();
    if (q) {
      list = list.filter(z =>
        z.zoneCode.toLowerCase().includes(q) ||
        z.zoneName.toLowerCase().includes(q) ||
        (z.description && z.description.toLowerCase().includes(q))
      );
    }
    return list;
  });

  // Valid parent candidates for location form
  readonly parentLocationCandidates = computed<WarehouseLocationItem[]>(() => {
    const all = this.locations().filter(l => l.isActive);
    const editingId = this.editingLocationId;
    if (!editingId) return all;
    return all.filter(l => (l.warehouseLocationId ?? l.locationId) !== editingId);
  });

  // Permission checks
  readonly canCreateWarehouse = computed(() =>
    this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.hasPermission('WAREHOUSE.CREATE')
  );
  readonly canEditWarehouse = computed(() =>
    this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.hasPermission('WAREHOUSE.EDIT') || this.authService.hasPermission('WAREHOUSE.UPDATE')
  );
  readonly canDeleteWarehouse = computed(() =>
    this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.hasPermission('WAREHOUSE.DELETE')
  );
  readonly canManageZones = computed(() =>
    this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.hasPermission('WAREHOUSE.CREATE') || this.authService.hasPermission('WAREHOUSE.EDIT') || this.authService.hasPermission('WAREHOUSE.MANAGE_ZONES')
  );
  readonly canCreateLocation = computed(() =>
    this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.hasPermission('LOCATION.CREATE')
  );
  readonly canEditLocation = computed(() =>
    this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.hasPermission('LOCATION.EDIT') || this.authService.hasPermission('LOCATION.UPDATE')
  );
  readonly canDeleteLocation = computed(() =>
    this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.hasPermission('LOCATION.DELETE')
  );

  ngOnInit(): void {
    const qParams = this.route.snapshot.queryParamMap;
    const initialWhId = qParams.get('warehouseId');
    const initialTab = qParams.get('tab');

    if (initialTab && ['locations', 'zones', 'warehouses', 'policies'].includes(initialTab)) {
      this.activeTab.set(initialTab as any);
    }
    if (initialWhId) {
      this.selectedWarehouseId.set(initialWhId);
    }

    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.error.set(null);
    this.actionError.set(null);

    this.masterDataService.getWarehouses().subscribe({
      next: (whs) => {
        this.warehouses.set(whs);
        if (whs.length > 0) {
          const currentId = this.selectedWarehouseId();
          const target = (currentId && whs.some(w => w.warehouseId === currentId))
            ? currentId
            : whs[0].warehouseId;
          this.selectWarehouse(target);
        } else {
          this.selectedWarehouseId.set(null);
          this.zones.set([]);
          this.locations.set([]);
          this.loading.set(false);
        }
      },
      error: (err) => {
        this.error.set(err?.error?.message || 'Failed to load warehouses from backend.');
        this.loading.set(false);
      }
    });

    this.masterDataService.getLocationTypes().subscribe({
      next: (types) => this.locationTypes.set(types),
      error: () => {}
    });
  }

  selectWarehouse(warehouseId: string): void {
    this.selectedWarehouseId.set(warehouseId);
    this.selectedZoneFilter.set('all');
    this.loadZonesAndLocations(warehouseId);
  }

  loadZonesAndLocations(warehouseId: string): void {
    this.locationsLoading.set(true);
    this.actionError.set(null);

    this.masterDataService.getWarehouseZones(warehouseId).subscribe({
      next: (zones) => {
        this.zones.set(zones);
      },
      error: () => {}
    });

    this.masterDataService.getWarehouseLocations(warehouseId, null, true).subscribe({
      next: (locs) => {
        this.locations.set(locs);
        this.locationsLoading.set(false);
        this.loading.set(false);
      },
      error: (err) => {
        this.actionError.set(err?.error?.message || 'Failed to load storage locations for warehouse.');
        this.locationsLoading.set(false);
        this.loading.set(false);
      }
    });
  }

  // --- Dedicated Page Navigation (Refactored from Dialog Modals) ---
  navigateToCreateWarehouse(): void {
    this.router.navigate(['/app/master-data/warehouses/new']);
  }

  navigateToEditWarehouse(warehouse: WarehouseItem): void {
    this.router.navigate(['/app/master-data/warehouses', warehouse.warehouseId, 'edit']);
  }

  navigateToCreateZone(): void {
    const whId = this.selectedWarehouseId();
    if (!whId) return;
    this.router.navigate(['/app/master-data/warehouses', whId, 'zones', 'new']);
  }

  navigateToEditZone(zone: WarehouseZoneItem): void {
    const whId = this.selectedWarehouseId();
    if (!whId) return;
    this.router.navigate(['/app/master-data/warehouses', whId, 'zones', zone.warehouseZoneId, 'edit']);
  }

  navigateToCreateLocation(): void {
    const whId = this.selectedWarehouseId();
    if (!whId) return;
    const activeZone = this.selectedZoneFilter();
    const queryParams: Record<string, string> = {};
    if (activeZone !== 'all' && activeZone !== 'unzoned') {
      queryParams['zoneId'] = activeZone;
    }
    this.router.navigate(['/app/master-data/warehouses', whId, 'locations', 'new'], { queryParams });
  }

  navigateToEditLocation(location: WarehouseLocationItem): void {
    const locId = location.warehouseLocationId ?? location.locationId;
    const whId = this.selectedWarehouseId();
    if (!whId || !locId) return;
    this.router.navigate(['/app/master-data/warehouses', whId, 'locations', locId, 'edit']);
  }

  // Backward-compatibility aliases
  openCreateWarehouseModal(): void {
    this.navigateToCreateWarehouse();
  }

  openEditWarehouseModal(warehouse: WarehouseItem): void {
    this.navigateToEditWarehouse(warehouse);
  }

  openCreateZoneModal(): void {
    this.navigateToCreateZone();
  }

  openEditZoneModal(zone: WarehouseZoneItem): void {
    this.navigateToEditZone(zone);
  }

  openCreateLocationModal(): void {
    this.navigateToCreateLocation();
  }

  openEditLocationModal(location: WarehouseLocationItem): void {
    this.navigateToEditLocation(location);
  }

  // --- Deactivation Operations (Real API Safeguards) ---
  deactivateWarehouse(warehouse: WarehouseItem): void {
    if (!confirm(`Are you sure you want to deactivate warehouse "${warehouse.warehouseName}" (${warehouse.warehouseCode})?`)) {
      return;
    }

    this.actionError.set(null);
    this.masterDataService.deactivateWarehouse(warehouse.warehouseId).subscribe({
      next: () => {
        this.showSuccess(`Warehouse ${warehouse.warehouseCode} deactivated successfully.`);
        this.loadData();
      },
      error: (err) => {
        this.actionError.set(err?.error?.message || 'Cannot deactivate warehouse: active zones or locations exist.');
      }
    });
  }

  deactivateZone(zone: WarehouseZoneItem): void {
    const whId = this.selectedWarehouseId();
    if (!whId) return;

    if (!confirm(`Are you sure you want to deactivate zone "${zone.zoneName}" (${zone.zoneCode})?`)) {
      return;
    }

    this.actionError.set(null);
    this.masterDataService.deactivateWarehouseZone(whId, zone.warehouseZoneId).subscribe({
      next: () => {
        this.showSuccess(`Zone ${zone.zoneCode} deactivated successfully.`);
        this.loadZonesAndLocations(whId);
      },
      error: (err) => {
        this.actionError.set(err?.error?.message || 'Cannot deactivate zone: active storage locations are assigned to it.');
      }
    });
  }

  deactivateLocation(location: WarehouseLocationItem): void {
    const locId = location.warehouseLocationId ?? location.locationId;
    if (!locId) return;
    const whId = this.selectedWarehouseId();
    if (!whId) return;

    if (!confirm(`Are you sure you want to deactivate location "${location.locationName}" (${location.locationCode})?`)) {
      return;
    }

    this.actionError.set(null);
    this.masterDataService.deactivateWarehouseLocation(locId).subscribe({
      next: () => {
        this.showSuccess(`Location ${location.locationCode} deactivated successfully.`);
        this.loadZonesAndLocations(whId);
      },
      error: (err) => {
        this.actionError.set(err?.error?.message || 'Cannot deactivate location: it currently has active child locations.');
      }
    });
  }

  getLocationsCountInZone(zoneId: string): number {
    return this.locations().filter(l => (l.warehouseZoneId ?? l.zoneId) === zoneId && l.isActive).length;
  }

  private showSuccess(msg: string): void {
    this.successMessage.set(msg);
    setTimeout(() => this.successMessage.set(null), 4000);
  }
}
