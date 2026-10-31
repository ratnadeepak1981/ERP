import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppStatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AppEmptyStateComponent } from '../../../../../shared/components/empty-state/empty-state.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import { CurrencyItem, CreateCurrencyDto, UpdateCurrencyDto } from '../../../../../core/models/scope.models';

@Component({
  selector: 'app-currency-master',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    AppPageHeaderComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AppEmptyStateComponent
  ],
  templateUrl: './currency-master.component.html',
  styleUrl: './currency-master.component.scss'
})
export class CurrencyMasterComponent implements OnInit {
  private readonly masterDataService = inject(MasterDataService);
  private readonly authService = inject(AuthService);

  readonly loading = signal<boolean>(true);
  readonly error = signal<string | null>(null);
  readonly message = signal<{ text: string; severity: 'success' | 'warn' | 'error' } | null>(null);

  readonly currencies = signal<CurrencyItem[]>([]);
  readonly searchTerm = signal<string>('');
  readonly statusFilter = signal<'ALL' | 'ACTIVE' | 'INACTIVE'>('ALL');

  // Inline Form State (Strictly NO POPUPS)
  readonly showForm = signal<boolean>(false);
  readonly isEditing = signal<boolean>(false);
  readonly editingId = signal<string | null>(null);

  currencyCode = '';
  currencyName = '';
  symbol = '$';
  decimalPlaces = 2;
  readonly formSaving = signal<boolean>(false);

  // Inline Confirmation Banner State (Strictly NO POPUPS)
  readonly pendingAction = signal<{
    type: 'deactivate' | 'reactivate';
    id: string;
    code: string;
    name: string;
  } | null>(null);

  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Supply Chain Masters', routerLink: '/app/master-data' },
    { label: 'Currencies' }
  ];

  readonly filteredCurrencies = computed(() => {
    const q = this.searchTerm().trim().toLowerCase();
    const filter = this.statusFilter();
    let list = this.currencies();

    if (filter === 'ACTIVE') {
      list = list.filter(c => c.isActive);
    } else if (filter === 'INACTIVE') {
      list = list.filter(c => !c.isActive);
    }

    if (!q) return list;

    return list.filter(c =>
      c.currencyCode.toLowerCase().includes(q) ||
      c.currencyName.toLowerCase().includes(q) ||
      c.symbol.toLowerCase().includes(q)
    );
  });

  canCreate(): boolean {
    return (
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin() ||
      this.authService.hasPermission('CURRENCY.CREATE')
    );
  }

  canEdit(): boolean {
    return (
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin() ||
      this.authService.hasPermission('CURRENCY.EDIT')
    );
  }

  canDelete(): boolean {
    return (
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin() ||
      this.authService.hasPermission('CURRENCY.DELETE')
    );
  }

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.error.set(null);

    this.masterDataService.getCurrencies().subscribe({
      next: (items) => {
        this.currencies.set(items);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.message || err?.message || 'Failed to load currencies.');
        this.loading.set(false);
      }
    });
  }

  // --- Inline Form Handlers ---
  openCreateForm(): void {
    this.isEditing.set(false);
    this.editingId.set(null);
    this.currencyCode = '';
    this.currencyName = '';
    this.symbol = '$';
    this.decimalPlaces = 2;
    this.showForm.set(true);
    this.pendingAction.set(null);
  }

  openEditForm(currency: CurrencyItem): void {
    this.isEditing.set(true);
    this.editingId.set(currency.currencyId);
    this.currencyCode = currency.currencyCode;
    this.currencyName = currency.currencyName;
    this.symbol = currency.symbol;
    this.decimalPlaces = currency.decimalPlaces;
    this.showForm.set(true);
    this.pendingAction.set(null);
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.isEditing.set(false);
    this.editingId.set(null);
  }

  saveCurrency(): void {
    const code = this.currencyCode.trim().toUpperCase();
    const name = this.currencyName.trim();
    const sym = this.symbol.trim();
    const dec = Number(this.decimalPlaces);

    if (!code || !name || !sym) {
      this.message.set({ text: 'Please fill in all required fields.', severity: 'warn' });
      return;
    }

    if (dec < 0 || dec > 4) {
      this.message.set({ text: 'Decimal places must be between 0 and 4.', severity: 'warn' });
      return;
    }

    this.formSaving.set(true);

    if (this.isEditing()) {
      const id = this.editingId()!;
      const dto: UpdateCurrencyDto = {
        currencyName: name,
        symbol: sym,
        decimalPlaces: dec
      };

      this.masterDataService.updateCurrency(id, dto).subscribe({
        next: (updated) => {
          this.currencies.update(list => list.map(c => c.currencyId === updated.currencyId ? updated : c));
          this.message.set({ text: `Currency "${updated.currencyCode}" updated successfully.`, severity: 'success' });
          this.formSaving.set(false);
          this.cancelForm();
        },
        error: (err) => {
          this.message.set({
            text: err?.error?.message || err?.message || 'Failed to update currency.',
            severity: 'error'
          });
          this.formSaving.set(false);
        }
      });
    } else {
      const dto: CreateCurrencyDto = {
        currencyCode: code,
        currencyName: name,
        symbol: sym,
        decimalPlaces: dec
      };

      this.masterDataService.createCurrency(dto).subscribe({
        next: (created) => {
          this.currencies.update(list => [created, ...list]);
          this.message.set({ text: `Currency "${created.currencyCode}" created successfully.`, severity: 'success' });
          this.formSaving.set(false);
          this.cancelForm();
        },
        error: (err) => {
          this.message.set({
            text: err?.error?.message || err?.message || 'Failed to create currency.',
            severity: 'error'
          });
          this.formSaving.set(false);
        }
      });
    }
  }

  // --- Inline Confirmation Handlers ---
  promptDeactivate(currency: CurrencyItem): void {
    this.cancelForm();
    this.pendingAction.set({
      type: 'deactivate',
      id: currency.currencyId,
      code: currency.currencyCode,
      name: currency.currencyName
    });
  }

  promptReactivate(currency: CurrencyItem): void {
    this.cancelForm();
    this.pendingAction.set({
      type: 'reactivate',
      id: currency.currencyId,
      code: currency.currencyCode,
      name: currency.currencyName
    });
  }

  cancelPendingAction(): void {
    this.pendingAction.set(null);
  }

  confirmPendingAction(): void {
    const action = this.pendingAction();
    if (!action) return;

    if (action.type === 'deactivate') {
      this.masterDataService.deactivateCurrency(action.id).subscribe({
        next: () => {
          this.currencies.update(list =>
            list.map(c => c.currencyId === action.id ? { ...c, isActive: false } : c)
          );
          this.message.set({
            text: `Currency "${action.code}" deactivated successfully.`,
            severity: 'warn'
          });
          this.pendingAction.set(null);
        },
        error: (err) => {
          this.message.set({
            text: err?.error?.message || err?.message || 'Failed to deactivate currency.',
            severity: 'error'
          });
          this.pendingAction.set(null);
        }
      });
    } else {
      this.masterDataService.activateCurrency(action.id).subscribe({
        next: () => {
          this.currencies.update(list =>
            list.map(c => c.currencyId === action.id ? { ...c, isActive: true } : c)
          );
          this.message.set({
            text: `Currency "${action.code}" reactivated successfully.`,
            severity: 'success'
          });
          this.pendingAction.set(null);
        },
        error: (err) => {
          this.message.set({
            text: err?.error?.message || err?.message || 'Failed to reactivate currency.',
            severity: 'error'
          });
          this.pendingAction.set(null);
        }
      });
    }
  }
}
