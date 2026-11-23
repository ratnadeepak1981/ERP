import {
  Component,
  OnInit,
  signal,
  inject,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { CustomerQuotationService } from '../../../../../core/services/customer-quotation.service';
import {
  CustomerSalesQuotationDto,
  getCustomerQuotationStatusLabel,
  getCustomerQuotationStatusSeverity
} from '../../../../../core/models/customer-quotation.models';

@Component({
  selector: 'app-customer-quotation-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    AppPageHeaderComponent,
    StatusBadgeComponent
  ],
  templateUrl: './customer-quotation-detail.component.html',
  styleUrl: './customer-quotation-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CustomerQuotationDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly quotationService = inject(CustomerQuotationService);

  readonly loading = signal<boolean>(true);
  readonly errorMessage = signal<string | null>(null);
  readonly quotation = signal<CustomerSalesQuotationDto | null>(null);
  readonly downloadingPdf = signal<boolean>(false);

  readonly breadcrumbs = [
    { label: 'Customer Workspace', url: '/customer-portal' },
    { label: 'Purchasing', url: '/customer-portal/quotations' },
    { label: 'Quotations', url: '/customer-portal/quotations' },
    { label: 'Quotation Details' }
  ];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.errorMessage.set('Invalid quotation ID.');
      this.loading.set(false);
      return;
    }

    this.loadDetails(id);
  }

  loadDetails(id: string): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.quotationService.getQuotationDetails(id).subscribe({
      next: (data) => {
        this.loading.set(false);
        if (!data) {
          this.errorMessage.set('Quotation not found or not accessible in your account.');
          return;
        }
        this.quotation.set(data);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.message || 'Unable to load quotation details.');
      }
    });
  }

  getStatusLabel(status: number): string {
    return getCustomerQuotationStatusLabel(status);
  }

  getStatusSeverity(status: number): 'active' | 'warning' | 'info' | 'success' | 'danger' {
    return getCustomerQuotationStatusSeverity(status);
  }

  downloadPdf(): void {
    const quote = this.quotation();
    if (!quote) return;

    this.downloadingPdf.set(true);
    this.quotationService.downloadQuotationPdf(quote.id).subscribe({
      next: (blob) => {
        this.downloadingPdf.set(false);
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `SalesQuotation_${quote.quotationNumber}.pdf`;
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: () => {
        this.downloadingPdf.set(false);
      }
    });
  }

  navigateBack(): void {
    this.router.navigate(['/customer-portal/quotations']);
  }
}
