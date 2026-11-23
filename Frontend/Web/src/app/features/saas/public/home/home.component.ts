import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { PublicNavService } from '../../../../core/services/public-nav.service';
import { TenantContextService } from '../../../../core/services/tenant-context.service';
import { EcommerceCatalogueService } from '../../../../core/services/ecommerce-catalogue.service';
import { PublicEcommerceProductDto, ProductCategorySummary } from '../../../../core/models/ecommerce-catalogue.models';
import { StatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';

export interface PlatformCapability {
  icon: string;
  title: string;
  description: string;
}

export interface BusinessServiceItem {
  icon: string;
  title: string;
  description: string;
}

export interface MilestoneMetric {
  value: string;
  label: string;
}

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterLink, ButtonModule, CardModule, StatusBadgeComponent, EmptyStateComponent],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent implements OnInit {
  readonly navService = inject(PublicNavService);
  readonly tenantContext = inject(TenantContextService);
  private readonly catalogueService = inject(EcommerceCatalogueService);
  private readonly router = inject(Router);

  readonly featuredProducts = signal<PublicEcommerceProductDto[]>([]);
  readonly productsLoading = signal<boolean>(false);

  readonly tenantName = computed(() => {
    const tenant = this.tenantContext.currentTenant();
    return tenant?.name || this.tenantContext.subdomain() || 'Enterprise Organization';
  });

  readonly tenantIndustry = computed(() => {
    return this.tenantContext.currentTenant()?.industryType || 'Enterprise Operations';
  });

  readonly tenantStructure = computed(() => {
    return this.tenantContext.currentTenant()?.companyStructure || 'Single Company';
  });

  readonly tenantSubdomain = computed(() => {
    return this.tenantContext.subdomain() || '';
  });

  readonly eCommerceEnabled = computed(() => {
    return !!this.tenantContext.currentTenant()?.eCommerceEnabled;
  });

  readonly tenantDescription = computed(() => {
    const name = this.tenantName();
    const industry = this.tenantIndustry();
    return `Welcome to the dedicated commercial portal for ${name}. Providing certified ${industry.toLowerCase()} solutions, verified quality assurance, and end-to-end supply chain fulfillment.`;
  });

  readonly milestones = computed<MilestoneMetric[]>(() => {
    const list: MilestoneMetric[] = [];
    const tenant = this.tenantContext.currentTenant();
    if (tenant?.companyStructure) {
      list.push({ value: tenant.companyStructure, label: 'Organization Structure' });
    }
    if (tenant?.industryType) {
      list.push({ value: tenant.industryType, label: 'Industry Sector' });
    }
    list.push({ value: '100% Certified', label: 'Quality & Traceability' });
    list.push({ value: 'Direct B2B', label: 'Commercial Fulfillment' });
    return list;
  });

  readonly categories = computed<ProductCategorySummary[]>(() => {
    const prods = this.featuredProducts();
    if (prods.length > 0) {
      const map = new Map<string, { name: string; count: number }>();
      for (const p of prods) {
        const catName = p.categoryName || 'General Components';
        const existing = map.get(catName);
        if (existing) {
          existing.count++;
        } else {
          map.set(catName, { name: catName, count: 1 });
        }
      }
      return Array.from(map.entries()).map(([name, data], idx) => ({
        id: `cat-${idx + 1}`,
        name: data.name,
        description: `${data.count} items in category`,
        icon: this.getCategoryIcon(data.name),
        itemCount: data.count
      }));
    }

    return this.getDefaultCategoriesForIndustry(this.tenantIndustry());
  });

  readonly businessServices = computed<BusinessServiceItem[]>(() => {
    return this.getServicesForIndustry(this.tenantIndustry());
  });

  readonly capabilities: PlatformCapability[] = [
    {
      icon: 'pi pi-shopping-cart',
      title: 'Procurement & Purchasing',
      description: 'Manage purchase requisitions, supplier information, purchasing workflows and approvals.'
    },
    {
      icon: 'pi pi-box',
      title: 'Inventory & Warehousing',
      description: 'Track stock across multiple warehouses and locations, with inventory valuation and stock movement visibility.'
    },
    {
      icon: 'pi pi-send',
      title: 'Sales & Order Fulfillment',
      description: 'Manage the sales lifecycle, from customer orders to dispatch and delivery tracking.'
    },
    {
      icon: 'pi pi-sitemap',
      title: 'Business Operations',
      description: 'Connect business processes, company structures and branch operations through a unified platform.'
    },
    {
      icon: 'pi pi-chart-line',
      title: 'AI Business Intelligence',
      description: 'Gain insights into purchasing, inventory, sales performance, demand patterns and business impact.'
    }
  ];

  ngOnInit(): void {
    if (this.tenantContext.isTenantDomain()) {
      this.loadFeaturedProducts();
    }
  }

  loadFeaturedProducts(): void {
    const tenantId = this.tenantContext.currentTenant()?.tenantId;
    if (!tenantId) {
      return;
    }

    this.productsLoading.set(true);
    this.catalogueService.getPublicCatalogue(tenantId).subscribe({
      next: (products) => {
        this.productsLoading.set(false);
        this.featuredProducts.set(products || []);
      },
      error: () => {
        this.productsLoading.set(false);
        this.featuredProducts.set([]);
      }
    });
  }

  scrollToSection(sectionId: string, event?: Event): void {
    if (event) {
      event.preventDefault();
    }
    if (sectionId === 'home') {
      window.scrollTo({ top: 0, behavior: 'smooth' });
      return;
    }
    const element = document.getElementById(sectionId);
    if (element) {
      element.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }

  switchToDirectory(): void {
    this.navService.setActiveTab('directory');
  }

  switchToPlatform(): void {
    this.navService.setActiveTab('platform');
  }

  private getCategoryIcon(name: string): string {
    const lower = name.toLowerCase();
    if (lower.includes('machin') || lower.includes('cnc') || lower.includes('part') || lower.includes('metal')) return 'pi pi-cog';
    if (lower.includes('hydraul') || lower.includes('valve') || lower.includes('flow')) return 'pi pi-wrench';
    if (lower.includes('fastener') || lower.includes('bolt') || lower.includes('hardware')) return 'pi pi-box';
    if (lower.includes('electr') || lower.includes('control') || lower.includes('sensor')) return 'pi pi-sliders-h';
    if (lower.includes('chemical') || lower.includes('fluid') || lower.includes('oil')) return 'pi pi-filter';
    if (lower.includes('packag') || lower.includes('box')) return 'pi pi-inbox';
    return 'pi pi-tag';
  }

  private getDefaultCategoriesForIndustry(industry: string): ProductCategorySummary[] {
    const lower = industry.toLowerCase();
    if (lower.includes('manufactur')) {
      return [
        { id: 'cat-1', name: 'Precision CNC Components', description: 'Certified machined parts', icon: 'pi pi-cog' },
        { id: 'cat-2', name: 'Hydraulic & Flow Systems', description: 'Valves, manifolds & regulators', icon: 'pi pi-wrench' },
        { id: 'cat-3', name: 'Industrial Hardware & Fasteners', description: 'High-tensile fasteners', icon: 'pi pi-box' },
        { id: 'cat-4', name: 'Electrical & Control Assemblies', description: 'Sensors and control units', icon: 'pi pi-sliders-h' }
      ];
    }
    if (lower.includes('retail') || lower.includes('supermarket')) {
      return [
        { id: 'cat-1', name: 'Packaged Goods & Groceries', description: 'Fast-moving consumer products', icon: 'pi pi-shopping-bag' },
        { id: 'cat-2', name: 'Fresh & Chilled Produce', description: 'Cold-chain tracked supply', icon: 'pi pi-apple' },
        { id: 'cat-3', name: 'Household & General Merchandise', description: 'Essential consumer goods', icon: 'pi pi-home' },
        { id: 'cat-4', name: 'Personal Care & Wellness', description: 'Health and hygiene lines', icon: 'pi pi-heart' }
      ];
    }
    return [
      { id: 'cat-1', name: 'Commercial Raw Materials', description: 'Production-grade feedstocks', icon: 'pi pi-box' },
      { id: 'cat-2', name: 'Manufactured Assemblies', description: 'Integrated sub-systems', icon: 'pi pi-cog' },
      { id: 'cat-3', name: 'Distribution Supplies', description: 'Bulk wholesale units', icon: 'pi pi-truck' },
      { id: 'cat-4', name: 'Specialized Hardware', description: 'Certified components', icon: 'pi pi-tag' }
    ];
  }

  private getServicesForIndustry(industry: string): BusinessServiceItem[] {
    const lower = industry.toLowerCase();
    if (lower.includes('manufactur')) {
      return [
        { icon: 'pi pi-sliders-v', title: 'Custom Fabrication', description: 'Build-to-print discrete component manufacturing with high precision CNC tolerances.' },
        { icon: 'pi pi-truck', title: 'Logistics & Warehousing', description: 'Multi-warehouse stock holding, scheduled JIT delivery and regional distribution.' },
        { icon: 'pi pi-verified', title: 'Quality Assurance & Mill Certs', description: 'Batch traceability, material test reports (MTR) and certified lot documentation.' },
        { icon: 'pi pi-credit-card', title: 'Commercial Trade Accounts', description: '30-day corporate trade terms and on-account purchasing for authorized partners.' },
        { icon: 'pi pi-sync', title: 'Contract Volume Pricing', description: 'Customer-specific sales price cards, tiered volume rates and dedicated agreements.' },
        { icon: 'pi pi-shield', title: 'Dedicated Engineering Support', description: 'In-house technical consultation, application engineering and order management.' }
      ];
    }
    return [
      { icon: 'pi pi-box', title: 'Wholesale & Bulk Supply', description: 'High-volume commercial replenishment with flexible minimum order quantities.' },
      { icon: 'pi pi-truck', title: 'Multi-Warehouse Logistics', description: 'Reliable stock visibility, dispatched from regional distribution centers.' },
      { icon: 'pi pi-verified', title: 'Quality & Lot Traceability', description: 'System-enforced batch and serial management across all storage locations.' },
      { icon: 'pi pi-credit-card', title: 'Flexible Trade Credit', description: 'Approved credit limits, commercial invoicing and consolidated billing.' },
      { icon: 'pi pi-sync', title: 'Assigned Price Agreements', description: 'Negotiated contract pricing maintained directly through your customer account.' },
      { icon: 'pi pi-users', title: 'Dedicated Account Management', description: 'Priority customer service desk and procurement coordination.' }
    ];
  }
}
