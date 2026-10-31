import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppStatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AppEmptyStateComponent } from '../../../../../shared/components/empty-state/empty-state.component';
import { AuthService } from '../../../../../core/auth/auth.service';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { CurrencyItem } from '../../../../../core/models/scope.models';
import { ExchangeRateItem, ExchangeRateRequest, ExchangeRateType } from '../../../../../core/models/exchange-rate.models';

@Component({
  selector: 'app-exchange-rate-master',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    AppPageHeaderComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AppEmptyStateComponent
  ],
  templateUrl: './exchange-rate-master.component.html',
  styleUrl: '../currencies/currency-master.component.scss'
})
export class ExchangeRateMasterComponent implements OnInit {
  private readonly masterDataService = inject(MasterDataService);
  private readonly authService = inject(AuthService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly message = signal<{ text: string; severity: 'success' | 'warn' | 'error' } | null>(null);
  readonly currencies = signal<CurrencyItem[]>([]);
  readonly rates = signal<ExchangeRateItem[]>([]);
  readonly searchTerm = signal('');
  readonly statusFilter = signal<'ALL' | 'ACTIVE' | 'INACTIVE'>('ALL');
  readonly showForm = signal(false);
  readonly isEditing = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly formSaving = signal(false);
  readonly pendingAction = signal<{ type: 'deactivate' | 'activate'; rate: ExchangeRateItem } | null>(null);

  fromCurrencyId = '';
  toCurrencyId = '';
  rateType = ExchangeRateType.Daily;
  effectiveDate = new Date().toISOString().substring(0, 10);
  rateValue = '';

  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Supply Chain Masters', routerLink: '/app/master-data' },
    { label: 'Exchange Rates' }
  ];

  readonly activeCurrencies = computed(() => this.currencies().filter(currency => currency.isActive));
  readonly filteredRates = computed(() => {
    const query = this.searchTerm().trim().toLowerCase();
    const status = this.statusFilter();
    let rows = this.rates();
    if (status === 'ACTIVE') rows = rows.filter(row => row.isActive);
    if (status === 'INACTIVE') rows = rows.filter(row => !row.isActive);
    if (!query) return rows;
    return rows.filter(row =>
      `${row.fromCurrencyCode} ${row.toCurrencyCode} ${this.rateTypeLabel(row.rateType)} ${row.effectiveDate}`
        .toLowerCase()
        .includes(query)
    );
  });

  canCreate(): boolean {
    return this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() ||
      this.authService.hasPermission('CURRENCY.CREATE');
  }

  canEdit(): boolean {
    return this.authService.isTenantAdmin() || this.authService.isPlatformAdmin() ||
      this.authService.hasPermission('CURRENCY.EDIT');
  }

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.error.set(null);
    forkJoin({
      currencies: this.masterDataService.getCurrencies(),
      rates: this.masterDataService.getExchangeRates()
    }).subscribe({
      next: result => {
        this.currencies.set(result.currencies);
        this.rates.set(result.rates);
        this.loading.set(false);
      },
      error: err => {
        this.error.set(err?.error?.message || err?.message || 'Failed to load exchange rates.');
        this.loading.set(false);
      }
    });
  }

  openCreateForm(): void {
    this.isEditing.set(false);
    this.editingId.set(null);
    this.fromCurrencyId = '';
    this.toCurrencyId = '';
    this.rateType = ExchangeRateType.Daily;
    this.effectiveDate = new Date().toISOString().substring(0, 10);
    this.rateValue = '';
    this.showForm.set(true);
    this.pendingAction.set(null);
  }

  openEditForm(rate: ExchangeRateItem): void {
    this.isEditing.set(true);
    this.editingId.set(rate.exchangeRateId);
    this.fromCurrencyId = rate.fromCurrencyId;
    this.toCurrencyId = rate.toCurrencyId;
    this.rateType = this.parseRateType(rate.rateType);
    this.effectiveDate = rate.effectiveDate.substring(0, 10);
    this.rateValue = String(rate.rate);
    this.showForm.set(true);
    this.pendingAction.set(null);
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.isEditing.set(false);
    this.editingId.set(null);
  }

  saveRate(): void {
    const value = Number(this.rateValue);
    if (!this.fromCurrencyId || !this.toCurrencyId || !this.effectiveDate || !Number.isFinite(value) || value <= 0) {
      this.message.set({ text: 'Select both currencies, an effective date, and a rate greater than zero.', severity: 'warn' });
      return;
    }
    if (this.fromCurrencyId === this.toCurrencyId) {
      this.message.set({ text: 'From and To currencies must be different.', severity: 'warn' });
      return;
    }

    const request: ExchangeRateRequest = {
      fromCurrencyId: this.fromCurrencyId,
      toCurrencyId: this.toCurrencyId,
      rateType: this.rateType,
      effectiveDate: new Date(this.effectiveDate).toISOString(),
      rate: value
    };
    this.formSaving.set(true);
    const operation = this.isEditing()
      ? this.masterDataService.updateExchangeRate(this.editingId()!, request)
      : this.masterDataService.createExchangeRate(request);
    operation.subscribe({
      next: saved => {
        this.rates.update(rows => this.isEditing()
          ? rows.map(row => row.exchangeRateId === saved.exchangeRateId ? saved : row)
          : [saved, ...rows]);
        this.message.set({
          text: `Exchange rate ${saved.fromCurrencyCode}/${saved.toCurrencyCode} saved.`,
          severity: 'success'
        });
        this.formSaving.set(false);
        this.cancelForm();
      },
      error: err => {
        this.message.set({
          text: err?.error?.message || err?.message || 'Failed to save exchange rate.',
          severity: 'error'
        });
        this.formSaving.set(false);
      }
    });
  }

  promptToggle(rate: ExchangeRateItem): void {
    this.pendingAction.set({ type: rate.isActive ? 'deactivate' : 'activate', rate });
    this.showForm.set(false);
  }

  cancelPendingAction(): void {
    this.pendingAction.set(null);
  }

  confirmPendingAction(): void {
    const pending = this.pendingAction();
    if (!pending) return;
    const operation = pending.type === 'deactivate'
      ? this.masterDataService.deactivateExchangeRate(pending.rate.exchangeRateId)
      : this.masterDataService.activateExchangeRate(pending.rate.exchangeRateId);
    operation.subscribe({
      next: () => {
        this.rates.update(rows => rows.map(row =>
          row.exchangeRateId === pending.rate.exchangeRateId
            ? { ...row, isActive: pending.type === 'activate' }
            : row
        ));
        this.message.set({
          text: `Exchange rate ${pending.rate.fromCurrencyCode}/${pending.rate.toCurrencyCode} ${pending.type === 'activate' ? 'activated' : 'deactivated'}.`,
          severity: 'success'
        });
        this.pendingAction.set(null);
      },
      error: err => {
        this.message.set({
          text: err?.error?.message || err?.message || `Failed to ${pending.type} exchange rate.`,
          severity: 'error'
        });
      }
    });
  }

  rateTypeLabel(rateType: ExchangeRateType | number | string): string {
    return this.parseRateType(rateType) === ExchangeRateType.Monthly ? 'Monthly' : 'Daily';
  }

  private parseRateType(rateType: ExchangeRateType | number | string): ExchangeRateType {
    return String(rateType).toLowerCase() === 'monthly' || Number(rateType) === ExchangeRateType.Monthly
      ? ExchangeRateType.Monthly
      : ExchangeRateType.Daily;
  }

  dismissMessage(): void {
    this.message.set(null);
  }
}
