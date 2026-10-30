import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
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
import { Company, Branch } from '../../../../core/models/scope.models';

export interface OnboardingStep {
  stepNumber: number;
  title: string;
  description: string;
  status: 'completed' | 'in_progress' | 'pending';
  icon: string;
  route?: string;
  badgeText: string;
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
    DialogModule,
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

  readonly loading = signal<boolean>(true);
  readonly error = signal<string | null>(null);
  readonly saving = signal<boolean>(false);
  readonly companies = signal<Company[]>([]);
  readonly branches = signal<Branch[]>([]);
  readonly warehouseCount = signal<number>(0);
  readonly productCount = signal<number>(0);
  readonly steps = signal<OnboardingStep[]>([]);

  // Reusable Data Table Configuration for Companies
  readonly companyColumns: DataTableColumn[] = [
    {
      field: 'code',
      header: 'Company Code',
      sortable: true,
      filterable: true,
      width: '180px'
    },
    {
      field: 'name',
      header: 'Company Name',
      sortable: true,
      filterable: true
    },
    {
      field: 'currency',
      header: 'Currency',
      sortable: true,
      filterable: true,
      width: '120px'
    },
    {
      field: 'status',
      header: 'Status',
      type: 'badge',
      sortable: true,
      filterable: true,
      width: '140px',
      badgeSeverityMap: (val, item) => item.isActive ? 'success' : 'neutral'
    },
    {
      field: 'branchCount',
      header: 'Branches',
      sortable: true,
      filterable: false,
      width: '140px'
    },
    {
      field: 'actions',
      header: 'Working Company',
      type: 'actions',
      sortable: false,
      filterable: false,
      width: '180px'
    }
  ];

  readonly tableCompanies = computed(() => {
    const branchCountStr = `${this.branches().length} Branches`;
    return this.companies().map(c => ({
      ...c,
      status: c.isActive ? 'Active' : 'Inactive',
      currency: c.baseCurrencyCode || c.currency || 'USD',
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

  // Reusable Data Table Configuration for Branches
  readonly branchColumns: DataTableColumn[] = [
    {
      field: 'code',
      header: 'Branch Code',
      sortable: true,
      filterable: true,
      width: '180px'
    },
    {
      field: 'name',
      header: 'Branch / Plant Name',
      sortable: true,
      filterable: true
    },
    {
      field: 'status',
      header: 'Status',
      type: 'badge',
      sortable: true,
      filterable: true,
      width: '140px',
      badgeSeverityMap: (val, item) => item.isActive ? 'success' : 'neutral'
    },
    {
      field: 'actions',
      header: 'Working Branch',
      type: 'actions',
      sortable: false,
      filterable: false,
      width: '180px'
    }
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

  // Dialog Controls
  readonly showAddCompanyDialog = signal<boolean>(false);
  readonly showAddBranchDialog = signal<boolean>(false);
  readonly showInitWizardDialog = signal<boolean>(false);
  readonly formError = signal<string | null>(null);

  // Add Company Form
  newCompanyName = '';
  newCompanyCode = '';
  newCompanyCurrency = 'USD';

  // Add Branch Form
  newBranchCompanyId = '';
  newBranchName = '';
  newBranchCode = '';

  // Initial Organization Setup Wizard Form
  initCompanyName = '';
  initCompanyCode = '';
  initCompanyCurrency = 'USD';
  initBranchName = '';
  initBranchCode = '';

  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Company Setup' }
  ];

  ngOnInit(): void {
    this.loadOnboardingState();
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
          this.loading.set(false);
          return;
        }

        // Fetch branches for the active company
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
    const hasWarehouse = whCount > 0;
    const hasProducts = prodCount > 0;

    const stepsList: OnboardingStep[] = [
      {
        stepNumber: 1,
        title: 'Company Details',
        description: hasCompany
          ? `${comps[0].name} (${comps[0].code}) is active and registered.`
          : 'Establish organization profile, legal identifiers, and operating currencies.',
        status: hasCompany ? 'completed' : 'in_progress',
        icon: 'pi pi-building',
        badgeText: hasCompany ? 'Configured' : 'Required'
      },
      {
        stepNumber: 2,
        title: 'Branch Configuration',
        description: hasBranch
          ? `${brs.length} operational branches linked to ${comps[0]?.name || 'company'}.`
          : 'Define operating locations, branch plants, and dispatch hubs.',
        status: hasBranch ? 'completed' : (hasCompany ? 'in_progress' : 'pending'),
        icon: 'pi pi-sitemap',
        badgeText: hasBranch ? `${brs.length} Active` : 'Pending'
      },
      {
        stepNumber: 3,
        title: 'Warehouse & Storage Setup',
        description: hasWarehouse
          ? `${whCount} warehouses configured with inventory storage locations.`
          : 'Set up receiving zones, bulk storage, and dispatch warehouse bins.',
        status: hasWarehouse ? 'completed' : (hasBranch ? 'in_progress' : 'pending'),
        icon: 'pi pi-box',
        route: '/app/master-data',
        badgeText: hasWarehouse ? `${whCount} Ready` : 'Setup'
      },
      {
        stepNumber: 4,
        title: 'Supply Chain Master Data',
        description: hasProducts
          ? `${prodCount} products registered across master catalog.`
          : 'Register products, categories, units of measure, and active suppliers.',
        status: hasProducts ? 'completed' : (hasWarehouse ? 'in_progress' : 'pending'),
        icon: 'pi pi-database',
        route: '/app/master-data',
        badgeText: hasProducts ? `${prodCount} Products` : 'Next Step'
      }
    ];

    this.steps.set(stepsList);
  }

  openAddCompanyModal(): void {
    this.newCompanyName = '';
    this.newCompanyCode = '';
    this.newCompanyCurrency = 'USD';
    this.formError.set(null);
    this.showAddCompanyDialog.set(true);
  }

  saveCompany(): void {
    const trimmedName = this.newCompanyName.trim();
    const trimmedCode = this.newCompanyCode.trim().toUpperCase();
    const trimmedCurrency = (this.newCompanyCurrency || 'USD').trim().toUpperCase();

    if (!trimmedName || !trimmedCode) {
      this.formError.set('Company name and company code are required.');
      return;
    }

    this.saving.set(true);
    this.formError.set(null);

    this.companyService.createCompany({
      name: trimmedName,
      code: trimmedCode,
      currency: trimmedCurrency
    }).subscribe({
      next: (comp) => {
        this.saving.set(false);
        this.showAddCompanyDialog.set(false);
        this.scopeService.selectCompany(comp);
        this.loadOnboardingState();
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(err?.error?.message || 'Failed to register company.');
      }
    });
  }

  openAddBranchModal(): void {
    const activeComp = this.scopeService.selectedCompany() || this.companies()[0];
    if (!activeComp) {
      this.formError.set('Please select or create an operating company first.');
      return;
    }
    this.newBranchCompanyId = activeComp.id;
    this.newBranchName = '';
    this.newBranchCode = '';
    this.formError.set(null);
    this.showAddBranchDialog.set(true);
  }

  saveBranch(): void {
    const targetCompId = this.newBranchCompanyId || this.scopeService.selectedCompany()?.id || this.companies()[0]?.id;
    const targetComp = this.companies().find(c => c.id === targetCompId);
    if (!targetCompId || !targetComp) {
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
        this.showAddBranchDialog.set(false);
        this.scopeService.selectCompany(targetComp);
        this.scopeService.selectBranch(br);
        this.loadOnboardingState();
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(err?.error?.message || 'Failed to link branch.');
      }
    });
  }

  openInitWizardModal(): void {
    const rawTenant = this.scopeService.activeTenantName() || 'Enterprise';
    const cleanPrefix = rawTenant.split('@')[0].replace(/[^A-Za-z0-9]/g, ' ').trim();
    const titleCase = cleanPrefix ? cleanPrefix.charAt(0).toUpperCase() + cleanPrefix.slice(1) : 'Apex Enterprise';
    const shortCode = cleanPrefix.replace(/[^A-Za-z0-9]/g, '').substring(0, 4).toUpperCase() || 'APX';

    this.initCompanyName = `${titleCase} (Pvt) Ltd`;
    this.initCompanyCode = `${shortCode}-MFG`;
    this.initCompanyCurrency = 'USD';
    this.initBranchName = `${titleCase} Main Branch`;
    this.initBranchCode = `${shortCode}-BR01`;
    this.formError.set(null);
    this.showInitWizardDialog.set(true);
  }

  saveInitWizard(): void {
    const cName = this.initCompanyName.trim();
    const cCode = this.initCompanyCode.trim().toUpperCase();
    const cCurr = (this.initCompanyCurrency || 'USD').trim().toUpperCase();
    const bName = this.initBranchName.trim();
    const bCode = this.initBranchCode.trim().toUpperCase();

    if (!cName || !cCode) {
      this.formError.set('Company name and company code are required.');
      return;
    }
    if (!bName || !bCode) {
      this.formError.set('Initial branch name and branch code are required.');
      return;
    }

    this.saving.set(true);
    this.formError.set(null);

    this.companyService.initializeDefaultCompanyAndBranch(undefined, {
      companyName: cName,
      companyCode: cCode,
      currency: cCurr,
      branchName: bName,
      branchCode: bCode
    }).subscribe({
      next: ({ company, branch }) => {
        this.saving.set(false);
        this.showInitWizardDialog.set(false);
        this.scopeService.selectCompany(company);
        this.scopeService.selectBranch(branch);
        this.loadOnboardingState();
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(err?.error?.message || 'Failed to complete organization setup.');
      }
    });
  }

  onInitializeDefault(): void {
    this.openInitWizardModal();
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
