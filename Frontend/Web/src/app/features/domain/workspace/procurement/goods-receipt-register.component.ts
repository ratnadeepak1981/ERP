import { Component, OnInit, OnDestroy, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../shared/components/data-table/data-table.component';
import { AppLoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { AppEmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { AccessDeniedComponent } from '../../../../shared/components/access-denied/access-denied.component';
import { GoodsReceiptNoteService } from '../../../../core/services/goods-receipt-note.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import {
  GoodsReceiptNoteDto,
  GoodsReceiptNoteStatus,
  getGrnStatusName,
  getGrnStatusSeverity
} from '../../../../core/models/goods-receipt-note.models';
import { DataTableColumn, DataTableAction } from '../../../../shared/models/data-table-config.model';

export type GRNStatusFilter = 'all' | 'draft' | 'confirmed' | 'cancelled';

@Component({
  selector: 'app-goods-receipt-register',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    ButtonModule,
    InputTextModule,
    AppPageHeaderComponent,
    AppDataTableComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AppEmptyStateComponent,
    AccessDeniedComponent
  ],
  templateUrl: './goods-receipt-register.component.html',
  styleUrl: './goods-receipt-register.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GoodsReceiptRegisterComponent implements OnInit, OnDestroy {
  readonly grnService = inject(GoodsReceiptNoteService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly router = inject(Router);

  private readonly subs = new Subscription();

  // Loading & State
  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to access goods receipts for this operating company and branch."
  );

  // Data
  readonly receipts = signal<GoodsReceiptNoteDto[]>([]);
  readonly searchTerm = signal<string>('');
  readonly activeStatusFilter = signal<GRNStatusFilter>('all');

  // Breadcrumbs
  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Procurement', routerLink: '/app/procurement' },
    { label: 'Goods Receipts' }
  ];

  // Table Columns
  readonly tableColumns: DataTableColumn<GoodsReceiptNoteDto>[] = [
    {
      field: 'grnNumber',
      header: 'GRN Number',
      sortable: true,
      width: '180px'
    },
    {
      field: 'receiptDate',
      header: 'Receipt Date',
      sortable: true,
      width: '130px',
      format: (val: any) => {
        if (!val) return '-';
        const d = new Date(val);
        return isNaN(d.getTime()) ? String(val) : d.toLocaleDateString();
      }
    },
    {
      field: 'deliveryNoteNumber',
      header: 'Delivery Note',
      sortable: true,
      width: '160px',
      format: (val: any) => val || '-'
    },
    {
      field: 'totalAmount',
      header: 'Total Value',
      sortable: true,
      align: 'right',
      width: '140px',
      type: 'currency'
    },
    {
      field: 'status',
      header: 'Status',
      width: '130px',
      type: 'badge',
      badgeSeverityMap: (val: any) => getGrnStatusSeverity(val),
      format: (val: any) => getGrnStatusName(val)
    },
    {
      field: 'goodsReceiptNoteId',
      header: 'Actions',
      width: '120px',
      type: 'actions'
    }
  ];

  // Table Row Actions
  readonly tableActions: DataTableAction<GoodsReceiptNoteDto>[] = [
    {
      id: 'view',
      label: 'View',
      icon: 'pi pi-eye',
      severity: 'secondary',
      action: (row: GoodsReceiptNoteDto) => this.navigateToView(row)
    }
  ];

  // Filtered List
  readonly filteredReceipts = computed<GoodsReceiptNoteDto[]>(() => {
    let list = this.receipts();
    const filter = this.activeStatusFilter();
    if (filter !== 'all') {
      list = list.filter(r => getGrnStatusName(r.status).toLowerCase() === filter);
    }

    const term = this.searchTerm().trim().toLowerCase();
    if (term) {
      list = list.filter(r => {
        const num = (r.grnNumber || '').toLowerCase();
        const dn = (r.deliveryNoteNumber || '').toLowerCase();
        return num.includes(term) || dn.includes(term);
      });
    }

    return list;
  });

  ngOnInit(): void {
    if (this.scopeService.canOperateTransactions()) {
      this.loadReceipts();
    }

    this.subs.add(
      this.scopeService.scopeChange$.subscribe(() => {
        if (this.scopeService.canOperateTransactions()) {
          this.loadReceipts();
        } else {
          this.receipts.set([]);
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  loadReceipts(): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) {
      this.receipts.set([]);
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.isAccessDenied.set(false);

    this.grnService.getGrns(company.id, branch.id).subscribe({
      next: grns => {
        this.loading.set(false);
        this.receipts.set(grns);
      },
      error: err => {
        this.loading.set(false);
        if (this.isForbiddenError(err)) {
          this.isAccessDenied.set(true);
        } else {
          this.error.set(this.getCleanErrorMessage(err, 'Failed to load goods receipts from server.'));
        }
      }
    });
  }

  navigateToView(grn: GoodsReceiptNoteDto): void {
    this.router.navigate(['/app/procurement/goods-receipts', grn.goodsReceiptNoteId]);
  }

  private isForbiddenError(err: any): boolean {
    if (!err) return false;
    if (err.status === 403 || err.statusText === 'Forbidden') return true;
    if (err?.error?.status === 403) return true;
    const msg = String(err?.message || err?.error?.message || '');
    return msg.includes('403') || msg.toLowerCase().includes('forbidden');
  }

  private getCleanErrorMessage(err: any, fallback: string): string {
    if (this.isForbiddenError(err)) {
      return "You don't have authorization to perform this action. Contact your administrator if you believe this is an error.";
    }
    const rawMsg = err?.error?.message;
    if (rawMsg && !rawMsg.includes('http://') && !rawMsg.includes('https://') && !rawMsg.includes('localhost')) {
      return rawMsg;
    }
    return fallback;
  }
}
