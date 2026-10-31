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
  CustomerItem,
  CreateCustomerDto,
  CustomerAddressItem,
  AddressTypeItem,
  AddressItem,
  PaymentTermItem,
  CurrencyItem
} from '../../../../../core/models/scope.models';

export type CustomerPageMode = 'create' | 'edit' | 'view';

@Component({
  selector: 'app-customer-page',
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
  templateUrl: './customer-page.component.html',
  styleUrl: './customer-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CustomerPageComponent implements OnInit, OnDestroy {
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly router = inject(Router);
  readonly route = inject(ActivatedRoute);

  private readonly subs = new Subscription();

  // Mode and identity
  readonly mode = signal<CustomerPageMode>('create');
  readonly customerId = signal<string | null>(null);
  readonly currentCustomer = signal<CustomerItem | null>(null);

  // Page State
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly serverErrors = signal<Record<string, string>>({});
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Multiple Addresses State (Strictly NO POPUPS)
  readonly customerAddresses = signal<CustomerAddressItem[]>([]);
  readonly addressTypes = signal<AddressTypeItem[]>([]);
  readonly allAddresses = signal<AddressItem[]>([]);
  readonly loadingAddresses = signal<boolean>(false);
  readonly showAddressForm = signal<boolean>(false);
  readonly addrSaving = signal<boolean>(false);
  readonly pendingDeleteAddress = signal<CustomerAddressItem | null>(null);

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
  readonly customerForm = new FormGroup({
    customerCode: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(2)]
    }),
    customerName: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(2)]
    }),
    contactPerson: new FormControl<string>('', { nonNullable: true }),
    email: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.email]
    }),
    phone: new FormControl<string>('', { nonNullable: true }),
    addressLine1: new FormControl<string>('', { nonNullable: true }),
    addressLine2: new FormControl<string>('', { nonNullable: true }),
    city: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required]
    }),
    postalCode: new FormControl<string>('', { nonNullable: true }),
    country: new FormControl<string>('Sri Lanka', { nonNullable: true }),
    taxNumber: new FormControl<string>('', { nonNullable: true }),
    creditLimit: new FormControl<number>(0, {
      nonNullable: true,
      validators: [Validators.min(0)]
    }),
    paymentTerms: new FormControl<string>('Net 30', {
      nonNullable: true,
      validators: [Validators.required]
    }),
    currencyId: new FormControl<string>('', { nonNullable: true }),
    isActive: new FormControl<boolean>(true, { nonNullable: true })
  });

  // Breadcrumbs
  readonly breadcrumbs = computed(() => {
    const list = [
      { label: 'Workspace', routerLink: '/app/dashboard' },
      { label: 'Supply Chain Masters', routerLink: '/app/master-data' },
      { label: 'Customers', routerLink: '/app/master-data/customers' }
    ];

    if (this.mode() === 'create') {
      list.push({ label: 'New Customer Account', routerLink: '' });
    } else {
      const code = this.currentCustomer()?.customerCode || 'Customer Details';
      list.push({ label: code, routerLink: '' });
    }
    return list;
  });

  // Page Title & Subtitle
  readonly pageTitle = computed(() => {
    if (this.mode() === 'create') {
      return 'New Customer Account';
    }
    const code = this.currentCustomer()?.customerCode;
    return code ? `Edit Customer — ${code}` : 'Edit Customer';
  });

  readonly pageSubtitle = computed(() => {
    if (this.mode() === 'create') {
      return 'Register a commercial client account, billing terms, credit threshold, and main operational address.';
    }
    return 'Update client profile, commercial agreements, multiple delivery destinations, and trading privileges.';
  });

  // Form Fields - Section 1: Business Identity & Contact
  readonly identityFields = computed<FormFieldConfig[]>(() => {
    const isEdit = this.mode() !== 'create';
    const isView = this.mode() === 'view';

    return [
      {
        name: 'customerCode',
        label: 'Customer Code',
        type: 'text',
        required: true,
        placeholder: 'e.g. CUST-001',
        readonly: isEdit || isView,
        tooltip: 'Unique system code for the client account',
        colSpan: 1
      },
      {
        name: 'customerName',
        label: 'Customer Name / Entity',
        type: 'text',
        required: true,
        placeholder: 'e.g. Apex Global Traders Ltd',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'contactPerson',
        label: 'Primary Contact Person',
        type: 'text',
        placeholder: 'e.g. Jane Perera',
        disabled: isView,
        colSpan: 1
      }
    ];
  });

  // Form Fields - Section 2: Primary Billing Address
  readonly addressFields = computed<FormFieldConfig[]>(() => {
    const isView = this.mode() === 'view';

    return [
      {
        name: 'email',
        label: 'Billing Email Address',
        type: 'text',
        placeholder: 'e.g. accounts@customer.com',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'phone',
        label: 'Business Telephone',
        type: 'text',
        placeholder: 'e.g. +94 11 234 5678',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'addressLine1',
        label: 'Primary Street Address',
        type: 'text',
        placeholder: 'e.g. 124 Galle Road',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'addressLine2',
        label: 'Address Line 2',
        type: 'text',
        placeholder: 'e.g. 4th Floor, Suite B',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'postalCode',
        label: 'Postal Code',
        type: 'text',
        placeholder: 'e.g. 00300',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'city',
        label: 'City / Municipal Region',
        type: 'text',
        required: true,
        placeholder: 'e.g. Colombo',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'country',
        label: 'Country / Jurisdiction',
        type: 'text',
        placeholder: 'e.g. Sri Lanka',
        disabled: isView,
        colSpan: 1
      },
      {
        name: 'taxNumber',
        label: 'Tax / VAT Registration No',
        type: 'text',
        placeholder: 'e.g. VAT-11447890',
        disabled: isView,
        colSpan: 1
      }
    ];
  });

  // Form Fields - Section 3: Commercial Terms & Credit Control
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
        name: 'creditLimit',
        label: 'Approved Credit Limit (LKR)',
        type: 'number',
        placeholder: '0.00',
        min: 0,
        disabled: isView,
        hint: 'Maximum outstanding balance permitted for Sales Orders',
        colSpan: 1
      },
      {
        name: 'isActive',
        label: 'Active Trading Status',
        placeholder: 'Active Customer (Eligible for sales orders and shipments)',
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
      this.customerId.set(id);
      this.mode.set('edit');
      this.loadCustomer(id);
      this.loadCustomerAddresses(id);
    } else {
      this.mode.set('create');
      this.customerId.set(null);
      this.currentCustomer.set(null);
      this.customerAddresses.set([]);
      this.customerForm.reset({
        customerCode: '',
        customerName: '',
        contactPerson: '',
        email: '',
        phone: '',
        addressLine1: '',
        addressLine2: '',
        city: '',
        postalCode: '',
        country: 'Sri Lanka',
        taxNumber: '',
        creditLimit: 0,
        paymentTerms: 'Net 30',
        currencyId: '',
        isActive: true
      });
      this.customerForm.get('customerCode')?.enable();
    }
  }

  loadCustomer(id: string): void {
    this.loading.set(true);
    this.error.set(null);

    this.masterDataService.getCustomer(id).subscribe({
      next: cust => {
        this.loading.set(false);
        if (!cust) {
          this.error.set('Customer account record could not be found.');
          return;
        }

        this.currentCustomer.set(cust);
        this.populateForm(cust);
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err?.error?.message || 'Failed to retrieve customer record from server.');
      }
    });
  }

  loadCustomerAddresses(customerId: string): void {
    this.loadingAddresses.set(true);
    forkJoin({
      linked: this.masterDataService.getCustomerAddresses(customerId),
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

        this.customerAddresses.set(enriched);
        this.loadingAddresses.set(false);
      },
      error: () => {
        this.loadingAddresses.set(false);
      }
    });
  }

  private populateForm(cust: CustomerItem): void {
    this.customerForm.patchValue({
      customerCode: cust.customerCode,
      customerName: cust.customerName,
      contactPerson: cust.contactPerson || '',
      email: cust.email || '',
      phone: cust.phone || '',
      addressLine1: cust.addressLine1 || '',
      addressLine2: cust.addressLine2 || '',
      city: cust.city || '',
      postalCode: cust.postalCode || '',
      country: cust.country || 'Sri Lanka',
      taxNumber: cust.taxNumber || '',
      creditLimit: Number(cust.creditLimit) || 0,
      paymentTerms: cust.paymentTerms || 'Net 30',
      currencyId: cust.currencyId || '',
      isActive: cust.isActive !== false
    });

    if (this.mode() !== 'create') {
      this.customerForm.get('customerCode')?.disable();
    }
  }

  // --- Inline Address Management (NO POPUPS) ---
  openAddAddress(): void {
    this.addrName = '';
    this.addrLine1 = '';
    this.addrLine2 = '';
    this.addrCity = this.currentCustomer()?.city || '';
    this.addrPostalCode = '';
    const deliveryType = this.addressTypes().find(t => t.code.toUpperCase() === 'DELIVERY') || this.addressTypes()[0];
    this.addrTypeId = deliveryType?.addressTypeId || '';
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

    const cId = this.customerId();
    if (!cId) return;

    this.addrSaving.set(true);

    this.masterDataService.createAddress({
      addressName: this.addrName.trim(),
      addressLine1: this.addrLine1.trim(),
      addressLine2: this.addrLine2.trim() || undefined,
      city: this.addrCity.trim(),
      postalCode: this.addrPostalCode.trim() || undefined
    }).subscribe({
      next: createdAddr => {
        this.masterDataService.linkCustomerAddress(cId, {
          addressId: createdAddr.addressId,
          addressTypeId: this.addrTypeId,
          isDefault: this.addrIsDefault
        }).subscribe({
          next: () => {
            this.addrSaving.set(false);
            this.showAddressForm.set(false);
            this.loadCustomerAddresses(cId);
            this.showMessage(`Address "${createdAddr.addressName}" added and linked successfully.`, 'success');
          },
          error: err => {
            this.addrSaving.set(false);
            this.showMessage(err?.error?.message || 'Failed to link address to customer.', 'danger');
          }
        });
      },
      error: err => {
        this.addrSaving.set(false);
        this.showMessage(err?.error?.message || 'Failed to create address.', 'danger');
      }
    });
  }

  setAsDefault(ca: CustomerAddressItem): void {
    const cId = this.customerId();
    if (!cId) return;

    this.masterDataService.setDefaultCustomerAddress(cId, ca.customerAddressId).subscribe({
      next: () => {
        this.loadCustomerAddresses(cId);
        this.showMessage('Default address updated successfully.', 'success');
      },
      error: err => {
        this.showMessage(err?.error?.message || 'Failed to set default address.', 'danger');
      }
    });
  }

  promptRemoveAddress(ca: CustomerAddressItem): void {
    this.pendingDeleteAddress.set(ca);
  }

  cancelRemoveAddress(): void {
    this.pendingDeleteAddress.set(null);
  }

  confirmRemoveAddress(): void {
    const target = this.pendingDeleteAddress();
    const cId = this.customerId();
    if (!target || !cId) return;

    this.masterDataService.removeCustomerAddress(cId, target.customerAddressId).subscribe({
      next: () => {
        this.pendingDeleteAddress.set(null);
        this.loadCustomerAddresses(cId);
        this.showMessage('Address unlinked successfully.', 'success');
      },
      error: err => {
        this.showMessage(err?.error?.message || 'Failed to unlink address.', 'danger');
      }
    });
  }

  saveCustomer(): void {
    if (this.customerForm.invalid) {
      this.customerForm.markAllAsTouched();
      this.showMessage('Please correct the highlighted form errors before saving.', 'danger');
      return;
    }

    const val = this.customerForm.getRawValue();
    this.saving.set(true);
    this.serverErrors.set({});

    const dto: CreateCustomerDto = {
      customerCode: val.customerCode.trim(),
      customerName: val.customerName.trim(),
      contactPerson: val.contactPerson?.trim() || undefined,
      email: val.email?.trim() || undefined,
      phone: val.phone?.trim() || undefined,
      addressLine1: val.addressLine1?.trim() || undefined,
      addressLine2: val.addressLine2?.trim() || undefined,
      city: val.city.trim(),
      postalCode: val.postalCode?.trim() || undefined,
      country: val.country?.trim() || undefined,
      taxNumber: val.taxNumber?.trim() || undefined,
      creditLimit: Number(val.creditLimit) || 0,
      paymentTerms: val.paymentTerms?.trim() || undefined,
      currencyId: val.currencyId ? val.currencyId : null
    };

    if (this.mode() !== 'create' && this.customerId()) {
      this.masterDataService.updateCustomer(this.customerId()!, dto).subscribe({
        next: updated => {
          this.saving.set(false);
          this.currentCustomer.set(updated || { ...this.currentCustomer()!, ...dto, isActive: !!val.isActive });
          this.showMessage(`Customer '${val.customerName}' updated successfully.`, 'success');
          setTimeout(() => {
            this.router.navigate(['/app/master-data/customers']);
          }, 800);
        },
        error: err => {
          this.saving.set(false);
          this.handleSaveError(err, 'Failed to update customer account.');
        }
      });
    } else {
      this.masterDataService.createCustomer(dto).subscribe({
        next: () => {
          this.saving.set(false);
          this.showMessage(`Customer '${val.customerName}' registered successfully.`, 'success');
          setTimeout(() => {
            this.router.navigate(['/app/master-data/customers']);
          }, 800);
        },
        error: err => {
          this.saving.set(false);
          this.handleSaveError(err, 'Failed to register customer account.');
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
      this.showMessage('Validation error occurred while saving the customer account.', 'danger');
    } else {
      const msg = err?.error?.message || err?.message || fallbackMessage;
      this.showMessage(msg, 'danger');
    }
  }

  showMessage(text: string, severity: 'success' | 'info' | 'warn' | 'danger'): void {
    this.message.set({ text, severity });
  }

  navigateBack(): void {
    this.router.navigate(['/app/master-data/customers']);
  }
}
