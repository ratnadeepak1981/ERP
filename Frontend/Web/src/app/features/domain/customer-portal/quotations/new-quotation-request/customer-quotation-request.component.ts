import {
  Component,
  OnInit,
  signal,
  computed,
  inject,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { CustomerQuotationService } from '../../../../../core/services/customer-quotation.service';
import { CustomerAddressService } from '../../../../../core/services/customer-address.service';
import { EcommerceCatalogueService } from '../../../../../core/services/ecommerce-catalogue.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { CustomerAddressPortalDto } from '../../../../../core/models/customer-address.models';
import { PublicEcommerceProductDto } from '../../../../../core/models/ecommerce-catalogue.models';
import {
  CreateCustomerQuotationRequest,
  CreateCustomerQuotationItemRequest
} from '../../../../../core/models/customer-quotation.models';
import { buildApiUrl } from '../../../../../core/utils/api-url.util';
import { ApiResponse } from '../../../../../core/models/auth.models';

export interface QuotationLineFormItem {
  id: string;
  productId: string;
  productCode: string;
  productName: string;
  unitOfMeasure: string;
  quantity: number;
  targetUnitPrice: number;
  targetLineTotal: number;
  notes: string;
}

@Component({
  selector: 'app-customer-quotation-request',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterModule,
    ButtonModule,
    AppPageHeaderComponent
  ],
  templateUrl: './customer-quotation-request.component.html',
  styleUrl: './customer-quotation-request.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CustomerQuotationRequestComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly http = inject(HttpClient);
  private readonly quotationService = inject(CustomerQuotationService);
  private readonly addressService = inject(CustomerAddressService);
  private readonly catalogueService = inject(EcommerceCatalogueService);
  readonly authService = inject(AuthService);

  // Breadcrumbs matching standard ApexERP conventions
  readonly breadcrumbs = [
    { label: 'Customer Workspace', url: '/customer-portal' },
    { label: 'Purchasing', url: '/customer-portal/quotations' },
    { label: 'Quotations', url: '/customer-portal/quotations' },
    { label: 'New Request' }
  ];

  // UI State Signals
  readonly loading = signal<boolean>(true);
  readonly actionInProgress = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  // Authenticated Context Signals
  readonly customerName = signal<string>('Corporate Account');
  readonly customerCode = signal<string>('CUST-B2B');
  readonly sellingCompanyName = signal<string>('Apex Manufacturing Ltd');
  readonly sellingCompanyId = signal<string>('');
  readonly currencyCode = signal<string>('USD');

  // Master Data Signals
  readonly availableAddresses = signal<CustomerAddressPortalDto[]>([]);
  readonly catalogueProducts = signal<PublicEcommerceProductDto[]>([]);

  // Selected Address Computed
  readonly selectedAddressId = signal<string>('');
  readonly selectedAddress = computed(() => {
    const id = this.selectedAddressId();
    return this.availableAddresses().find(a => a.addressId === id || a.customerAddressId === id) || null;
  });

  // Line items state
  readonly lines = signal<QuotationLineFormItem[]>([]);

  // Summary Computeds
  readonly totalRequestedLines = computed(() => this.lines().length);
  readonly totalRequestedQuantity = computed(() => {
    return this.lines().reduce((acc, line) => acc + (line.quantity || 0), 0);
  });
  readonly estimatedTargetTotal = computed(() => {
    return this.lines().reduce((acc, line) => acc + (line.targetLineTotal || 0), 0);
  });

  // Header and Metadata Form
  readonly requestForm: FormGroup = this.fb.group({
    requestDate: [{ value: new Date().toISOString().substring(0, 10), disabled: true }],
    requiredDeliveryDate: ['', [Validators.required]],
    customerReference: [''],
    projectReference: [''],
    deliveryInstructions: [''],
    customerRemarks: ['']
  });

  // Today's date for date-picker min boundary
  readonly minDeliveryDate = new Date().toISOString().substring(0, 10);

  ngOnInit(): void {
    this.loadInitialContext();
  }

  private loadInitialContext(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    // 1. Load Customer Profile to resolve customer details and Selling Company
    this.http.get<ApiResponse<any>>(buildApiUrl('/portal/profile')).subscribe({
      next: (res) => {
        if (res?.data) {
          const cust = res.data.customer;
          if (cust) {
            this.customerName.set(cust.customerName || cust.customerCode || 'Corporate Customer');
            this.customerCode.set(cust.customerCode || 'B2B');
            if (cust.currencyCode) {
              this.currencyCode.set(cust.currencyCode);
            }
          }

          const sellingCompanies = res.data.availableSellingCompanies;
          if (sellingCompanies && sellingCompanies.length > 0) {
            this.sellingCompanyName.set(sellingCompanies[0].companyName || 'Selling Company');
            this.sellingCompanyId.set(sellingCompanies[0].companyId || '');
            if (sellingCompanies[0].currencyCode && !cust?.currencyCode) {
              this.currencyCode.set(sellingCompanies[0].currencyCode);
            }
          }
        }

        // 2. Load Customer Delivery Addresses
        this.loadAddresses();

        // 3. Load Sellable Product Catalogue for Lookup
        this.loadProducts();
      },
      error: () => {
        // Fallback for standalone demo mode
        this.loadAddresses();
        this.loadProducts();
      }
    });
  }

  private loadAddresses(): void {
    this.addressService.getAddresses().subscribe({
      next: (addrs) => {
        this.availableAddresses.set(addrs || []);
        // Auto-select default address
        const def = addrs?.find(a => a.isDefault) || addrs?.[0];
        if (def) {
          this.selectedAddressId.set(def.addressId || def.customerAddressId);
        }
      },
      error: () => {
        this.availableAddresses.set([]);
      }
    });
  }

  private loadProducts(): void {
    const compId = this.sellingCompanyId();
    this.catalogueService.getPublicCatalogue(compId).subscribe({
      next: (prods) => {
        this.catalogueProducts.set(prods || []);
        this.loading.set(false);

        // If no lines present, add an initial empty row
        if (this.lines().length === 0) {
          this.addLine();
        }
      },
      error: () => {
        this.loading.set(false);
        if (this.lines().length === 0) {
          this.addLine();
        }
      }
    });
  }

  onAddressChange(addressId: string): void {
    this.selectedAddressId.set(addressId);
  }

  addLine(): void {
    const newLine: QuotationLineFormItem = {
      id: 'line_' + Date.now() + '_' + Math.floor(Math.random() * 1000),
      productId: '',
      productCode: '',
      productName: '',
      unitOfMeasure: 'EA',
      quantity: 1,
      targetUnitPrice: 0,
      targetLineTotal: 0,
      notes: ''
    };

    this.lines.update(current => [...current, newLine]);
  }

  removeLine(index: number): void {
    this.lines.update(current => {
      const updated = [...current];
      updated.splice(index, 1);
      return updated;
    });
  }

  onProductSelected(index: number, productId: string): void {
    const prod = this.catalogueProducts().find(p => p.productId === productId);
    this.lines.update(current => {
      const updated = [...current];
      const item = { ...updated[index] };
      if (prod) {
        item.productId = prod.productId;
        item.productCode = prod.productCode;
        item.productName = prod.productName;
        item.unitOfMeasure = prod.unitOfMeasure || 'EA';
        item.targetUnitPrice = prod.sellingPrice || 0;
        item.targetLineTotal = (item.quantity || 1) * item.targetUnitPrice;
      } else {
        item.productId = '';
        item.productCode = '';
        item.productName = '';
        item.targetUnitPrice = 0;
        item.targetLineTotal = 0;
      }
      updated[index] = item;
      return updated;
    });
  }

  onQuantityChange(index: number, qty: number): void {
    const safeQty = Math.max(0, qty || 0);
    this.lines.update(current => {
      const updated = [...current];
      const item = { ...updated[index] };
      item.quantity = safeQty;
      item.targetLineTotal = safeQty * (item.targetUnitPrice || 0);
      updated[index] = item;
      return updated;
    });
  }

  onTargetPriceChange(index: number, price: number): void {
    const safePrice = Math.max(0, price || 0);
    this.lines.update(current => {
      const updated = [...current];
      const item = { ...updated[index] };
      item.targetUnitPrice = safePrice;
      item.targetLineTotal = (item.quantity || 0) * safePrice;
      updated[index] = item;
      return updated;
    });
  }

  onLineNotesChange(index: number, notes: string): void {
    this.lines.update(current => {
      const updated = [...current];
      updated[index] = { ...updated[index], notes };
      return updated;
    });
  }

  submitRequest(isDraft: boolean): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);

    // 1. Validation checks
    if (!isDraft && this.requestForm.invalid) {
      this.requestForm.markAllAsTouched();
      this.errorMessage.set('Please select a required delivery date.');
      return;
    }

    const validLines = this.lines().filter(l => l.productId && l.quantity > 0);
    if (validLines.length === 0) {
      this.errorMessage.set('At least one product line item with quantity greater than zero is required.');
      return;
    }

    // 2. Prevent duplicate submission
    if (this.actionInProgress()) {
      return;
    }
    this.actionInProgress.set(true);

    const formVal = this.requestForm.getRawValue();
    const selAddr = this.selectedAddress();

    const requestPayload: CreateCustomerQuotationRequest = {
      requiredDeliveryDate: formVal.requiredDeliveryDate || null,
      currency: this.currencyCode(),
      deliveryAddressId: selAddr ? (selAddr.addressId || selAddr.customerAddressId) : null,
      deliveryAddressSnapshot: selAddr
        ? `${selAddr.addressLine1}, ${selAddr.city}${selAddr.postalCode ? ' ' + selAddr.postalCode : ''}`
        : null,
      customerReference: formVal.customerReference || null,
      projectReference: formVal.projectReference || null,
      deliveryInstructions: formVal.deliveryInstructions || null,
      customerRemarks: formVal.customerRemarks || null,
      idempotencyKey: 'RFQ-' + Date.now() + '-' + Math.random().toString(36).substring(2, 8),
      isDraft: isDraft,
      items: validLines.map(line => ({
        productId: line.productId,
        quantity: line.quantity,
        unitPrice: line.targetUnitPrice || 0,
        notes: line.notes || null
      }))
    };

    const compId = this.sellingCompanyId() || undefined;

    this.quotationService.submitQuotationRequest(requestPayload, compId).subscribe({
      next: (res) => {
        this.actionInProgress.set(false);
        const refNum = res?.data?.quotationNumber || 'SQ-REQ';
        this.successMessage.set(
          isDraft
            ? `Quotation request '${refNum}' saved as draft.`
            : `Quotation request '${refNum}' submitted successfully for commercial review.`
        );

        setTimeout(() => {
          this.router.navigate(['/customer-portal/quotations']);
        }, 1200);
      },
      error: (err) => {
        this.actionInProgress.set(false);
        const errDetail = err?.error?.message || err?.message || 'Unable to submit quotation request at this time.';
        this.errorMessage.set(errDetail);
      }
    });
  }

  navigateBack(): void {
    this.router.navigate(['/customer-portal/quotations']);
  }
}
