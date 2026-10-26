import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppStatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AppEmptyStateComponent } from '../../../../../shared/components/empty-state/empty-state.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { SupplierItem } from '../../../../../core/models/scope.models';

@Component({
  selector: 'app-supplier-master',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    ButtonModule,
    InputTextModule,
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
  readonly router = inject(Router);

  readonly loading = signal<boolean>(true);
  readonly error = signal<string | null>(null);
  readonly message = signal<{ text: string; severity: 'success' | 'warn' | 'error' } | null>(null);

  readonly suppliers = signal<SupplierItem[]>([]);
  readonly searchTerm = signal<string>('');
  readonly statusFilter = signal<'all' | 'active' | 'inactive'>('all');

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
    this.router.navigate(['/app/master-data/suppliers/new']);
  }

  openEditDialog(supplier: SupplierItem): void {
    this.router.navigate(['/app/master-data/suppliers', supplier.supplierId]);
  }

  promptDeactivate(supplier: SupplierItem): void {
    this.pendingAction.set({
      type: 'deactivate',
      id: supplier.supplierId,
      code: supplier.supplierCode,
      name: supplier.supplierName
    });
    this.message.set(null);
  }

  promptReactivate(supplier: SupplierItem): void {
    this.pendingAction.set({
      type: 'reactivate',
      id: supplier.supplierId,
      code: supplier.supplierCode,
      name: supplier.supplierName
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
      this.masterDataService.deactivateSupplier(action.id).subscribe({
        next: () => {
          this.suppliers.update(list =>
            list.map(s => s.supplierId === action.id ? { ...s, isActive: false } : s)
          );
          this.pendingAction.set(null);
          this.message.set({ text: `Supplier "${action.name}" deactivated successfully.`, severity: 'success' });
        },
        error: (err) => {
          this.message.set({ text: err?.error?.message || 'Failed to deactivate supplier.', severity: 'error' });
        }
      });
    } else {
      this.masterDataService.reactivateSupplier(action.id).subscribe({
        next: () => {
          this.suppliers.update(list =>
            list.map(s => s.supplierId === action.id ? { ...s, isActive: true } : s)
          );
          this.pendingAction.set(null);
          this.message.set({ text: `Supplier "${action.name}" reactivated successfully.`, severity: 'success' });
        },
        error: (err) => {
          this.message.set({ text: err?.error?.message || 'Failed to reactivate supplier.', severity: 'error' });
        }
      });
    }
  }
}
