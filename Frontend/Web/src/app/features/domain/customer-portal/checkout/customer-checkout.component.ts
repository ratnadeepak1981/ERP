import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { EcommerceCartService } from '../../../../core/services/ecommerce-cart.service';
import { CustomerAddressService } from '../../../../core/services/customer-address.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { TenantContextService } from '../../../../core/services/tenant-context.service';
import { CustomerAddressPortalDto } from '../../../../core/models/customer-address.models';
import { CheckoutRequest, CheckoutResultDto } from '../../../../core/models/ecommerce-cart.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';

export interface CheckoutActionFeedback {
  success: boolean;
  message: string;
}

@Component({
  selector: 'app-customer-checkout',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingStateComponent
  ],
  templateUrl: './customer-checkout.component.html',
  styleUrl: './customer-checkout.component.scss'
})
export class CustomerCheckoutComponent implements OnInit, OnDestroy {
  readonly cartService = inject(EcommerceCartService);
  readonly addressService = inject(CustomerAddressService);
  readonly authService = inject(AuthService);
  readonly tenantContext = inject(TenantContextService);
  private readonly router = inject(Router);

  // Address state
  readonly addresses = signal<CustomerAddressPortalDto[]>([]);
  readonly selectedAddressId = signal<string | null>(null);
  readonly loadingAddresses = signal<boolean>(false);
  readonly addressError = signal<string | null>(null);

  // Form options state
  readonly requestedDeliveryDate = signal<string>('');
  readonly customerNotes = signal<string>('');

  // Cart loading & submission state
  readonly cart = this.cartService.cart;
  readonly loadingCart = this.cartService.loading;
  readonly cartError = this.cartService.error;
  readonly isSubmitting = signal<boolean>(false);

  // Active idempotency key for this logical checkout attempt (reused on retry)
  readonly currentIdempotencyKey = signal<string | null>(null);

  // Completed checkout result
  readonly checkoutResult = signal<CheckoutResultDto | null>(null);

  // Inline feedback & timer handle
  readonly actionFeedback = signal<CheckoutActionFeedback | null>(null);
  private feedbackTimerId: ReturnType<typeof setTimeout> | null = null;

  // Minimum selectable delivery date: today formatted as YYYY-MM-DD
  readonly minDeliveryDate = new Date().toISOString().split('T')[0];

  readonly breadcrumbs: MenuItem[] = [
    { label: 'Customer Portal', routerLink: '/customer-portal' },
    { label: 'Shopping Cart', routerLink: '/customer-portal/cart' },
    { label: 'Checkout' }
  ];

  readonly errorState = computed(() => {
    return this.cartError() || this.addressError();
  });

  readonly canProceedToOrder = computed(() => {
    const c = this.cart();
    const hasItems = c !== null && c.lines.length > 0;
    const hasAddress = this.selectedAddressId() !== null;
    return hasItems && hasAddress && !this.loadingCart() && !this.loadingAddresses();
  });

  ngOnInit(): void {
    if (!this.authService.isAuthenticated()) {
      this.router.navigate(['/login'], { queryParams: { returnUrl: '/customer-portal/checkout' } });
      return;
    }

    this.reloadAll();
  }

  ngOnDestroy(): void {
    this.cancelFeedbackTimer();
  }

  reloadAll(): void {
    this.addressError.set(null);
    this.loadCart();
    this.loadAddresses();
  }

  loadCart(): void {
    const resolvedCompanyId: string | undefined =
      this.tenantContext.currentTenant()?.tenantId ??
      this.authService.currentUser()?.tenantId ??
      undefined;

    this.cartService.setCompanyContext(resolvedCompanyId ?? null);
    this.cartService.getCart(resolvedCompanyId).subscribe({
      error: () => {
        // Handled reactively by this.cartService.error
      }
    });
  }

  loadAddresses(): void {
    this.loadingAddresses.set(true);
    this.addressError.set(null);

    this.addressService.getAddresses().subscribe({
      next: (data) => {
        this.loadingAddresses.set(false);
        const activeAddresses = (data || []).filter(a => a.isActive);
        this.addresses.set(activeAddresses);

        // Auto-select default address if available, or first address
        if (activeAddresses.length > 0) {
          const defaultAddr = activeAddresses.find(a => a.isDefault);
          this.selectedAddressId.set(defaultAddr ? defaultAddr.addressId : activeAddresses[0].addressId);
        } else {
          this.selectedAddressId.set(null);
        }
      },
      error: (err) => {
        this.loadingAddresses.set(false);
        const msg = err?.error?.message || err?.message || 'Unable to load delivery addresses.';
        this.addressError.set(msg);
      }
    });
  }

  selectAddress(addressId: string): void {
    this.selectedAddressId.set(addressId);
  }

  onDeliveryDateChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const val = input.value;

    if (val && val < this.minDeliveryDate) {
      input.value = '';
      this.requestedDeliveryDate.set('');
      this.setFeedback(false, 'Requested delivery date cannot be in the past.');
      return;
    }

    this.requestedDeliveryDate.set(val);
  }

  onCustomerNotesChange(event: Event): void {
    const textarea = event.target as HTMLTextAreaElement;
    this.customerNotes.set(textarea.value);
  }

  /**
   * Generates a cryptographically secure UUID v4 string.
   * Utilizes crypto.randomUUID() where available, with crypto.getRandomValues fallback.
   */
  generateUuidV4(): string {
    if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
      return crypto.randomUUID();
    }

    if (typeof crypto !== 'undefined' && typeof crypto.getRandomValues === 'function') {
      const bytes = new Uint8Array(16);
      crypto.getRandomValues(bytes);
      bytes[6] = (bytes[6] & 0x0f) | 0x40; // Version 4
      bytes[8] = (bytes[8] & 0x3f) | 0x80; // Variant 10xx
      const hex = Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('');
      return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20, 32)}`;
    }

    // Defensive fallback
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
      const r = (Math.random() * 16) | 0;
      const v = c === 'x' ? r : (r & 0x3) | 0x8;
      return v.toString(16);
    });
  }

  /**
   * Stage 4B Checkout Submission:
   * Dispatches authenticated checkout request to create a Draft Sales Order.
   * Uses cryptographically secure UUID v4 idempotency key, submission locking,
   * duplicate click prevention, and handles idempotent retry on network uncertainty.
   */
  onPlaceOrderClick(): void {
    if (this.isSubmitting()) {
      return;
    }

    if (!this.canProceedToOrder()) {
      if (!this.selectedAddressId()) {
        this.setFeedback(false, 'Please select a delivery address to proceed.');
      } else if (!this.cart() || this.cart()!.lines.length === 0) {
        this.setFeedback(false, 'Cannot checkout an empty shopping cart.');
      }
      return;
    }

    const currentCart = this.cart();
    if (!currentCart || currentCart.lines.length === 0) {
      this.setFeedback(false, 'Cannot checkout an empty shopping cart.');
      return;
    }

    // 1. Maintain or generate cryptographically secure UUID v4 idempotency key
    let idempotencyKey = this.currentIdempotencyKey();
    if (!idempotencyKey) {
      idempotencyKey = this.generateUuidV4();
      this.currentIdempotencyKey.set(idempotencyKey);
    }

    // 2. Lock submission to prevent concurrent clicks or duplicate requests
    this.isSubmitting.set(true);
    this.actionFeedback.set(null);

    // 3. Resolve active Selling Company ID
    const resolvedCompanyId: string =
      currentCart.companyId ||
      this.tenantContext.currentTenant()?.tenantId ||
      this.authService.currentUser()?.tenantId ||
      '';

    // 4. Construct authoritative CheckoutRequest
    const payload: CheckoutRequest = {
      deliveryAddressId: this.selectedAddressId(),
      customerNotes: this.customerNotes().trim() || null,
      requestedDeliveryDate: this.requestedDeliveryDate() || null,
      idempotencyKey: idempotencyKey,
      sessionId: currentCart.sessionId ?? null
    };

    // 5. Dispatch to EcommerceCartService
    this.cartService.checkout(payload, resolvedCompanyId).subscribe({
      next: (result) => {
        this.isSubmitting.set(false);
        this.checkoutResult.set(result);
        // Clear current idempotency key on confirmed success so future new orders get fresh keys
        this.currentIdempotencyKey.set(null);
        this.setFeedback(
          true,
          `Order ${result.orderNumber} successfully created in Draft status! (${result.currency} ${result.totalAmount.toFixed(2)})`
        );
        // Stage 4C: Navigate to Order Confirmation page with order details
        this.router.navigate(['/customer-portal/checkout/confirmation'], {
          queryParams: {
            orderNumber: result.orderNumber,
            salesOrderId: result.salesOrderId,
            orderStatus: result.orderStatus,
            total: result.totalAmount,
            currency: result.currency
          },
          state: { order: result }
        });
      },
      error: (err) => {
        this.isSubmitting.set(false);
        // Retain currentIdempotencyKey so retrying this attempt sends the same key!
        const msg = err?.message || 'Checkout failed. Please review your order or retry.';
        this.setFeedback(false, msg);
      }
    });
  }

  goToCatalog(): void {
    this.router.navigate(['/customer-portal/catalog']);
  }

  clearFeedback(): void {
    this.cancelFeedbackTimer();
    this.actionFeedback.set(null);
  }

  private setFeedback(success: boolean, message: string): void {
    this.cancelFeedbackTimer();
    this.actionFeedback.set({ success, message });
    this.feedbackTimerId = setTimeout(() => {
      this.actionFeedback.set(null);
      this.feedbackTimerId = null;
    }, 5000);
  }

  private cancelFeedbackTimer(): void {
    if (this.feedbackTimerId !== null) {
      clearTimeout(this.feedbackTimerId);
      this.feedbackTimerId = null;
    }
  }
}
