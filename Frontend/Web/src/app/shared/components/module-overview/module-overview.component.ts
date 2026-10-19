import { Component, Input, Output, EventEmitter, inject, computed, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { AppPageHeaderComponent } from '../page-header/page-header.component';
import { AppDataTableComponent } from '../data-table/data-table.component';
import { AppProcessFlowComponent } from '../process-flow/process-flow.component';
import { AppLoadingStateComponent } from '../loading-state/loading-state.component';
import { AppErrorStateComponent } from '../error-state/error-state.component';
import { AppEmptyStateComponent } from '../empty-state/empty-state.component';
import { AccessDeniedComponent } from '../access-denied/access-denied.component';
import { AuthService } from '../../../core/auth/auth.service';
import {
  ModuleOverviewConfig,
  DashboardCardConfig,
  ProcessStageConfig,
  ProcessFlowPosition
} from '../../models/module-overview-config.model';
import { DataTableColumn, DataTableAction } from '../../models/data-table-config.model';

@Component({
  selector: 'app-module-overview',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    InputTextModule,
    TooltipModule,
    AppPageHeaderComponent,
    AppDataTableComponent,
    AppProcessFlowComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AppEmptyStateComponent,
    AccessDeniedComponent
  ],
  templateUrl: './module-overview.component.html',
  styleUrl: './module-overview.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppModuleOverviewComponent<T extends Record<string, any> = Record<string, any>> {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router, { optional: true });

  private readonly _config = signal<ModuleOverviewConfig<T> | null>(null);
  @Input({ required: true })
  set config(val: ModuleOverviewConfig<T>) {
    this._config.set(val);
  }
  get config(): ModuleOverviewConfig<T> {
    return this._config()!;
  }

  // Loading & State
  @Input() loading: boolean = false;
  @Input() error: string | null = null;
  @Input() isAccessDenied: boolean = false;
  @Input() accessDeniedMessage: string =
    "You don't have permission to access records for this operating company and branch. Contact your administrator if you believe this is an error.";
  @Input() scopeRequired: boolean = false;
  @Input() scopeRequiredTitle: string = 'Operating Company and Branch Required';
  @Input() scopeRequiredDescription: string =
    'Overview metrics and recent records are scoped to an operating company and branch. Please select both in the navigation bar.';

  // Recent Transactions Data & Table Inputs
  private readonly _recentData = signal<T[]>([]);
  @Input()
  set recentData(val: T[]) {
    this._recentData.set(val || []);
  }
  get recentData(): T[] {
    return this._recentData();
  }
  @Input() recentColumns: DataTableColumn<T>[] = [];
  @Input() recentActions?: DataTableAction<T>[];
  @Input() recentRowKey: string = 'id';
  @Input() statusOptions?: { label: string; value: any }[];
  @Input() activeStatusFilter: any = 'all';
  @Input() activeSearchTerm: string = '';

  // Outputs
  @Output() refresh = new EventEmitter<void>();
  @Output() createAction = new EventEmitter<void>();
  @Output() viewAll = new EventEmitter<void>();
  @Output() cardClick = new EventEmitter<DashboardCardConfig>();
  @Output() stageClick = new EventEmitter<ProcessStageConfig>();
  @Output() recentRowClick = new EventEmitter<T>();
  @Output() searchChange = new EventEmitter<string>();
  @Output() statusChange = new EventEmitter<any>();

  // Process Flow Placement Position
  readonly processFlowPosition = computed<ProcessFlowPosition>(() => {
    return this._config()?.processFlow?.position || 'belowCards';
  });

  // Dynamic derivation of eligible KPI dashboard cards based on permissions and enabled state
  readonly eligibleCards = computed<DashboardCardConfig[]>(() => {
    const cards = this._config()?.dashboardCards || [];
    return cards.filter(card => this.isCardPermitted(card));
  });

  // Recent transactions record limit
  readonly recentLimit = computed<number>(() => {
    return this._config()?.recentTransactions?.limit || 10;
  });

  // Sliced / filtered recent records
  readonly displayedRecentData = computed<T[]>(() => {
    const data = this._recentData();
    return data.slice(0, Math.max(1, this.recentLimit()));
  });

  isCardPermitted(card: DashboardCardConfig): boolean {
    if (card.enabled === false) return false;
    if (!card.requiredPermissions || card.requiredPermissions.length === 0) return true;
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.isPlatformStaff()) {
      return true;
    }
    const userPerms = this.authService.permissions();
    return card.requiredPermissions.some(p => userPerms.includes(p));
  }

  canPerformCreate(): boolean {
    const perm = this.config?.recentTransactions?.createActionPermission;
    if (!perm) return true;
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin()) {
      return true;
    }
    return this.authService.hasPermission(perm);
  }

  isNavLinkPermitted(link: { requiredPermissions?: string[] }): boolean {
    if (!link.requiredPermissions || link.requiredPermissions.length === 0) return true;
    if (this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() || this.authService.isPlatformStaff()) {
      return true;
    }
    const userPerms = this.authService.permissions();
    return link.requiredPermissions.some(p => userPerms.includes(p));
  }

  onCardClick(card: DashboardCardConfig): void {
    this.cardClick.emit(card);
    if (card.route && this.router) {
      this.router.navigate([card.route], {
        queryParams: card.queryParams
      });
    }
  }

  onStageClick(stage: ProcessStageConfig): void {
    this.stageClick.emit(stage);
  }

  onRowClick(row: T): void {
    this.recentRowClick.emit(row);
  }

  onRefresh(): void {
    this.refresh.emit();
  }

  onCreateAction(): void {
    if (this.config?.recentTransactions?.createActionRoute && this.router) {
      this.router.navigate([this.config.recentTransactions.createActionRoute], {
        queryParams: this.config.recentTransactions.createActionQueryParams
      });
    }
    this.createAction.emit();
  }

  onViewAll(): void {
    if (this.config?.recentTransactions?.viewAllRoute && this.router) {
      this.router.navigate([this.config.recentTransactions.viewAllRoute]);
    }
    this.viewAll.emit();
  }

  onSearchInput(val: string): void {
    this.searchChange.emit(val);
  }

  onStatusSelect(val: any): void {
    this.statusChange.emit(val);
  }

  getButtonSeverity(sev?: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' | undefined {
    if (!sev) return 'secondary';
    if (sev === 'warning') return 'warn';
    if (sev === 'primary') return undefined; // In PrimeNG Button, primary is default when severity is unset or 'primary'
    return sev as 'success' | 'info' | 'warn' | 'danger' | 'secondary';
  }
}
