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
import { TaxCodeItem, CreateTaxCodeDto, UpdateTaxCodeDto } from '../../../../../core/models/scope.models';

@Component({
  selector: 'app-tax-codes',
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
  templateUrl: './tax-codes.component.html',
  styleUrl: './tax-codes.component.scss'
})
export class TaxCodesComponent implements OnInit {
  private readonly masterDataService = inject(MasterDataService);

  readonly loading = signal<boolean>(true);
  readonly error = signal<string | null>(null);
  readonly message = signal<{ text: string; severity: 'success' | 'warn' | 'error' } | null>(null);

  readonly taxCodes = signal<TaxCodeItem[]>([]);
  readonly searchTerm = signal<string>('');
  readonly applicabilityFilter = signal<string>('ALL');

  // Inline Form State (Strictly NO POPUPS)
  readonly showForm = signal<boolean>(false);
  readonly isEditing = signal<boolean>(false);
  readonly editingId = signal<string | null>(null);
  code = '';
  name = '';
  rate = 0;
  applicability: number = 0;
  effectiveFrom = '';
  effectiveTo = '';
  description = '';
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
    { label: 'Tax Codes' }
  ];

  readonly filteredTaxCodes = computed(() => {
    const q = this.searchTerm().trim().toLowerCase();
    const filter = this.applicabilityFilter();
    let list = this.taxCodes();

    if (filter !== 'ALL') {
      const target = Number(filter);
      list = list.filter(t => {
        const app = this.parseApplicability(t.applicability);
        return app === target || app === 0;
      });
    }

    if (!q) return list;

    return list.filter(t =>
      t.code.toLowerCase().includes(q) ||
      t.name.toLowerCase().includes(q) ||
      (t.description && t.description.toLowerCase().includes(q))
    );
  });

  parseApplicability(val: number | string | undefined): number {
    if (typeof val === 'number') return val;
    if (val === 'Purchase') return 1;
    if (val === 'Sales') return 2;
    return 0;
  }

  getApplicabilityLabel(val: number | string | undefined): string {
    const num = this.parseApplicability(val);
    switch (num) {
      case 1: return 'Purchase';
      case 2: return 'Sales';
      default: return 'Both';
    }
  }

  getApplicabilityClass(val: number | string | undefined): string {
    const num = this.parseApplicability(val);
    switch (num) {
      case 1: return 'badge-purchase';
      case 2: return 'badge-sales';
      default: return 'badge-both';
    }
  }

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.error.set(null);

    this.masterDataService.getTaxCodes().subscribe({
      next: (codes) => {
        this.taxCodes.set(codes);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.message || err?.message || 'Failed to load tax codes.');
        this.loading.set(false);
      }
    });
  }

  // --- Inline Form Handlers ---
  openCreateForm(): void {
    this.isEditing.set(false);
    this.editingId.set(null);
    this.code = '';
    this.name = '';
    this.rate = 0;
    this.applicability = 0;
    this.effectiveFrom = '';
    this.effectiveTo = '';
    this.description = '';
    this.showForm.set(true);
    this.pendingAction.set(null);
    this.message.set(null);
  }

  openEditForm(taxCode: TaxCodeItem): void {
    this.isEditing.set(true);
    this.editingId.set(taxCode.taxCodeId);
    this.code = taxCode.code;
    this.name = taxCode.name;
    this.rate = taxCode.rate;
    this.applicability = this.parseApplicability(taxCode.applicability);
    this.effectiveFrom = taxCode.effectiveFrom ? taxCode.effectiveFrom.substring(0, 10) : '';
    this.effectiveTo = taxCode.effectiveTo ? taxCode.effectiveTo.substring(0, 10) : '';
    this.description = taxCode.description || '';
    this.showForm.set(true);
    this.pendingAction.set(null);
    this.message.set(null);
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.isEditing.set(false);
    this.editingId.set(null);
    this.code = '';
    this.name = '';
    this.rate = 0;
    this.applicability = 0;
    this.effectiveFrom = '';
    this.effectiveTo = '';
    this.description = '';
  }

  saveTaxCode(): void {
    if (!this.code.trim() || !this.name.trim()) {
      this.message.set({ text: 'Please fill in both Tax Code and Name.', severity: 'warn' });
      return;
    }

    if (this.rate < 0 || this.rate > 100) {
      this.message.set({ text: 'Tax rate must be between 0% and 100%.', severity: 'warn' });
      return;
    }

    if (this.effectiveFrom && this.effectiveTo && this.effectiveFrom > this.effectiveTo) {
      this.message.set({ text: 'Effective From date cannot be after Effective To date.', severity: 'warn' });
      return;
    }

    this.formSaving.set(true);
    this.message.set(null);

    if (this.isEditing() && this.editingId()) {
      const dto: UpdateTaxCodeDto = {
        name: this.name.trim(),
        rate: Number(this.rate),
        applicability: Number(this.applicability),
        effectiveFrom: this.effectiveFrom ? new Date(this.effectiveFrom).toISOString() : undefined,
        effectiveTo: this.effectiveTo ? new Date(this.effectiveTo).toISOString() : undefined,
        description: this.description.trim() || undefined
      };
      this.masterDataService.updateTaxCode(this.editingId()!, dto).subscribe({
        next: (updated) => {
          this.taxCodes.update(list => list.map(t => t.taxCodeId === updated.taxCodeId ? updated : t));
          this.formSaving.set(false);
          this.cancelForm();
          this.message.set({ text: `Tax Code "${updated.code}" updated successfully.`, severity: 'success' });
        },
        error: (err) => {
          this.formSaving.set(false);
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to update tax code.', severity: 'error' });
        }
      });
    } else {
      const dto: CreateTaxCodeDto = {
        code: this.code.trim().toUpperCase(),
        name: this.name.trim(),
        rate: Number(this.rate),
        applicability: Number(this.applicability),
        effectiveFrom: this.effectiveFrom ? new Date(this.effectiveFrom).toISOString() : undefined,
        effectiveTo: this.effectiveTo ? new Date(this.effectiveTo).toISOString() : undefined,
        description: this.description.trim() || undefined
      };
      this.masterDataService.createTaxCode(dto).subscribe({
        next: (created) => {
          this.taxCodes.update(list => [created, ...list]);
          this.formSaving.set(false);
          this.cancelForm();
          this.message.set({ text: `Tax Code "${created.code}" created successfully.`, severity: 'success' });
        },
        error: (err) => {
          this.formSaving.set(false);
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to create tax code.', severity: 'error' });
        }
      });
    }
  }

  // --- Inline Deactivate / Reactivate Handlers ---
  promptDeactivate(taxCode: TaxCodeItem): void {
    this.pendingAction.set({
      type: 'deactivate',
      id: taxCode.taxCodeId,
      code: taxCode.code,
      name: taxCode.name
    });
    this.message.set(null);
  }

  promptReactivate(taxCode: TaxCodeItem): void {
    this.pendingAction.set({
      type: 'reactivate',
      id: taxCode.taxCodeId,
      code: taxCode.code,
      name: taxCode.name
    });
    this.message.set(null);
  }

  cancelPendingAction(): void {
    this.pendingAction.set(null);
  }

  confirmPendingAction(): void {
    const action = this.pendingAction();
    if (!action) return;

    if (action.type === 'deactivate') {
      this.masterDataService.deactivateTaxCode(action.id).subscribe({
        next: () => {
          this.taxCodes.update(list =>
            list.map(t => t.taxCodeId === action.id ? { ...t, isActive: false } : t)
          );
          this.pendingAction.set(null);
          this.message.set({ text: `Tax code "${action.code}" deactivated successfully.`, severity: 'success' });
        },
        error: (err) => {
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to deactivate tax code.', severity: 'error' });
        }
      });
    } else {
      this.masterDataService.reactivateTaxCode(action.id).subscribe({
        next: () => {
          this.taxCodes.update(list =>
            list.map(t => t.taxCodeId === action.id ? { ...t, isActive: true } : t)
          );
          this.pendingAction.set(null);
          this.message.set({ text: `Tax code "${action.code}" reactivated successfully.`, severity: 'success' });
        },
        error: (err) => {
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to reactivate tax code.', severity: 'error' });
        }
      });
    }
  }
}
