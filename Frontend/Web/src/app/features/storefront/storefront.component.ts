import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { EcommerceCatalogueService } from '../../core/services/ecommerce-catalogue.service';
import { TenantContextService } from '../../core/services/tenant-context.service';
import { ThemeService } from '../../core/services/theme.service';
import { PublicEcommerceProductDto } from '../../core/models/ecommerce-catalogue.models';

export interface CategoryFilterOption {
  name: string;
  count: number;
}

@Component({
  selector: 'app-storefront',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    ButtonModule,
    StatusBadgeComponent,
    EmptyStateComponent
  ],
  templateUrl: './storefront.component.html',
  styleUrl: './storefront.component.scss'
})
export class StorefrontComponent implements OnInit {
  private readonly catalogueService = inject(EcommerceCatalogueService);
  private readonly router = inject(Router);
  readonly tenantContext = inject(TenantContextService);
  readonly themeService = inject(ThemeService);

  readonly rawProducts = signal<PublicEcommerceProductDto[]>([]);
  readonly searchQuery = signal<string>('');
  readonly selectedCategory = signal<string>('ALL');
  readonly sortBy = signal<'name' | 'price-asc' | 'price-desc'>('name');
  readonly loading = signal<boolean>(true);
  readonly errorMessage = signal<string | null>(null);

  readonly tenantName = computed(() => {
    return this.tenantContext.currentTenant()?.name || this.tenantContext.subdomain() || 'ApexERP';
  });

  readonly tenantIndustry = computed(() => {
    return this.tenantContext.currentTenant()?.industryType || 'Precision Components & Materials';
  });

  readonly categories = computed<CategoryFilterOption[]>(() => {
    const map = new Map<string, number>();
    const items = this.rawProducts();
    for (const p of items) {
      const cat = p.categoryName || 'General';
      map.set(cat, (map.get(cat) || 0) + 1);
    }
    const result: CategoryFilterOption[] = [
      { name: 'ALL', count: items.length }
    ];
    for (const [name, count] of map.entries()) {
      result.push({ name, count });
    }
    return result;
  });

  readonly filteredProducts = computed<PublicEcommerceProductDto[]>(() => {
    let items = [...this.rawProducts()];
    const query = this.searchQuery().trim().toLowerCase();
    const cat = this.selectedCategory();
    const sort = this.sortBy();

    // Category filter
    if (cat !== 'ALL') {
      items = items.filter(p => (p.categoryName || 'General') === cat);
    }

    // Search query filter
    if (query) {
      items = items.filter(p =>
        p.productName.toLowerCase().includes(query) ||
        p.productCode.toLowerCase().includes(query) ||
        (p.onlineDescription && p.onlineDescription.toLowerCase().includes(query)) ||
        (p.categoryName && p.categoryName.toLowerCase().includes(query))
      );
    }

    // Sort order
    if (sort === 'price-asc') {
      items.sort((a, b) => a.sellingPrice - b.sellingPrice);
    } else if (sort === 'price-desc') {
      items.sort((a, b) => b.sellingPrice - a.sellingPrice);
    } else {
      items.sort((a, b) => a.productName.localeCompare(b.productName));
    }

    return items;
  });

  ngOnInit(): void {
    this.loadCatalogue();
  }

  loadCatalogue(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    const companyId = this.tenantContext.currentTenant()?.tenantId || '';

    this.catalogueService.getPublicCatalogue(companyId).subscribe({
      next: (items) => {
        this.loading.set(false);
        this.rawProducts.set(items || []);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Unable to load catalogue items at this time.');
      }
    });
  }

  onSearchChange(term: string): void {
    this.searchQuery.set(term);
  }

  selectCategory(categoryName: string): void {
    this.selectedCategory.set(categoryName);
  }

  onSortChange(sort: 'name' | 'price-asc' | 'price-desc'): void {
    this.sortBy.set(sort);
  }

  clearFilters(): void {
    this.searchQuery.set('');
    this.selectedCategory.set('ALL');
    this.sortBy.set('name');
  }

  getCategoryIcon(categoryName?: string): string {
    const name = (categoryName || '').toLowerCase();
    if (name.includes('machin') || name.includes('cnc') || name.includes('precision')) {
      return 'pi pi-cog';
    }
    if (name.includes('hard') || name.includes('fasten') || name.includes('tool')) {
      return 'pi pi-wrench';
    }
    if (name.includes('hydrau') || name.includes('valve') || name.includes('flow')) {
      return 'pi pi-sliders-h';
    }
    if (name.includes('pack') || name.includes('box') || name.includes('contain')) {
      return 'pi pi-box';
    }
    if (name.includes('electr') || name.includes('sensor') || name.includes('power')) {
      return 'pi pi-bolt';
    }
    return 'pi pi-tag';
  }

  viewProductDetail(productId: string): void {
    this.router.navigate(['/storefront/products', productId]);
  }
}
