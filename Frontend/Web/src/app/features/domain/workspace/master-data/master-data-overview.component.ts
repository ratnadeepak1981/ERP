import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppStatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { TenantSetupService, TenantMasterDataSetupResult } from '../../../../core/services/tenant-setup.service';

interface MasterDataCard {
  title: string;
  badge: string;
  badgeSeverity: 'success' | 'info' | 'neutral' | 'warning';
  icon: string;
  description: string;
  route?: string;
  available: boolean;
  footnote?: string;
}

@Component({
  selector: 'app-master-data-overview',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    ButtonModule,
    InputTextModule,
    AppPageHeaderComponent,
    AppStatusBadgeComponent
  ],
  templateUrl: './master-data-overview.component.html',
  styleUrl: './master-data-overview.component.scss'
})
export class MasterDataOverviewComponent {
  readonly scopeService = inject(WorkspaceScopeService);
  readonly setupService = inject(TenantSetupService);

  readonly showSetupPanel = signal<boolean>(false);
  readonly showSetupDialog = this.showSetupPanel; // Backward-compatible alias
  readonly setupLoading = signal<boolean>(false);
  readonly setupError = signal<string | null>(null);
  readonly setupResult = signal<TenantMasterDataSetupResult | null>(null);
  selectedProfileCode = 'retail';

  readonly availableProfiles = [
    { code: 'retail', name: 'Retail (Gate 6 Baseline) — Default' }
  ];

  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Supply Chain Masters' }
  ];

  toggleSetupPanel(): void {
    this.showSetupPanel.update(v => !v);
    this.setupError.set(null);
  }

  openSetupDialog(): void {
    this.setupError.set(null);
    this.setupResult.set(null);
    this.selectedProfileCode = 'retail';
    this.showSetupPanel.set(true);
  }

  closeSetupDialog(): void {
    this.showSetupPanel.set(false);
  }

  runMasterDataSetup(): void {
    this.setupLoading.set(true);
    this.setupError.set(null);

    const companyId = this.scopeService.selectedCompany()?.id;
    const branchId = !this.scopeService.isAllBranches() ? this.scopeService.selectedBranch()?.id : undefined;

    this.setupService.initializeMasterData({
      companyId: companyId,
      branchId: branchId,
      profileCode: this.selectedProfileCode.trim() || 'retail'
    }).subscribe({
      next: (res) => {
        this.setupLoading.set(false);
        if (res?.succeeded && res.data) {
          this.setupResult.set(res.data);
        } else {
          this.setupError.set(res?.message || 'Master data setup did not return a success status.');
        }
      },
      error: (err) => {
        this.setupLoading.set(false);
        this.setupError.set(err?.error?.message || err?.message || 'Failed to complete master data setup.');
      }
    });
  }

  readonly masterCards: MasterDataCard[] = [
    {
      title: 'Product Master',
      badge: 'Core master',
      badgeSeverity: 'info',
      icon: 'pi pi-box',
      description: 'Products, services, codes, categories, units and purchase/sales/manufacturing flags.',
      route: '/app/master-data/products',
      available: true
    },
        {
      title: 'Customer Master',
      badge: 'Core master',
      badgeSeverity: 'info',
      icon: 'pi pi-users',
      description: 'Customer directory, billing addresses, contact details, credit limits and trading status.',
      route: '/app/master-data/customers',
      available: true
    },
{
      title: 'Supplier Master',
      badge: 'Core master',
      badgeSeverity: 'info',
      icon: 'pi pi-truck',
      description: 'Supplier details, contacts, addresses, payment terms and active status.',
      route: '/app/master-data/suppliers',
      available: true
    },
    {
      title: 'Categories & Brands',
      badge: 'Classification',
      badgeSeverity: 'neutral',
      icon: 'pi pi-tag',
      description: 'Product classification, brand, category hierarchy and reporting groups.',
      route: '/app/master-data/categories-brands',
      available: true
    },
    {
      title: 'Payment Terms',
      badge: 'Commercial',
      badgeSeverity: 'info',
      icon: 'pi pi-credit-card',
      description: 'Payment terms, credit conditions, and standard invoice due day calculations.',
      route: '/app/master-data/payment-terms',
      available: true
    },
    {
      title: 'Tax Codes',
      badge: 'Financial',
      badgeSeverity: 'info',
      icon: 'pi pi-percentage',
      description: 'Tax codes, percentage rates, purchase and sales applicability, and validity periods.',
      route: '/app/master-data/tax-codes',
      available: true
    },
    {
      title: 'Currencies',
      badge: 'Financial & FX',
      badgeSeverity: 'info',
      icon: 'pi pi-dollar',
      description: 'Tenant operational currencies, symbols, and decimal precision.',
      route: '/app/master-data/currencies',
      available: true
    },
    {
      title: 'Exchange Rates',
      badge: 'Financial & FX',
      badgeSeverity: 'info',
      icon: 'pi pi-chart-line',
      description: 'Maintain tenant-scoped direct currency-pair rates with daily and monthly effective dates.',
      route: '/app/master-data/exchange-rates',
      available: true
    },
    {
      title: 'Price Cards',
      badge: 'Pricing & Commercial',
      badgeSeverity: 'info',
      icon: 'pi pi-tags',
      description: 'Common Purchase and Sales price cards, tiered unit prices, and validity intervals.',
      route: '/app/master-data/price-cards',
      available: true
    },
    {
      title: 'Units of Measure',
      badge: 'Standards',
      badgeSeverity: 'neutral',
      icon: 'pi pi-sliders-v',
      description: 'Each, box, kg, litre, pack and controlled conversion rules.',
      route: '/app/master-data/uom',
      available: true
    },
    {
      title: 'Warehouse & Locations',
      badge: 'Operational',
      badgeSeverity: 'info',
      icon: 'pi pi-building',
      description: 'Warehouses, zones, locations, racks, shelves and bins where required.',
      route: '/app/master-data/warehouses',
      available: true
    },
    {
      title: 'Operational Reference Data',
      badge: 'Defaults',
      badgeSeverity: 'neutral',
      icon: 'pi pi-sliders-h',
      description: 'Receiving, QC, putaway, picking, rejected, staging and dispatch locations.',
      available: false,
      footnote: 'Configured per warehouse operational policy.'
    },
    {
      title: 'Supplier–Product Links',
      badge: 'Purchasing',
      badgeSeverity: 'neutral',
      icon: 'pi pi-link',
      description: 'Supplier item codes, preferred supplier, lead time and purchasing units.',
      available: false,
      footnote: 'Active when supplier pricing agreements are established.'
    },
    {
      title: 'Tracking & Replenishment',
      badge: 'Inventory Policy',
      badgeSeverity: 'neutral',
      icon: 'pi pi-calendar-plus',
      description: 'Batch/expiry requirements, reorder levels and minimum/maximum stock settings.',
      available: false,
      footnote: 'Add when supported by the domain APIs.'
    }
  ];
}
