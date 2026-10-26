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
import { PaymentTermItem, CreatePaymentTermDto, UpdatePaymentTermDto } from '../../../../../core/models/scope.models';

@Component({
  selector: 'app-payment-terms',
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
  templateUrl: './payment-terms.component.html',
  styleUrl: './payment-terms.component.scss'
})
export class PaymentTermsComponent implements OnInit {
  private readonly masterDataService = inject(MasterDataService);

  readonly loading = signal<boolean>(true);
  readonly error = signal<string | null>(null);
  readonly message = signal<{ text: string; severity: 'success' | 'warn' | 'error' } | null>(null);

  readonly paymentTerms = signal<PaymentTermItem[]>([]);
  readonly searchTerm = signal<string>('');

  // Inline Form State (Strictly NO POPUPS)
  readonly showForm = signal<boolean>(false);
  readonly isEditing = signal<boolean>(false);
  readonly editingId = signal<string | null>(null);
  termCode = '';
  termName = '';
  days = 30;
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
    { label: 'Payment Terms' }
  ];

  readonly filteredPaymentTerms = computed(() => {
    const q = this.searchTerm().trim().toLowerCase();
    const list = this.paymentTerms();
    if (!q) return list;
    return list.filter(t =>
      t.termCode.toLowerCase().includes(q) ||
      t.termName.toLowerCase().includes(q) ||
      (t.description && t.description.toLowerCase().includes(q))
    );
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.error.set(null);

    this.masterDataService.getPaymentTerms().subscribe({
      next: (terms) => {
        this.paymentTerms.set(terms);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.message || err?.message || 'Failed to load payment terms.');
        this.loading.set(false);
      }
    });
  }

  // --- Inline Form Handlers ---
  openCreateForm(): void {
    this.isEditing.set(false);
    this.editingId.set(null);
    this.termCode = '';
    this.termName = '';
    this.days = 30;
    this.description = '';
    this.showForm.set(true);
    this.pendingAction.set(null);
    this.message.set(null);
  }

  openEditForm(term: PaymentTermItem): void {
    this.isEditing.set(true);
    this.editingId.set(term.paymentTermId);
    this.termCode = term.termCode;
    this.termName = term.termName;
    this.days = term.days;
    this.description = term.description || '';
    this.showForm.set(true);
    this.pendingAction.set(null);
    this.message.set(null);
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.isEditing.set(false);
    this.editingId.set(null);
    this.termCode = '';
    this.termName = '';
    this.days = 30;
    this.description = '';
  }

  savePaymentTerm(): void {
    if (!this.termCode.trim() || !this.termName.trim()) {
      this.message.set({ text: 'Please fill in both Term Code and Term Name.', severity: 'warn' });
      return;
    }

    if (this.days < 0) {
      this.message.set({ text: 'Payment term days cannot be negative.', severity: 'warn' });
      return;
    }

    this.formSaving.set(true);
    this.message.set(null);

    if (this.isEditing() && this.editingId()) {
      const dto: UpdatePaymentTermDto = {
        termName: this.termName.trim(),
        days: this.days,
        description: this.description.trim() || undefined
      };
      this.masterDataService.updatePaymentTerm(this.editingId()!, dto).subscribe({
        next: (updated) => {
          this.paymentTerms.update(list => list.map(t => t.paymentTermId === updated.paymentTermId ? updated : t));
          this.formSaving.set(false);
          this.cancelForm();
          this.message.set({ text: `Payment Term "${updated.termName}" updated successfully.`, severity: 'success' });
        },
        error: (err) => {
          this.formSaving.set(false);
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to update payment term.', severity: 'error' });
        }
      });
    } else {
      const dto: CreatePaymentTermDto = {
        termCode: this.termCode.trim().toUpperCase(),
        termName: this.termName.trim(),
        days: this.days,
        description: this.description.trim() || undefined
      };
      this.masterDataService.createPaymentTerm(dto).subscribe({
        next: (created) => {
          this.paymentTerms.update(list => [created, ...list]);
          this.formSaving.set(false);
          this.cancelForm();
          this.message.set({ text: `Payment Term "${created.termName}" created successfully.`, severity: 'success' });
        },
        error: (err) => {
          this.formSaving.set(false);
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to create payment term.', severity: 'error' });
        }
      });
    }
  }

  // --- Inline Deactivate / Reactivate Handlers ---
  promptDeactivate(term: PaymentTermItem): void {
    this.pendingAction.set({
      type: 'deactivate',
      id: term.paymentTermId,
      code: term.termCode,
      name: term.termName
    });
    this.message.set(null);
  }

  promptReactivate(term: PaymentTermItem): void {
    this.pendingAction.set({
      type: 'reactivate',
      id: term.paymentTermId,
      code: term.termCode,
      name: term.termName
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
      this.masterDataService.deactivatePaymentTerm(action.id).subscribe({
        next: () => {
          this.paymentTerms.update(list =>
            list.map(t => t.paymentTermId === action.id ? { ...t, isActive: false } : t)
          );
          this.pendingAction.set(null);
          this.message.set({ text: `Payment term "${action.name}" deactivated successfully.`, severity: 'success' });
        },
        error: (err) => {
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to deactivate payment term.', severity: 'error' });
        }
      });
    } else {
      this.masterDataService.reactivatePaymentTerm(action.id).subscribe({
        next: () => {
          this.paymentTerms.update(list =>
            list.map(t => t.paymentTermId === action.id ? { ...t, isActive: true } : t)
          );
          this.pendingAction.set(null);
          this.message.set({ text: `Payment term "${action.name}" reactivated successfully.`, severity: 'success' });
        },
        error: (err) => {
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to reactivate payment term.', severity: 'error' });
        }
      });
    }
  }
}
