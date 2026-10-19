import {
  Component,
  OnInit,
  OnDestroy,
  inject,
  signal,
  computed,
  ChangeDetectionStrategy
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormGroup, FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Subscription, forkJoin, of } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { CheckboxModule } from 'primeng/checkbox';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppStatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import {
  WarehouseItem,
  WarehouseZoneItem,
  WarehouseLocationItem,
  LocationTypeItem,
  CreateWarehouseLocationDto,
  UpdateWarehouseLocationDto
} from '../../../../../core/models/scope.models';

export type LocationPageMode = 'create' | 'edit' | 'view';

@Component({
  selector: 'app-location-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    ButtonModule,
    SelectModule,
    CheckboxModule,
    AppPageHeaderComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent
  ],
  templateUrl: './location-page.component.html',
  styleUrl: './location-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LocationPageComponent implements OnInit, OnDestroy {
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly router = inject(Router);
  readonly route = inject(ActivatedRoute);

  private readonly subs = new Subscription();

  // Mode and Identity
  readonly mode = signal<LocationPageMode>('create');
  readonly warehouseId = signal<string | null>(null);
  readonly locationId = signal<string | null>(null);
  readonly currentWarehouse = signal<WarehouseItem | null>(null);
  readonly currentLocation = signal<WarehouseLocationItem | null>(null);

  // Collections for Selectors
  readonly zones = signal<WarehouseZoneItem[]>([]);
  readonly locationTypes = signal<LocationTypeItem[]>([]);
  readonly allLocations = signal<WarehouseLocationItem[]>([]);

  // Page State
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly serverErrors = signal<Record<string, string>>({});
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Reactive Form
  readonly locationForm = new FormGroup({
    locationCode: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(1), Validators.maxLength(30)]
    }),
    locationName: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(1), Validators.maxLength(100)]
    }),
    warehouseZoneId: new FormControl<string | null>(null),
    locationTypeId: new FormControl<string | null>(null),
    parentLocationId: new FormControl<string | null>(null),
    canStoreInventory: new FormControl<boolean>(true, { nonNullable: true }),
    canPick: new FormControl<boolean>(true, { nonNullable: true }),
    canPutAway: new FormControl<boolean>(true, { nonNullable: true }),
    isActive: new FormControl<boolean>(true, { nonNullable: true })
  });

  // Reactive handles for form inputs
  readonly enteredLocationCode = toSignal(this.locationForm.controls.locationCode.valueChanges, { initialValue: '' });
  readonly selectedParentLocationId = toSignal(this.locationForm.controls.parentLocationId.valueChanges, { initialValue: null });
  readonly selectedWarehouseZoneId = toSignal(this.locationForm.controls.warehouseZoneId.valueChanges, { initialValue: null });

  // Filtered parent candidates (prevents selecting oneself as parent)
  readonly parentCandidates = computed<WarehouseLocationItem[]>(() => {
    const locId = this.locationId();
    const list = this.allLocations().filter(l => l.isActive);
    if (!locId) return list;
    return list.filter(l => (l.warehouseLocationId ?? l.locationId) !== locId);
  });

  // Predicted live hierarchy path
  readonly hierarchyPathPreview = computed<string>(() => {
    const code = (this.enteredLocationCode() ?? this.locationForm.controls.locationCode.value)?.trim();
    const parentId = this.selectedParentLocationId() ?? this.locationForm.controls.parentLocationId.value;
    const parent = parentId ? this.allLocations().find(l => (l.warehouseLocationId ?? l.locationId) === parentId) : null;

    const parts: string[] = [];
    if (parent) {
      if (parent.hierarchyPath) {
        parts.push(parent.hierarchyPath);
      } else {
        parts.push(parent.locationCode);
      }
    }
    if (code) {
      parts.push(code);
    } else {
      parts.push('...');
    }

    return parts.join(' > ');
  });

  // Selected Zone Display
  readonly selectedZoneName = computed<string>(() => {
    const zid = this.selectedWarehouseZoneId() ?? this.locationForm.controls.warehouseZoneId.value;
    if (!zid) return 'Direct / Unzoned';
    const match = this.zones().find(z => z.warehouseZoneId === zid);
    return match ? `${match.zoneCode} — ${match.zoneName}` : 'Unassigned';
  });

  // Breadcrumbs
  readonly breadcrumbs = computed(() => {
    const wh = this.currentWarehouse();
    const whLabel = wh ? `${wh.warehouseCode} (${wh.warehouseName})` : 'Warehouse';
    const whId = this.warehouseId();

    const list = [
      { label: 'Workspace', routerLink: '/app/dashboard' },
      { label: 'Supply Chain Masters', routerLink: '/app/master-data' },
      {
        label: 'Warehouses & Storage Locations',
        routerLink: '/app/master-data/warehouses'
      }
    ];

    if (whId) {
      list.push({
        label: whLabel,
        routerLink: `/app/master-data/warehouses?warehouseId=${whId}&tab=locations`
      });
    }

    if (this.mode() === 'create') {
      list.push({ label: 'New Location', routerLink: '' });
    } else {
      const code = this.currentLocation()?.locationCode || 'Location Profile';
      list.push({ label: code, routerLink: '' });
    }
    return list;
  });

  // Page Title & Subtitle
  readonly pageTitle = computed(() => {
    if (this.mode() === 'create') {
      return 'Register Storage Location';
    }
    const loc = this.currentLocation();
    return loc ? `Edit Location — ${loc.locationCode} (${loc.locationName})` : 'Edit Storage Location';
  });

  readonly pageSubtitle = computed(() => {
    const whName = this.currentWarehouse()?.warehouseName || 'warehouse facility';
    if (this.mode() === 'create') {
      return `Configure a multi-level inventory storage location within "${whName}". Specify zone assignment, structural hierarchy, and operational capability flags.`;
    }
    return `Update storage location details, classification type, parent positioning, and picking/putaway rules in "${whName}".`;
  });

  ngOnInit(): void {
    this.subs.add(
      this.route.paramMap.subscribe(params => {
        const whId = params.get('warehouseId');
        const lId = params.get('locationId');
        this.warehouseId.set(whId);
        this.locationId.set(lId);

        const url = this.route.snapshot.url.map(s => s.path).join('/');
        if (url.includes('new')) {
          this.mode.set('create');
          const preselectedZone = this.route.snapshot.queryParamMap.get('zoneId');
          this.initCreateForm(preselectedZone);
        } else if (url.includes('edit')) {
          this.mode.set('edit');
        } else {
          this.mode.set('view');
        }

        if (whId) {
          this.loadData(whId, lId);
        }
      })
    );

    // Watch for Location Type changes to offer recommended capability presets
    this.subs.add(
      this.locationForm.controls.locationTypeId.valueChanges.subscribe(typeId => {
        if (!typeId) return;
        const matched = this.locationTypes().find(t => t.locationTypeId === typeId);
        if (matched) {
          // If in create mode or pristine capability, apply standard type policy defaults
          if (this.mode() === 'create') {
            this.locationForm.patchValue({
              canStoreInventory: matched.canStoreInventory,
              canPick: matched.canPick,
              canPutAway: matched.canPutAway
            }, { emitEvent: false });
          }
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  private initCreateForm(preselectedZoneId?: string | null): void {
    this.locationForm.reset({
      locationCode: '',
      locationName: '',
      warehouseZoneId: preselectedZoneId && preselectedZoneId !== 'all' && preselectedZoneId !== 'unzoned' ? preselectedZoneId : null,
      locationTypeId: null,
      parentLocationId: null,
      canStoreInventory: true,
      canPick: true,
      canPutAway: true,
      isActive: true
    });
    this.serverErrors.set({});
  }

  loadData(whId: string, lId: string | null): void {
    this.loading.set(true);
    this.error.set(null);

    const observables = {
      warehouse: this.masterDataService.getWarehouse(whId),
      zones: this.masterDataService.getWarehouseZones(whId),
      types: this.masterDataService.getLocationTypes(),
      locations: this.masterDataService.getWarehouseLocations(whId, null, true),
      location: lId ? this.masterDataService.getWarehouseLocation(lId, whId) : of<WarehouseLocationItem | null>(null)
    };

    forkJoin(observables).subscribe({
      next: (res) => {
        this.loading.set(false);
        this.currentWarehouse.set(res.warehouse);
        this.zones.set(res.zones);
        this.locationTypes.set(res.types);
        this.allLocations.set(res.locations);

        if (this.mode() === 'create' && !this.locationForm.controls.locationTypeId.value && res.types.length > 0) {
          // Preselect first location type default (e.g. Rack or Bin)
          const defaultType = res.types.find(t => t.code === 'BIN') || res.types[0];
          this.locationForm.patchValue({ locationTypeId: defaultType.locationTypeId });
        }

        if (lId) {
          const loc = res.location || res.locations.find(l => (l.warehouseLocationId ?? l.locationId) === lId);
          if (!loc) {
            this.error.set(`Storage location with ID "${lId}" was not found.`);
            return;
          }
          this.currentLocation.set(loc);
          this.locationForm.patchValue({
            locationCode: loc.locationCode,
            locationName: loc.locationName,
            warehouseZoneId: loc.warehouseZoneId ?? loc.zoneId ?? null,
            locationTypeId: loc.locationTypeId ?? null,
            parentLocationId: loc.parentLocationId ?? null,
            canStoreInventory: loc.canStoreInventory ?? true,
            canPick: loc.canPick ?? true,
            canPutAway: loc.canPutAway ?? true,
            isActive: loc.isActive
          });
          this.locationForm.get('locationCode')?.disable();
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.message || 'Failed to load location metadata from server.');
      }
    });
  }

  saveLocation(): void {
    if (this.mode() === 'view') {
      this.mode.set('edit');
      return;
    }

    const whId = this.warehouseId();
    if (!whId) {
      this.message.set({ text: 'Warehouse identifier is missing from route.', severity: 'danger' });
      return;
    }

    if (this.locationForm.invalid) {
      this.locationForm.markAllAsTouched();
      this.message.set({
        text: 'Please correct the validation errors marked on the form before saving.',
        severity: 'warn'
      });
      return;
    }

    this.saving.set(true);
    this.serverErrors.set({});
    this.message.set(null);

    const raw = this.locationForm.getRawValue();

    if (this.mode() === 'create') {
      const dto: CreateWarehouseLocationDto = {
        locationCode: raw.locationCode.trim().toUpperCase(),
        locationName: raw.locationName.trim(),
        warehouseZoneId: raw.warehouseZoneId || null,
        locationTypeId: raw.locationTypeId || null,
        parentLocationId: raw.parentLocationId || null
      };

      this.masterDataService.createWarehouseLocation(whId, dto).subscribe({
        next: (created) => {
          this.saving.set(false);
          this.router.navigate(['/app/master-data/warehouses'], {
            queryParams: { warehouseId: whId, tab: 'locations' }
          });
        },
        error: (err) => {
          this.saving.set(false);
          this.handleApiError(err, 'Failed to create storage location.');
        }
      });
    } else {
      const locId = this.locationId()!;
      const dto: UpdateWarehouseLocationDto = {
        locationName: raw.locationName.trim(),
        locationTypeId: raw.locationTypeId || null,
        parentLocationId: raw.parentLocationId || null
      };

      this.masterDataService.updateWarehouseLocation(locId, dto).subscribe({
        next: () => {
          this.saving.set(false);
          this.router.navigate(['/app/master-data/warehouses'], {
            queryParams: { warehouseId: whId, tab: 'locations' }
          });
        },
        error: (err) => {
          this.saving.set(false);
          this.handleApiError(err, 'Failed to update storage location.');
        }
      });
    }
  }

  navigateBack(): void {
    const whId = this.warehouseId();
    if (whId) {
      this.router.navigate(['/app/master-data/warehouses'], {
        queryParams: { warehouseId: whId, tab: 'locations' }
      });
    } else {
      this.router.navigate(['/app/master-data/warehouses']);
    }
  }

  private handleApiError(err: any, fallback: string): void {
    const msg = err?.error?.message || err?.message || fallback;
    this.message.set({ text: msg, severity: 'danger' });

    if (err?.error?.errors && typeof err.error.errors === 'object') {
      const errMap: Record<string, string> = {};
      for (const [key, value] of Object.entries(err.error.errors)) {
        errMap[key.charAt(0).toLowerCase() + key.slice(1)] = Array.isArray(value) ? value.join(', ') : String(value);
      }
      this.serverErrors.set(errMap);
    }
  }
}
