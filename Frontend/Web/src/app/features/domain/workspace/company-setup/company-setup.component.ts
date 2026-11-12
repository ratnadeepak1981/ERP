import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { FormsModule } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { SharedModule } from 'primeng/api';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppStatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { AppDataTableComponent } from '../../../../shared/components/data-table/data-table.component';
import { DataTableColumn, DataTableAction } from '../../../../shared/models/data-table-config.model';
import { CompanyService } from '../../../../core/services/company.service';
import { MasterDataService } from '../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { TenantSetupService, TenantUserSetupResult, TenantMasterDataSetupResult } from '../../../../core/services/tenant-setup.service';
import { Company, Branch, CurrencyItem } from '../../../../core/models/scope.models';

export interface OnboardingStep {
  stepNumber: number;
  title: string;
  description: string;
  status: 'completed' | 'in_progress' | 'pending';
  icon: string;
  badgeText: string;
}

export const STANDARD_BASE_CURRENCIES: CurrencyItem[] = [
  { currencyId: 'LKR', tenantId: '', currencyCode: 'LKR', currencyName: 'Sri Lankan Rupee (Default)', symbol: 'Rs', decimalPlaces: 2, isActive: true },
  { currencyId: 'USD', tenantId: '', currencyCode: 'USD', currencyName: 'US Dollar', symbol: '$', decimalPlaces: 2, isActive: true },
  { currencyId: 'EUR', tenantId: '', currencyCode: 'EUR', currencyName: 'Euro', symbol: '€', decimalPlaces: 2, isActive: true },
  { currencyId: 'GBP', tenantId: '', currencyCode: 'GBP', currencyName: 'British Pound', symbol: '£', decimalPlaces: 2, isActive: true },
  { currencyId: 'INR', tenantId: '', currencyCode: 'INR', currencyName: 'Indian Rupee', symbol: '₹', decimalPlaces: 2, isActive: true }
];

export interface IndustryProfileOption {
  code: string;
  name: string;
  description: string;
}

@Component({
  selector: 'app-company-setup',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    ButtonModule,
    CardModule,
    InputTextModule,
    SharedModule,
    AppPageHeaderComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AppDataTableComponent
  ],
  templateUrl: './company-setup.component.html',
  styleUrl: './company-setup.component.scss'
})
export class CompanySetupComponent implements OnInit {
  readonly companyService = inject(CompanyService);
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly tenantSetupService = inject(TenantSetupService);

  readonly loading = signal<boolean>(true);
  readonly error = signal<string | null>(null);
  readonly saving = signal<boolean>(false);
  readonly formError = signal<string | null>(null);

  readonly companies = signal<Company[]>([]);
  readonly branches = signal<Branch[]>([]);
  readonly activeCurrencies = signal<CurrencyItem[]>(STANDARD_BASE_CURRENCIES);
  readonly currenciesLoading = signal<boolean>(false);
  readonly warehouseCount = signal<number>(0);
  readonly productCount = signal<number>(0);
  readonly steps = signal<OnboardingStep[]>([]);

  // Sequential Onboarding Stepper (1: Organization, 2: Users, 3: Master Data)
  readonly activeOnboardingStep = signal<number>(1);

  // Step 1 Form Fields
  initCompanyName = '';
  initCompanyCode = '';
  initCompanyCurrencyCode = 'LKR';
  initBranchName = '';
  initBranchCode = 'MAIN';

  // Step 2 User Provisioning
  readonly userProvisioningLoading = signal<boolean>(false);
  readonly userProvisioningSuccess = signal<boolean>(false);
  readonly userProvisioningResult = signal<TenantUserSetupResult | null>(null);

  // Step 3 Master Data Setup
  readonly availableProfiles: IndustryProfileOption[] = [
    { code: 'retail', name: 'Retail (Gate 6 Baseline) — Default', description: 'Currencies, categories, 5 products, barcodes, 2 warehouses, zones, locations, pricing, suppliers, customers.' }
  ];
  selectedIndustryProfile = 'retail';
  readonly masterDataLoading = signal<boolean>(false);
  readonly masterDataSuccess = signal<boolean>(false);
  readonly masterDataResult = signal<TenantMasterDataSetupResult | null>(null);
  readonly masterDataError = signal<string | null>(null);

  // Inline forms for additional Company / Branch creation
  readonly showAddCompanyInline = signal<boolean>(false);
  readonly showAddBranchInline = signal<boolean>(false);
  newCompanyName = '';
  newCompanyCode = '';
  newCompanyCurrencyCode = 'LKR';
  newBranchCompanyId = '';
  newBranchName = '';
  newBranchCode = '';

  readonly companyColumns: DataTableColumn[] = [
    { field: 'code', header: 'Company Code', sortable: true, filterable: true, width: '180px' },
    { field: 'name', header: 'Company Name', sortable: true, filterable: true },
    { field: 'currency', header: 'Currency', sortable: true, filterable: true, width: '120px' },
    { field: 'status', header: 'Status', type: 'badge', sortable: true, filterable: true, width: '140px', badgeSeverityMap: (val, item) => item.isActive ? 'success' : 'neutral' },
    { field: 'branchCount', header: 'Branches', sortable: true, filterable: false, width: '140px' },
    { field: 'actions', header: 'Working Company', type: 'actions', sortable: false, filterable: false, width: '180px' }
  ];

  readonly tableCompanies = computed(() => {
    const branchCountStr = `${this.branches().length} Branches`;
    return this.companies().map(c => ({
      ...c,
      status: c.isActive ? 'Active' : 'Inactive',
      currency: c.baseCurrencyCode || 'LKR',
      branchCount: branchCountStr
    }));
  });

  readonly companyActions: DataTableAction<Company>[] = [
    {
      id: 'activeScopeBadge',
      label: 'Current Company',
      icon: 'pi pi-check-circle',
      severity: 'success',
      outlined: false,
      disabled: () => true,
      action: () => {},
      visible: (row) => this.scopeService.selectedCompany()?.id === row.id
    },
    {
      id: 'selectScope',
      label: 'Select Company',
      icon: 'pi pi-check',
      severity: 'primary',
      outlined: true,
      action: (row) => this.setActiveCompany(row),
      visible: (row) => this.scopeService.selectedCompany()?.id !== row.id
    }
  ];

  readonly branchColumns: DataTableColumn[] = [
    { field: 'code', header: 'Branch Code', sortable: true, filterable: true, width: '180px' },
    { field: 'name', header: 'Branch / Plant Name', sortable: true, filterable: true },
    { field: 'status', header: 'Status', type: 'badge', sortable: true, filterable: true, width: '140px', badgeSeverityMap: (val, item) => item.isActive ? 'success' : 'neutral' },
    { field: 'actions', header: 'Working Branch', type: 'actions', sortable: false, filterable: false, width: '180px' }
  ];

  readonly tableBranches = computed(() => {
    return this.branches().map(b => ({
      ...b,
      status: b.isActive ? 'Operational' : 'Inactive'
    }));
  });

  readonly branchActions: DataTableAction<Branch>[] = [
    {
      id: 'activeScopeBadge',
      label: 'Current Branch',
      icon: 'pi pi-check-circle',
      severity: 'success',
      outlined: false,
      disabled: () => true,
      action: () => {},
      visible: (row) => !this.scopeService.isAllBranches() && this.scopeService.selectedBranch()?.id === row.id
    },
    {
      id: 'selectScope',
      label: 'Select Branch',
      icon: 'pi pi-check',
      severity: 'primary',
      outlined: true,
      action: (row) => this.setActiveBranch(row),
      visible: (row) => this.scopeService.isAllBranches() || this.scopeService.selectedBranch()?.id !== row.id
    }
  ];

  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Tenant Onboarding' }
  ];

  ngOnInit(): void {
    this.initOnboardingDefaults();
    this.loadCurrencies();
    this.loadOnboardingState();
  }

  private initOnboardingDefaults(): void {
    const rawTenant = this.scopeService.activeTenantName() || 'Enterprise';
    const cleanPrefix = rawTenant.split('@')[0].replace(/[^A-Za-z0-9]/g, ' ').trim();
    const titleCase = cleanPrefix ? cleanPrefix.charAt(0).toUpperCase() + cleanPrefix.slice(1) : 'Apex Enterprise';
    const shortCode = cleanPrefix.replace(/[^A-Za-z0-9]/g, '').substring(0, 6).toUpperCase() || 'APX';

    if (!this.initCompanyName) {
      this.initCompanyName = `${titleCase} (Pvt) Ltd`;
    }
    if (!this.initCompanyCode) {
      this.initCompanyCode = shortCode;
    }
    if (!this.initCompanyCurrencyCode) {
      this.initCompanyCurrencyCode = 'LKR';
    }
    if (!this.initBranchName) {
      this.initBranchName = `${titleCase} Main Branch`;
    }
    if (!this.initBranchCode) {
      this.initBranchCode = 'MAIN';
    }
  }

  private loadCurrencies(): void {
    this.currenciesLoading.set(true);
    this.masterDataService.getCurrencies().subscribe({
      next: currencies => {
        const active = (currencies || []).filter(c => c.isActive);
        if (active.length > 0) {
          this.activeCurrencies.set(active);
        } else {
          this.activeCurrencies.set(STANDARD_BASE_CURRENCIES);
        }
        if (!this.initCompanyCurrencyCode) {
          this.initCompanyCurrencyCode = 'LKR';
        }
        this.currenciesLoading.set(false);
      },
      error: () => {
        this.activeCurrencies.set(STANDARD_BASE_CURRENCIES);
        if (!this.initCompanyCurrencyCode) {
          this.initCompanyCurrencyCode = 'LKR';
        }
        this.currenciesLoading.set(false);
      }
    });
  }

  loadOnboardingState(): void {
    this.loading.set(true);
    this.error.set(null);

    this.companyService.getCompanies().subscribe({
      next: (comps) => {
        this.companies.set(comps);
        const selectedId = this.scopeService.selectedCompany()?.id;
        const activeComp = comps.find(c => c.id === selectedId) || comps[0];

        if (!activeComp) {
          this.buildSteps(comps, [], 0, 0);
          this.activeOnboardingStep.set(1);
          this.loading.set(false);
          return;
        }

        this.companyService.getBranches(activeComp.id).subscribe({
          next: (brs) => {
            this.branches.set(brs);

            this.masterDataService.getWarehouses().subscribe({
              next: (whs) => {
                this.warehouseCount.set(whs.length);

                this.masterDataService.getProducts().subscribe({
                  next: (prods) => {
                    this.productCount.set(prods.length);
                    this.buildSteps(comps, brs, whs.length, prods.length);

                    // Determine sensible active step
                    if (comps.length === 0) {
                      this.activeOnboardingStep.set(1);
                    } else if (prods.length === 0) {
                      this.activeOnboardingStep.set(3);
                    }
                    this.loading.set(false);
                  },
                  error: () => {
                    this.buildSteps(comps, brs, whs.length, 0);
                    this.loading.set(false);
                  }
                });
              },
              error: () => {
                this.buildSteps(comps, brs, 0, 0);
                this.loading.set(false);
              }
            });
          },
          error: (err) => {
            this.loading.set(false);
            this.error.set(err?.error?.message || 'Failed to load branch configuration.');
          }
        });
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.message || 'Unable to retrieve company configuration from backend.');
      }
    });
  }

  private buildSteps(comps: Company[], brs: Branch[], whCount: number, prodCount: number): void {
    const hasCompany = comps.length > 0;
    const hasBranch = brs.length > 0;
    const hasMasters = prodCount > 0 && whCount > 0;

    const stepsList: OnboardingStep[] = [
      {
        stepNumber: 1,
        title: 'Organization & Base Currency',
        description: hasCompany
          ? `${comps[0].name} (${comps[0].code}) configured with base currency LKR.`
          : 'Define operating company, branch, and ensure base operating currency (LKR).',
        status: hasCompany ? 'completed' : 'in_progress',
        icon: 'pi pi-building',
        badgeText: hasCompany ? 'Configured' : 'Step 1'
      },
      {
        stepNumber: 2,
        title: 'Departmental User Provisioning',
        description: this.userProvisioningSuccess()
          ? 'Standard departmental roles (Procurement, Inventory, Sales, Finance, Approver) active.'
          : 'Provision standard departmental role accounts with Company and Branch scopes.',
        status: this.userProvisioningSuccess() ? 'completed' : (hasCompany ? 'in_progress' : 'pending'),
        icon: 'pi pi-users',
        badgeText: this.userProvisioningSuccess() ? 'Provisioned' : 'Step 2'
      },
      {
        stepNumber: 3,
        title: 'Supply Chain Master Data',
        description: hasMasters
          ? `${prodCount} products & ${whCount} warehouses active across catalog.`
          : 'Seed products, packaging, barcodes, warehouses, suppliers, customers, and price cards.',
        status: hasMasters ? 'completed' : (hasBranch ? 'in_progress' : 'pending'),
        icon: 'pi pi-database',
        badgeText: hasMasters ? 'Active Baseline' : 'Step 3'
      }
    ];

    this.steps.set(stepsList);
  }

  goToStep(stepNumber: number): void {
    this.activeOnboardingStep.set(stepNumber);
    this.formError.set(null);
  }

  saveStep1Organization(): void {
    const cName = this.initCompanyName.trim();
    const cCode = this.initCompanyCode.trim().toUpperCase();
    const bName = this.initBranchName.trim();
    const bCode = this.initBranchCode.trim().toUpperCase();
    const currCode = this.initCompanyCurrencyCode || 'LKR';

    if (!cName || !cCode) {
      this.formError.set('Company name and company code are required.');
      return;
    }
    if (!bName || !bCode) {
      this.formError.set('Branch name and branch code are required.');
      return;
    }

    this.saving.set(true);
    this.formError.set(null);

    this.tenantSetupService.initializeOrganization({
      companyName: cName,
      companyCode: cCode,
      baseCurrencyCode: currCode,
      branchName: bName,
      branchCode: bCode
    }).subscribe({
      next: (res) => {
        this.saving.set(false);
        this.activeOnboardingStep.set(2);
        this.loadOnboardingState();
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(err?.error?.message || err?.message || 'Failed to complete organization setup.');
      }
    });
  }

  saveStep2Users(): void {
    const activeComp = this.scopeService.selectedCompany() || this.companies()[0];
    const activeBranch = !this.scopeService.isAllBranches() ? this.scopeService.selectedBranch() : this.branches()[0];

    this.userProvisioningLoading.set(true);
    this.formError.set(null);

    this.tenantSetupService.initializeUsers({
      companyId: activeComp?.id,
      branchId: activeBranch?.id
    }).subscribe({
      next: (res) => {
        this.userProvisioningLoading.set(false);
        this.userProvisioningSuccess.set(true);
        this.userProvisioningResult.set(res?.data || null);
        this.activeOnboardingStep.set(3);
      },
      error: (err) => {
        this.userProvisioningLoading.set(false);
        this.formError.set(err?.error?.message || err?.message || 'Failed to provision tenant users.');
      }
    });
  }

  saveStep3MasterData(): void {
    const activeComp = this.scopeService.selectedCompany() || this.companies()[0];
    const activeBranch = !this.scopeService.isAllBranches() ? this.scopeService.selectedBranch() : this.branches()[0];

    this.masterDataLoading.set(true);
    this.masterDataError.set(null);

    this.tenantSetupService.initializeMasterData({
      companyId: activeComp?.id,
      branchId: activeBranch?.id,
      profileCode: this.selectedIndustryProfile
    }).subscribe({
      next: (res) => {
        this.masterDataLoading.set(false);
        if (res?.succeeded && res.data) {
          this.masterDataSuccess.set(true);
          this.masterDataResult.set(res.data);
          this.loadOnboardingState();
        } else {
          this.masterDataError.set(res?.message || 'Master data setup failed.');
        }
      },
      error: (err) => {
        this.masterDataLoading.set(false);
        this.masterDataError.set(err?.error?.message || err?.message || 'Failed to complete master data setup.');
      }
    });
  }

  toggleAddCompanyInline(): void {
    this.showAddCompanyInline.update(v => !v);
    this.newCompanyName = '';
    this.newCompanyCode = '';
    this.newCompanyCurrencyCode = 'LKR';
    this.formError.set(null);
  }

  saveNewCompany(): void {
    const trimmedName = this.newCompanyName.trim();
    const trimmedCode = this.newCompanyCode.trim().toUpperCase();
    const currencyCode = this.newCompanyCurrencyCode || 'LKR';

    if (!trimmedName || !trimmedCode) {
      this.formError.set('Company name and company code are required.');
      return;
    }

    this.saving.set(true);
    this.formError.set(null);

    this.tenantSetupService.initializeOrganization({
      companyName: trimmedName,
      companyCode: trimmedCode,
      baseCurrencyCode: currencyCode
    }).subscribe({
      next: (res) => {
        this.saving.set(false);
        this.showAddCompanyInline.set(false);
        this.loadOnboardingState();
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(err?.error?.message || err?.message || 'Failed to register company.');
      }
    });
  }

  toggleAddBranchInline(): void {
    const activeComp = this.scopeService.selectedCompany() || this.companies()[0];
    if (!activeComp) {
      this.formError.set('Please select or create an operating company first.');
      return;
    }
    this.newBranchCompanyId = activeComp.id;
    this.newBranchName = '';
    this.newBranchCode = '';
    this.formError.set(null);
    this.showAddBranchInline.update(v => !v);
  }

  saveNewBranch(): void {
    const targetCompId = this.newBranchCompanyId || this.scopeService.selectedCompany()?.id || this.companies()[0]?.id;
    if (!targetCompId) {
      this.formError.set('Please select an operating company.');
      return;
    }
    const trimmedName = this.newBranchName.trim();
    const trimmedCode = this.newBranchCode.trim().toUpperCase();

    if (!trimmedName || !trimmedCode) {
      this.formError.set('Branch name and branch code are required.');
      return;
    }

    this.saving.set(true);
    this.formError.set(null);

    this.companyService.createBranch(targetCompId, {
      name: trimmedName,
      code: trimmedCode
    }).subscribe({
      next: (br) => {
        this.saving.set(false);
        this.showAddBranchInline.set(false);
        this.scopeService.selectBranch(br);
        this.loadOnboardingState();
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(err?.error?.message || 'Failed to link branch.');
      }
    });
  }

  setActiveCompany(company: Company): void {
    this.scopeService.selectCompany(company);
    this.loadOnboardingState();
  }

  setActiveBranch(branch: Branch | null): void {
    if (branch) {
      this.scopeService.selectBranch(branch);
    } else {
      this.scopeService.selectAllBranches();
    }
  }
}
