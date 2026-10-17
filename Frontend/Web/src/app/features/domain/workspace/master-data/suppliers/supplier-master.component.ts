import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppStatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AppEmptyStateComponent } from '../../../../../shared/components/empty-state/empty-state.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { SupplierItem, CreateSupplierDto } from '../../../../../core/models/scope.models';

@Component({
  selector: 'app-supplier-master',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    InputTextModule,
    DialogModule,
    AppPageHeaderComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AppEmptyStateComponent
  ],
  templateUrl: './supplier-master.component.html',
  styleUrl: './supplier-master.component.scss'
})
export class SupplierMasterComponent implements OnInit {
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);

  readonly loading = signal<boolean>(true);
  readonly saving = signal<boolean>(false);
  readonly error = signal<string | null>(null);

  readonly suppliers = signal<SupplierItem[]>([]);
  readonly searchTerm = signal<string>('');
  readonly statusFilter = signal<'all' | 'active' | 'inactive'>('all');

  // Dialog State
  readonly dialogVisible = signal<boolean>(false);
  readonly isEditing = signal<boolean>(false);
  readonly editingSupplierId = signal<string | null>(null);
  readonly formError = signal<string | null>(null);

  // Form Fields
  formCode: string = '';
  formName: string = '';
  formContactName: string = '';
  formEmail: string = '';
  formPhone: string = '';
  formCity: string = '';
  formPaymentTerms: string = 'Net 30';

  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Supply Chain Masters', routerLink: '/app/master-data' },
    { label: 'Supplier Master' }
  ];

  readonly filteredSuppliers = computed(() => {
    let list = this.suppliers();
    const q = this.searchTerm().trim().toLowerCase();
    if (q) {
      list = list.filter(s =>
        s.supplierCode.toLowerCase().includes(q) ||
        s.supplierName.toLowerCase().includes(q) ||
        (s.contactName && s.contactName.toLowerCase().includes(q))
      );
    }
    const st = this.statusFilter();
    if (st === 'active') {
      list = list.filter(s => s.isActive);
    } else if (st === 'inactive') {
      list = list.filter(s => !s.isActive);
    }
    return list;
  });

  ngOnInit(): void {
    this.loadSuppliers();
  }

  loadSuppliers(): void {
    this.loading.set(true);
    this.error.set(null);

    this.masterDataService.getSuppliers().subscribe({
      next: (sups) => {
        this.suppliers.set(sups);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.message || 'Failed to retrieve suppliers from server.');
      }
    });
  }

  openCreateDialog(): void {
    this.isEditing.set(false);
    this.editingSupplierId.set(null);
    this.formError.set(null);

    this.formCode = '';
    this.formName = '';
    this.formContactName = '';
    this.formEmail = '';
    this.formPhone = '';
    this.formCity = '';
    this.formPaymentTerms = 'Net 30';

    this.dialogVisible.set(true);
  }

  openEditDialog(supplier: SupplierItem): void {
    this.isEditing.set(true);
    this.editingSupplierId.set(supplier.supplierId);
    this.formError.set(null);

    this.formCode = supplier.supplierCode;
    this.formName = supplier.supplierName;
    this.formContactName = supplier.contactName || '';
    this.formEmail = supplier.email || '';
    this.formPhone = supplier.phone || '';
    this.formCity = supplier.city || '';
    this.formPaymentTerms = supplier.paymentTerms || 'Net 30';

    this.dialogVisible.set(true);
  }

  onSaveSupplier(): void {
    if (!this.formCode.trim()) {
      this.formError.set('Supplier code is required.');
      return;
    }
    if (!this.formName.trim()) {
      this.formError.set('Supplier name is required.');
      return;
    }

    this.saving.set(true);
    this.formError.set(null);

    const dto: CreateSupplierDto = {
      supplierCode: this.formCode.trim(),
      supplierName: this.formName.trim(),
      contactName: this.formContactName.trim() || undefined,
      email: this.formEmail.trim() || undefined,
      phone: this.formPhone.trim() || undefined,
      city: this.formCity.trim() || undefined,
      paymentTerms: this.formPaymentTerms.trim() || undefined,
      isActive: true
    };

    if (this.isEditing() && this.editingSupplierId()) {
      this.masterDataService.updateSupplier(this.editingSupplierId()!, dto).subscribe({
        next: () => {
          this.saving.set(false);
          this.dialogVisible.set(false);
          this.loadSuppliers();
        },
        error: (err) => {
          this.saving.set(false);
          this.formError.set(err?.error?.message || 'Failed to update supplier.');
        }
      });
    } else {
      this.masterDataService.createSupplier(dto).subscribe({
        next: () => {
          this.saving.set(false);
          this.dialogVisible.set(false);
          this.loadSuppliers();
        },
        error: (err) => {
          this.saving.set(false);
          this.formError.set(err?.error?.message || 'Failed to create supplier.');
        }
      });
    }
  }

  onDeactivate(supplier: SupplierItem): void {
    if (confirm(`Deactivate supplier ${supplier.supplierName} (${supplier.supplierCode})?`)) {
      this.masterDataService.deactivateSupplier(supplier.supplierId).subscribe({
        next: () => this.loadSuppliers(),
        error: (err) => alert(err?.error?.message || 'Failed to deactivate supplier.')
      });
    }
  }
}
