import {
  Component,
  OnInit,
  OnDestroy,
  inject,
  signal,
  computed,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../../shared/components/data-table/data-table.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AppEmptyStateComponent } from '../../../../../shared/components/empty-state/empty-state.component';
import { AccessDeniedComponent } from '../../../../../shared/components/access-denied/access-denied.component';
import { InventoryService } from '../../../../../core/services/inventory.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import {
  StockIssueDto,
  StockIssuePurpose,
  StockIssueStatus,
  getStockIssuePurposeName,
  getStockIssuePurposeSeverity,
  getStockIssueStatusSeverity
} from '../../../../../core/models/inventory.models';
import { DataTableColumn, DataTableAction } from '../../../../../shared/models/data-table-config.model';

export type StockIssuePurposeFilter = 'all' | 'internal' | 'sales' | 'manufacturing' | 'other';

@Component({
  selector: 'app-issue-stock-register',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    ButtonModule,
    InputTextModule,
    AppPageHeaderComponent,
    AppDataTableComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AppEmptyStateComponent,
    AccessDeniedComponent
  ],
  templateUrl: './issue-stock-register.component.html',
  styleUrl: './issue-stock-register.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class IssueStockRegisterComponent implements OnInit, OnDestroy {
  readonly inventoryService = inject(InventoryService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);

  private readonly subs = new Subscription();

  // Loading & State
  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to access inventory stock issue records."
  );

  // Issues data from real API
  readonly issues = signal<StockIssueDto[]>([]);

  // Search & Filter
  readonly searchTerm = signal<string>('');
  readonly activePurposeFilter = signal<StockIssuePurposeFilter>('all');

  // Breadcrumbs
  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Inventory', routerLink: '/app/inventory' },
    { label: 'Stock Issue' }
  ];

  // Purpose options for filtering
  readonly purposeOptions = [
    { label: 'All Purposes', value: 'all' },
    { label: 'Internal Use / Maintenance', value: 'internal' },
    { label: 'Sales / Customer Dispatch', value: 'sales' },
    { label: 'Manufacturing / Material Issue', value: 'manufacturing' },
    { label: 'Other Authorized', value: 'other' }
  ];

  // Computed filtered list
  readonly filteredIssues = computed<StockIssueDto[]>(() => {
    let list = [...this.issues()];

    const purpose = this.activePurposeFilter();
    if (purpose !== 'all') {
      list = list.filter(i => {
        if (purpose === 'internal') return Number(i.purpose) === StockIssuePurpose.InternalUse;
        if (purpose === 'sales') return Number(i.purpose) === StockIssuePurpose.SalesDispatch;
        if (purpose === 'manufacturing') return Number(i.purpose) === StockIssuePurpose.Manufacturing;
        if (purpose === 'other') return Number(i.purpose) === StockIssuePurpose.Other;
        return true;
      });
    }

    const term = this.searchTerm().trim().toLowerCase();
    if (term) {
      list = list.filter(i => {
        const num = (i.issueNumber || '').toLowerCase();
        const code = (i.productCode || '').toLowerCase();
        const name = (i.productName || '').toLowerCase();
        const wh = (i.warehouseName || '').toLowerCase();
        const ref = (i.sourceDocumentReference || '').toLowerCase();
        return (
          num.includes(term) ||
          code.includes(term) ||
          name.includes(term) ||
          wh.includes(term) ||
          ref.includes(term)
        );
      });
    }

    return list;
  });

  // Table columns
  readonly columns: DataTableColumn<StockIssueDto>[] = [
    {
      field: 'issueNumber',
      header: 'ISSUE # / REF',
      sortable: true,
      format: (_, row) => `${row.issueNumber}${row.sourceDocumentReference ? ' (' + row.sourceDocumentReference + ')' : ''}`
    },
    {
      field: 'issueDate',
      header: 'DATE',
      sortable: true,
      align: 'center',
      format: val => (val ? new Date(val).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) : '-')
    },
    {
      field: 'purpose',
      header: 'PURPOSE',
      type: 'badge',
      sortable: true,
      align: 'center',
      format: val => getStockIssuePurposeName(val),
      badgeSeverityMap: val => getStockIssuePurposeSeverity(val)
    },
    {
      field: 'productName',
      header: 'PRODUCT / SKU',
      sortable: true,
      format: (_, row) => `${row.productCode} - ${row.productName}`
    },
    {
      field: 'warehouseName',
      header: 'WAREHOUSE / BIN',
      sortable: true,
      format: (_, row) => `${row.warehouseName}${row.locationCode ? ' / ' + row.locationCode : ''}`
    },
    {
      field: 'quantity',
      header: 'QUANTITY',
      sortable: true,
      align: 'right',
      format: (val, row) => `${Number(val).toLocaleString()} ${row.unitOfMeasure || 'PCS'}`
    },
    {
      field: 'totalCost',
      header: 'TOTAL VALUE',
      sortable: true,
      align: 'right',
      format: (val) => {
        const curr = this.scopeService.selectedCompany()?.currency || 'LKR';
        const num = Number(val) || 0;
        return `${curr} ${num.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
      }
    },
    {
      field: 'status',
      header: 'STATUS',
      type: 'badge',
      sortable: true,
      align: 'center',
      format: () => 'Posted',
      badgeSeverityMap: () => 'success'
    }
  ];

  readonly tableActions: DataTableAction<StockIssueDto>[] = [
    {
      id: 'view',
      label: 'View Details',
      icon: 'pi pi-eye',
      severity: 'secondary',
      action: (row: StockIssueDto) => this.navigateToIssue(row)
    }
  ];

  canCreateIssue(): boolean {
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin()) {
      return true;
    }
    return this.authService.hasPermission('PRODUCT.EDIT');
  }

  ngOnInit(): void {
    this.loadStockIssues();
    this.subs.add(
      this.scopeService.scopeChange$.subscribe(() => {
        this.loadStockIssues();
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  loadStockIssues(): void {
    this.loading.set(true);
    this.error.set(null);
    this.isAccessDenied.set(false);

    this.inventoryService.getStockIssues().subscribe({
      next: data => {
        this.issues.set(data || []);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        if (err?.status === 403) {
          this.isAccessDenied.set(true);
        } else {
          this.error.set(err?.message || 'Failed to load stock issue records.');
        }
      }
    });
  }

  navigateToCreate(): void {
    this.router.navigate(['/app/inventory/issue-stock/new']);
  }

  navigateToIssue(issue: StockIssueDto): void {
    this.router.navigate(['/app/inventory/issue-stock', issue.stockIssueId]);
  }
}
