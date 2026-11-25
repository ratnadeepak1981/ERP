import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { EcommerceCartService } from '../../../../core/services/ecommerce-cart.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { TenantContextService } from '../../../../core/services/tenant-context.service';
import { CartItemDto, CartDto } from '../../../../core/models/ecommerce-cart.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { LoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';

export interface ActionFeedback {
  success: boolean;
  message: string;
}

export const MAX_CART_ITEM_QUANTITY = 99999;

@Component({
  selector: 'app-customer-cart',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    PageHeaderComponent,
    EmptyStateComponent,
    LoadingStateComponent
  ],
  templateUrl: './customer-cart.component.html',
  styleUrl: './customer-cart.component.scss'
})
export class CustomerCartComponent implements OnInit, OnDestroy {
  readonly cartService = inject(EcommerceCartService);
  readonly authService = inject(AuthService);
  readonly tenantContext = inject(TenantContextService);
  private readonly router = inject(Router);

  // Component-level timer handle for auto-dismissing feedback messages
  private feedbackTimerId: ReturnType<typeof setTimeout> | null = null;

  // In-flight operation state
  readonly updatingLineId = signal<string | null>(null);
  readonly removingLineId = signal<string | null>(null);
  readonly clearingCart = signal<boolean>(false);
  readonly actionFeedback = signal<ActionFeedback | null>(null);

  // Expose signals from cartService
  readonly cart = this.cartService.cart;
  readonly loading = this.cartService.loading;
  readonly error = this.cartService.error;

  readonly breadcrumbs: MenuItem[] = [
    { label: 'Customer Portal', routerLink: '/customer-portal' },
    { label: 'Shopping Cart' }
  ];

  readonly cartHeaderSubtitle = computed(() => {
    const c = this.cart();
    if (!c || c.lines.length === 0) {
      return 'Your cart is currently empty.';
    }
    return `${c.totalItems} ${c.totalItems === 1 ? 'item' : 'items'} in your active shopping cart • ${c.currency}`;
  });

  ngOnInit(): void {
    // If not authenticated as customer, redirect to login
    if (!this.authService.isAuthenticated()) {
      this.router.navigate(['/login'], { queryParams: { returnUrl: '/customer-portal/cart' } });
      return;
    }

    this.reloadCart();
  }

  ngOnDestroy(): void {
    this.cancelFeedbackTimer();
  }

  reloadCart(): void {
    // Resolve selling company context from tenantContext or fallback to authenticated customer session tenantId
    const resolvedCompanyId: string | undefined =
      this.tenantContext.currentTenant()?.tenantId ??
      this.authService.currentUser()?.tenantId ??
      undefined;

    this.cartService.setCompanyContext(resolvedCompanyId ?? null);
    this.cartService.getCart(resolvedCompanyId).subscribe({
      error: (err) => {
        // Error state is captured reactively in this.cartService.error
      }
    });
  }

  incrementQuantity(item: CartItemDto): void {
    if (this.isOperationInProgress(item.lineId)) {
      return;
    }

    if (item.quantity >= MAX_CART_ITEM_QUANTITY) {
      this.setFeedback(false, `Maximum item quantity of ${MAX_CART_ITEM_QUANTITY.toLocaleString()} reached.`);
      return;
    }

    const newQuantity = item.quantity + 1;
    this.executeQuantityUpdate(item, newQuantity);
  }

  decrementQuantity(item: CartItemDto): void {
    if (this.isOperationInProgress(item.lineId) || item.quantity <= 1) {
      return;
    }

    const newQuantity = item.quantity - 1;
    this.executeQuantityUpdate(item, newQuantity);
  }

  onQuantityInputChange(item: CartItemDto, event: Event): void {
    if (this.isOperationInProgress(item.lineId)) {
      return;
    }

    const input = event.target as HTMLInputElement;
    const rawVal = parseInt(input.value, 10);

    // Reject non-numeric, non-finite, zero or negative quantities
    if (isNaN(rawVal) || !isFinite(rawVal) || rawVal < 1) {
      // Revert input field to authoritative quantity
      input.value = item.quantity.toString();
      this.setFeedback(false, 'Quantity must be at least 1.');
      return;
    }

    // Enforce upper bound of 99,999
    if (rawVal > MAX_CART_ITEM_QUANTITY) {
      input.value = item.quantity.toString();
      this.setFeedback(
        false,
        `Quantity cannot exceed the maximum limit of ${MAX_CART_ITEM_QUANTITY.toLocaleString()}.`
      );
      return;
    }

    if (rawVal === item.quantity) {
      return;
    }

    this.executeQuantityUpdate(item, rawVal);
  }

  private executeQuantityUpdate(item: CartItemDto, newQuantity: number): void {
    this.updatingLineId.set(item.lineId);
    this.actionFeedback.set(null);

    this.cartService.updateLineQuantity(item.lineId, newQuantity).subscribe({
      next: () => {
        this.updatingLineId.set(null);
        this.setFeedback(true, `Updated quantity for ${item.productName}.`);
      },
      error: (err) => {
        this.updatingLineId.set(null);
        this.setFeedback(false, err?.message || 'Unable to update item quantity.');
      }
    });
  }

  removeLine(item: CartItemDto): void {
    if (this.isOperationInProgress(item.lineId)) {
      return;
    }

    this.removingLineId.set(item.lineId);
    this.actionFeedback.set(null);

    this.cartService.removeLine(item.lineId).subscribe({
      next: () => {
        this.removingLineId.set(null);
        this.setFeedback(true, `Removed ${item.productName} from cart.`);
      },
      error: (err) => {
        this.removingLineId.set(null);
        this.setFeedback(false, err?.message || 'Unable to remove item from cart.');
      }
    });
  }

  confirmClearCart(): void {
    if (this.clearingCart() || this.loading()) {
      return;
    }

    this.clearingCart.set(true);
    this.actionFeedback.set(null);

    this.cartService.clearCart().subscribe({
      next: () => {
        this.clearingCart.set(false);
        this.setFeedback(true, 'Shopping cart cleared.');
      },
      error: (err) => {
        this.clearingCart.set(false);
        this.setFeedback(false, err?.message || 'Unable to clear shopping cart.');
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
    // 1. Cancel any active timer to prevent an earlier callback from clearing this message prematurely
    this.cancelFeedbackTimer();

    // 2. Set new feedback message
    this.actionFeedback.set({ success, message });

    // 3. Schedule auto-dismissal
    this.feedbackTimerId = setTimeout(() => {
      this.actionFeedback.set(null);
      this.feedbackTimerId = null;
    }, 4000);
  }

  private cancelFeedbackTimer(): void {
    if (this.feedbackTimerId !== null) {
      clearTimeout(this.feedbackTimerId);
      this.feedbackTimerId = null;
    }
  }

  private isOperationInProgress(lineId: string): boolean {
    return (
      this.updatingLineId() === lineId ||
      this.removingLineId() === lineId ||
      this.clearingCart() ||
      this.loading()
    );
  }
}
