import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { EcommerceCatalogueService } from '../../../core/services/ecommerce-catalogue.service';
import { TenantContextService } from '../../../core/services/tenant-context.service';
import { ThemeService } from '../../../core/services/theme.service';
import { PublicEcommerceProductDto } from '../../../core/models/ecommerce-catalogue.models';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { KNOWN_STOREFRONTS } from '../storefront.component';

@Component({
  selector: 'app-product-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    ButtonModule,
    StatusBadgeComponent,
    EmptyStateComponent
  ],
  templateUrl: './product-detail.component.html',
  styleUrl: './product-detail.component.scss'
})
export class ProductDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly catalogueService = inject(EcommerceCatalogueService);
  readonly tenantContext = inject(TenantContextService);
  readonly themeService = inject(ThemeService);

  readonly product = signal<PublicEcommerceProductDto | null>(null);
  readonly relatedProducts = signal<PublicEcommerceProductDto[]>([]);
  readonly loading = signal<boolean>(true);
  readonly errorMessage = signal<string | null>(null);

  readonly activeFallback = computed(() => {
    const querySlug = this.route.snapshot.queryParamMap.get('tenant')?.toLowerCase();
    return KNOWN_STOREFRONTS.find(s => s.slug === querySlug) || KNOWN_STOREFRONTS[0];
  });

  readonly tenantName = computed(() => {
    return this.tenantContext.currentTenant()?.name || this.activeFallback().name;
  });

  readonly tenantIndustry = computed(() => {
    return this.tenantContext.currentTenant()?.industryType || this.activeFallback().industry;
  });

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const productId = params.get('productId');
      if (productId) {
        this.loadProduct(productId);
      } else {
        this.loading.set(false);
        this.errorMessage.set('Product ID was not specified.');
      }
    });
  }

  loadProduct(productId: string): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    const companyId = this.tenantContext.currentTenant()?.tenantId || this.activeFallback().companyId;

    this.catalogueService.getPublicProductDetail(companyId, productId).subscribe({
      next: (prod) => {
        this.loading.set(false);
        if (prod) {
          this.product.set(prod);
          this.loadRelatedProducts(companyId, prod);
        } else {
          this.errorMessage.set('Product not found in this catalogue.');
        }
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Unable to load product specifications.');
      }
    });
  }

  private loadRelatedProducts(companyId: string, currentProd: PublicEcommerceProductDto): void {
    this.catalogueService.getPublicCatalogue(companyId, { categoryId: currentProd.categoryId }).subscribe({
      next: (items) => {
        const related = (items || [])
          .filter(p => p.productId !== currentProd.productId)
          .slice(0, 4);
        this.relatedProducts.set(related);
      }
    });
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

  goBack(): void {
    this.router.navigate(['/storefront']);
  }
}
