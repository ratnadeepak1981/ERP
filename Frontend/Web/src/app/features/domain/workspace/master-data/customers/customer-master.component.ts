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
import { CustomerItem } from '../../../../../core/models/scope.models';

@Component({
  selector: 'app-customer-master',
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
  templateUrl: './customer-master.component.html',
  styleUrl: './customer-master.component.scss'
})
export class CustomerMasterComponent implements OnInit {
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly router = inject(Router);

  readonly loading = signal<boolean>(true);
  readonly error = signal<string | null>(null);
  readonly message = signal<{ text: string; severity: 'success' | 'warn' | 'error' } | null>(null);

  readonly customers = signal<CustomerItem[]>([]);
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
    { label: 'Customer Master' }
  ];

  readonly filteredCustomers = computed(() => {
    let list = this.customers();
    const q = this.searchTerm().trim().toLowerCase();
    if (q) {
      list = list.filter(c =>
        c.customerCode.toLowerCase().includes(q) ||
        c.customerName.toLowerCase().includes(q) ||
        (c.contactPerson && c.contactPerson.toLowerCase().includes(q)) ||
        (c.city && c.city.toLowerCase().includes(q))
      );
    }
    const st = this.statusFilter();
    if (st === 'active') {
      list = list.filter(c => c.isActive);
    } else if (st === 'inactive') {
      list = list.filter(c => !c.isActive);
    }
    return list;
  });

  ngOnInit(): void {
    this.loadCustomers();
  }

  loadCustomers(): void {
    this.loading.set(true);
    this.error.set(null);

    this.masterDataService.getCustomers().subscribe({
      next: (custs) => {
        this.customers.set(custs);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.message || 'Failed to retrieve customers from server.');
      }
    });
  }

  openCreateDialog(): void {
    this.router.navigate(['/app/master-data/customers/new']);
  }

  openEditDialog(customer: CustomerItem): void {
    this.router.navigate(['/app/master-data/customers', customer.customerId]);
  }

  promptDeactivate(customer: CustomerItem): void {
    this.pendingAction.set({
      type: 'deactivate',
      id: customer.customerId,
      code: customer.customerCode,
      name: customer.customerName
    });
    this.message.set(null);
  }

  promptReactivate(customer: CustomerItem): void {
    this.pendingAction.set({
      type: 'reactivate',
      id: customer.customerId,
      code: customer.customerCode,
      name: customer.customerName
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
      this.masterDataService.deactivateCustomer(action.id).subscribe({
        next: () => {
          this.customers.update(list =>
            list.map(c => c.customerId === action.id ? { ...c, isActive: false } : c)
          );
          this.pendingAction.set(null);
          this.message.set({ text: `Customer "${action.name}" deactivated successfully.`, severity: 'success' });
        },
        error: (err) => {
          this.message.set({ text: err?.error?.message || 'Failed to deactivate customer.', severity: 'error' });
        }
      });
    } else {
      this.masterDataService.reactivateCustomer(action.id).subscribe({
        next: () => {
          this.customers.update(list =>
            list.map(c => c.customerId === action.id ? { ...c, isActive: true } : c)
          );
          this.pendingAction.set(null);
          this.message.set({ text: `Customer "${action.name}" reactivated successfully.`, severity: 'success' });
        },
        error: (err) => {
          this.message.set({ text: err?.error?.message || 'Failed to reactivate customer.', severity: 'error' });
        }
      });
    }
  }
}
