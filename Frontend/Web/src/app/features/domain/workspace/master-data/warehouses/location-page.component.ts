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
import { FormGroup, FormControl, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
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

export interface ParentLocationOption {
  value: string;
  code: string;
  name: string;
  typeName: string;
  zoneName: string;
  label: string;
}

@Component({
  selector: 'app-location-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
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
  readonly selectedLocationTypeId = toSignal(this.locationForm.controls.locationTypeId.valueChanges, { initialValue: null });

  // Search & Validation Signals for Parent Selector
  readonly parentSearchTerm = signal<string>('');
  readonly parentWarning = signal<string | null>(null);

  // Helper method to collect all descendant IDs of a location recursively (prevents circular hierarchies)
  private getDescendantIds(currentId: string, locations: WarehouseLocationItem[]): Set<string> {
    const descendants = new Set<string>();
    const queue = [currentId];
    while (queue.length > 0) {
      const parentId = queue.shift()!;
      descendants.add(parentId);
      for (const loc of locations) {
        const id = loc.warehouseLocationId ?? loc.locationId;
        if (loc.parentLocationId === parentId && !descendants.has(id)) {
          queue.push(id);
        }
      }
    }
    return descendants;
  }

  // Selected child location classification type item
  readonly currentChildType = computed<LocationTypeItem | null>(() => {
    const typeId = this.selectedLocationTypeId() ?? this.locationForm.controls.locationTypeId.value;
    if (!typeId) return null;
    return this.locationTypes().find(t => t.locationTypeId === typeId) ?? null;
  });

  // Hierarchy rules and expected parent metadata
  readonly expectedParentInfo = computed<{
    code: string | null;
    name: string;
    isTopLevel: boolean;
    isRequired: boolean;
    helperText: string;
  }>(() => {
    const type = this.currentChildType();
    if (!type) {
      return {
        code: null,
        name: 'Top-Level or Container',
        isTopLevel: false,
        isRequired: false,
        helperText: 'Select a structural classification type to determine eligible parent locations.'
      };
    }

    const code = type.code.trim().toUpperCase();
    if (code === 'RACK') {
      return {
        code: null,
        name: 'None (Top-Level)',
        isTopLevel: true,
        isRequired: false,
        helperText: 'Racks are top-level locations. No parent is required.'
      };
    }
    if (code === 'FLOOR') {
      return {
        code: null,
        name: 'None (Top-Level)',
        isTopLevel: true,
        isRequired: false,
        helperText: 'Floor staging areas are top-level locations. No parent is required.'
      };
    }
    if (code === 'STAGING') {
      return {
        code: null,
        name: 'None (Top-Level)',
        isTopLevel: true,
        isRequired: false,
        helperText: 'Staging bays are top-level locations. No parent is required.'
      };
    }
    if (code === 'SHELF') {
      return {
        code: 'RACK',
        name: 'RACK',
        isTopLevel: false,
        isRequired: true,
        helperText: 'Select a rack in the same warehouse and zone.'
      };
    }
    if (code === 'BIN') {
      return {
        code: 'SHELF',
        name: 'SHELF',
        isTopLevel: false,
        isRequired: true,
        helperText: 'Select a shelf in the same warehouse and zone.'
      };
    }
    // CUPBOARD or other
    return {
      code: null,
      name: 'Top-Level or Container',
      isTopLevel: false,
      isRequired: false,
      helperText: 'Select an active container in the same warehouse and zone, or choose Top-Level.'
    };
  });

  // Filtered parent candidates:
  // 1. Same warehouse
  // 2. Same zone
  // 3. Active status
  // 4. Type compatibility (RACK for SHELF, SHELF for BIN, top-level for RACK/FLOOR/STAGING)
  // 5. Exclude current location
  // 6. Exclude all descendants of current location (circular hierarchy prevention)
  // 7. Exclude locations with canContainChildren === false
  readonly parentCandidates = computed<WarehouseLocationItem[]>(() => {
    const parentRule = this.expectedParentInfo();

    // RACK, FLOOR, STAGING are top-level locations
    if (parentRule.isTopLevel) {
      return [];
    }

    const currentWhId = this.warehouseId();
    const currentLocId = this.locationId();
    const selectedZone = this.selectedWarehouseZoneId() ?? this.locationForm.controls.warehouseZoneId.value;

    const all = this.allLocations();
    const excludedIds = currentLocId ? this.getDescendantIds(currentLocId, all) : new Set<string>();

    return all.filter(loc => {
      const locId = loc.warehouseLocationId ?? loc.locationId;
      if (excludedIds.has(locId)) return false;

      // Must be active
      if (!loc.isActive) return false;

      // Must belong to the same warehouse
      if (currentWhId && loc.warehouseId && loc.warehouseId !== currentWhId) return false;

      // Must belong to the selected zone
      if (selectedZone) {
        const lZone = loc.warehouseZoneId ?? loc.zoneId;
        if (lZone !== selectedZone) return false;
      } else {
        // If unzoned child, parent must also be unzoned
        const lZone = loc.warehouseZoneId ?? loc.zoneId;
        if (lZone) return false;
      }

      // Must be able to contain child locations
      const pType = this.locationTypes().find(t => t.locationTypeId === loc.locationTypeId);
      if (pType && !pType.canContainChildren) return false;
      if (loc.canContainChildren === false) return false;

      const pTypeCode = (loc.locationTypeCode ?? pType?.code ?? '').trim().toUpperCase();

      // Rule: SHELF requires RACK, BIN requires SHELF
      if (parentRule.code) {
        return pTypeCode === parentRule.code || pTypeCode.startsWith(parentRule.code);
      }

      // For general types, cannot be BIN
      if (pTypeCode === 'BIN') return false;

      return true;
    });
  });

  // Formatted options with code, descriptive name, type pill, zone name, and type-ahead search
  readonly parentOptions = computed<ParentLocationOption[]>(() => {
    const candidates = this.parentCandidates();
    const search = this.parentSearchTerm().trim().toLowerCase();

    return candidates
      .map(p => {
        const type = this.locationTypes().find(t => t.locationTypeId === p.locationTypeId);
        const typeName = p.locationTypeCode || type?.code || type?.name || 'LOCATION';
        const zone = this.zones().find(z => z.warehouseZoneId === (p.warehouseZoneId ?? p.zoneId));
        const zoneName = p.zoneName || zone?.zoneName || (p.warehouseZoneId ? 'Zoned' : 'General Facility');
        const label = `${p.locationCode} — ${p.locationName} (${typeName}) · ${zoneName}`;
        const value = p.warehouseLocationId ?? p.locationId;

        return {
          value,
          code: p.locationCode,
          name: p.locationName,
          typeName,
          zoneName,
          label
        };
      })
      .filter(opt => {
        if (!search) return true;
        return (
          opt.code.toLowerCase().includes(search) ||
          opt.name.toLowerCase().includes(search) ||
          opt.typeName.toLowerCase().includes(search) ||
          opt.zoneName.toLowerCase().includes(search) ||
          opt.label.toLowerCase().includes(search)
        );
      });
  });

  // Selected parent item for display
  readonly selectedParentLocation = computed<WarehouseLocationItem | null>(() => {
    const pId = this.selectedParentLocationId() ?? this.locationForm.controls.parentLocationId.value;
    if (!pId) return null;
    return this.allLocations().find(l => (l.warehouseLocationId ?? l.locationId) === pId) ?? null;
  });

  readonly selectedParentOption = computed<ParentLocationOption | null>(() => {
    const loc = this.selectedParentLocation();
    if (!loc) return null;
    const type = this.locationTypes().find(t => t.locationTypeId === loc.locationTypeId);
    const typeName = loc.locationTypeCode || type?.code || type?.name || 'LOCATION';
    const zone = this.zones().find(z => z.warehouseZoneId === (loc.warehouseZoneId ?? loc.zoneId));
    const zoneName = loc.zoneName || zone?.zoneName || (loc.warehouseZoneId ? 'Zoned' : 'General Facility');
    return {
      value: loc.warehouseLocationId ?? loc.locationId,
      code: loc.locationCode,
      name: loc.locationName,
      typeName,
      zoneName,
      label: `${loc.locationCode} — ${loc.locationName} (${typeName}) · ${zoneName}`
    };
  });

  // Empty state guidance message
  readonly parentEmptyGuidance = computed<string | null>(() => {
    const rule = this.expectedParentInfo();
    if (rule.isTopLevel) return null;
    const candidates = this.parentCandidates();
    if (candidates.length > 0) return null;

    if (rule.code === 'RACK') {
      return 'No eligible rack locations are available in this zone. Create an active rack first.';
    }
    if (rule.code === 'SHELF') {
      return 'No eligible shelf locations are available in this zone. Create an active shelf first.';
    }
    return 'No compatible container locations are available in this zone. Create an active parent location first.';
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

    // Watch for Zone changes to validate and clear parent if no longer valid
    this.subs.add(
      this.locationForm.controls.warehouseZoneId.valueChanges.subscribe(() => {
        this.validateAndClearParentIfNeeded('zone');
      })
    );

    // Watch for Location Type changes to offer recommended capability presets and validate parent
    this.subs.add(
      this.locationForm.controls.locationTypeId.valueChanges.subscribe(typeId => {
        if (!typeId) {
          this.validateAndClearParentIfNeeded('type');
          return;
        }
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
        this.validateAndClearParentIfNeeded('type');
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  validateAndClearParentIfNeeded(trigger: 'zone' | 'type'): void {
    const currentParentId = this.locationForm.controls.parentLocationId.value;
    if (!currentParentId) {
      this.parentWarning.set(null);
      return;
    }

    const validCandidates = this.parentCandidates();
    const isValid = validCandidates.some(c => (c.warehouseLocationId ?? c.locationId) === currentParentId);
    if (!isValid) {
      this.locationForm.controls.parentLocationId.setValue(null);
      const reason = trigger === 'zone' ? 'the selected warehouse zone' : 'the selected classification type';
      this.parentWarning.set(`Previous parent selection was cleared because it is no longer compatible with ${reason}.`);
    } else {
      this.parentWarning.set(null);
    }
  }

  clearParentSelection(): void {
    this.locationForm.controls.parentLocationId.setValue(null);
    this.parentWarning.set(null);
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
    this.parentSearchTerm.set('');
    this.parentWarning.set(null);
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
          // Preselect first location type default (prefer RACK for top-level or first type)
          const defaultType = res.types.find(t => t.code === 'RACK') || res.types.find(t => t.code === 'BIN') || res.types[0];
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

    const raw = this.locationForm.getRawValue();
    const typeInfo = this.expectedParentInfo();

    // Enforce required parent when candidates exist
    if (typeInfo.isRequired && !raw.parentLocationId && this.parentCandidates().length > 0) {
      this.locationForm.controls.parentLocationId.markAsTouched();
      const msg = typeInfo.code === 'SHELF'
        ? 'A bin must be placed under a shelf.'
        : 'Select a rack in the selected warehouse and zone.';
      this.message.set({ text: msg, severity: 'warn' });
      return;
    }

    this.saving.set(true);
    this.serverErrors.set({});
    this.message.set(null);

    const effectiveParentId = typeInfo.isTopLevel ? null : (raw.parentLocationId || null);

    if (this.mode() === 'create') {
      const dto: CreateWarehouseLocationDto = {
        locationCode: raw.locationCode.trim().toUpperCase(),
        locationName: raw.locationName.trim(),
        warehouseZoneId: raw.warehouseZoneId || null,
        locationTypeId: raw.locationTypeId || null,
        parentLocationId: effectiveParentId
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
        parentLocationId: effectiveParentId
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
