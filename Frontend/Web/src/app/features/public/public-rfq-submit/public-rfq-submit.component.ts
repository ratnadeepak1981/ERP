import { Component, OnInit, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { RfqService } from '../../../core/services/rfq.service';
import {
  PublicRfqDetailsDto,
  SubmitPublicQuotationRequest,
  SubmitQuotationResultDto
} from '../../../core/models/rfq.models';

interface SupplierLineEntry {
  rfqLineId: string;
  lineNumber: number;
  productId: string;
  productCode: string;
  productName: string;
  quantity: number;
  quotedQuantity: number;
  unitOfMeasureId: string;
  unitOfMeasureCode: string;
  technicalSpecifications?: string;
  unitPrice: number;
  discountPercentage: number;
  taxRate: number;
  leadTimeDays: number;
  technicalRemarks: string;
}

@Component({
  selector: 'app-public-rfq-submit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './public-rfq-submit.component.html',
  styleUrls: ['./public-rfq-submit.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PublicRfqSubmitComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly rfqService = inject(RfqService);

  readonly loading = signal<boolean>(true);
  readonly error = signal<string | null>(null);
  readonly submitting = signal<boolean>(false);
  readonly token = signal<string>('');

  readonly submissionResult = signal<SubmitQuotationResultDto | null>(null);
  readonly rfqDetails = signal<PublicRfqDetailsDto | null>(null);
  readonly lineEntries = signal<SupplierLineEntry[]>([]);

  // Header input fields
  readonly quotationReference = signal<string>('');
  readonly quotationDate = signal<string>('');
  readonly validUntil = signal<string>('');
  readonly notes = signal<string>('');

  // Validation display state
  readonly submitAttempted = signal<boolean>(false);
  private readonly touched = signal<ReadonlySet<string>>(new Set<string>());

  readonly currencyCode = computed(() => this.rfqDetails()?.currencyCode || '');

  readonly errors = computed<Record<string, string>>(() => {
    const result: Record<string, string> = {};

    const reference = this.quotationReference().trim();
    if (!reference) {
      result['reference'] = 'Your quotation reference is required.';
    } else if (reference.length > 50) {
      result['reference'] = 'Quotation reference cannot exceed 50 characters.';
    }

    const quotationDate = this.quotationDate();
    const validUntil = this.validUntil();
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    if (!quotationDate) {
      result['quotationDate'] = 'Quotation date is required.';
    }

    if (!validUntil) {
      result['validUntil'] = 'Validity date is required.';
    } else if (new Date(validUntil) < today) {
      result['validUntil'] = 'Validity date cannot be in the past.';
    } else if (quotationDate && new Date(validUntil) < new Date(quotationDate)) {
      result['validUntil'] = 'Validity date cannot be before the quotation date.';
    }

    for (const line of this.lineEntries()) {
      const key = line.rfqLineId;

      const quotedQty = Number(line.quotedQuantity);
      if (!(quotedQty > 0)) {
        result[`${key}.quotedQuantity`] = 'Enter a quantity greater than zero.';
      } else if (quotedQty > line.quantity) {
        result[`${key}.quotedQuantity`] = `Cannot exceed the requested ${line.quantity}.`;
      }

      const unitPrice = Number(line.unitPrice);
      if (!(unitPrice > 0)) {
        result[`${key}.unitPrice`] = 'Enter a unit price greater than zero.';
      }

      const discount = Number(line.discountPercentage);
      if (isNaN(discount) || discount < 0 || discount > 100) {
        result[`${key}.discountPercentage`] = 'Discount must be between 0 and 100.';
      }

      const tax = Number(line.taxRate);
      if (isNaN(tax) || tax < 0 || tax > 100) {
        result[`${key}.taxRate`] = 'Tax rate must be between 0 and 100.';
      }

      const lead = Number(line.leadTimeDays);
      if (isNaN(lead) || lead < 0 || lead > 365) {
        result[`${key}.leadTimeDays`] = 'Lead time must be between 0 and 365 days.';
      }
    }

    return result;
  });

  readonly canSubmit = computed(() => Object.keys(this.errors()).length === 0);

  // Recipient totals (tax-inclusive, matching the persisted quotation).
  readonly calculatedSubtotal = computed(() =>
    this.lineEntries().reduce((total, line) => total + this.lineGross(line), 0)
  );

  readonly calculatedDiscountTotal = computed(() =>
    this.lineEntries().reduce((total, line) => total + this.lineDiscount(line), 0)
  );

  readonly calculatedTax = computed(() =>
    this.lineEntries().reduce((total, line) => total + this.lineTax(line), 0)
  );

  readonly calculatedGrandTotal = computed(() =>
    this.calculatedSubtotal() - this.calculatedDiscountTotal() + this.calculatedTax()
  );

  ngOnInit(): void {
    const rawToken = this.route.snapshot.queryParamMap.get('token');
    if (!rawToken || !rawToken.trim()) {
      this.loading.set(false);
      this.error.set('No invitation token was provided in the URL. Please verify the invitation link from your email.');
      return;
    }

    this.token.set(rawToken.trim());
    this.loadInvitationDetails(rawToken.trim());
  }

  loadInvitationDetails(token: string): void {
    this.loading.set(true);
    this.error.set(null);

    this.rfqService.getPublicInvitation(token).subscribe({
      next: data => {
        this.rfqDetails.set(data);
        this.loading.set(false);

        // Pre-populate quotation dates
        const today = new Date();
        this.quotationDate.set(today.toISOString().split('T')[0]);
        if (data.closingDate) {
          this.validUntil.set(data.closingDate.split('T')[0]);
        }

        // Initialize line entries with the requested quantity as the default quote.
        const entries: SupplierLineEntry[] = (data.lines || []).map(line => ({
          rfqLineId: line.rfqLineId,
          lineNumber: line.lineNumber,
          productId: line.productId,
          productCode: line.productCode,
          productName: line.productName,
          quantity: line.quantity,
          quotedQuantity: line.quantity,
          unitOfMeasureId: line.unitOfMeasureId,
          unitOfMeasureCode: line.unitOfMeasureCode,
          technicalSpecifications: line.technicalSpecifications,
          unitPrice: 0,
          discountPercentage: 0,
          taxRate: 0,
          leadTimeDays: 7,
          technicalRemarks: ''
        }));
        this.lineEntries.set(entries);
      },
      error: err => {
        this.loading.set(false);
        this.error.set(
          err?.error?.message ||
          err?.message ||
          'This invitation link is invalid, expired, or has already been fulfilled.'
        );
      }
    });
  }

  markTouched(key: string): void {
    const next = new Set(this.touched());
    next.add(key);
    this.touched.set(next);
  }

  onLineChange(): void {
    this.lineEntries.update(entries => [...entries]);
  }

  updateQuotedQuantity(line: SupplierLineEntry, val: unknown): void {
    const num = typeof val === 'number' ? val : parseFloat(String(val));
    line.quotedQuantity = isNaN(num) ? 0 : num;
    this.lineEntries.update(entries => [...entries]);
  }

  updateUnitPrice(line: SupplierLineEntry, val: unknown): void {
    const num = typeof val === 'number' ? val : parseFloat(String(val));
    line.unitPrice = isNaN(num) ? 0 : num;
    this.lineEntries.update(entries => [...entries]);
  }

  updateDiscount(line: SupplierLineEntry, val: unknown): void {
    const num = typeof val === 'number' ? val : parseFloat(String(val));
    line.discountPercentage = isNaN(num) ? 0 : num;
    this.lineEntries.update(entries => [...entries]);
  }

  updateTaxRate(line: SupplierLineEntry, val: unknown): void {
    const num = typeof val === 'number' ? val : parseFloat(String(val));
    line.taxRate = isNaN(num) ? 0 : num;
    this.lineEntries.update(entries => [...entries]);
  }

  updateLeadTime(line: SupplierLineEntry, val: unknown): void {
    const num = typeof val === 'number' ? val : parseInt(String(val), 10);
    line.leadTimeDays = isNaN(num) ? 0 : num;
    this.lineEntries.update(entries => [...entries]);
  }

  updateRemarks(line: SupplierLineEntry, val: string): void {
    line.technicalRemarks = val;
    this.lineEntries.update(entries => [...entries]);
  }

  shouldShowError(key: string): boolean {
    return this.submitAttempted() || this.touched().has(key);
  }

  lineGross(line: SupplierLineEntry): number {
    const qty = Number(line.quotedQuantity) || 0;
    const price = Number(line.unitPrice) || 0;
    return this.round(qty * price);
  }

  lineDiscount(line: SupplierLineEntry): number {
    const disc = Number(line.discountPercentage) || 0;
    return this.round(this.lineGross(line) * disc / 100);
  }

  lineTax(line: SupplierLineEntry): number {
    const tax = Number(line.taxRate) || 0;
    return this.round((this.lineGross(line) - this.lineDiscount(line)) * tax / 100);
  }

  calculateLineTotal(line: SupplierLineEntry): number {
    return this.round(this.lineGross(line) - this.lineDiscount(line) + this.lineTax(line));
  }

  submitQuotation(): void {
    this.submitAttempted.set(true);
    if (!this.canSubmit()) {
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    const request: SubmitPublicQuotationRequest = {
      quotationReference: this.quotationReference().trim(),
      quotationDate: new Date(this.quotationDate()).toISOString(),
      validUntil: new Date(this.validUntil()).toISOString(),
      currencyId: this.rfqDetails()?.currencyId || undefined,
      notes: this.notes().trim() || undefined,
      lines: this.lineEntries().map(line => ({
        rfqLineId: line.rfqLineId,
        quotedQuantity: Number(line.quotedQuantity),
        unitOfMeasureId: line.unitOfMeasureId,
        unitPrice: Number(line.unitPrice),
        discountPercentage: Number(line.discountPercentage) || 0,
        taxRate: Number(line.taxRate) || 0,
        leadTimeDays: Number(line.leadTimeDays) || 0,
        technicalRemarks: (line.technicalRemarks || '').trim() || undefined
      }))
    };

    this.rfqService.submitPublicQuotation(this.token(), request).subscribe({
      next: result => {
        this.submitting.set(false);
        this.submissionResult.set(result);
      },
      error: err => {
        this.submitting.set(false);
        this.error.set(
          err?.error?.message ||
          err?.message ||
          'Failed to submit quotation. Please check your submission and try again.'
        );
      }
    });
  }

  private round(value: number): number {
    return Math.round((value + Number.EPSILON) * 100) / 100;
  }
}
