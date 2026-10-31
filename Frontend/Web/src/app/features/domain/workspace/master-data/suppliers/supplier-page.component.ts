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
import { FormsModule, FormGroup, FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Subscription, forkJoin } from 'rxjs';
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
  SupplierItem,
  CreateSupplierDto,
  SupplierAddressItem,
  AddressTypeItem,
  AddressItem,
  PaymentTermItem,
  CurrencyItem
} from '../../../../../core/models/scope.models';

export type SupplierPageMode = 'create' | 'edit' | 'view';

@Component({
  selector: 'app-supplier-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterModule,
    ButtonModule,
    AppPageHeaderComponent,
    AppReactiveFormComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent
  ],
  templateUrl: './supplier-page.component.html',
  styleUrl: './supplier-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SupplierPageComponent implements OnInit, OnDestroy {
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly router = inject(Router);
  readonly route = inject(ActivatedRoute);

  private readonly subs = new Subscription();

  // Mode and identity
  readonly mode = signal<SupplierPageMode>('create');
  readonly supplierId = signal<string | null>(null);
  readonly currentSupplier = signal<SupplierItem | null>(null);

  // Page State
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly serverErrors = signal<Record<string, string>>({});
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Multiple Addresses State (Strictly NO POPUPS)
  readonly supplierAddresses = signal<SupplierAddressItem[]>([]);
  readonly addressTypes = signal<AddressTypeItem[]>([]);
  readonly allAddresses = signal<AddressItem[]>([]);
  readonly loadingAddresses = signal<boolean>(false);
  readonly showAddressForm = signal<boolean>(false);
  readonly addrSaving = signal<boolean>(false);
  readonly pendingDeleteAddress = signal<SupplierAddressItem | null>(null);

  // Address Inline Form fields
  addrName = '';
  addrLine1 = '';
  addrLine2 = '';
  addrCity = '';
  addrPostalCode = '';
  addrTypeId = '';
  addrIsDefault = false;

  // Payment Terms
  readonly paymentTermsList = signal<PaymentTermItem[]>([]);

  // Currencies
  readonly currenciesList = signal<CurrencyItem[]>([]);
  readonly currencyOptions = computed(() => {
    const list = this.currenciesList();
    const opts = [{ label: '-- None (No default currency) --', value: '' }];
    list.forEach(c => {
      opts.push({
        label: `${c.currencyCode} - ${c.currencyName} (${c.symbol})`,
        value: c.currencyId
      });
    });
    return opts;
  });

  // Reactive Form
  readonly supplierForm = new FormGroup({
    supplierCode: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(2)]
    }),
    supplierName: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(2)]
    }),
    contactName: new FormControl<string>('', { nonNullable: true }),
    email: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.email]
    }),
    phone: new FormControl<string>('', { nonNullable: true }),
    city: new FormControl<string>('', { nonNullable: true }),
    country: new FormControl<string>('Sri Lanka', { nonNullable: true }),
    paymentTerms: new FormControl<string>('Net 30', {
      nonNullable: true,
      validators: [Validators.required]
    }),
    currencyId: new FormControl<string>('', { nonNullable: true }),
    leadTimeDays: new FormControl<number>(7, {
      nonNullable: true,
      validators: [Validators.min(0)]
    }),
    isActive: new FormControl<boolean>(true, { nonNullable: true })
  });

  // Breadcrumbs
  readonly breadcrumbs = computed(() => {
    const list = [
      { label: 'Workspace', routerLink: '/app/dashboard' },
      { label: 'Supply Chain Masters', routerLink: '/app/master-data' },
      { label: 'Suppliers', routerLink: '/app/master-data/suppliers' }
    ];

    if (this.mode() === 'create') {
      list.push({ label: 'New Supplier Profile', routerLink: '' });
    } else {
      const code = this.currentSupplier()?.supplierCode || 'Supplier Details';
      list.push({ label: code, routerLink: '' });
    }
    return list;
  });

  // Page Title & Subtitle
  readonly pageTitle = computed(() => {
    if (this.mode() === 'create') {
      return 'New Supplier Profile';
    }
    const code = this.currentSupplier()?.supplierCode;
    return code ? `Edit Supplier — ${code}` : 'Edit Supplier';
  });

  readonly pageSubtitle = computed(() => {
    if (this.mode() === 'create') {
      return 'Configure a vendor profile, operational contact person, delivery terms, and billing specifications.';
    }
    return 'Update vendor profile, active purchasing contact, settlement terms, and fulfillment capabilities.';
  });

  // Form Fields - Section 1: Business Identity & Profile
  readonly identityFields = computed<FormFieldConfig[]>(() => {
    const isEdit = this.mode() !== 'create';
    const isView = this.mode() === 'view';

    return [
      {
        name: 'supplierCode',
        label: 'Supplier Code',
        type: 'text',
        required: true,
        placeholder: 'e.g. SUP-001',
        readonly: isEdit || isView,
        tooltip: 'Unique system code for the vendor organization',
        colSpan: 1
      },
      {
        name: 'supplierName',
        label: 'Supplier Name',
        type: 'text',
        required: true,
        placeholder: 'e.g. Prime Materials Ltd',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'contactName',
        label: 'Contact Person',
        type: 'text',
        placeholder: 'e.g. John Doe',
        disabled: isView,
        colSpan: 1
      }
    ];
  });

  // Form Fields - Section 2: Contact Details & Location
  readonly contactFields = computed<FormFieldConfig[]>(() => {
    const isView = this.mode() === 'view';

    return [
      {
        name: 'email',
        label: 'Email Address',
        type: 'text',
        placeholder: 'e.g. vendor@supplier.com',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'phone',
        label: 'Phone Number',
        type: 'text',
        placeholder: 'e.g. +1 555-0199',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'city',
        label: 'City',
        type: 'text',
        placeholder: 'e.g. Colombo',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'country',
        label: 'Country',
        type: 'text',
        placeholder: 'e.g. Sri Lanka',
        disabled: isView,
        colSpan: 1
      }
    ];
  });

  // Form Fields - Section 3: Commercial Terms & Operational Status
  readonly termsFields = computed<FormFieldConfig[]>(() => {
    const isView = this.mode() === 'view';
    const terms = this.paymentTermsList();

    const termOptions = terms.length > 0
      ? terms.map(t => ({ label: `${t.termName} (${t.days}d)`, value: t.termCode }))
      : [
          { label: 'Net 15 Days', value: 'Net 15' },
          { label: 'Net 30 Days', value: 'Net 30' },
          { label: 'Net 45 Days', value: 'Net 45' },
          { label: 'Net 60 Days', value: 'Net 60' },
          { label: 'Cash on Delivery (COD)', value: 'Cash on Delivery' },
          { label: 'Due on Receipt', value: 'Due on Receipt' },
          { label: 'Advance Payment', value: 'Advance Payment' }
        ];

    return [
      {
        name: 'paymentTerms',
        label: 'Payment Terms',
        type: 'select',
        required: true,
        placeholder: '-- Select Terms --',
        options: termOptions,
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'currencyId',
        label: 'Default Currency',
        type: 'select',
        placeholder: '-- Select Default Currency (Optional) --',
        options: this.currencyOptions(),
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'leadTimeDays',
        label: 'Standard Lead Time (Days)',
        type: 'number',
        placeholder: '7',
        min: 0,
        disabled: isView,
        hint: 'Expected fulfillment days from PO confirmation to warehouse receipt',
        colSpan: 1
      },
      {
        name: 'isActive',
        label: 'Active Status',
        placeholder: 'Active Vendor (Eligible for purchase requisitions and receipts)',
        type: 'checkbox',
        disabled: isView,
        colSpan: 1
      }
    ];
  });

  ngOnInit(): void {
    this.detectRoute();
    this.loadPaymentTerms();
    this.loadCurrencies();

    this.subs.add(
      this.route.paramMap.subscribe(() => {
        this.detectRoute();
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  private loadPaymentTerms(): void {
    this.masterDataService.getPaymentTerms().subscribe({
      next: terms => this.paymentTermsList.set(terms.filter(t => t.isActive)),
      error: () => {}
    });
  }

  private loadCurrencies(): void {
    this.masterDataService.getCurrencies().subscribe({
      next: list => this.currenciesList.set(list.filter(c => c.isActive)),
      error: () => {}
    });
  }

  private detectRoute(): void {
    const id = this.route.snapshot.paramMap.get('id');

    if (id) {
      this.supplierId.set(id);
      this.mode.set('edit');
      this.loadSupplier(id);
      this.loadSupplierAddresses(id);
    } else {
      this.mode.set('create');
      this.supplierId.set(null);
      this.currentSupplier.set(null);
      this.supplierAddresses.set([]);
      this.supplierForm.reset({
        supplierCode: '',
        supplierName: '',
        contactName: '',
        email: '',
        phone: '',
        city: '',
        country: 'Sri Lanka',
        paymentTerms: 'Net 30',
        currencyId: '',
        leadTimeDays: 7,
        isActive: true
      });
      this.supplierForm.get('supplierCode')?.enable();
    }
  }

  loadSupplier(id: string): void {
    this.loading.set(true);
    this.error.set(null);

    this.masterDataService.getSupplier(id).subscribe({
      next: sup => {
        this.loading.set(false);
        if (!sup) {
          this.error.set('Supplier profile could not be found.');
          return;
        }

        this.currentSupplier.set(sup);
        this.populateForm(sup);
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err?.error?.message || 'Failed to retrieve supplier record from server.');
      }
    });
  }

  loadSupplierAddresses(supplierId: string): void {
    this.loadingAddresses.set(true);
    forkJoin({
      linked: this.masterDataService.getSupplierAddresses(supplierId),
      all: this.masterDataService.getAddresses(),
      types: this.masterDataService.getAddressTypes()
    }).subscribe({
      next: ({ linked, all, types }) => {
        this.allAddresses.set(all);
        this.addressTypes.set(types);

        const enriched = linked.map(link => {
          const matchedAddr = all.find(a => a.addressId === link.addressId);
          const matchedType = types.find(t => t.addressTypeId === link.addressTypeId);
          return {
            ...link,
            address: matchedAddr,
            addressTypeName: matchedType?.name || 'Address'
          };
        });

        this.supplierAddresses.set(enriched);
        this.loadingAddresses.set(false);
      },
      error: () => {
        this.loadingAddresses.set(false);
      }
    });
  }

  private populateForm(sup: SupplierItem): void {
    this.supplierForm.patchValue({
      supplierCode: sup.supplierCode,
      supplierName: sup.supplierName,
      contactName: sup.contactName || '',
      email: sup.email || '',
      phone: sup.phone || '',
      city: sup.city || '',
      country: sup.country || 'Sri Lanka',
      paymentTerms: sup.paymentTerms || 'Net 30',
      currencyId: sup.currencyId || '',
      leadTimeDays: Number(sup.leadTimeDays) || 7,
      isActive: sup.isActive !== false
    });

    if (this.mode() !== 'create') {
      this.supplierForm.get('supplierCode')?.disable();
    }
  }

  // --- Inline Address Management (NO POPUPS) ---
  openAddAddress(): void {
    this.addrName = '';
    this.addrLine1 = '';
    this.addrLine2 = '';
    this.addrCity = this.currentSupplier()?.city || '';
    this.addrPostalCode = '';
    const officeType = this.addressTypes().find(t => t.code.toUpperCase() === 'OFFICE' || t.code.toUpperCase() === 'BILLING') || this.addressTypes()[0];
    this.addrTypeId = officeType?.addressTypeId || '';
    this.addrIsDefault = false;
    this.showAddressForm.set(true);
    this.pendingDeleteAddress.set(null);
  }

  cancelAddAddress(): void {
    this.showAddressForm.set(false);
  }

  saveNewAddress(): void {
    if (!this.addrName.trim() || !this.addrLine1.trim() || !this.addrCity.trim() || !this.addrTypeId) {
      this.showMessage('Please provide Address Name, Line 1, City, and Address Type.', 'warn');
      return;
    }

    const sId = this.supplierId();
    if (!sId) return;

    this.addrSaving.set(true);

    this.masterDataService.createAddress({
      addressName: this.addrName.trim(),
      addressLine1: this.addrLine1.trim(),
      addressLine2: this.addrLine2.trim() || undefined,
      city: this.addrCity.trim(),
      postalCode: this.addrPostalCode.trim() || undefined
    }).subscribe({
      next: createdAddr => {
        this.masterDataService.linkSupplierAddress(sId, {
          addressId: createdAddr.addressId,
          addressTypeId: this.addrTypeId,
          isDefault: this.addrIsDefault
        }).subscribe({
          next: () => {
            this.addrSaving.set(false);
            this.showAddressForm.set(false);
            this.loadSupplierAddresses(sId);
            this.showMessage(`Address "${createdAddr.addressName}" linked to supplier successfully.`, 'success');
          },
          error: err => {
            this.addrSaving.set(false);
            this.showMessage(err?.error?.message || 'Failed to link address to supplier.', 'danger');
          }
        });
      },
      error: err => {
        this.addrSaving.set(false);
        this.showMessage(err?.error?.message || 'Failed to create address.', 'danger');
      }
    });
  }

  setAsDefault(sa: SupplierAddressItem): void {
    const sId = this.supplierId();
    if (!sId) return;

    this.masterDataService.setDefaultSupplierAddress(sId, sa.supplierAddressId).subscribe({
      next: () => {
        this.loadSupplierAddresses(sId);
        this.showMessage('Default address updated successfully.', 'success');
      },
      error: err => {
        this.showMessage(err?.error?.message || 'Failed to set default address.', 'danger');
      }
    });
  }

  promptRemoveAddress(sa: SupplierAddressItem): void {
    this.pendingDeleteAddress.set(sa);
  }

  cancelRemoveAddress(): void {
    this.pendingDeleteAddress.set(null);
  }

  confirmRemoveAddress(): void {
    const target = this.pendingDeleteAddress();
    const sId = this.supplierId();
    if (!target || !sId) return;

    this.masterDataService.removeSupplierAddress(sId, target.supplierAddressId).subscribe({
      next: () => {
        this.pendingDeleteAddress.set(null);
        this.loadSupplierAddresses(sId);
        this.showMessage('Address unlinked successfully.', 'success');
      },
      error: err => {
        this.showMessage(err?.error?.message || 'Failed to unlink address.', 'danger');
      }
    });
  }

  saveSupplier(): void {
    if (this.supplierForm.invalid) {
      this.supplierForm.markAllAsTouched();
      this.showMessage('Please correct the highlighted form errors before saving.', 'danger');
      return;
    }

    const val = this.supplierForm.getRawValue();
    this.saving.set(true);
    this.serverErrors.set({});

    const dto: CreateSupplierDto = {
      supplierCode: val.supplierCode.trim(),
      supplierName: val.supplierName.trim(),
      contactName: val.contactName?.trim() || undefined,
      email: val.email?.trim() || undefined,
      phone: val.phone?.trim() || undefined,
      city: val.city?.trim() || undefined,
      country: val.country?.trim() || undefined,
      paymentTerms: val.paymentTerms?.trim() || undefined,
      currencyId: val.currencyId ? val.currencyId : null,
      leadTimeDays: Number(val.leadTimeDays) || 0,
      isActive: !!val.isActive
    };

    if (this.mode() !== 'create' && this.supplierId()) {
      this.masterDataService.updateSupplier(this.supplierId()!, dto).subscribe({
        next: updated => {
          this.saving.set(false);
          this.currentSupplier.set(updated || { ...this.currentSupplier()!, ...dto });
          this.showMessage(`Supplier '${val.supplierName}' updated successfully.`, 'success');
          setTimeout(() => {
            this.router.navigate(['/app/master-data/suppliers']);
          }, 800);
        },
        error: err => {
          this.saving.set(false);
          this.handleSaveError(err, 'Failed to update supplier.');
        }
      });
    } else {
      this.masterDataService.createSupplier(dto).subscribe({
        next: () => {
          this.saving.set(false);
          this.showMessage(`Supplier '${val.supplierName}' registered successfully.`, 'success');
          setTimeout(() => {
            this.router.navigate(['/app/master-data/suppliers']);
          }, 800);
        },
        error: err => {
          this.saving.set(false);
          this.handleSaveError(err, 'Failed to create supplier.');
        }
      });
    }
  }

  private handleSaveError(err: any, fallbackMessage: string): void {
    if (err?.error?.errors && typeof err.error.errors === 'object') {
      const fieldErrors: Record<string, string> = {};
      for (const [key, msgs] of Object.entries(err.error.errors)) {
        const fieldName = key.charAt(0).toLowerCase() + key.slice(1);
        fieldErrors[fieldName] = Array.isArray(msgs) ? msgs.join(' ') : String(msgs);
      }
      this.serverErrors.set(fieldErrors);
      this.showMessage('Validation error occurred while saving the supplier.', 'danger');
    } else {
      const msg = err?.error?.message || err?.message || fallbackMessage;
      this.showMessage(msg, 'danger');
    }
  }

  showMessage(text: string, severity: 'success' | 'info' | 'warn' | 'danger'): void {
    this.message.set({ text, severity });
  }

  navigateBack(): void {
    this.router.navigate(['/app/master-data/suppliers']);
  }
}
