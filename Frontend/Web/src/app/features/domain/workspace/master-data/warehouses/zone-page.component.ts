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
import { FormGroup, FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppReactiveFormComponent } from '../../../../../shared/components/forms/reactive-form.component';
import { FormFieldConfig } from '../../../../../shared/components/forms/form-field-config.model';
import { AppStatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import {
  WarehouseItem,
  WarehouseZoneItem,
  CreateWarehouseZoneDto,
  UpdateWarehouseZoneDto
} from '../../../../../core/models/scope.models';

export type ZonePageMode = 'create' | 'edit' | 'view';

@Component({
  selector: 'app-zone-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    ButtonModule,
    AppPageHeaderComponent,
    AppReactiveFormComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent
  ],
  templateUrl: './zone-page.component.html',
  styleUrl: './zone-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ZonePageComponent implements OnInit, OnDestroy {
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly router = inject(Router);
  readonly route = inject(ActivatedRoute);

  private readonly subs = new Subscription();

  // Mode and Identity
  readonly mode = signal<ZonePageMode>('create');
  readonly warehouseId = signal<string | null>(null);
  readonly zoneId = signal<string | null>(null);
  readonly currentWarehouse = signal<WarehouseItem | null>(null);
  readonly currentZone = signal<WarehouseZoneItem | null>(null);

  // Page State
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly serverErrors = signal<Record<string, string>>({});
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Reactive Form
  readonly zoneForm = new FormGroup({
    zoneCode: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(2), Validators.maxLength(20)]
    }),
    zoneName: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(2), Validators.maxLength(100)]
    }),
    description: new FormControl<string>('', { nonNullable: true }),
    isActive: new FormControl<boolean>(true, { nonNullable: true })
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
        routerLink: `/app/master-data/warehouses?warehouseId=${whId}&tab=zones`
      });
    }

    if (this.mode() === 'create') {
      list.push({ label: 'New Zone', routerLink: '' });
    } else {
      const code = this.currentZone()?.zoneCode || 'Zone Profile';
      list.push({ label: code, routerLink: '' });
    }
    return list;
  });

  // Page Title & Subtitle
  readonly pageTitle = computed(() => {
    if (this.mode() === 'create') {
      return 'Register Warehouse Zone';
    }
    const zone = this.currentZone();
    return zone ? `Edit Zone — ${zone.zoneCode} (${zone.zoneName})` : 'Edit Warehouse Zone';
  });

  readonly pageSubtitle = computed(() => {
    const whName = this.currentWarehouse()?.warehouseName || 'warehouse facility';
    if (this.mode() === 'create') {
      return `Define a logical operational zone within "${whName}" (e.g., Bulk Pallet Storage, Fast-Pick Area, Quarantine, Receiving Bay).`;
    }
    return `Update zone name, operational purpose, and active availability in "${whName}".`;
  });

  // Form Fields - Section 1: Zone Specification
  readonly zoneFields = computed<FormFieldConfig[]>(() => {
    const isEdit = this.mode() !== 'create';
    const isView = this.mode() === 'view';

    return [
      {
        name: 'zoneCode',
        label: 'Zone Code',
        type: 'text',
        required: true,
        placeholder: 'e.g. BULK, PICK, QC, STAGE',
        readonly: isEdit || isView,
        tooltip: 'Unique code identifying this zone within the warehouse facility (immutable once created)',
        colSpan: 1
      },
      {
        name: 'zoneName',
        label: 'Zone Name',
        type: 'text',
        required: true,
        placeholder: 'e.g. Bulk Pallet Racking Zone',
        disabled: isView,
        colSpan: 2
      },
      {
        name: 'description',
        label: 'Operational Purpose & Description',
        type: 'textarea',
        placeholder: 'e.g. High-density pallet storage area for heavy pallets and bulk reserves.',
        disabled: isView,
        colSpan: 3
      }
    ];
  });

  // Form Fields - Section 2: Lifecycle Status
  readonly statusFields = computed<FormFieldConfig[]>(() => {
    const isView = this.mode() === 'view';

    return [
      {
        name: 'isActive',
        label: 'Zone Operational Status',
        placeholder: 'Zone Active (Available for location creation, putaway, and stock operations)',
        type: 'checkbox',
        disabled: isView,
        hint: 'Deactivating a zone requires all child storage locations within it to be inactive or reassigned.',
        colSpan: 3
      }
    ];
  });

  ngOnInit(): void {
    this.subs.add(
      this.route.paramMap.subscribe(params => {
        const whId = params.get('warehouseId');
        const zId = params.get('zoneId');
        this.warehouseId.set(whId);
        this.zoneId.set(zId);

        const url = this.route.snapshot.url.map(s => s.path).join('/');
        if (url.includes('new')) {
          this.mode.set('create');
          this.initCreateForm();
        } else if (url.includes('edit')) {
          this.mode.set('edit');
        } else {
          this.mode.set('view');
        }

        if (whId) {
          this.loadWarehouse(whId);
        }
        if (whId && zId) {
          this.loadZone(whId, zId);
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  private initCreateForm(): void {
    this.zoneForm.reset({
      zoneCode: '',
      zoneName: '',
      description: '',
      isActive: true
    });
    this.serverErrors.set({});
  }

  private loadWarehouse(whId: string): void {
    this.masterDataService.getWarehouse(whId).subscribe({
      next: (wh) => this.currentWarehouse.set(wh),
      error: () => {}
    });
  }

  loadZone(whId: string, zId: string): void {
    this.loading.set(true);
    this.error.set(null);

    this.masterDataService.getWarehouseZone(whId, zId).subscribe({
      next: (zone) => {
        this.loading.set(false);
        if (!zone) {
          this.error.set(`Zone with ID "${zId}" was not found in warehouse "${whId}".`);
          return;
        }
        this.currentZone.set(zone);
        this.zoneForm.patchValue({
          zoneCode: zone.zoneCode,
          zoneName: zone.zoneName,
          description: zone.description || '',
          isActive: zone.isActive
        });
        this.zoneForm.get('zoneCode')?.disable();
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.message || `Failed to load zone "${zId}" from server.`);
      }
    });
  }

  saveZone(): void {
    if (this.mode() === 'view') {
      this.mode.set('edit');
      return;
    }

    const whId = this.warehouseId();
    if (!whId) {
      this.message.set({ text: 'Warehouse identifier is missing from route.', severity: 'danger' });
      return;
    }

    if (this.zoneForm.invalid) {
      this.zoneForm.markAllAsTouched();
      this.message.set({
        text: 'Please correct the validation errors marked on the form before saving.',
        severity: 'warn'
      });
      return;
    }

    this.saving.set(true);
    this.serverErrors.set({});
    this.message.set(null);

    const raw = this.zoneForm.getRawValue();

    if (this.mode() === 'create') {
      const dto: CreateWarehouseZoneDto = {
        zoneCode: raw.zoneCode.trim().toUpperCase(),
        zoneName: raw.zoneName.trim(),
        description: raw.description?.trim() || undefined
      };

      this.masterDataService.createWarehouseZone(whId, dto).subscribe({
        next: (created) => {
          this.saving.set(false);
          this.router.navigate(['/app/master-data/warehouses'], {
            queryParams: { warehouseId: whId, tab: 'zones' }
          });
        },
        error: (err) => {
          this.saving.set(false);
          this.handleApiError(err, 'Failed to create warehouse zone.');
        }
      });
    } else {
      const zId = this.zoneId()!;
      const dto: UpdateWarehouseZoneDto = {
        zoneName: raw.zoneName.trim(),
        description: raw.description?.trim() || undefined
      };

      this.masterDataService.updateWarehouseZone(whId, zId, dto).subscribe({
        next: () => {
          this.saving.set(false);
          this.router.navigate(['/app/master-data/warehouses'], {
            queryParams: { warehouseId: whId, tab: 'zones' }
          });
        },
        error: (err) => {
          this.saving.set(false);
          this.handleApiError(err, 'Failed to update warehouse zone.');
        }
      });
    }
  }

  navigateBack(): void {
    const whId = this.warehouseId();
    if (whId) {
      this.router.navigate(['/app/master-data/warehouses'], {
        queryParams: { warehouseId: whId, tab: 'zones' }
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
