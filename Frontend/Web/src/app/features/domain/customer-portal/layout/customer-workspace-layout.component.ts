import { Component, signal, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, RouterLink, RouterLinkActive, Router } from '@angular/router';
import { ThemeService } from '../../../../core/services/theme.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { TenantContextService } from '../../../../core/services/tenant-context.service';

export interface CustomerNavItem {
  label: string;
  icon: string;
  route: string;
  badge?: string;
}

export interface CustomerNavGroup {
  groupName: string;
  items: CustomerNavItem[];
}

@Component({
  selector: 'app-customer-workspace-layout',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive
  ],
  templateUrl: './customer-workspace-layout.component.html',
  styleUrl: './customer-workspace-layout.component.scss'
})
export class CustomerWorkspaceLayoutComponent {
  readonly themeService = inject(ThemeService);
  readonly authService = inject(AuthService);
  readonly tenantContext = inject(TenantContextService);
  private readonly router = inject(Router);

  readonly sidebarCollapsed = signal<boolean>(false);
  readonly mobileMenuOpen = signal<boolean>(false);

  readonly tenantName = computed(() => {
    return this.tenantContext.currentTenant()?.name || this.tenantContext.subdomain() || 'Alpha Manufacturing Ltd';
  });

  readonly isOrganisation = computed(() => {
    return this.authService.isCustomerCompany();
  });

  readonly brandTag = computed(() => {
    return this.isOrganisation() ? 'B2B Corporate Portal' : 'Retail Customer Portal';
  });

  // Distinct navigation groups for Retail vs Organisation B2B
  readonly navGroups = computed<CustomerNavGroup[]>(() => {
    if (this.isOrganisation()) {
      return [
        {
          groupName: 'Corporate Overview',
          items: [
            { label: 'Dashboard', icon: 'pi pi-chart-bar', route: '/customer-portal/dashboard' }
          ]
        },
        {
          groupName: 'Catalogue & Purchasing',
          items: [
            { label: 'Product Catalogue', icon: 'pi pi-shopping-bag', route: '/customer-portal/catalog' },
            { label: 'Shopping Cart', icon: 'pi pi-shopping-cart', route: '/customer-portal/cart' },
            { label: 'Quotations', icon: 'pi pi-file-edit', route: '/customer-portal/quotations' },
            { label: 'Direct Orders', icon: 'pi pi-send', route: '/customer-portal/direct-orders' },
            { label: 'Order History', icon: 'pi pi-receipt', route: '/customer-portal/orders' }
          ]
        },
        {
          groupName: 'Company Account',
          items: [
            { label: 'Company Profile', icon: 'pi pi-building', route: '/customer-portal/profile' },
            { label: 'Delivery Addresses', icon: 'pi pi-map-marker', route: '/customer-portal/addresses' }
          ]
        }
      ];
    }

    // Default Retail Customer Navigation
    return [
      {
        groupName: 'Overview',
        items: [
          { label: 'Dashboard', icon: 'pi pi-home', route: '/customer-portal/dashboard' }
        ]
      },
      {
        groupName: 'Shopping',
        items: [
          { label: 'Product Catalogue', icon: 'pi pi-shopping-bag', route: '/customer-portal/catalog' },
          { label: 'Shopping Cart', icon: 'pi pi-shopping-cart', route: '/customer-portal/cart' }
        ]
      },
      {
        groupName: 'My Account',
        items: [
          { label: 'My Orders', icon: 'pi pi-receipt', route: '/customer-portal/orders' },
          { label: 'My Addresses', icon: 'pi pi-map-marker', route: '/customer-portal/addresses' },
          { label: 'My Profile', icon: 'pi pi-user', route: '/customer-portal/profile' }
        ]
      }
    ];
  });

  toggleSidebar(): void {
    this.sidebarCollapsed.update(val => !val);
  }

  toggleMobileMenu(): void {
    this.mobileMenuOpen.update(val => !val);
  }

  closeMobileMenu(): void {
    this.mobileMenuOpen.set(false);
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  onSignOut(): void {
    this.authService.logout();
    this.router.navigate(['/']);
  }
}
