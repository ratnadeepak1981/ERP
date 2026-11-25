import {
  Component,
  OnInit,
  signal,
  computed,
  inject,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { ConfirmationDialogComponent, ConfirmationDialogConfig, ConfirmationDialogResult } from '../../../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { CustomerQuotationService } from '../../../../../core/services/customer-quotation.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import {
  CustomerSalesQuotationDto,
  CustomerSalesQuotationStatus,
  getCustomerQuotationStatusLabel,
  getCustomerQuotationStatusSeverity,
  AcceptQuotationRequest,
  DeclineQuotationRequest
} from '../../../../../core/models/customer-quotation.models';

@Component({
  selector: 'app-customer-quotation-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    AppPageHeaderComponent,
    StatusBadgeComponent,
    ConfirmationDialogComponent
  ],
  templateUrl: './customer-quotation-detail.component.html',
  styleUrl: './customer-quotation-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CustomerQuotationDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly quotationService = inject(CustomerQuotationService);
  readonly authService = inject(AuthService);

  readonly loading = signal<boolean>(true);
  readonly actionInProgress = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly quotation = signal<CustomerSalesQuotationDto | null>(null);
  readonly downloadingPdf = signal<boolean>(false);

  // Confirmation dialog state
  readonly confirmDialogVisible = signal<boolean>(false);
  readonly confirmDialogConfig = signal<ConfirmationDialogConfig | null>(null);
  readonly confirmDialogLoading = signal<boolean>(false);
  readonly confirmDialogError = signal<string | null>(null);

  readonly breadcrumbs = [
    { label: 'Customer Workspace', url: '/customer-portal' },
    { label: 'Purchasing', url: '/customer-portal/quotations' },
    { label: 'Quotations', url: '/customer-portal/quotations' },
    { label: 'Quotation Details' }
  ];

  // Invariant Computeds
  readonly isCustomerRequest = computed<boolean>(() => {
    const q = this.quotation();
    return q ? Number(q.documentType) === 2 : false;
  });

  readonly isProposalActive = computed<boolean>(() => {
    const q = this.quotation();
    if (!q) return false;
    return Number(q.documentType) !== 2 
      && (q.status === CustomerSalesQuotationStatus.Sent || q.status === CustomerSalesQuotationStatus.Confirmed)
      && !q.isExpired;
  });

  readonly isConverted = computed<boolean>(() => {
    const q = this.quotation();
    return q ? q.status === CustomerSalesQuotationStatus.Converted : false;
  });

  readonly isAccepted = computed<boolean>(() => {
    const q = this.quotation();
    return q ? q.status === CustomerSalesQuotationStatus.Accepted : false;
  });

  readonly isDeclined = computed<boolean>(() => {
    const q = this.quotation();
    return q ? q.status === CustomerSalesQuotationStatus.DeclinedByCustomer : false;
  });

  readonly isExpired = computed<boolean>(() => {
    const q = this.quotation();
    return q ? q.isExpired || (q.daysUntilExpiry <= 0 && (q.status === CustomerSalesQuotationStatus.Sent || q.status === CustomerSalesQuotationStatus.Confirmed)) : false;
  });

  readonly canAct = computed<boolean>(() => {
    return this.authService.hasPermission('PORTAL_QUOTATION.ACCEPT')
      || this.authService.hasPermission('PORTAL.QUOTATION.ACCEPT')
      || this.authService.hasRole('CUSTOMER_COMPANY_ADMIN')
      || this.authService.hasRole('Company Administrator')
      || this.authService.isCustomerCompany()
      || this.authService.isCustomer();
  });

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

  openAcceptDialog(): void {
    const q = this.quotation();
    if (!q || !this.isProposalActive() || !this.canAct()) return;

    this.confirmDialogError.set(null);
    this.confirmDialogConfig.set({
      action: 'accept',
      title: `Accept Quotation ${q.quotationNumber}`,
      message: `Accept formal quotation ${q.quotationNumber} (${q.currency} ${q.totalAmount.toFixed(2)})? This will confirm your acceptance of the commercial proposal.`,
      subMessage: `Validity: ${q.daysUntilExpiry} days remaining. Once accepted, line prices and terms are locked.`,
      commentLabel: 'Customer Purchase Order Ref / Remarks (Optional)',
      commentPlaceholder: 'e.g. PO-2026-9042 or billing department notes',
      commentRequired: false,
      confirmLabel: 'Accept Quotation',
      confirmIcon: 'pi pi-check',
      confirmSeverity: 'success',
      cancelLabel: 'Keep Reviewing'
    });
    this.confirmDialogVisible.set(true);
  }

  openDeclineDialog(): void {
    const q = this.quotation();
    if (!q || !this.isProposalActive() || !this.canAct()) return;

    this.confirmDialogError.set(null);
    this.confirmDialogConfig.set({
      action: 'decline',
      title: `Reject Quotation ${q.quotationNumber}`,
      message: `Please specify the reason for rejecting quotation ${q.quotationNumber}. Your feedback will be shared with the commercial sales team.`,
      commentLabel: 'Reason for rejecting',
      commentPlaceholder: 'e.g. Quoted unit prices exceed current project budget; lead time too long...',
      commentRequired: true,
      commentMinLength: 3,
      confirmLabel: 'Reject Quotation',
      confirmIcon: 'pi pi-times',
      confirmSeverity: 'danger',
      cancelLabel: 'Keep Reviewing'
    });
    this.confirmDialogVisible.set(true);
  }

  handleConfirmResult(result: ConfirmationDialogResult): void {
    const q = this.quotation();
    if (!q) return;

    this.confirmDialogLoading.set(true);
    this.confirmDialogError.set(null);
    this.actionInProgress.set(true);

    if (result.action === 'accept') {
      const request: AcceptQuotationRequest = {
        customerRemarks: result.comment ? result.comment.trim() : null,
        idempotencyKey: `ACCEPT-${Date.now()}-${Math.random().toString(36).substring(2, 8)}`
      };

      this.quotationService.acceptQuotation(q.id, request).subscribe({
        next: (res) => {
          this.confirmDialogLoading.set(false);
          this.confirmDialogVisible.set(false);
          this.actionInProgress.set(false);
          const updated = res?.data;
          if (updated) {
            this.quotation.set(updated);
          }
          this.successMessage.set(
            `Quotation ${q.quotationNumber} accepted successfully! The commercial sales team has been notified.`
          );
          this.loadDetails(q.id);
        },
        error: (err) => {
          this.confirmDialogLoading.set(false);
          this.actionInProgress.set(false);
          const msg = err?.error?.message || err?.message || 'Failed to accept quotation.';
          this.confirmDialogError.set(msg);
        }
      });
    } else if (result.action === 'decline') {
      const request: DeclineQuotationRequest = {
        remarks: result.comment ? result.comment.trim() : 'Declined by customer'
      };

      this.quotationService.declineQuotation(q.id, request).subscribe({
        next: (res) => {
          this.confirmDialogLoading.set(false);
          this.confirmDialogVisible.set(false);
          this.actionInProgress.set(false);
          const updated = res?.data;
          if (updated) {
            this.quotation.set(updated);
          }
          this.successMessage.set(`Quotation ${q.quotationNumber} has been rejected.`);
          this.loadDetails(q.id);
        },
        error: (err) => {
          this.confirmDialogLoading.set(false);
          this.actionInProgress.set(false);
          const msg = err?.error?.message || err?.message || 'Failed to decline quotation.';
          this.confirmDialogError.set(msg);
        }
      });
    }
  }

  onCancelDialog(): void {
    this.confirmDialogVisible.set(false);
    this.confirmDialogConfig.set(null);
    this.confirmDialogError.set(null);
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
