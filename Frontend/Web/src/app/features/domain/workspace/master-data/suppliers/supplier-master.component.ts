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

  readonly suppliers = signal<SupplierItem[]>([]);
  readonly searchTerm = signal<string>('');
  readonly statusFilter = signal<'all' | 'active' | 'inactive'>('all');

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

  onDeactivate(supplier: SupplierItem): void {
    if (confirm(`Deactivate supplier ${supplier.supplierName} (${supplier.supplierCode})?`)) {
      this.masterDataService.deactivateSupplier(supplier.supplierId).subscribe({
        next: () => this.loadSuppliers(),
        error: (err) => alert(err?.error?.message || 'Failed to deactivate supplier.')
      });
    }
  }
}
