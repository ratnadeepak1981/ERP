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

import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AccessDeniedComponent } from '../../../../../shared/components/access-denied/access-denied.component';
import { ConfirmationDialogComponent, ConfirmationDialogConfig, ConfirmationDialogResult } from '../../../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { SalesQuotationService } from '../../../../../core/services/sales-quotation.service';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import {
  SalesQuotationDto,
  SalesQuotationItemDto,
  CreateSalesQuotationDraftRequest,
  UpdateSalesQuotationDraftRequest,
  CreateSalesQuotationItemRequest,
  SalesQuotationStatus,
  SalesQuotationType,
  getSalesQuotationStatusName,
  getSalesQuotationStatusSeverity,
  getSalesQuotationEmailStatusSeverity,
  ApprovalAuditRecord
} from '../../../../../core/models/sales-quotation.models';
import { CustomerItem, ProductItem, TaxCodeItem } from '../../../../../core/models/scope.models';

export type SQPageMode = 'create' | 'view' | 'prepare' | 'edit';

export interface EditableQuotationLine {
  lineId: string;
  productId: string;
  productCode: string;
  productName: string;
  unitOfMeasureId?: string | null;
  unitOfMeasureCode?: string | null;
  quantity: number;
  unitPrice: number;
  unitCost: number;
  grossMarginPercentage?: number | null;
  discountRate: number;
  discountAmount: number;
  taxCodeId?: string | null;
  taxRate: number;
  taxAmount: number;
  subTotal: number;
  lineTotal: number;
  isTaxInclusive: boolean;
  notes?: string | null;
}

@Component({
  selector: 'app-sales-quotation-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterModule,
    AppPageHeaderComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AccessDeniedComponent,
    ConfirmationDialogComponent
  ],
  templateUrl: './sales-quotation-page.component.html',
  styleUrl: './sales-quotation-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SalesQuotationPageComponent implements OnInit, OnDestroy {
  readonly quotationService = inject(SalesQuotationService);
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);
  readonly route = inject(ActivatedRoute);

  private readonly subs = new Subscription();

  // Mode & Quotation State
  readonly mode = signal<SQPageMode>('create');
  readonly quotationId = signal<string | null>(null);
  readonly currentQuotation = signal<SalesQuotationDto | null>(null);
  readonly approvalHistory = signal<ApprovalAuditRecord[]>([]);

  // Loading & States
  readonly loading = signal<boolean>(false);
  readonly actionInProgress = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to access sales quotations for this operating company and branch. Contact your administrator if you believe this is an error."
  );
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Master Data
  readonly customers = signal<CustomerItem[]>([]);
  readonly products = signal<ProductItem[]>([]);
  readonly taxCodes = signal<TaxCodeItem[]>([]);

  // Form & Offered Lines
  quotationForm!: FormGroup;
  readonly offeredLines = signal<EditableQuotationLine[]>([]);

  // Action Dialogs
  readonly remarksDialogVisible = signal<boolean>(false);
  readonly remarksAction = signal<'approve' | 'reject' | 'cancel' | 'return-to-draft' | 'send' | 'retry' | null>(null);
  readonly remarksText = signal<string>('');
  readonly remarksError = signal<string | null>(null);

  // Confirmation Dialog
  readonly confirmDialogVisible = signal<boolean>(false);
  readonly confirmDialogConfig = signal<ConfirmationDialogConfig | null>(null);
  private confirmCallback: (() => void) | null = null;

  // Permissions
  readonly canCreate = computed(() => this.authService.hasPermission('SALES_QUOTATION.CREATE'));
  readonly canEdit = computed(() => this.authService.hasPermission('SALES_QUOTATION.EDIT'));
  readonly canApprove = computed(() => this.authService.hasPermission('SALES_QUOTATION.APPROVE') || this.authService.hasPermission('SALES_ORDER.CONFIRM'));
  readonly canCreateOrder = computed(() => this.authService.hasPermission('SALES_ORDER.CREATE'));

  // Scope Labels
  readonly orderCompanyName = computed(() => this.scopeService.selectedCompany()?.name ?? '—');
  readonly orderBranchName = computed(() => this.scopeService.selectedBranch()?.name ?? '—');

  // Breadcrumbs
  readonly breadcrumbs = computed(() => {
    const list = [
      { label: 'Workspace', routerLink: '/app/dashboard' },
      { label: 'Sales', routerLink: '/app/sales' },
      { label: 'Quotations', routerLink: '/app/sales/quotations' }
    ];

    switch (this.mode()) {
      case 'create':
        list.push({ label: 'New Commercial Quotation', routerLink: '' });
        break;
      case 'edit':
        list.push({ label: `Amend Quotation ${this.currentQuotation()?.quotationNumber || ''}`, routerLink: '' });
        break;
      case 'view':
      default: {
        const num = this.currentQuotation()?.quotationNumber || 'Quotation Details';
        list.push({ label: num, routerLink: '' });
        break;
      }
    }
    return list;
  });

  // Page Title & Subtitle (Matching Sales Order Reference Standard)
  readonly pageTitle = computed<string>(() => {
    const q = this.currentQuotation();
    const num = q?.quotationNumber ? ` - ${q.quotationNumber}` : (this.quotationId() ? ` - ${this.quotationId()}` : '');
    switch (this.mode()) {
      case 'create':
        return 'New Commercial Quotation';
      case 'edit':
        return `Amend Quotation${num}`;
      case 'view':
      default:
        return `Quotation Details${num}`;
    }
  });

  readonly pageSubtitle = computed<string>(() => {
    switch (this.mode()) {
      case 'create':
        return 'Create a commercial quotation with line pricing, discounts, tax, and delivery terms.';
      case 'edit':
        return 'Adjust offered products, quantities, commercial pricing, or settlement terms.';
      case 'view':
      default:
        return 'Inspect quotation lines, commercial margins, validity, and customer status.';
    }
  });

  readonly isCustomerRequest = computed<boolean>(() => {
    const q = this.currentQuotation();
    return q ? Number(q.documentType) === SalesQuotationType.CustomerRequest : false;
  });

  readonly isReadOnly = computed<boolean>(() => {
    const q = this.currentQuotation();
    if (!q) return false;
    return Number(q.status) !== SalesQuotationStatus.Draft;
  });

  // Computed Totals
  readonly calculatedSubTotal = computed<number>(() => {
    const sum = this.offeredLines().reduce((acc, l) => acc + (l.subTotal || 0), 0);
    return Math.round(sum * 100) / 100;
  });

  readonly calculatedDiscountAmount = computed<number>(() => {
    const sum = this.offeredLines().reduce((acc, l) => acc + (l.discountAmount || 0), 0);
    return Math.round(sum * 100) / 100;
  });

  readonly calculatedTaxAmount = computed<number>(() => {
    const sum = this.offeredLines().reduce((acc, l) => acc + (l.taxAmount || 0), 0);
    return Math.round(sum * 100) / 100;
  });

  readonly calculatedTotalAmount = computed<number>(() => {
    const sum = this.offeredLines().reduce((acc, l) => acc + (l.lineTotal || 0), 0);
    return Math.round(sum * 100) / 100;
  });

  readonly calculatedTotalCost = computed<number>(() => {
    const sum = this.offeredLines().reduce((acc, l) => acc + ((l.unitCost || 0) * (l.quantity || 0)), 0);
    return Math.round(sum * 100) / 100;
  });

  readonly calculatedGrossMarginPercentage = computed<number | null>(() => {
    const sub = this.calculatedSubTotal();
    const cost = this.calculatedTotalCost();
    if (sub <= 0 || cost <= 0) return null;
    return Math.round(((sub - cost) / sub) * 10000) / 100;
  });

  ngOnInit(): void {
    this.initForm();

    const id = this.route.snapshot.paramMap.get('id');
    if (id && id !== 'new') {
      this.quotationId.set(id);
      this.mode.set('view');
      this.loadExistingQuotation(id);
    } else {
      this.mode.set('create');
      this.loadMasterData();
    }
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  private initForm(): void {
    const today = new Date().toISOString().substring(0, 10);
    const validUntil = new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString().substring(0, 10);

    this.quotationForm = new FormGroup({
      customerId: new FormControl<string>('', [Validators.required]),
      customerContactId: new FormControl<string | null>(null),
      quotationDate: new FormControl<string>(today, [Validators.required]),
      validUntilDateUtc: new FormControl<string>(validUntil, [Validators.required]),
      deliveryDate: new FormControl<string | null>(null),
      currency: new FormControl<string>('USD', [Validators.required]),
      paymentTerms: new FormControl<string>('Net 30'),
      deliveryTerms: new FormControl<string>('FOB Destination'),
      deliveryAddressSnapshot: new FormControl<string | null>(null),
      customerRemarks: new FormControl<string | null>(null),
      internalNotes: new FormControl<string | null>(null)
    });
  }

  loadMasterData(callback?: () => void): void {
    forkJoin({
      customers: this.masterDataService.getCustomers().pipe(catchError(() => of([]))),
      products: this.masterDataService.getProducts().pipe(catchError(() => of([]))),
      taxCodes: this.masterDataService.getTaxCodes('Sales', false).pipe(catchError(() => of([])))
    }).subscribe({
      next: res => {
        this.customers.set(res.customers || []);
        this.products.set(res.products || []);
        this.taxCodes.set(res.taxCodes || []);
        if (callback) callback();
      }
    });
  }

  loadExistingQuotation(id: string): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();

    if (!company || !branch) {
      this.loadMasterData(() => this.fetchQuotationDetails(id));
      return;
    }

    this.loadMasterData(() => this.fetchQuotationDetails(id));
  }

  private fetchQuotationDetails(id: string): void {
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) return;

    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      quote: this.quotationService.getQuotation(company.id, branch.id, id),
      history: this.quotationService.getApprovalHistory(company.id, branch.id, id).pipe(catchError(() => of([])))
    }).subscribe({
      next: res => {
        this.loading.set(false);
        const q = res.quote;
        if (!q) {
          this.error.set(`Sales quotation '${id}' was not found in authorized scope.`);
          return;
        }

        this.currentQuotation.set(q);
        this.approvalHistory.set(res.history || []);

        // Populate Form
        this.quotationForm.patchValue({
          customerId: q.customerId,
          customerContactId: q.customerContactId,
          quotationDate: q.quotationDate ? q.quotationDate.substring(0, 10) : '',
          validUntilDateUtc: q.validUntilDateUtc ? q.validUntilDateUtc.substring(0, 10) : '',
          deliveryDate: q.deliveryDate ? q.deliveryDate.substring(0, 10) : null,
          currency: q.currency || 'USD',
          paymentTerms: q.paymentTerms || '',
          deliveryTerms: q.deliveryTerms || '',
          deliveryAddressSnapshot: q.deliveryAddressSnapshot || '',
          customerRemarks: q.customerRemarks || '',
          internalNotes: q.internalNotes || ''
        });

        // Populate Lines
        const lines: EditableQuotationLine[] = (q.items || []).map(i => {
          const prod = this.products().find(p => p.productId === i.productId);
          const cost = i.unitCost !== null && i.unitCost !== undefined ? Number(i.unitCost) : (prod?.standardCost ?? 0);
          return {
            lineId: i.id || `line-${Date.now()}-${Math.random()}`,
            productId: i.productId,
            productCode: i.productCode || prod?.productCode || '',
            productName: i.productName || prod?.productName || '',
            unitOfMeasureId: i.unitOfMeasureId,
            unitOfMeasureCode: i.unitOfMeasureCode || prod?.unitOfMeasure || '',
            quantity: Number(i.quantity) || 1,
            unitPrice: Number(i.unitPrice) || 0,
            unitCost: cost,
            grossMarginPercentage: i.grossMarginPercentage,
            discountRate: Number(i.discountRate) || 0,
            discountAmount: Number(i.discountAmount) || 0,
            taxCodeId: i.taxCodeId,
            taxRate: Number(i.taxRate) || 0,
            taxAmount: Number(i.taxAmount) || 0,
            subTotal: Number(i.subTotal) || 0,
            lineTotal: Number(i.lineTotal) || 0,
            isTaxInclusive: !!i.isTaxInclusive,
            notes: i.notes || ''
          };
        });

        this.offeredLines.set(lines);
      },
      error: err => {
        this.loading.set(false);
        if (err?.status === 403) {
          this.isAccessDenied.set(true);
        } else {
          this.error.set(err?.message || 'Failed to load quotation details.');
        }
      }
    });
  }

  // Line Operations
  addLine(): void {
    const products = this.products();
    const defaultProduct = products.length > 0 ? products[0] : null;

    const newLine: EditableQuotationLine = {
      lineId: `new-${Date.now()}-${Math.random()}`,
      productId: defaultProduct?.productId || '',
      productCode: defaultProduct?.productCode || '',
      productName: defaultProduct?.productName || '',
      unitOfMeasureId: defaultProduct?.unitOfMeasureId || null,
      unitOfMeasureCode: defaultProduct?.unitOfMeasure || '',
      quantity: 1,
      unitPrice: defaultProduct?.sellingPrice || 0,
      unitCost: defaultProduct?.standardCost || 0,
      grossMarginPercentage: null,
      discountRate: 0,
      discountAmount: 0,
      taxCodeId: null,
      taxRate: 0,
      taxAmount: 0,
      subTotal: defaultProduct?.sellingPrice || 0,
      lineTotal: defaultProduct?.sellingPrice || 0,
      isTaxInclusive: false,
      notes: ''
    };

    this.recalculateLine(newLine);
    this.offeredLines.update(lines => [...lines, newLine]);
  }

  removeLine(index: number): void {
    this.offeredLines.update(lines => lines.filter((_, i) => i !== index));
  }

  onProductChanged(index: number, productId: string): void {
    const prod = this.products().find(p => p.productId === productId);
    if (!prod) return;

    this.offeredLines.update(lines => {
      const copy = [...lines];
      const line = copy[index];
      if (line) {
        line.productId = prod.productId;
        line.productCode = prod.productCode;
        line.productName = prod.productName;
        line.unitOfMeasureId = prod.unitOfMeasureId || null;
        line.unitOfMeasureCode = prod.unitOfMeasure || '';
        line.unitPrice = prod.sellingPrice || line.unitPrice;
        line.unitCost = prod.standardCost || 0;
        this.recalculateLine(line);
      }
      return copy;
    });
  }

  onTaxCodeChanged(index: number, taxCodeId: string): void {
    const tc = this.taxCodes().find(t => t.taxCodeId === taxCodeId);
    this.offeredLines.update(lines => {
      const copy = [...lines];
      const line = copy[index];
      if (line) {
        line.taxCodeId = tc ? tc.taxCodeId : null;
        line.taxRate = tc ? Number(tc.rate) : 0;
        this.recalculateLine(line);
      }
      return copy;
    });
  }

  onLineValuesChanged(index: number): void {
    this.offeredLines.update(lines => {
      const copy = [...lines];
      const line = copy[index];
      if (line) {
        this.recalculateLine(line);
      }
      return copy;
    });
  }

  private recalculateLine(line: EditableQuotationLine): void {
    const qty = Math.max(0, Number(line.quantity) || 0);
    const price = Math.max(0, Number(line.unitPrice) || 0);
    const discRate = Math.min(100, Math.max(0, Number(line.discountRate) || 0));
    const taxRate = Math.max(0, Number(line.taxRate) || 0);

    const gross = qty * price;
    const discount = Math.round((gross * (discRate / 100)) * 100) / 100;
    const subtotal = Math.round((gross - discount) * 100) / 100;
    const tax = Math.round((subtotal * (taxRate / 100)) * 100) / 100;
    const total = Math.round((subtotal + tax) * 100) / 100;

    line.quantity = qty;
    line.unitPrice = price;
    line.discountRate = discRate;
    line.discountAmount = discount;
    line.subTotal = subtotal;
    line.taxRate = taxRate;
    line.taxAmount = tax;
    line.lineTotal = total;

    if (price > 0 && line.unitCost > 0) {
      line.grossMarginPercentage = Math.round(((price - line.unitCost) / price) * 10000) / 100;
    } else {
      line.grossMarginPercentage = null;
    }
  }

  // Save Draft (New or Update)
  saveDraft(): void {
    if (this.quotationForm.invalid) {
      this.quotationForm.markAllAsTouched();
      this.message.set({ text: 'Please complete all required fields.', severity: 'warn' });
      return;
    }

    if (this.offeredLines().length === 0) {
      this.message.set({ text: 'Commercial offer must include at least one product line.', severity: 'warn' });
      return;
    }

    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!company || !branch) {
      this.message.set({ text: 'Operating scope is not selected.', severity: 'danger' });
      return;
    }

    const formVal = this.quotationForm.value;
    const itemsReq: CreateSalesQuotationItemRequest[] = this.offeredLines().map(l => ({
      productId: l.productId,
      unitOfMeasureId: l.unitOfMeasureId,
      quantity: l.quantity,
      unitPrice: l.unitPrice,
      discountRate: l.discountRate,
      taxCodeId: l.taxCodeId,
      taxRate: l.taxRate,
      isTaxInclusive: l.isTaxInclusive,
      notes: l.notes
    }));

    this.actionInProgress.set(true);
    const existingId = this.quotationId();

    if (existingId) {
      const updateReq: UpdateSalesQuotationDraftRequest = {
        quotationDate: formVal.quotationDate ? new Date(formVal.quotationDate).toISOString() : null,
        validUntilDateUtc: formVal.validUntilDateUtc ? new Date(formVal.validUntilDateUtc).toISOString() : null,
        deliveryDate: formVal.deliveryDate ? new Date(formVal.deliveryDate).toISOString() : null,
        currency: formVal.currency,
        paymentTerms: formVal.paymentTerms,
        deliveryTerms: formVal.deliveryTerms,
        notes: formVal.customerRemarks,
        internalNotes: formVal.internalNotes,
        deliveryAddressSnapshot: formVal.deliveryAddressSnapshot,
        items: itemsReq
      };

      this.quotationService.updateDraft(company.id, branch.id, existingId, updateReq).subscribe({
        next: updated => {
          this.actionInProgress.set(false);
          this.message.set({ text: `Commercial quotation draft ${updated.quotationNumber} updated successfully.`, severity: 'success' });
          this.fetchQuotationDetails(existingId);
        },
        error: err => {
          this.actionInProgress.set(false);
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to update quotation draft.', severity: 'danger' });
        }
      });
    } else {
      const createReq: CreateSalesQuotationDraftRequest = {
        customerId: formVal.customerId,
        customerContactId: formVal.customerContactId,
        quotationDate: formVal.quotationDate ? new Date(formVal.quotationDate).toISOString() : null,
        validUntilDateUtc: formVal.validUntilDateUtc ? new Date(formVal.validUntilDateUtc).toISOString() : null,
        deliveryDate: formVal.deliveryDate ? new Date(formVal.deliveryDate).toISOString() : null,
        currency: formVal.currency || 'USD',
        paymentTerms: formVal.paymentTerms,
        deliveryTerms: formVal.deliveryTerms,
        notes: formVal.customerRemarks,
        internalNotes: formVal.internalNotes,
        deliveryAddressSnapshot: formVal.deliveryAddressSnapshot,
        items: itemsReq
      };

      this.quotationService.createDraft(company.id, branch.id, createReq).subscribe({
        next: created => {
          this.actionInProgress.set(false);
          this.message.set({ text: `Commercial quotation draft ${created.quotationNumber} created successfully.`, severity: 'success' });
          this.router.navigate(['/app/sales/quotations', created.id]);
        },
        error: err => {
          this.actionInProgress.set(false);
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to create quotation draft.', severity: 'danger' });
        }
      });
    }
  }

  // Submit for Approval
  submitForApproval(): void {
    const q = this.currentQuotation();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!q || !company || !branch) return;

    this.confirmDialogConfig.set({
      action: 'submit',
      title: 'Submit Offer for Internal Approval',
      message: `Submit commercial offer ${q.quotationNumber} with total value of ${this.calculatedTotalAmount()} ${q.currency} for management approval? Authoring will be locked pending managerial review.`,
      confirmLabel: 'Submit for Approval',
      confirmSeverity: 'primary',
      confirmIcon: 'pi pi-send'
    });
    this.confirmCallback = () => {
      this.actionInProgress.set(true);
      this.quotationService.submitQuotation(company.id, branch.id, q.id).subscribe({
        next: updated => {
          this.actionInProgress.set(false);
          this.message.set({ text: `Quotation ${updated.quotationNumber} submitted for approval successfully.`, severity: 'success' });
          this.fetchQuotationDetails(q.id);
        },
        error: err => {
          this.actionInProgress.set(false);
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to submit quotation.', severity: 'danger' });
        }
      });
    };
    this.confirmDialogVisible.set(true);
  }

  // Return to Draft / Prepare Offer
  prepareCommercialOffer(): void {
    const q = this.currentQuotation();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!q || !company || !branch) return;

    this.actionInProgress.set(true);
    this.quotationService.returnToDraft(company.id, branch.id, q.id, 'Offer preparation by sales staff').subscribe({
      next: updated => {
        this.actionInProgress.set(false);
        this.message.set({ text: `Quotation returned to Draft. You can now adjust offered products, quantities, prices, and terms.`, severity: 'info' });
        this.fetchQuotationDetails(q.id);
      },
      error: err => {
        this.actionInProgress.set(false);
        this.message.set({ text: err?.error?.message || err?.message || 'Failed to return quotation to Draft.', severity: 'danger' });
      }
    });
  }

  // Convert to Sales Order
  convertToSalesOrder(): void {
    const q = this.currentQuotation();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!q || !company || !branch) return;

    if (Number(q.status) !== SalesQuotationStatus.Accepted) {
      this.message.set({
        text: 'Sales Order conversion is locked until formal customer acceptance has been verified.',
        severity: 'warn'
      });
      return;
    }

    this.confirmDialogConfig.set({
      action: 'convert',
      title: 'Convert Quotation to Sales Order',
      message: `Convert quotation ${q.quotationNumber} into an official Sales Order Draft? All line items, negotiated prices, and quantities will be automatically copied.`,
      confirmLabel: 'Convert to Sales Order',
      confirmSeverity: 'primary',
      confirmIcon: 'pi pi-shopping-bag'
    });
    this.confirmCallback = () => {
      this.actionInProgress.set(true);
      this.quotationService.convertQuotationToOrder(company.id, branch.id, q.id).subscribe({
        next: (updated: SalesQuotationDto) => {
          this.actionInProgress.set(false);
          this.message.set({
            text: `Quotation ${updated.quotationNumber} successfully converted to Sales Order Draft.`,
            severity: 'success'
          });
          this.fetchQuotationDetails(q.id);
          this.router.navigate(['/app/sales/orders']);
        },
        error: (err: any) => {
          this.actionInProgress.set(false);
          this.message.set({
            text: err?.error?.message || err?.message || 'Failed to convert quotation to order.',
            severity: 'danger'
          });
        }
      });
    };
    this.confirmDialogVisible.set(true);
  }

  navigateToSalesOrders(): void {
    this.router.navigate(['/app/sales/orders']);
  }

  // Open Dialogs
  openApproveDialog(): void {
    const q = this.currentQuotation();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!q || !company || !branch) return;

    this.remarksAction.set('approve');
    this.confirmDialogConfig.set({
      action: 'approve',
      title: 'Approve Commercial Offer',
      message: `Approve formal commercial quotation ${q.quotationNumber} for customer ${q.customerName || 'Customer'}? Authoring will be locked and the offer will be ready for formal client delivery.`,
      subMessage: `Commercial Total: ${this.calculatedTotalAmount()} ${this.quotationForm.get('currency')?.value || q.currency}`,
      commentLabel: 'Approval Remarks (Optional)',
      commentPlaceholder: 'Enter any managerial approval notes or margin justifications...',
      commentRequired: false,
      confirmLabel: 'Approve Offer',
      confirmIcon: 'pi pi-check',
      confirmSeverity: 'primary'
    });
    this.confirmDialogVisible.set(true);
  }

  openRejectDialog(): void {
    const q = this.currentQuotation();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!q || !company || !branch) return;

    this.remarksAction.set('reject');
    this.confirmDialogConfig.set({
      action: 'reject',
      title: 'Reject Commercial Offer',
      message: `Reject commercial quotation ${q.quotationNumber}? A mandatory rejection reason is required for managerial audit tracking.`,
      commentLabel: 'Rejection Reason *',
      commentPlaceholder: 'State why this commercial offer cannot be approved...',
      commentRequired: true,
      commentMinLength: 3,
      confirmLabel: 'Reject Offer',
      confirmIcon: 'pi pi-times',
      confirmSeverity: 'danger'
    });
    this.confirmDialogVisible.set(true);
  }

  openSendEmailDialog(isRetry: boolean = false): void {
    const q = this.currentQuotation();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!q || !company || !branch) return;

    const action = isRetry ? 'retry' : 'send';
    this.remarksAction.set(action);
    this.confirmDialogConfig.set({
      action,
      title: isRetry ? 'Retry Failed Email Notification' : 'Send Quotation Email',
      message: `Dispatch formal quotation ${q.quotationNumber} to customer ${q.customerName || 'Customer'} via email? Recipient: ${q.contactEmail || q.emailRecipient || 'Customer Primary Email'}.`,
      subMessage: 'A professional notification email containing Selling Company details, Quotation reference, Offer expiry date, and a secure "View Quotation" button linking directly to the Customer Workspace will be dispatched.',
      commentLabel: 'Dispatch Notes (Optional)',
      commentPlaceholder: 'Enter optional internal dispatch notes...',
      commentRequired: false,
      confirmLabel: isRetry ? 'Retry Email' : 'Send Quotation Email',
      confirmIcon: 'pi pi-envelope',
      confirmSeverity: 'primary'
    });
    this.confirmDialogVisible.set(true);
  }

  openCancelDialog(): void {
    const q = this.currentQuotation();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!q || !company || !branch) return;

    this.remarksAction.set('cancel');
    this.confirmDialogConfig.set({
      action: 'cancel',
      title: 'Cancel Quotation',
      message: `Cancel quotation ${q.quotationNumber}? Cancelled quotations cannot be approved or converted.`,
      commentLabel: 'Cancellation Reason (Optional)',
      commentPlaceholder: 'Enter reason for cancellation...',
      commentRequired: false,
      confirmLabel: 'Cancel Quotation',
      confirmIcon: 'pi pi-ban',
      confirmSeverity: 'danger'
    });
    this.confirmDialogVisible.set(true);
  }

  handleConfirmDialogResult(result: ConfirmationDialogResult): void {
    this.confirmDialogVisible.set(false);
    if ((result.action === 'submit' || result.action === 'convert') && this.confirmCallback) {
      this.confirmCallback();
      this.confirmCallback = null;
      return;
    }
    this.executeRemarksActionWith(result.action, result.comment);
  }

  executeRemarksActionWith(action: string, remarks: string): void {
    const q = this.currentQuotation();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!q || !action || !company || !branch) return;

    const trimmedRemarks = (remarks || '').trim();
    this.actionInProgress.set(true);
    this.remarksError.set(null);

    if (action === 'approve') {
      this.quotationService.approveQuotation(company.id, branch.id, q.id, trimmedRemarks).subscribe({
        next: (updated: SalesQuotationDto) => {
          this.actionInProgress.set(false);
          this.message.set({ text: `Quotation ${updated.quotationNumber} approved successfully. You can now email it to the customer.`, severity: 'success' });
          this.fetchQuotationDetails(q.id);
        },
        error: (err: any) => {
          this.actionInProgress.set(false);
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to approve quotation.', severity: 'danger' });
        }
      });
    } else if (action === 'reject') {
      this.quotationService.rejectQuotation(company.id, branch.id, q.id, trimmedRemarks).subscribe({
        next: (updated: SalesQuotationDto) => {
          this.actionInProgress.set(false);
          this.message.set({ text: `Quotation ${updated.quotationNumber} rejected.`, severity: 'warn' });
          this.fetchQuotationDetails(q.id);
        },
        error: (err: any) => {
          this.actionInProgress.set(false);
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to reject quotation.', severity: 'danger' });
        }
      });
    } else if (action === 'cancel') {
      this.quotationService.cancelQuotation(company.id, branch.id, q.id, trimmedRemarks).subscribe({
        next: (updated: SalesQuotationDto) => {
          this.actionInProgress.set(false);
          this.message.set({ text: `Quotation ${updated.quotationNumber} cancelled.`, severity: 'info' });
          this.fetchQuotationDetails(q.id);
        },
        error: (err: any) => {
          this.actionInProgress.set(false);
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to cancel quotation.', severity: 'danger' });
        }
      });
    } else if (action === 'send' || action === 'retry') {
      const isRetry = action === 'retry';
      this.quotationService.sendQuotationEmail(company.id, branch.id, q.id, isRetry).subscribe({
        next: (updated: SalesQuotationDto) => {
          this.actionInProgress.set(false);
          this.message.set({ text: `Quotation ${updated.quotationNumber} email notification sent to customer successfully.`, severity: 'success' });
          this.fetchQuotationDetails(q.id);
        },
        error: (err: any) => {
          this.actionInProgress.set(false);
          this.message.set({ text: err?.error?.message || err?.message || 'Failed to dispatch quotation email.', severity: 'danger' });
          this.fetchQuotationDetails(q.id);
        }
      });
    }
  }

  closeRemarksDialog(): void {
    this.confirmDialogVisible.set(false);
    this.remarksAction.set(null);
    this.remarksText.set('');
    this.remarksError.set(null);
  }

  downloadPdf(): void {
    const q = this.currentQuotation();
    const company = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!q || !company || !branch) return;

    this.quotationService.downloadPdf(company.id, branch.id, q.id).subscribe({
      next: (blob: Blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `Quotation_${q.quotationNumber}.pdf`;
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: () => {
        this.message.set({ text: 'Failed to generate quotation PDF.', severity: 'danger' });
      }
    });
  }

  getStatusName(status: SalesQuotationStatus | number | string): string {
    return getSalesQuotationStatusName(status);
  }

  getStatusSeverity(status: SalesQuotationStatus | number | string): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
    return getSalesQuotationStatusSeverity(status);
  }

  getEmailSeverity(status?: string | null): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
    return getSalesQuotationEmailStatusSeverity(status);
  }

  navigateBack(): void {
    this.router.navigate(['/app/sales/quotations']);
  }
}
