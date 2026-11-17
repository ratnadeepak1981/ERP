import { Component, OnInit, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { RfqService } from '../../../core/services/rfq.service';
import {
  PublicRfqDetailsDto,
  PublicRfqLineDto,
  SubmitPublicQuotationRequest,
  SubmitPublicQuotationLineRequest,
  SupplierQuotationDto
} from '../../../core/models/rfq.models';

interface SupplierLineEntry {
  rfqLineId: string;
  lineNumber: number;
  productId: string;
  productCode: string;
  productName: string;
  quantity: number;
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
  styleUrl: './public-rfq-submit.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PublicRfqSubmitComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly rfqService = inject(RfqService);

  readonly token = signal<string>('');
  readonly loading = signal<boolean>(true);
  readonly submitting = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly submittedQuotation = signal<SupplierQuotationDto | null>(null);

  readonly rfqDetails = signal<PublicRfqDetailsDto | null>(null);
  readonly lineEntries = signal<SupplierLineEntry[]>([]);

  // Header input fields
  readonly quotationReference = signal<string>('');
  readonly quotationDate = signal<string>('');
  readonly validUntil = signal<string>('');
  readonly notes = signal<string>('');

  // Dynamically calculated totals
  readonly calculatedSubtotal = computed(() => {
    return this.lineEntries().reduce((acc, l) => {
      const discounted = l.quantity * l.unitPrice * (1 - (l.discountPercentage || 0) / 100);
      return acc + discounted;
    }, 0);
  });

  readonly calculatedTax = computed(() => {
    return this.lineEntries().reduce((acc, l) => {
      const discounted = l.quantity * l.unitPrice * (1 - (l.discountPercentage || 0) / 100);
      const tax = discounted * ((l.taxRate || 0) / 100);
      return acc + tax;
    }, 0);
  });

  readonly calculatedDiscountTotal = computed(() => {
    return this.lineEntries().reduce((acc, l) => {
      const gross = l.quantity * l.unitPrice;
      const discount = gross * ((l.discountPercentage || 0) / 100);
      return acc + discount;
    }, 0);
  });

  readonly calculatedGrandTotal = computed(() => {
    return this.calculatedSubtotal() + this.calculatedTax();
  });

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

        // Initialize line entries
        const entries: SupplierLineEntry[] = (data.lines || []).map(line => ({
          rfqLineId: line.rfqLineId,
          lineNumber: line.lineNumber,
          productId: line.productId,
          productCode: line.productCode,
          productName: line.productName,
          quantity: line.quantity,
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

  calculateLineTotal(line: SupplierLineEntry): number {
    const discounted = line.quantity * line.unitPrice * (1 - (line.discountPercentage || 0) / 100);
    const withTax = discounted * (1 + (line.taxRate || 0) / 100);
    return withTax;
  }

  canSubmit(): boolean {
    if (!this.quotationReference().trim()) return false;
    if (!this.validUntil()) return false;
    const lines = this.lineEntries();
    if (lines.length === 0) return false;
    // Every line must have a positive unit price
    return lines.every(l => l.unitPrice > 0);
  }

  submitQuotation(): void {
    if (!this.canSubmit()) return;

    this.submitting.set(true);
    this.error.set(null);

    const request: SubmitPublicQuotationRequest = {
      quotationReference: this.quotationReference().trim(),
      quotationDate: new Date(this.quotationDate()).toISOString(),
      validUntil: new Date(this.validUntil()).toISOString(),
      currencyId: this.rfqDetails()?.currencyId || '00000000-0000-0000-0000-000000000000',
      notes: this.notes().trim() || undefined,
      lines: this.lineEntries().map(l => ({
        rfqLineId: l.rfqLineId,
        quotedQuantity: l.quantity,
        unitOfMeasureId: l.unitOfMeasureId,
        unitPrice: l.unitPrice,
        discountPercentage: l.discountPercentage || 0,
        leadTimeDays: l.leadTimeDays || 7,
        technicalRemarks: l.technicalRemarks.trim() || undefined
      }))
    };

    this.rfqService.submitPublicQuotation(this.token(), request).subscribe({
      next: result => {
        this.submitting.set(false);
        this.submittedQuotation.set(result);
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
}
