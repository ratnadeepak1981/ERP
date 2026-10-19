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
import { AuthService } from '../../../../../core/auth/auth.service';
import {
  WarehouseItem,
  CreateWarehouseDto,
  UpdateWarehouseDto
} from '../../../../../core/models/scope.models';

export type WarehousePageMode = 'create' | 'edit' | 'view';

@Component({
  selector: 'app-warehouse-page',
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
  templateUrl: './warehouse-page.component.html',
  styleUrl: './warehouse-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class WarehousePageComponent implements OnInit, OnDestroy {
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);
  readonly route = inject(ActivatedRoute);

  private readonly subs = new Subscription();

  // Mode and Identity
  readonly mode = signal<WarehousePageMode>('create');
  readonly warehouseId = signal<string | null>(null);
  readonly currentWarehouse = signal<WarehouseItem | null>(null);

  // Page State
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly serverErrors = signal<Record<string, string>>({});
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Reactive Form
  readonly warehouseForm = new FormGroup({
    warehouseCode: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(2), Validators.maxLength(20)]
    }),
    warehouseName: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(2), Validators.maxLength(100)]
    }),
    description: new FormControl<string>('', { nonNullable: true }),
    addressLine1: new FormControl<string>('', { nonNullable: true }),
    addressLine2: new FormControl<string>('', { nonNullable: true }),
    city: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required]
    }),
    postalCode: new FormControl<string>('', { nonNullable: true }),
    country: new FormControl<string>('', { nonNullable: true }),
    isActive: new FormControl<boolean>(true, { nonNullable: true })
  });

  // Breadcrumbs
  readonly breadcrumbs = computed(() => {
    const list = [
      { label: 'Workspace', routerLink: '/app/dashboard' },
      { label: 'Supply Chain Masters', routerLink: '/app/master-data' },
      { label: 'Warehouses & Storage Locations', routerLink: '/app/master-data/warehouses' }
    ];

    if (this.mode() === 'create') {
      list.push({ label: 'New Warehouse Facility', routerLink: '' });
    } else {
      const code = this.currentWarehouse()?.warehouseCode || 'Warehouse Profile';
      list.push({ label: code, routerLink: '' });
    }
    return list;
  });

  // Page Title & Subtitle
  readonly pageTitle = computed(() => {
    if (this.mode() === 'create') {
      return 'Register Warehouse Facility';
    }
    const wh = this.currentWarehouse();
    return wh ? `Edit Warehouse — ${wh.warehouseCode} (${wh.warehouseName})` : 'Edit Warehouse Facility';
  });

  readonly pageSubtitle = computed(() => {
    if (this.mode() === 'create') {
      return 'Define an operational inventory warehouse facility, logistics contact address, and organizational scope.';
    }
    return 'Update warehouse identification, storage address, operational scope, and active logistics status.';
  });

  // Form Fields - Section 1: Facility Identity
  readonly identityFields = computed<FormFieldConfig[]>(() => {
    const isEdit = this.mode() !== 'create';
    const isView = this.mode() === 'view';

    return [
      {
        name: 'warehouseCode',
        label: 'Warehouse Code',
        type: 'text',
        required: true,
        placeholder: 'e.g. WH-MAIN',
        readonly: isEdit || isView,
        tooltip: 'Unique system identifier for the warehouse facility (immutable once created)',
        colSpan: 1
      },
      {
        name: 'warehouseName',
        label: 'Warehouse Name',
        type: 'text',
        required: true,
        placeholder: 'e.g. Main Distribution Center',
        disabled: isView,
        colSpan: 2
      },
      {
        name: 'description',
        label: 'Facility Description',
        type: 'textarea',
        placeholder: 'e.g. Primary central warehouse handling receiving, bulk pallet storage, and staging.',
        disabled: isView,
        colSpan: 3
      }
    ];
  });

  // Form Fields - Section 2: Physical Address & Geography
  readonly addressFields = computed<FormFieldConfig[]>(() => {
    const isView = this.mode() === 'view';

    return [
      {
        name: 'addressLine1',
        label: 'Address Line 1',
        type: 'text',
        placeholder: 'e.g. 100 Logistics Boulevard',
        disabled: isView,
        colSpan: 2
      },
      {
        name: 'addressLine2',
        label: 'Address Line 2 (Optional)',
        type: 'text',
        placeholder: 'e.g. Suite 4B / Dock Wing',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'city',
        label: 'City / Logistics District',
        type: 'text',
        required: true,
        placeholder: 'e.g. London',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'postalCode',
        label: 'Postal Code / ZIP',
        type: 'text',
        placeholder: 'e.g. E1 6AN',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'country',
        label: 'Country / Jurisdiction',
        type: 'text',
        placeholder: 'e.g. United Kingdom',
        disabled: isView,
        colSpan: 1
      }
    ];
  });

  // Form Fields - Section 3: Status & Operational Eligibility
  readonly statusFields = computed<FormFieldConfig[]>(() => {
    const isView = this.mode() === 'view';

    return [
      {
        name: 'isActive',
        label: 'Facility Operational Status',
        placeholder: 'Facility Active (Permitted for stock receipts, putaway, transfers, and issues)',
        type: 'checkbox',
        disabled: isView,
        hint: 'Inactive facilities are hidden from standard transactional operations and cannot receive new inventory.',
        colSpan: 3
      }
    ];
  });

  ngOnInit(): void {
    this.subs.add(
      this.route.url.subscribe(segments => {
        const path = segments.map(s => s.path).join('/');
        if (path.includes('new')) {
          this.mode.set('create');
          this.warehouseId.set(null);
          this.initCreateForm();
        } else {
          const id = this.route.snapshot.paramMap.get('id');
          if (id) {
            this.warehouseId.set(id);
            if (path.includes('edit')) {
              this.mode.set('edit');
            } else {
              this.mode.set('view');
            }
            this.loadWarehouse(id);
          }
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  private initCreateForm(): void {
    this.warehouseForm.reset({
      warehouseCode: '',
      warehouseName: '',
      description: '',
      addressLine1: '',
      addressLine2: '',
      city: '',
      postalCode: '',
      country: '',
      isActive: true
    });
    this.serverErrors.set({});
  }

  loadWarehouse(id: string): void {
    this.loading.set(true);
    this.error.set(null);

    this.masterDataService.getWarehouse(id).subscribe({
      next: (wh) => {
        this.loading.set(false);
        if (!wh) {
          this.error.set(`Warehouse with ID "${id}" was not found.`);
          return;
        }
        this.currentWarehouse.set(wh);
        this.warehouseForm.patchValue({
          warehouseCode: wh.warehouseCode,
          warehouseName: wh.warehouseName,
          description: wh.description || '',
          addressLine1: wh.addressLine1 || '',
          addressLine2: wh.addressLine2 || '',
          city: wh.city || '',
          postalCode: wh.postalCode || '',
          country: wh.country || '',
          isActive: wh.isActive
        });
        this.warehouseForm.get('warehouseCode')?.disable();
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.message || `Failed to load warehouse "${id}" from server.`);
      }
    });
  }

  saveWarehouse(): void {
    if (this.mode() === 'view') {
      this.mode.set('edit');
      return;
    }

    if (this.warehouseForm.invalid) {
      this.warehouseForm.markAllAsTouched();
      this.message.set({
        text: 'Please correct the validation errors marked on the form before saving.',
        severity: 'warn'
      });
      return;
    }

    this.saving.set(true);
    this.serverErrors.set({});
    this.message.set(null);

    const raw = this.warehouseForm.getRawValue();

    if (this.mode() === 'create') {
      const dto: CreateWarehouseDto = {
        warehouseCode: raw.warehouseCode.trim().toUpperCase(),
        warehouseName: raw.warehouseName.trim(),
        description: raw.description?.trim() || undefined,
        addressLine1: raw.addressLine1?.trim() || undefined,
        addressLine2: raw.addressLine2?.trim() || undefined,
        city: raw.city.trim(),
        postalCode: raw.postalCode?.trim() || undefined,
        country: raw.country?.trim() || undefined
      };

      this.masterDataService.createWarehouse(dto).subscribe({
        next: (created) => {
          this.saving.set(false);
          this.router.navigate(['/app/master-data/warehouses'], {
            queryParams: { warehouseId: created.warehouseId, tab: 'warehouses' }
          });
        },
        error: (err) => {
          this.saving.set(false);
          this.handleApiError(err, 'Failed to create warehouse.');
        }
      });
    } else {
      const id = this.warehouseId()!;
      const dto: UpdateWarehouseDto = {
        warehouseName: raw.warehouseName.trim(),
        description: raw.description?.trim() || undefined,
        addressLine1: raw.addressLine1?.trim() || undefined,
        addressLine2: raw.addressLine2?.trim() || undefined,
        city: raw.city.trim(),
        postalCode: raw.postalCode?.trim() || undefined,
        country: raw.country?.trim() || undefined
      };

      this.masterDataService.updateWarehouse(id, dto).subscribe({
        next: (updated) => {
          this.saving.set(false);
          this.router.navigate(['/app/master-data/warehouses'], {
            queryParams: { warehouseId: updated.warehouseId, tab: 'warehouses' }
          });
        },
        error: (err) => {
          this.saving.set(false);
          this.handleApiError(err, 'Failed to update warehouse.');
        }
      });
    }
  }

  navigateBack(): void {
    const currentId = this.warehouseId();
    if (currentId) {
      this.router.navigate(['/app/master-data/warehouses'], {
        queryParams: { warehouseId: currentId, tab: 'warehouses' }
      });
    } else {
      this.router.navigate(['/app/master-data/warehouses']);
    }
  }

  navigateToLocations(): void {
    const id = this.warehouseId();
    if (id) {
      this.router.navigate(['/app/master-data/warehouses'], {
        queryParams: { warehouseId: id, tab: 'locations' }
      });
    }
  }

  navigateToZones(): void {
    const id = this.warehouseId();
    if (id) {
      this.router.navigate(['/app/master-data/warehouses'], {
        queryParams: { warehouseId: id, tab: 'zones' }
      });
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
