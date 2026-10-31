import {
  Component,
  OnInit,
  ChangeDetectionStrategy,
  signal,
  inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import {
  PriceCardItem,
  PriceCardType,
  PriceCardQueryParams
} from '../../../../../core/models/price-card.models';
import {
  DataTableColumn,
  DataTableAction,
  TableQueryState
} from '../../../../../shared/models/data-table-config.model';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../../shared/components/data-table/data-table.component';
import { ActionDialogComponent } from '../../../../../shared/components/action-dialog/action-dialog.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';

interface ContextMessage {
  severity: 'success' | 'warn' | 'error';
  text: string;
}

interface PendingActionConfig {
  type: 'deactivate' | 'reactivate';
  priceCard: PriceCardItem;
  title: string;
  message: string;
  confirmLabel: string;
  severity: 'danger' | 'success';
}

@Component({
  selector: 'app-price-card-master',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    AppPageHeaderComponent,
    AppDataTableComponent,
    ActionDialogComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent
  ],
  templateUrl: './price-card-master.component.html',
  styleUrl: './price-card-master.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PriceCardMasterComponent implements OnInit {
  private readonly masterDataService = inject(MasterDataService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly breadcrumbs = [
    { label: 'Home', routerLink: '/app' },
    { label: 'Master Data', routerLink: '/app/master-data' },
    { label: 'Price Cards' }
  ];

  // State signals
  readonly loading = signal<boolean>(false);
  readonly actionLoading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly message = signal<ContextMessage | null>(null);

  readonly priceCards = signal<PriceCardItem[]>([]);
  readonly totalCount = signal<number>(0);
  readonly page = signal<number>(1);
  readonly pageSize = signal<number>(10);
  readonly searchTerm = signal<string>('');
  readonly typeFilter = signal<'ALL' | '1' | '2'>('ALL');
  readonly statusFilter = signal<'ALL' | 'ACTIVE' | 'INACTIVE'>('ALL');
  readonly sortBy = signal<string>('code');
  readonly sortDirection = signal<'asc' | 'desc'>('asc');

  // Confirmation dialog state
  readonly isActionDialogVisible = signal<boolean>(false);
  readonly pendingAction = signal<PendingActionConfig | null>(null);

  // Permission helpers
  canCreate(): boolean {
    return (
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin() ||
      this.authService.hasPermission('PRICE_CARD.CREATE')
    );
  }

  canEdit(): boolean {
    return (
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin() ||
      this.authService.hasPermission('PRICE_CARD.EDIT')
    );
  }

  canManage(): boolean {
    return (
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin() ||
      this.authService.hasPermission('PRICE_CARD.MANAGE')
    );
  }

  // Data Table Columns
  readonly tableColumns: DataTableColumn<PriceCardItem>[] = [
    {
      field: 'priceCardCode',
      header: 'Code',
      width: '140px',
      sortable: true
    },
    {
      field: 'priceCardName',
      header: 'Name',
      sortable: true
    },
    {
      field: 'priceCardType',
      header: 'Type',
      width: '120px',
      sortable: true,
      type: 'badge',
      badgeSeverityMap: (val: any) => (val === PriceCardType.Purchase || val === 1 ? 'info' : 'success'),
      format: (val: any) => (val === PriceCardType.Purchase || val === 1 ? 'Purchase' : 'Sales')
    },
    {
      field: 'currency',
      header: 'Currency',
      width: '90px',
      sortable: true
    },
    {
      field: 'effectiveFrom',
      header: 'Effective From',
      width: '130px',
      sortable: true,
      format: (val: any) => (val ? this.formatDate(val) : '—')
    },
    {
      field: 'effectiveTo',
      header: 'Effective To',
      width: '130px',
      sortable: true,
      format: (val: any) => (val ? this.formatDate(val) : 'Open-ended')
    },
    {
      field: 'lineCount',
      header: 'Lines',
      width: '80px',
      align: 'right'
    },
    {
      field: 'isActive',
      header: 'Status',
      width: '100px',
      sortable: true,
      type: 'badge',
      badgeSeverityMap: (val: any) => (val ? 'success' : 'neutral'),
      format: (val: any) => (val ? 'Active' : 'Inactive')
    },
    {
      field: 'priceCardId',
      header: 'Actions',
      width: '110px',
      type: 'actions'
    }
  ];

  // Table Row Actions
  readonly tableActions: DataTableAction<PriceCardItem>[] = [
    {
      id: 'view',
      label: 'View',
      icon: 'pi pi-eye',
      severity: 'secondary',
      action: (row: PriceCardItem) => this.navigateToView(row)
    },
    {
      id: 'edit',
      label: 'Edit',
      icon: 'pi pi-pencil',
      severity: 'secondary',
      visible: (row: PriceCardItem) => row.isActive && this.canEdit(),
      action: (row: PriceCardItem) => this.navigateToEdit(row)
    },
    {
      id: 'deactivate',
      label: 'Deactivate',
      icon: 'pi pi-ban',
      severity: 'danger',
      visible: (row: PriceCardItem) => row.isActive && this.canManage(),
      action: (row: PriceCardItem) => this.promptDeactivate(row)
    },
    {
      id: 'reactivate',
      label: 'Reactivate',
      icon: 'pi pi-replay',
      severity: 'success',
      visible: (row: PriceCardItem) => !row.isActive && this.canManage(),
      action: (row: PriceCardItem) => this.promptReactivate(row)
    }
  ];

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.error.set(null);

    const queryParams: PriceCardQueryParams = {
      page: this.page(),
      pageSize: this.pageSize(),
      sortBy: this.sortBy(),
      sortDirection: this.sortDirection()
    };

    const search = this.searchTerm().trim();
    if (search) {
      queryParams.search = search;
    }

    const typeF = this.typeFilter();
    if (typeF === '1') {
      queryParams.type = PriceCardType.Purchase;
    } else if (typeF === '2') {
      queryParams.type = PriceCardType.Sales;
    }

    const statusF = this.statusFilter();
    if (statusF === 'ACTIVE') {
      queryParams.isActive = true;
    } else if (statusF === 'INACTIVE') {
      queryParams.isActive = false;
    }

    this.masterDataService.getPriceCards(queryParams).subscribe({
      next: res => {
        this.priceCards.set(res.items);
        this.totalCount.set(res.totalCount);
        this.loading.set(false);
      },
      error: err => {
        this.error.set(err?.error?.message || err?.message || 'Failed to load price cards.');
        this.loading.set(false);
      }
    });
  }

  onSearchChange(term: string): void {
    this.searchTerm.set(term);
    this.page.set(1);
    this.loadData();
  }

  onTypeFilterChange(type: 'ALL' | '1' | '2'): void {
    this.typeFilter.set(type);
    this.page.set(1);
    this.loadData();
  }

  onStatusFilterChange(status: 'ALL' | 'ACTIVE' | 'INACTIVE'): void {
    this.statusFilter.set(status);
    this.page.set(1);
    this.loadData();
  }

  onQueryChange(state: TableQueryState): void {
    if (state.sortField) {
      this.sortBy.set(state.sortField);
      this.sortDirection.set(state.sortOrder === 1 ? 'asc' : 'desc');
    }
    if (state.rows) {
      this.pageSize.set(state.rows);
    }
    if (state.first !== undefined && state.rows) {
      this.page.set(Math.floor(state.first / state.rows) + 1);
    }
    this.loadData();
  }

  navigateToCreate(): void {
    this.router.navigate(['/app/master-data/price-cards/new']);
  }

  navigateToView(row: PriceCardItem): void {
    this.router.navigate(['/app/master-data/price-cards', row.priceCardId]);
  }

  navigateToEdit(row: PriceCardItem): void {
    this.router.navigate(['/app/master-data/price-cards', row.priceCardId, 'edit']);
  }

  promptDeactivate(row: PriceCardItem): void {
    this.pendingAction.set({
      type: 'deactivate',
      priceCard: row,
      title: 'Deactivate Price Card',
      message: `Are you sure you want to deactivate price card "${row.priceCardName} (${row.priceCardCode})"? It will no longer be selected for new transactions.`,
      confirmLabel: 'Deactivate',
      severity: 'danger'
    });
    this.isActionDialogVisible.set(true);
  }

  promptReactivate(row: PriceCardItem): void {
    this.pendingAction.set({
      type: 'reactivate',
      priceCard: row,
      title: 'Reactivate Price Card',
      message: `Are you sure you want to reactivate price card "${row.priceCardName} (${row.priceCardCode})"? It will become available for active transactions.`,
      confirmLabel: 'Reactivate',
      severity: 'success'
    });
    this.isActionDialogVisible.set(true);
  }

  executePendingAction(): void {
    const action = this.pendingAction();
    if (!action) return;

    this.actionLoading.set(true);
    const op$ =
      action.type === 'deactivate'
        ? this.masterDataService.deactivatePriceCard(action.priceCard.priceCardId)
        : this.masterDataService.reactivatePriceCard(action.priceCard.priceCardId);

    op$.subscribe({
      next: () => {
        this.actionLoading.set(false);
        this.isActionDialogVisible.set(false);
        this.pendingAction.set(null);
        this.message.set({
          severity: 'success',
          text: `Price card "${action.priceCard.priceCardCode}" was successfully ${action.type === 'deactivate' ? 'deactivated' : 'reactivated'}.`
        });
        this.loadData();
      },
      error: err => {
        this.actionLoading.set(false);
        this.isActionDialogVisible.set(false);
        this.pendingAction.set(null);
        this.message.set({
          severity: 'error',
          text: err?.error?.message || err?.message || `Failed to ${action.type} price card.`
        });
      }
    });
  }

  cancelPendingAction(): void {
    this.isActionDialogVisible.set(false);
    this.pendingAction.set(null);
  }

  private formatDate(dateVal: string): string {
    try {
      const d = new Date(dateVal);
      if (isNaN(d.getTime())) return String(dateVal);
      return d.toLocaleDateString('en-US', {
        year: 'numeric',
        month: 'short',
        day: '2-digit'
      });
    } catch {
      return String(dateVal);
    }
  }
}
