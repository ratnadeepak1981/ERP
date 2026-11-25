import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppStatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { CheckoutResultDto } from '../../../../../core/models/ecommerce-cart.models';

@Component({
  selector: 'app-customer-order-confirmation',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    AppPageHeaderComponent,
    AppStatusBadgeComponent
  ],
  templateUrl: './customer-order-confirmation.component.html',
  styleUrl: './customer-order-confirmation.component.scss'
})
export class CustomerOrderConfirmationComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly order = signal<CheckoutResultDto | null>(null);
  readonly hasOrder = computed<boolean>(() => !!this.order());
  readonly confirmationDate = new Date();

  readonly statusLabel = computed<string>(() => {
    const s = this.order()?.orderStatus;
    if (s === 0 || s === undefined) return 'Draft';
    if (s === 1) return 'Submitted';
    if (s === 2) return 'Approved';
    return 'Draft';
  });

  readonly breadcrumbs: MenuItem[] = [
    { label: 'Customer Portal', routerLink: '/customer-portal' },
    { label: 'Orders', routerLink: '/customer-portal/orders' },
    { label: 'Order Confirmation' }
  ];

  ngOnInit(): void {
    // 1. Check navigation state from current navigation
    const navState = this.router.getCurrentNavigation()?.extras?.state as { order?: CheckoutResultDto } | undefined;
    if (navState?.order) {
      this.order.set(navState.order);
      return;
    }

    // 2. Check history.state fallback
    if (typeof history !== 'undefined' && history.state?.order) {
      this.order.set(history.state.order);
      return;
    }

    // 3. Check query parameters fallback
    const qp = this.route.snapshot.queryParamMap;
    const orderNumber = qp.get('orderNumber');
    if (orderNumber) {
      const salesOrderId = qp.get('salesOrderId') || 'unknown';
      const orderStatus = parseInt(qp.get('orderStatus') || '0', 10);
      const totalAmount = parseFloat(qp.get('total') || '0');
      const currency = qp.get('currency') || 'USD';
      const cartId = qp.get('cartId') || '';

      this.order.set({
        success: true,
        salesOrderId,
        orderNumber,
        orderStatus,
        cartId,
        totalAmount,
        currency
      });
    }
  }

  goToOrders(): void {
    this.router.navigate(['/customer-portal/orders']);
  }

  goToCatalog(): void {
    this.router.navigate(['/customer-portal/catalog']);
  }

  printConfirmation(): void {
    if (typeof window !== 'undefined') {
      window.print();
    }
  }
}
