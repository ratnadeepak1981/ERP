import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';

@Component({
  selector: 'app-customer-cart',
  standalone: true,
  imports: [CommonModule, RouterLink, EmptyStateComponent],
  template: `
    <div class="customer-cart-page">
      <div class="milestone-banner erp-card">
        <div class="banner-icon">
          <i class="pi pi-shopping-cart"></i>
        </div>
        <div class="banner-body">
          <h2>Authenticated Shopping Cart &amp; Checkout</h2>
          <p>
            The persistent shopping cart engine with server-side pricing and tax calculations will activate in Milestone 3.
            You can explore published products and technical specifications in the live catalogue.
          </p>
        </div>
        <div class="banner-actions">
          <a routerLink="/customer-portal/catalog" class="erp-button erp-button-primary">
            <i class="pi pi-search"></i>
            <span>Browse Products</span>
          </a>
        </div>
      </div>

      <div class="cart-empty-wrap erp-card">
        <app-empty-state
          title="Your Shopping Cart is Empty"
          description="Browse our industrial catalog to discover manufactured parts, raw materials, and assemblies."
          icon="pi-shopping-bag"
          actionLabel="Explore Product Catalogue"
          (action)="goToCatalog()"
        ></app-empty-state>
      </div>
    </div>
  `,
  styles: [`
    .customer-cart-page {
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-4);
    }

    .milestone-banner {
      display: flex;
      align-items: center;
      gap: var(--erp-space-4);
      padding: var(--erp-space-5);
      background-color: var(--erp-bg-surface);
      border-left: 4px solid var(--erp-primary);
      flex-wrap: wrap;

      .banner-icon {
        width: 48px;
        height: 48px;
        border-radius: var(--erp-radius-md);
        background-color: var(--erp-primary-light);
        color: var(--erp-primary);
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 22px;
      }

      .banner-body {
        flex: 1;
        min-width: 260px;

        h2 {
          font-size: 1.15rem;
          margin: 0 0 4px 0;
          color: var(--erp-text-main);
        }

        p {
          font-size: 0.88rem;
          color: var(--erp-text-secondary);
          margin: 0;
          line-height: 1.4;
        }
      }
    }

    .cart-empty-wrap {
      padding: var(--erp-space-6) var(--erp-space-4);
      background-color: var(--erp-bg-surface);
      border-radius: var(--erp-radius-lg);
    }
  `]
})
export class CustomerCartComponent {
  goToCatalog(): void {
    window.location.href = '/customer-portal/catalog';
  }
}
