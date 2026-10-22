import {
  Component,
  OnInit,
  OnDestroy,
  inject,
  signal,
  computed,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { Subscription, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { TooltipModule } from 'primeng/tooltip';
import { SharedModule } from 'primeng/api';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppStatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AccessDeniedComponent } from '../../../../../shared/components/access-denied/access-denied.component';
import { SalesInvoiceService } from '../../../../../core/services/sales-invoice.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import {
  SalesInvoiceDto,
  SalesInvoiceItemDto,
  SalesInvoiceStatus,
  getSalesInvoiceStatusName,
  getSalesInvoiceStatusSeverity
} from '../../../../../core/models/sales-invoice.models';

export interface EditableInvoiceLine {
  id: string;
  salesOrderItemId?: string | null;
  salesShipmentItemId?: string | null;
  productId: string;
  productCode: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  discountRate: number;
  discountAmount: number;
  taxRate: number;
  taxAmount: number;
  subtotal: number;
  lineTotal: number;
  unitOfMeasureId?: string | null;
  notes?: string | null;
}

@Component({
  selector: 'app-sales-invoice-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterModule,
    ButtonModule,
    InputTextModule,
    DialogModule,
    SharedModule,
    TooltipModule,
    AppPageHeaderComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AccessDeniedComponent
  ],
  templateUrl: './sales-invoice-page.component.html',
  styleUrl: './sales-invoice-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SalesInvoicePageComponent implements OnInit, OnDestroy {
  readonly invoiceService = inject(SalesInvoiceService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);
  readonly route = inject(ActivatedRoute);

  private readonly subs = new Subscription();

  // Mode & State
  readonly mode = signal<'view' | 'edit'>('view');
  readonly invoiceId = signal<string | null>(null);
  readonly currentInvoice = signal<SalesInvoiceDto | null>(null);

  // Action States
  readonly loading = signal<boolean>(false);
  readonly actionInProgress = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Editable Working Lines
  readonly workingLines = signal<EditableInvoiceLine[]>([]);

  // Confirm Dialog
  readonly confirmDialogVisible = signal<boolean>(false);
  readonly confirmNotes = signal<string>('');

  // Form Group
  readonly headerForm = new FormGroup({
    invoiceNumber: new FormControl({ value: '', disabled: true }),
    orderNumber: new FormControl({ value: '', disabled: true }),
    shipmentNumber: new FormControl({ value: '', disabled: true }),
    customerName: new FormControl({ value: '', disabled: true }),
    invoiceDate: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    dueDate: new FormControl(''),
    currency: new FormControl({ value: 'USD', disabled: true }),
    paymentTerms: new FormControl(''),
    notes: new FormControl('')
  });

  // Breadcrumbs
  readonly breadcrumbs = computed(() => [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Sales', routerLink: '/app/sales' },
    { label: 'Sales Invoices', routerLink: '/app/sales/invoices' },
    { label: this.headerForm.get('invoiceNumber')?.value || 'Invoice Details' }
  ]);

  readonly isDraft = computed<boolean>(() => {
    const inv = this.currentInvoice();
    return inv ? Number(inv.status) === SalesInvoiceStatus.Draft : false;
  });

  readonly isConfirmed = computed<boolean>(() => {
    const inv = this.currentInvoice();
    return inv ? Number(inv.status) === SalesInvoiceStatus.Confirmed : false;
  });

  readonly canConfirm = computed<boolean>(() => {
    return (
      (this.authService.hasPermission('SALES_INVOICE.CONFIRM') ||
        this.authService.isTenantAdmin() ||
        this.authService.isPlatformAdmin()) &&
      this.isDraft()
    );
  });

  // Reactive Totals
  readonly subtotalAmount = computed<number>(() => {
    return this.workingLines().reduce((sum, item) => sum + (Number(item.subtotal) || 0), 0);
  });

  readonly discountAmount = computed<number>(() => {
    return this.workingLines().reduce((sum, item) => sum + (Number(item.discountAmount) || 0), 0);
  });

  readonly taxAmount = computed<number>(() => {
    return this.workingLines().reduce((sum, item) => sum + (Number(item.taxAmount) || 0), 0);
  });

  readonly grandTotalAmount = computed<number>(() => {
    return this.workingLines().reduce((sum, item) => sum + (Number(item.lineTotal) || 0), 0);
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    const url = this.router.url;
    if (id) {
      this.invoiceId.set(id);
      if (url.endsWith('/edit')) {
        this.mode.set('edit');
      } else {
        this.mode.set('view');
      }
      this.loadInvoice(id);
    }
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  loadInvoice(id: string): void {
    const comp = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!comp?.id) return;

    this.loading.set(true);
    this.error.set(null);

    this.invoiceService.getInvoice(comp.id, branch?.id || null, id).subscribe({
      next: (inv: SalesInvoiceDto | null) => {
        this.loading.set(false);
        if (!inv) {
          this.error.set('Sales invoice not found.');
          return;
        }

        this.currentInvoice.set(inv);
        this.headerForm.patchValue({
          invoiceNumber: inv.invoiceNumber,
          orderNumber: inv.orderNumber,
          shipmentNumber: inv.shipmentNumber || 'N/A',
          customerName: inv.customerName,
          invoiceDate: inv.invoiceDate ? inv.invoiceDate.substring(0, 10) : '',
          dueDate: inv.dueDate ? inv.dueDate.substring(0, 10) : '',
          currency: inv.currency,
          paymentTerms: inv.paymentTerms || '',
          notes: inv.notes || ''
        });

        if (this.mode() === 'view' || !this.isDraft()) {
          this.headerForm.disable();
        }

        // Map lines
        const lines: EditableInvoiceLine[] = (inv.items || []).map(item => ({
          id: item.id,
          salesOrderItemId: item.salesOrderItemId,
          salesShipmentItemId: item.salesShipmentItemId,
          productId: item.productId,
          productCode: item.productCode,
          productName: item.productName,
          quantity: item.quantity,
          unitPrice: item.unitPrice,
          discountRate: item.discountRate,
          discountAmount: item.discountAmount,
          taxRate: item.taxRate,
          taxAmount: item.taxAmount,
          subtotal: item.subtotal,
          lineTotal: item.lineTotal,
          unitOfMeasureId: item.unitOfMeasureId,
          notes: item.notes
        }));

        this.workingLines.set(lines);
      },
      error: (err: any) => {
        this.loading.set(false);
        this.error.set(err?.message || 'Failed to load sales invoice details.');
      }
    });
  }

  recalculateLine(index: number): void {
    const lines = [...this.workingLines()];
    const line = lines[index];
    if (!line) return;

    const qty = Number(line.quantity) || 0;
    const price = Number(line.unitPrice) || 0;
    const discRate = Number(line.discountRate) || 0;
    const taxRate = Number(line.taxRate) || 0;

    const subtotal = Math.round(qty * price * 10000) / 10000;
    const discountAmt = discRate > 0 ? Math.round(subtotal * (discRate / 100) * 10000) / 10000 : 0;
    const taxable = subtotal - discountAmt;
    const taxAmt = taxRate > 0 ? Math.round(taxable * (taxRate / 100) * 10000) / 10000 : 0;
    const lineTotal = taxable + taxAmt;

    line.subtotal = subtotal;
    line.discountAmount = discountAmt;
    line.taxAmount = taxAmt;
    line.lineTotal = lineTotal;

    this.workingLines.set(lines);
  }

  updateLinePrice(index: number, price: number): void {
    const lines = [...this.workingLines()];
    if (lines[index]) {
      lines[index].unitPrice = Math.max(0, price);
      this.workingLines.set(lines);
      this.recalculateLine(index);
    }
  }

  updateLineDiscount(index: number, disc: number): void {
    const lines = [...this.workingLines()];
    if (lines[index]) {
      lines[index].discountRate = Math.min(100, Math.max(0, disc));
      this.workingLines.set(lines);
      this.recalculateLine(index);
    }
  }

  updateLineTax(index: number, tax: number): void {
    const lines = [...this.workingLines()];
    if (lines[index]) {
      lines[index].taxRate = Math.min(100, Math.max(0, tax));
      this.workingLines.set(lines);
      this.recalculateLine(index);
    }
  }

  saveDraft(): void {
    const comp = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    const id = this.invoiceId();
    if (!comp?.id || !branch?.id || !id) return;

    this.actionInProgress.set(true);
    const formVal = this.headerForm.getRawValue();

    this.invoiceService
      .updateDraft(comp.id, branch.id, id, {
        invoiceDate: formVal.invoiceDate || undefined,
        dueDate: formVal.dueDate || undefined,
        paymentTerms: formVal.paymentTerms || undefined,
        notes: formVal.notes || undefined,
        items: this.workingLines().map(l => ({
          productId: l.productId,
          quantity: l.quantity,
          unitPrice: l.unitPrice,
          discountRate: l.discountRate,
          taxRate: l.taxRate,
          salesOrderItemId: l.salesOrderItemId,
          salesShipmentItemId: l.salesShipmentItemId,
          unitOfMeasureId: l.unitOfMeasureId,
          notes: l.notes || undefined
        }))
      })
      .subscribe({
        next: (updated: SalesInvoiceDto) => {
          this.actionInProgress.set(false);
          this.currentInvoice.set(updated);
          this.message.set({ text: 'Sales invoice draft updated successfully.', severity: 'success' });
          this.mode.set('view');
          this.headerForm.disable();
        },
        error: (err: any) => {
          this.actionInProgress.set(false);
          this.message.set({
            text: err?.error?.message || err?.message || 'Failed to update invoice draft.',
            severity: 'danger'
          });
        }
      });
  }

  openConfirmDialog(): void {
    this.confirmNotes.set('');
    this.confirmDialogVisible.set(true);
  }

  closeConfirmDialog(): void {
    this.confirmDialogVisible.set(false);
  }

  executeConfirm(): void {
    const comp = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    const id = this.invoiceId();
    if (!comp?.id || !branch?.id || !id) return;

    this.actionInProgress.set(true);
    this.invoiceService
      .confirmInvoice(comp.id, branch.id, id, this.confirmNotes().trim() || undefined)
      .subscribe({
        next: (updated: SalesInvoiceDto) => {
          this.actionInProgress.set(false);
          this.closeConfirmDialog();
          this.currentInvoice.set(updated);
          this.message.set({
            text: `Sales invoice ${updated.invoiceNumber} explicitly confirmed successfully!`,
            severity: 'success'
          });
          this.mode.set('view');
          this.headerForm.disable();
        },
        error: (err: any) => {
          this.actionInProgress.set(false);
          this.message.set({
            text: err?.error?.message || err?.message || 'Failed to confirm sales invoice.',
            severity: 'danger'
          });
        }
      });
  }

  navigateBack(): void {
    this.router.navigate(['/app/sales/invoices']);
  }
}
