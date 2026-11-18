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
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { Subscription, forkJoin } from 'rxjs';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppStatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { AppEmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ConfirmationDialogComponent, ConfirmationDialogConfig, ConfirmationDialogResult } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { AppMaterialEditableDataTableComponent } from '../../../../shared/components/material-editable-data-table/core/app-material-editable-data-table.component';
import { MaterialLookupConfig, MaterialSelectOption, MaterialTableColumn } from '../../../../shared/components/material-editable-data-table/models/material-data-table.models';
import { RfqService } from '../../../../core/services/rfq.service';
import { MasterDataService } from '../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { TenantHostService } from '../../../../core/tenant/tenant-host.service';
import { TenantContextService } from '../../../../core/services/tenant-context.service';
import {
  RfqDetailsDto,
  RfqLineDto,
  RfqSupplierDto,
  SupplierQuotationDto,
  RfqComparisonSupplierBidDto,
  RfqComparisonMatrixDto,
  RfqComparisonLineDto,
  RfqComparisonLineBidDto,
  RfqEvaluationFilterState,
  RfqEvaluationSortOption,
  CreateRfqRequest,
  UpdateRfqRequest,
  CreateRfqLineRequest,
  AddRfqSupplierRequest,
  SendRfqInvitationsRequest,
  RecordManualQuotationRequest,
  ApproveQuotationRequest,
  RejectQuotationRequest,
  RequestForQuotationStatus,
  DraftRfqLineItem,
  RfqInvitationAccessOptionsDto,
  getRfqStatusName,
  getRfqStatusSeverity,
  getSupplierInvitationStatusName,
  getSupplierQuotationStatusName
} from '../../../../core/models/rfq.models';
import { ProductItem, SupplierItem, UnitOfMeasureItem } from '../../../../core/models/scope.models';

export type RfqWorkflowStep = 'details' | 'suppliers' | 'invitations' | 'quotations' | 'comparison';

@Component({
  selector: 'app-rfq-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    AppPageHeaderComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AppEmptyStateComponent,
    ConfirmationDialogComponent,
    AppMaterialEditableDataTableComponent
  ],
  templateUrl: './rfq-page.component.html',
  styleUrl: './rfq-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RfqPageComponent implements OnInit, OnDestroy {
  readonly getRfqStatusName = getRfqStatusName;
  readonly getRfqStatusSeverity = getRfqStatusSeverity;
  readonly getSupplierInvitationStatusName = getSupplierInvitationStatusName;
  readonly getSupplierQuotationStatusName = getSupplierQuotationStatusName;

  readonly rfqService = inject(RfqService);
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly tenantHostService = inject(TenantHostService, { optional: true });
  readonly tenantContextService = inject(TenantContextService, { optional: true });
  readonly router = inject(Router);
  readonly route = inject(ActivatedRoute);

  private readonly subs = new Subscription();

  // State Signals
  readonly isNew = signal<boolean>(false);
  readonly rfqId = signal<string | null>(null);
  readonly rfq = signal<RfqDetailsDto | null>(null);
  readonly activeStep = signal<RfqWorkflowStep>('details');

  readonly loading = signal<boolean>(false);
  readonly actionLoading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Master Data
  readonly products = signal<ProductItem[]>([]);
  readonly suppliers = signal<SupplierItem[]>([]);
  readonly uoms = signal<UnitOfMeasureItem[]>([]);
  readonly supplierSearchTerm = signal<string>('');
  readonly stagedSupplierIds = signal<string[]>([]);
  readonly selectedInvitationSupplierIds = signal<string[]>([]);

  readonly availableSuppliers = computed(() => {
    const query = this.supplierSearchTerm().trim().toLowerCase();
    const assignedIds = new Set((this.rfq()?.invitedSuppliers || []).map(supplier => supplier.supplierId));
    const stagedIds = new Set(this.stagedSupplierIds());
    return this.suppliers().filter(supplier =>
      supplier.isActive &&
      !assignedIds.has(supplier.supplierId) &&
      !stagedIds.has(supplier.supplierId) &&
      (!query || [
        supplier.supplierCode,
        supplier.supplierName,
        supplier.contactName,
        supplier.email,
        supplier.city,
        supplier.country
      ].some(value => value?.toLowerCase().includes(query)))
    );
  });

  readonly stagedSuppliers = computed(() => {
    const stagedIds = new Set(this.stagedSupplierIds());
    return this.suppliers().filter(supplier => stagedIds.has(supplier.supplierId));
  });

  readonly approvedQuotation = computed(() =>
    this.rfq()?.quotations?.find(q => getSupplierQuotationStatusName(q.status) === 'Approved')
  );

  readonly winningSupplierId = computed(() =>
    this.approvedQuotation()?.supplierId
  );

  readonly eligibleInvitationSuppliers = computed(() =>
    (this.rfq()?.invitedSuppliers || []).filter(supplier => this.isInvitationEligible(supplier))
  );

  // RFQ Edit Header Form State
  readonly formTitle = signal<string>('');
  readonly formClosingDate = signal<string>('');
  readonly formDeliveryRequiredDate = signal<string>('');
  readonly formDeliveryTerms = signal<string>('');
  readonly formInstructions = signal<string>('');

  // Add Line Form State
  readonly newLineProductId = signal<string>('');
  readonly newLineQuantity = signal<number>(1);
  readonly newLineUomId = signal<string>('');
  readonly newLineTargetPrice = signal<number | null>(null);
  readonly newLineRequiredDate = signal<string>('');
  readonly newLineSpecs = signal<string>('');
  readonly newLineNotes = signal<string>('');

  // Draft Lines for New RFQ
  readonly newRfqLines = signal<DraftRfqLineItem[]>([]);
  readonly editingDraftLineIndex = signal<number | null>(null);

  // Add Supplier Form State
  readonly newSupplierId = signal<string>('');

  // Manual Quotation Modal / Form State
  readonly manualQuoteModalVisible = signal<boolean>(false);
  readonly manualSupplierId = signal<string>('');
  readonly manualReference = signal<string>('');
  readonly manualDate = signal<string>('');
  readonly manualValidUntil = signal<string>('');
  readonly manualNotes = signal<string>('');
  readonly manualLines = signal<{
    rfqLineId: string;
    productId: string;
    unitOfMeasureId: string;
    productName: string;
    quantity: number;
    uomCode: string;
    unitPrice: number;
    discountPct: number;
    taxRate: number;
    leadTimeDays: number;
    technicalRemarks: string;
  }[]>([]);

  // Bid Comparison Matrix
  readonly comparisonMatrix = signal<RfqComparisonMatrixDto | null>(null);
  readonly matrixLoading = signal<boolean>(false);

  // Evaluation Dashboard State (Phase 1)
  readonly evaluationFilterState = signal<RfqEvaluationFilterState>({
    searchTerm: '',
    statusFilter: 'ALL',
    expiryFilter: 'ALL',
    maxLeadTimeDays: null,
    minBaseAmount: null,
    maxBaseAmount: null,
    currencyFilter: 'ALL',
    sortBy: 'PRICE_ASC',
    viewMode: 'grid'
  });

  readonly evaluationActiveTab = signal<'cards' | 'commercial' | 'matrix'>('cards');

  readonly availableCurrencies = computed<string[]>(() => {
    const m = this.comparisonMatrix();
    if (!m || !m.suppliers) return [];
    const set = new Set<string>();
    for (const s of m.suppliers) {
      if (s.currencyCode) set.add(s.currencyCode);
    }
    return Array.from(set).sort();
  });

  readonly filteredSuppliers = computed<RfqComparisonSupplierBidDto[]>(() => {
    const m = this.comparisonMatrix();
    if (!m || !m.suppliers) return [];

    const f = this.evaluationFilterState();
    let list = [...m.suppliers];

    // Search filter
    if (f.searchTerm && f.searchTerm.trim()) {
      const q = f.searchTerm.trim().toLowerCase();
      list = list.filter(s =>
        (s.supplierName && s.supplierName.toLowerCase().includes(q)) ||
        (s.supplierCode && s.supplierCode.toLowerCase().includes(q)) ||
        (s.quotationReference && s.quotationReference.toLowerCase().includes(q))
      );
    }

    // Status filter
    if (f.statusFilter && f.statusFilter !== 'ALL') {
      list = list.filter(s => s.status === f.statusFilter);
    }

    // Expiry filter
    if (f.expiryFilter === 'ACTIVE') {
      list = list.filter(s => !s.isExpired);
    } else if (f.expiryFilter === 'EXPIRED') {
      list = list.filter(s => s.isExpired);
    }

    // Lead time filter (maximum supplier fulfillment lead time)
    if (f.maxLeadTimeDays !== null && f.maxLeadTimeDays !== undefined && f.maxLeadTimeDays > 0) {
      list = list.filter(s =>
        s.maxLeadTimeDays !== null &&
        s.maxLeadTimeDays !== undefined &&
        s.maxLeadTimeDays <= f.maxLeadTimeDays!
      );
    }

    // Min base amount
    if (f.minBaseAmount !== null && f.minBaseAmount !== undefined && f.minBaseAmount >= 0) {
      list = list.filter(s => s.baseTotalAmount >= f.minBaseAmount!);
    }

    // Max base amount
    if (f.maxBaseAmount !== null && f.maxBaseAmount !== undefined && f.maxBaseAmount > 0) {
      list = list.filter(s => s.baseTotalAmount <= f.maxBaseAmount!);
    }

    // Currency filter
    if (f.currencyFilter && f.currencyFilter !== 'ALL') {
      list = list.filter(s => s.currencyCode === f.currencyFilter);
    }

    // Sorting
    list.sort((a, b) => {
      switch (f.sortBy) {
        case 'PRICE_ASC': {
          const aValid = a.hasValidExchangeRate && a.baseTotalAmount > 0;
          const bValid = b.hasValidExchangeRate && b.baseTotalAmount > 0;
          if (!aValid && !bValid) return 0;
          if (!aValid) return 1;
          if (!bValid) return -1;
          return a.baseTotalAmount - b.baseTotalAmount;
        }
        case 'PRICE_DESC': {
          const aValid = a.hasValidExchangeRate && a.baseTotalAmount > 0;
          const bValid = b.hasValidExchangeRate && b.baseTotalAmount > 0;
          if (!aValid && !bValid) return 0;
          if (!aValid) return 1;
          if (!bValid) return -1;
          return b.baseTotalAmount - a.baseTotalAmount;
        }
        case 'LEAD_TIME_ASC': {
          const aLead = a.maxLeadTimeDays ?? 999999;
          const bLead = b.maxLeadTimeDays ?? 999999;
          return aLead - bLead;
        }
        case 'VALIDITY_DESC': {
          const aTime = a.validUntil ? new Date(a.validUntil).getTime() : 0;
          const bTime = b.validUntil ? new Date(b.validUntil).getTime() : 0;
          return bTime - aTime;
        }
        case 'NAME_ASC':
          return (a.supplierName || '').localeCompare(b.supplierName || '');
        default:
          return 0;
      }
    });

    return list;
  });

  readonly evaluationSummaryMetrics = computed(() => {
    const m = this.comparisonMatrix();
    if (!m || !m.suppliers || m.suppliers.length === 0) return null;

    const allSuppliers = m.suppliers;
    const activeSuppliers = allSuppliers.filter(s => s.status !== 'Draft' && s.status !== 'Rejected');
    const validBids = activeSuppliers.filter(s => s.hasValidExchangeRate && s.baseTotalAmount > 0);
    const sortedByPrice = [...validBids].sort((a, b) => a.baseTotalAmount - b.baseTotalAmount);

    const lowestBid = sortedByPrice.length > 0 ? sortedByPrice[0] : null;
    const highestBid = sortedByPrice.length > 0 ? sortedByPrice[sortedByPrice.length - 1] : null;
    const priceSpread = lowestBid && highestBid ? Math.max(0, highestBid.baseTotalAmount - lowestBid.baseTotalAmount) : 0;
    const spreadPercentage = lowestBid && highestBid && lowestBid.baseTotalAmount > 0
      ? (priceSpread / lowestBid.baseTotalAmount) * 100
      : 0;

    const activeLeadTimes = activeSuppliers
      .map(s => s.maxLeadTimeDays)
      .filter((lead): lead is number => lead !== null && lead !== undefined);
    const fastestOrderDelivery = activeLeadTimes.length > 0 ? Math.min(...activeLeadTimes) : null;

    const expiredCount = allSuppliers.filter(s => s.isExpired).length;
    const hasExchangeRateWarning = allSuppliers.some(s => s.hasValidExchangeRate === false);

    return {
      totalSubmissions: allSuppliers.length,
      activeBidsCount: activeSuppliers.length,
      lowestBid,
      highestBid,
      priceSpread,
      spreadPercentage,
      fastestOrderDelivery,
      expiredCount,
      hasExchangeRateWarning
    };
  });

  readonly comparisonAnalysis = computed(() => {
    const m = this.comparisonMatrix();
    if (!m || m.suppliers.length === 0) return null;

    const eligibleSuppliers = m.suppliers.filter(s => s.status !== 'Draft' && s.status !== 'Rejected');
    if (eligibleSuppliers.length === 0) return null;

    const validBids = eligibleSuppliers.filter(s => s.baseTotalAmount > 0);
    const sortedByPrice = [...validBids].sort((a, b) => a.baseTotalAmount - b.baseTotalAmount);

    const lowest = sortedByPrice.length > 0 ? sortedByPrice[0] : null;
    const highest = sortedByPrice.length > 0 ? sortedByPrice[sortedByPrice.length - 1] : null;
    const priceSpread = lowest && highest ? Math.max(0, highest.baseTotalAmount - lowest.baseTotalAmount) : 0;

    const leadTimes: number[] = [];
    const discounts: number[] = [];
    for (const line of m.lines) {
      for (const bid of line.bids) {
        if (bid.leadTimeDays !== undefined && bid.leadTimeDays !== null) {
          leadTimes.push(bid.leadTimeDays);
        }
        if (bid.discountPercentage !== undefined && bid.discountPercentage !== null && bid.discountPercentage > 0) {
          discounts.push(bid.discountPercentage);
        }
      }
    }

    const minLeadTime = leadTimes.length > 0 ? Math.min(...leadTimes) : null;
    const maxLeadTime = leadTimes.length > 0 ? Math.max(...leadTimes) : null;
    const maxDiscount = discounts.length > 0 ? Math.max(...discounts) : 0;

    return {
      eligibleCount: eligibleSuppliers.length,
      lowestBid: lowest,
      highestBid: highest,
      priceSpread,
      minLeadTime,
      maxLeadTime,
      maxDiscount,
      hasCurrencyConversionIssue: eligibleSuppliers.some(s => s.hasValidExchangeRate === false)
    };
  });

  // Decision Modal (Approve / Reject) State
  readonly decisionModalVisible = signal<boolean>(false);
  readonly decisionType = signal<'approve' | 'reject'>('approve');
  readonly selectedQuotation = signal<SupplierQuotationDto | null>(null);
  readonly decisionRemarks = signal<string>('');
  readonly decisionAwardRfq = signal<boolean>(true);

  // Step 4 quotation review state
  readonly expandedQuotationId = signal<string | null>(null);
  readonly quotationDetail = signal<SupplierQuotationDto | null>(null);
  readonly quotationDetailLoading = signal<boolean>(false);

  // Confirmation Modal
  readonly confirmDialogVisible = signal<boolean>(false);
  readonly confirmDialogConfig = signal<ConfirmationDialogConfig | null>(null);
  readonly confirmAction = signal<(() => void) | null>(null);

  // Supplier Portal Access Options state
  readonly latestAccessOptions = signal<RfqInvitationAccessOptionsDto | null>(null);
  readonly portalAccessModalVisible = signal<boolean>(false);
  readonly selectedSupplierForAccess = signal<RfqSupplierDto | null>(null);

  readonly activeAccessOptions = computed<RfqInvitationAccessOptionsDto | null>(() => {
    const selected = this.selectedSupplierForAccess();
    if (selected) {
      if (selected.accessOptions) return selected.accessOptions;
      if (selected.standardLocalhostUrl || selected.lanIpUrl) {
        return {
          standardLocalhostUrl: selected.standardLocalhostUrl,
          tenantLocalhostUrl: selected.tenantLocalhostUrl,
          tenantLocalhostUnavailableReason: selected.tenantLocalhostUnavailableReason,
          externalTenantUrl: selected.externalTenantUrl,
          externalTenantUnavailableReason: selected.externalTenantUnavailableReason,
          lanIpUrl: selected.lanIpUrl,
          lanIpUnavailableReason: selected.lanIpUnavailableReason
        };
      }
    }
    return this.latestAccessOptions();
  });

  // Breadcrumbs Computed
  readonly breadcrumbs = computed(() => [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Procurement', routerLink: '/app/procurement' },
    { label: 'Request for Quotations', routerLink: '/app/procurement/rfqs' },
    { label: this.isNew() ? 'New RFQ' : (this.rfq()?.rfqNumber || 'RFQ Details') }
  ]);
  readonly workspaceSubtitle = computed(() => {
    const title = this.rfq()?.title;
    const companyName = this.scopeService.selectedCompany()?.name;
    return [title, companyName ? `Company: ${companyName}` : null]
      .filter((value): value is string => !!value)
      .join(' · ') || 'Manage RFQ requirements, suppliers, and quotations.';
  });

  // Computed Quantities and Values
  readonly totalDraftItemsCount = computed(() => this.newRfqLines().length);
  readonly totalDraftRequestedQuantity = computed(() =>
    this.newRfqLines().reduce((sum, l) => sum + (Number(l.quantity) || 0), 0)
  );
  readonly totalDraftEstimatedAmount = computed(() =>
    this.newRfqLines().reduce((sum, l) => sum + ((Number(l.quantity) || 0) * (Number(l.targetUnitPrice) || 0)), 0)
  );

  readonly totalExistingItemsCount = computed(() => this.rfq()?.lines?.length || 0);
  readonly totalExistingRequestedQuantity = computed(() =>
    (this.rfq()?.lines || []).reduce((sum, l) => sum + (Number(l.quantity) || 0), 0)
  );
  readonly totalExistingEstimatedAmount = computed(() =>
    (this.rfq()?.lines || []).reduce((sum, l) => sum + ((Number(l.quantity) || 0) * (Number(l.targetUnitPrice) || 0)), 0)
  );

  // Permissions & Workflow Guards
  readonly canManageSuppliers = computed(() => {
    const isPrivileged = this.authService.isTenantAdmin() || this.authService.isPlatformAdmin();
    const status = getRfqStatusName(this.rfq()?.status);
    return (status === 'Draft' || status === 'Issued') &&
      (isPrivileged || this.authService.hasPermission('RFQ.SUPPLIER.MANAGE'));
  });

  readonly canEdit = computed(() => {
    const isPrivileged = this.authService.isTenantAdmin() || this.authService.isPlatformAdmin();
    const r = this.rfq();
    if (!r) return this.isNew() && (isPrivileged || this.authService.hasPermission('RFQ.CREATE'));
    const status = getRfqStatusName(r.status);
    const isDraftOrIssued = status === 'Draft' || status === 'Issued';
    return isDraftOrIssued && (isPrivileged || this.authService.hasPermission('RFQ.EDIT'));
  });

  workflowStepComplete(step: RfqWorkflowStep): boolean {
    const currentRfq = this.rfq();
    if (!currentRfq) return false;

    switch (step) {
      case 'details':
        return currentRfq.lines.length > 0;
      case 'suppliers':
        return currentRfq.invitedSuppliers.length > 0;
      case 'invitations':
        return currentRfq.invitedSuppliers.length > 0 &&
          currentRfq.invitedSuppliers.every(supplier => {
            const invStatus = getSupplierInvitationStatusName(supplier.invitationStatus);
            return invStatus === 'Opened' || invStatus === 'Submitted' || invStatus === 'Declined';
          });
      case 'quotations':
        return currentRfq.quotations.length > 0;
      case 'comparison':
        return getRfqStatusName(currentRfq.status) === 'Awarded' ||
          currentRfq.quotations.some(quotation => getSupplierQuotationStatusName(quotation.status) === 'Approved');
    }
  }

  workflowStepStatus(step: RfqWorkflowStep): 'Complete' | 'Ready' | 'Pending' {
    if (this.workflowStepComplete(step)) return 'Complete';
    if (step === 'comparison' && (this.rfq()?.quotations.length || 0) > 0) return 'Ready';
    if (step === 'quotations' && (this.rfq()?.quotations.length || 0) > 0) return 'Ready';
    if (step === 'invitations' && this.rfq()?.invitedSuppliers.some(supplier => getSupplierInvitationStatusName(supplier.invitationStatus) === 'Sent')) {
      return 'Ready';
    }
    return 'Pending';
  }

  readonly canSendInvitations = computed(() => {
    const isPrivileged = this.authService.isTenantAdmin() || this.authService.isPlatformAdmin();
    const r = this.rfq();
    if (!r) return false;
    const status = getRfqStatusName(r.status);
    if (status === 'Awarded') {
      return r.invitedSuppliers.some(supplier => this.isInvitationEligible(supplier)) &&
        (isPrivileged || this.authService.hasPermission('RFQ.AWARD') || this.authService.hasPermission('RFQ.SEND'));
    }
    return (status === 'Draft' || status === 'Issued') &&
      r.invitedSuppliers.some(supplier => this.isInvitationEligible(supplier)) &&
      (isPrivileged || this.authService.hasPermission('RFQ.SEND'));
  });

  readonly canRecordManualQuote = computed(() => {
    const isPrivileged = this.authService.isTenantAdmin() || this.authService.isPlatformAdmin();
    const r = this.rfq();
    if (!r) return false;
    const status = getRfqStatusName(r.status);
    return (status === 'Draft' || status === 'Issued') &&
      (isPrivileged || this.authService.hasPermission('RFQ.QUOTATION.RECORD'));
  });

  readonly canCompare = computed(() => {
    const isPrivileged = this.authService.isTenantAdmin() || this.authService.isPlatformAdmin();
    const r = this.rfq();
    if (!r) return false;
    return isPrivileged || this.authService.hasPermission('RFQ.COMPARE');
  });

  readonly canViewQuotations = computed(() => {
    const isPrivileged = this.authService.isTenantAdmin() || this.authService.isPlatformAdmin();
    return isPrivileged || this.authService.hasPermission('RFQ.QUOTATION.VIEW');
  });

  readonly submittedQuotationCount = computed(() =>
    (this.rfq()?.quotations || []).filter(q => getSupplierQuotationStatusName(q.status) !== 'Draft').length
  );

  readonly pendingSupplierCount = computed(() => {
    const r = this.rfq();
    if (!r) return 0;
    const submittedSupplierIds = new Set(r.quotations.map(q => q.supplierId));
    return r.invitedSuppliers.filter(s => !submittedSupplierIds.has(s.supplierId)).length;
  });

  readonly canApproveReject = computed(() => {
    const isPrivileged = this.authService.isTenantAdmin() || this.authService.isPlatformAdmin();
    const r = this.rfq();
    if (!r) return false;
    const currentUserId = this.authService.currentUser()?.userId;
    // 4-eyes principle check
    const isCreator = currentUserId ? r.createdBy === currentUserId : false;
    return !isCreator && (isPrivileged || this.authService.hasPermission('RFQ.APPROVE'));
  });

  readonly canCloseRfq = computed(() => {
    const isPrivileged = this.authService.isTenantAdmin() || this.authService.isPlatformAdmin();
    const r = this.rfq();
    return getRfqStatusName(r?.status) === 'Issued' && (isPrivileged || this.authService.hasPermission('RFQ.EDIT'));
  });

  readonly canCancelRfq = computed(() => {
    const isPrivileged = this.authService.isTenantAdmin() || this.authService.isPlatformAdmin();
    const r = this.rfq();
    const status = getRfqStatusName(r?.status);
    const isDraftOrIssued = status === 'Draft' || status === 'Issued';
    return isDraftOrIssued && (isPrivileged || this.authService.hasPermission('RFQ.EDIT'));
  });

  ngOnInit(): void {
    this.subs.add(this.route.queryParamMap.subscribe(params => {
      const step = this.toWorkflowStep(params.get('step'));
      this.activeStep.set(step || 'details');
      if (step === 'comparison' && this.rfq() && this.canCompare()) {
        this.loadComparisonMatrix();
      }
    }));

    const idParam = this.route.snapshot.paramMap.get('id');
    if (!idParam || idParam === 'new') {
      this.isNew.set(true);
      this.initDefaultNewForm();
      this.loadMasterData();
    } else {
      this.rfqId.set(idParam);
      this.loadRfq(idParam);
      this.loadMasterData();
    }
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  initDefaultNewForm(): void {
    const today = new Date();
    const defaultClosing = new Date(today.getTime() + 14 * 24 * 60 * 60 * 1000); // 14 days later
    const defaultDelivery = new Date(today.getTime() + 30 * 24 * 60 * 60 * 1000); // 30 days later

    this.formTitle.set('');
    this.formClosingDate.set(defaultClosing.toISOString().split('T')[0]);
    this.formDeliveryRequiredDate.set(defaultDelivery.toISOString().split('T')[0]);
    this.formDeliveryTerms.set('DDP - Delivered Duty Paid to Central Warehouse');
    this.formInstructions.set('All quotes must be submitted in base currency, inclusive of applicable delivery and tax details.');
  }

  loadMasterData(): void {
    forkJoin({
      products: this.masterDataService.getProducts(),
      suppliers: this.masterDataService.getSuppliers(),
      uoms: this.masterDataService.getUnitsOfMeasure()
    }).subscribe({
      next: ({ products, suppliers, uoms }) => {
        this.products.set(products || []);
        this.suppliers.set(suppliers || []);
        this.uoms.set(uoms || []);
      },
      error: err => {
        this.message.set({
          text: err?.error?.message || err?.message || 'Failed to load procurement master data. Check supplier access and try again.',
          severity: 'danger'
        });
      }
    });
  }

  readonly draftLineColumns = computed<MaterialTableColumn<DraftRfqLineItem>[]>(() => {
    const uomOptions: MaterialSelectOption[] = this.uoms().map(uom => ({
      value: uom.unitOfMeasureId,
      label: `${uom.code} (${uom.name})`
    }));

    const productLookupConfig: MaterialLookupConfig<ProductItem> = {
      title: 'Select RFQ product',
      searchPlaceholder: 'Search by product code or name',
      displayField: 'productCode',
      valueField: 'productId',
      items: this.products(),
      columns: [
        { field: 'productCode', header: 'Code', width: '130px' },
        { field: 'productName', header: 'Product Name', width: '220px' },
        { field: 'categoryName', header: 'Category', width: '140px' },
        { field: 'unitOfMeasure', header: 'UOM', width: '90px' }
      ],
      onSelect: (product, row) => {
        const unitOfMeasureId = product.unitOfMeasureId || this.uoms().find(u => u.code.toLowerCase() === product.unitOfMeasure?.toLowerCase())?.unitOfMeasureId || row.unitOfMeasureId;
        const matchedUom = this.uoms().find(u => u.unitOfMeasureId === unitOfMeasureId);

        row.productId = product.productId;
        row.productCode = product.productCode;
        row.productName = product.productName;
        row.unitOfMeasureId = unitOfMeasureId || '';
        row.unitOfMeasureCode = matchedUom?.code || product.unitOfMeasure || row.unitOfMeasureCode || 'EA';
      }
    };

    return [
      {
        field: 'productCode',
        header: 'Product',
        type: 'lookup',
        required: true,
        lookupConfig: productLookupConfig,
        width: '230px',
        placeholder: 'Select product',
        format: (_value, row) => row.productName ? `${row.productCode} — ${row.productName}` : row.productCode || ''
      },
      {
        field: 'quantity',
        header: 'Quantity',
        type: 'number',
        required: true,
        min: 0.01,
        width: '110px',
        align: 'right',
        placeholder: '1'
      },
      {
        field: 'unitOfMeasureId',
        header: 'UOM',
        type: 'select',
        required: true,
        options: uomOptions,
        width: '120px',
        placeholder: 'Select UOM',
        format: (_value, row) => row.unitOfMeasureCode || ''
      },
      {
        field: 'targetUnitPrice',
        header: 'Target Price',
        type: 'currency',
        min: 0,
        width: '130px',
        align: 'right',
        placeholder: '0.00',
        format: (value) => value !== null && value !== undefined && value !== '' ? Number(value).toFixed(2) : ''
      },
      {
        field: 'requiredDate',
        header: 'Required Date',
        type: 'date',
        width: '150px',
        placeholder: 'Select date'
      },
      {
        field: 'technicalSpecifications',
        header: 'Technical Specs',
        type: 'text',
        width: '220px',
        placeholder: 'Specifications'
      },
      {
        field: 'notes',
        header: 'Notes',
        type: 'text',
        width: '190px',
        placeholder: 'Notes'
      },
      {
        field: 'actions',
        header: 'Actions',
        type: 'actions',
        width: '80px',
        align: 'center'
      }
    ];
  });

  readonly createDraftLineRow = (): DraftRfqLineItem => {
    return {
      lineNumber: this.newRfqLines().length + 1,
      productId: '',
      productCode: '',
      productName: '',
      quantity: 1,
      unitOfMeasureId: '',
      unitOfMeasureCode: '',
      targetUnitPrice: null,
      requiredDate: this.formDeliveryRequiredDate() || '',
      technicalSpecifications: '',
      notes: ''
    };
  };

  onDraftLinesChanged(rows: DraftRfqLineItem[]): void {
    const normalized = rows.map((row, index) => ({
      ...row,
      lineNumber: index + 1,
      productCode: row.productCode || '',
      productName: row.productName || '',
      quantity: Number(row.quantity) > 0 ? Number(row.quantity) : 1,
      unitOfMeasureId: row.unitOfMeasureId || '',
      unitOfMeasureCode: row.unitOfMeasureCode || 'EA',
      targetUnitPrice: row.targetUnitPrice ?? null,
      requiredDate: row.requiredDate || '',
      technicalSpecifications: row.technicalSpecifications || '',
      notes: row.notes || ''
    }));

    this.newRfqLines.set(normalized);
  }

  onDraftLineDeleted(row: DraftRfqLineItem): void {
    this.newRfqLines.update(lines => lines.filter(line => line !== row).map((line, index) => ({ ...line, lineNumber: index + 1 })));
  }

  getCurrentScope(): { companyId: string; branchId: string } | null {
    const company = this.scopeService.selectedCompany();
    if (!company) return null;
    const branch = this.scopeService.selectedBranch();
    const branchId = branch?.id || this.scopeService.branches()[0]?.id;
    if (!branchId) return null;
    return { companyId: company.id, branchId };
  }

  loadRfq(id: string): void {
    const scope = this.getCurrentScope();
    if (!scope) return;

    this.loading.set(true);
    this.error.set(null);

    this.rfqService.getRfqById(scope.companyId, scope.branchId, id).subscribe({
      next: data => {
        this.rfq.set(data);
        this.loading.set(false);

        const openQuotationId = this.expandedQuotationId();
        if (openQuotationId && data) {
          const refreshed = data.quotations.find(quote => quote.id === openQuotationId) || null;
          this.quotationDetail.set(refreshed);
          if (!refreshed) {
            this.expandedQuotationId.set(null);
          }
        }

        if (data) {
          this.selectedInvitationSupplierIds.set(
            data.invitedSuppliers
              .filter(supplier => this.isInvitationEligible(supplier))
              .map(supplier => supplier.supplierId)
          );
          this.formTitle.set(data.title || '');
          this.formClosingDate.set(data.closingDate ? data.closingDate.split('T')[0] : '');
          this.formDeliveryRequiredDate.set(data.deliveryRequiredDate ? data.deliveryRequiredDate.split('T')[0] : '');
          this.formDeliveryTerms.set(data.deliveryTerms || '');
          this.formInstructions.set(data.instructions || '');

          if (this.activeStep() === 'comparison' && this.canCompare()) {
            this.loadComparisonMatrix();
          }
        }
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err?.error?.message || err?.message || 'Failed to load RFQ details.');
      }
    });
  }

  switchStep(step: RfqWorkflowStep): void {
    if (step !== 'quotations' && step !== 'comparison') {
      this.expandedQuotationId.set(null);
      this.quotationDetail.set(null);
      this.quotationDetailLoading.set(false);
    }
    this.activeStep.set(step);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { step },
      queryParamsHandling: 'merge'
    });
  }

  private toWorkflowStep(value: string | null): RfqWorkflowStep | null {
    switch (value) {
      case 'details':
      case 'suppliers':
      case 'invitations':
      case 'quotations':
      case 'comparison':
        return value;
      default:
        return null;
    }
  }

  // --- RFQ Lifecycle Operations ---

  saveRfq(): void {
    const scope = this.getCurrentScope();
    if (!scope) return;

    if (!this.formTitle().trim()) {
      this.message.set({ text: 'Title is required.', severity: 'warn' });
      return;
    }

    if (!this.formClosingDate()) {
      this.message.set({ text: 'Closing date is required.', severity: 'warn' });
      return;
    }

    this.actionLoading.set(true);
    this.error.set(null);

    if (this.isNew()) {
      if (this.newRfqLines().length === 0) {
        this.message.set({ text: 'At least one requested product item is required before saving the RFQ.', severity: 'warn' });
        this.actionLoading.set(false);
        return;
      }

      const request: CreateRfqRequest = {
        title: this.formTitle().trim(),
        closingDate: new Date(this.formClosingDate()).toISOString(),
        deliveryRequiredDate: this.formDeliveryRequiredDate() ? new Date(this.formDeliveryRequiredDate()).toISOString() : undefined,
        deliveryTerms: this.formDeliveryTerms().trim(),
        instructions: this.formInstructions().trim(),
        lines: this.newRfqLines().map(l => ({
          productId: l.productId,
          quantity: l.quantity,
          unitOfMeasureId: l.unitOfMeasureId,
          targetUnitPrice: l.targetUnitPrice || undefined,
          requiredDate: l.requiredDate ? new Date(l.requiredDate).toISOString() : undefined,
          technicalSpecifications: l.technicalSpecifications || undefined,
          notes: l.notes || undefined
        }))
      };

      this.rfqService.createRfq(scope.companyId, scope.branchId, request).subscribe({
        next: created => {
          this.actionLoading.set(false);
          this.message.set({ text: `RFQ ${created.rfqNumber} successfully created with ${created.lines.length} requested item(s).`, severity: 'success' });
          this.isNew.set(false);
          this.rfqId.set(created.id);
          this.rfq.set(created);
          this.newRfqLines.set([]);
          this.router.navigate(['/app/procurement/rfqs', created.id]);
        },
        error: err => {
          this.actionLoading.set(false);
          this.error.set(err?.error?.message || err?.message || 'Failed to create RFQ.');
        }
      });
    } else {
      const id = this.rfqId();
      if (!id) return;

      const request: UpdateRfqRequest = {
        title: this.formTitle().trim(),
        closingDate: new Date(this.formClosingDate()).toISOString(),
        deliveryRequiredDate: this.formDeliveryRequiredDate() ? new Date(this.formDeliveryRequiredDate()).toISOString() : undefined,
        deliveryTerms: this.formDeliveryTerms().trim(),
        instructions: this.formInstructions().trim()
      };

      this.rfqService.updateRfq(scope.companyId, scope.branchId, id, request).subscribe({
        next: updated => {
          this.actionLoading.set(false);
          this.rfq.set(updated);
          this.message.set({ text: 'RFQ details updated successfully.', severity: 'success' });
        },
        error: err => {
          this.actionLoading.set(false);
          this.error.set(err?.error?.message || err?.message || 'Failed to update RFQ.');
        }
      });
    }
  }

  // --- Line Items Operations ---

  resetNewLineForm(): void {
    this.newLineProductId.set('');
    this.newLineQuantity.set(1);
    this.newLineUomId.set('');
    this.newLineTargetPrice.set(null);
    this.newLineRequiredDate.set('');
    this.newLineSpecs.set('');
    this.newLineNotes.set('');
    this.editingDraftLineIndex.set(null);
  }

  onProductSelected(productId: string): void {
    this.newLineProductId.set(productId);
    const prod = this.products().find(p => p.productId === productId);
    if (prod && (prod as any).unitOfMeasureId) {
      this.newLineUomId.set((prod as any).unitOfMeasureId);
    } else if (prod && (prod as any).unitOfMeasure) {
      const uom = this.uoms().find(u => u.code.toLowerCase() === (prod as any).unitOfMeasure.toLowerCase());
      if (uom) {
        this.newLineUomId.set(uom.unitOfMeasureId);
      }
    }
  }

  addOrUpdateDraftLine(): void {
    if (!this.newLineProductId()) {
      this.message.set({ text: 'Please select a product for the line item.', severity: 'warn' });
      return;
    }

    if (this.newLineQuantity() <= 0) {
      this.message.set({ text: 'Quantity must be greater than zero.', severity: 'warn' });
      return;
    }

    if (!this.newLineUomId()) {
      this.message.set({ text: 'Unit of Measure is required.', severity: 'warn' });
      return;
    }

    const prod = this.products().find(p => p.productId === this.newLineProductId());
    const uom = this.uoms().find(u => u.unitOfMeasureId === this.newLineUomId());
    const editIdx = this.editingDraftLineIndex();

    if (editIdx !== null && editIdx >= 0) {
      // Update existing draft line in client collection
      this.newRfqLines.update(lines => {
        const copy = [...lines];
        copy[editIdx] = {
          ...copy[editIdx],
          productId: this.newLineProductId(),
          productCode: prod?.productCode || copy[editIdx].productCode,
          productName: prod?.productName || copy[editIdx].productName,
          quantity: this.newLineQuantity(),
          unitOfMeasureId: this.newLineUomId(),
          unitOfMeasureCode: uom?.code || (prod as any)?.unitOfMeasure || 'EA',
          targetUnitPrice: this.newLineTargetPrice() || null,
          requiredDate: this.newLineRequiredDate() || this.formDeliveryRequiredDate() || null,
          technicalSpecifications: this.newLineSpecs().trim() || null,
          notes: this.newLineNotes().trim() || null
        };
        return copy;
      });
      this.message.set({ text: 'Line item updated.', severity: 'info' });
    } else {
      // Add new draft line to client collection
      const nextNum = this.newRfqLines().length + 1;
      const newLine: DraftRfqLineItem = {
        lineNumber: nextNum,
        productId: this.newLineProductId(),
        productCode: prod?.productCode || '',
        productName: prod?.productName || '',
        quantity: this.newLineQuantity(),
        unitOfMeasureId: this.newLineUomId(),
        unitOfMeasureCode: uom?.code || (prod as any)?.unitOfMeasure || 'EA',
        targetUnitPrice: this.newLineTargetPrice() || null,
        requiredDate: this.newLineRequiredDate() || this.formDeliveryRequiredDate() || null,
        technicalSpecifications: this.newLineSpecs().trim() || null,
        notes: this.newLineNotes().trim() || null
      };
      this.newRfqLines.update(lines => [...lines, newLine]);
      this.message.set({ text: 'Line item added.', severity: 'success' });
    }

    this.resetNewLineForm();
  }

  editDraftLine(index: number): void {
    const lines = this.newRfqLines();
    if (index < 0 || index >= lines.length) return;
    const item = lines[index];
    this.newLineProductId.set(item.productId);
    this.newLineQuantity.set(item.quantity);
    this.newLineUomId.set(item.unitOfMeasureId);
    this.newLineTargetPrice.set(item.targetUnitPrice ?? null);
    this.newLineRequiredDate.set(item.requiredDate ? item.requiredDate.split('T')[0] : '');
    this.newLineSpecs.set(item.technicalSpecifications || '');
    this.newLineNotes.set(item.notes || '');
    this.editingDraftLineIndex.set(index);
  }

  cancelEditDraftLine(): void {
    this.resetNewLineForm();
  }

  removeDraftLine(index: number): void {
    this.newRfqLines.update(lines => {
      const filtered = lines.filter((_, idx) => idx !== index);
      return filtered.map((line, idx) => ({ ...line, lineNumber: idx + 1 }));
    });
    if (this.editingDraftLineIndex() === index) {
      this.resetNewLineForm();
    }
    this.message.set({ text: 'Line item removed.', severity: 'info' });
  }

  addLineItem(): void {
    const scope = this.getCurrentScope();
    const id = this.rfqId();
    if (!scope || !id) return;

    if (!this.newLineProductId()) {
      this.message.set({ text: 'Please select a product for the line item.', severity: 'warn' });
      return;
    }

    if (this.newLineQuantity() <= 0) {
      this.message.set({ text: 'Quantity must be greater than zero.', severity: 'warn' });
      return;
    }

    if (!this.newLineUomId()) {
      this.message.set({ text: 'Unit of Measure is required.', severity: 'warn' });
      return;
    }

    this.actionLoading.set(true);
    const request: CreateRfqLineRequest = {
      productId: this.newLineProductId(),
      quantity: this.newLineQuantity(),
      unitOfMeasureId: this.newLineUomId(),
      targetUnitPrice: this.newLineTargetPrice() || undefined,
      requiredDate: this.newLineRequiredDate() ? new Date(this.newLineRequiredDate()).toISOString() : undefined,
      technicalSpecifications: this.newLineSpecs().trim() || undefined,
      notes: this.newLineNotes().trim() || undefined
    };

    this.rfqService.addLine(scope.companyId, scope.branchId, id, request).subscribe({
      next: updated => {
        this.actionLoading.set(false);
        this.rfq.set(updated);
        this.message.set({ text: 'Line item added successfully.', severity: 'success' });
        // Reset line form
        this.newLineProductId.set('');
        this.newLineQuantity.set(1);
        this.newLineUomId.set('');
        this.newLineTargetPrice.set(null);
        this.newLineRequiredDate.set('');
        this.newLineSpecs.set('');
        this.newLineNotes.set('');
      },
      error: err => {
        this.actionLoading.set(false);
        this.message.set({ text: err?.error?.message || err?.message || 'Failed to add line item.', severity: 'danger' });
      }
    });
  }

  removeLineItem(lineId: string): void {
    const scope = this.getCurrentScope();
    const id = this.rfqId();
    if (!scope || !id) return;

    this.actionLoading.set(true);
    this.rfqService.removeLine(scope.companyId, scope.branchId, id, lineId).subscribe({
      next: updated => {
        this.actionLoading.set(false);
        this.rfq.set(updated);
        this.message.set({ text: 'Line item removed.', severity: 'info' });
      },
      error: err => {
        this.actionLoading.set(false);
        this.message.set({ text: err?.error?.message || err?.message || 'Failed to remove line item.', severity: 'danger' });
      }
    });
  }

  // --- Supplier Invitation Operations ---

  stageSupplier(supplierId: string): void {
    const supplier = this.suppliers().find(item => item.supplierId === supplierId);
    if (!supplier?.isActive || this.rfq()?.invitedSuppliers.some(item => item.supplierId === supplierId)) {
      return;
    }

    this.stagedSupplierIds.update(ids => ids.includes(supplierId) ? ids : [...ids, supplierId]);
  }

  removeStagedSupplier(supplierId: string): void {
    this.stagedSupplierIds.update(ids => ids.filter(id => id !== supplierId));
  }

  supplierLocation(supplier: SupplierItem): string {
    return [supplier.city, supplier.country].filter((value): value is string => !!value).join(', ') || '—';
  }

  cancelSupplierSelection(): void {
    this.stagedSupplierIds.set([]);
    this.supplierSearchTerm.set('');
  }

  saveSupplierSelection(): void {
    const scope = this.getCurrentScope();
    const id = this.rfqId();
    const supplierIds = this.stagedSupplierIds();
    if (!scope || !id || !this.canManageSuppliers()) return;

    if (supplierIds.length === 0) {
      this.message.set({ text: 'Select at least one supplier before saving.', severity: 'warn' });
      return;
    }

    this.actionLoading.set(true);
    const savedNames: string[] = [];
    const failures: string[] = [];

    const saveNext = (index: number): void => {
      if (index >= supplierIds.length) {
        this.actionLoading.set(false);
        const summary = savedNames.length
          ? `${savedNames.length} supplier(s) saved.`
          : 'No suppliers were saved.';
        const failureSummary = failures.length ? ` ${failures.join(' ')}` : '';
        const note = savedNames.length ? ' Saving suppliers selects them for this RFQ; send invitations in Stage 3.' : '';
        this.message.set({
          text: `${summary}${failureSummary}${note}`,
          severity: failures.length ? (savedNames.length ? 'warn' : 'danger') : 'success'
        });
        return;
      }

      const supplierId = supplierIds[index];
      const supplier = this.suppliers().find(item => item.supplierId === supplierId);
      const request: AddRfqSupplierRequest = { supplierId };
      this.rfqService.addSupplier(scope.companyId, scope.branchId, id, request).subscribe({
        next: updated => {
          this.rfq.set(updated);
          savedNames.push(supplier?.supplierName || supplierId);
          this.removeStagedSupplier(supplierId);
          saveNext(index + 1);
        },
        error: err => {
          const reason = err?.error?.message || err?.message || 'The server rejected this supplier.';
          failures.push(`${supplier?.supplierName || supplierId}: ${reason}`);
          saveNext(index + 1);
        }
      });
    };

    saveNext(0);
  }

  removeSupplier(supplierId: string): void {
    const scope = this.getCurrentScope();
    const id = this.rfqId();
    if (!scope || !id || !this.canManageSuppliers() || getRfqStatusName(this.rfq()?.status) !== 'Draft') return;

    this.actionLoading.set(true);
    this.rfqService.removeSupplier(scope.companyId, scope.branchId, id, supplierId).subscribe({
      next: updated => {
        this.actionLoading.set(false);
        this.rfq.set(updated);
        this.message.set({ text: 'Supplier removed from invitation list.', severity: 'info' });
      },
      error: err => {
        this.actionLoading.set(false);
        this.message.set({ text: err?.error?.message || err?.message || 'Failed to remove supplier.', severity: 'danger' });
      }
    });
  }

  isInvitationEligible(supplier: RfqSupplierDto): boolean {
    const rfqStatus = getRfqStatusName(this.rfq()?.status);
    if (rfqStatus === 'Awarded') {
      const isWinner = supplier.supplierId === this.winningSupplierId();
      if (!isWinner) return false;
      return supplier.awardNotificationStatus === 'Failed' ||
        (!supplier.awardNotificationStatus && !!supplier.contactEmail);
    }
    const invStatus = getSupplierInvitationStatusName(supplier.invitationStatus);
    return invStatus === 'Pending' || invStatus === 'Failed';
  }

  isInvitationSelected(supplierId: string): boolean {
    return this.selectedInvitationSupplierIds().includes(supplierId);
  }

  supplierContactName(supplierId: string): string {
    return this.suppliers().find(supplier => supplier.supplierId === supplierId)?.contactName?.trim() || '—';
  }

  toggleInvitationSelection(supplierId: string): void {
    const supplier = this.rfq()?.invitedSuppliers.find(item => item.supplierId === supplierId);
    if (!supplier || !this.isInvitationEligible(supplier)) return;

    this.selectedInvitationSupplierIds.update(ids =>
      ids.includes(supplierId)
        ? ids.filter(id => id !== supplierId)
        : [...ids, supplierId]
    );
  }

  selectEligibleInvitations(): void {
    this.selectedInvitationSupplierIds.set(
      this.eligibleInvitationSuppliers().map(supplier => supplier.supplierId)
    );
  }

  sendSingleInvitation(supplierId: string): void {
    const supplier = this.rfq()?.invitedSuppliers.find(item => item.supplierId === supplierId);
    if (!supplier || !this.canSendInvitations() || !this.isInvitationEligible(supplier)) return;

    this.selectedInvitationSupplierIds.set([supplierId]);
    this.sendInvitations();
  }

  sendInvitations(): void {
    const scope = this.getCurrentScope();
    const id = this.rfqId();
    const currentRfq = this.rfq();
    if (!scope || !id || !currentRfq) return;

    if (currentRfq.lines.length === 0) {
      this.message.set({ text: 'Cannot send invitations: please add at least one product line item first.', severity: 'warn' });
      return;
    }

    const selectedSuppliers = currentRfq.invitedSuppliers.filter(supplier =>
      this.selectedInvitationSupplierIds().includes(supplier.supplierId) &&
      this.isInvitationEligible(supplier)
    );
    if (selectedSuppliers.length === 0) {
      this.message.set({ text: 'Select at least one pending or failed invitation to send or retry.', severity: 'warn' });
      return;
    }

    const recipientSummary = selectedSuppliers
      .map(supplier => `${supplier.supplierName} (${supplier.contactEmail || 'no contact email'})`)
      .join('; ');

    const rfqStatus = getRfqStatusName(currentRfq.status);
    if (rfqStatus === 'Awarded') {
      const winner = selectedSuppliers.find(s => s.supplierId === this.winningSupplierId());
      if (!winner) {
        this.message.set({ text: 'Only the winning supplier can receive the award notification.', severity: 'warn' });
        return;
      }
      const failureReasonText = winner.awardNotificationFailureReason
        ? ` Previous failure: "${winner.awardNotificationFailureReason}".`
        : '';
      const isRetry = winner.awardNotificationStatus === 'Failed';

      this.confirmDialogConfig.set({
        action: 'send-award-notification',
        title: isRetry ? 'Retry RFQ Award Notification' : 'Send RFQ Award Notification',
        message: `Send award notification email to winning supplier: ${winner.supplierName} (${winner.contactEmail || 'no contact email'}).${failureReasonText} Award notification confirms quotation selection without disclosing competing quotations or prices. Purchase Order details will be communicated separately.`,
        confirmLabel: isRetry ? 'Retry Award Notification' : 'Send Award Notification',
        confirmSeverity: 'primary',
        cancelLabel: 'Cancel',
        confirmIcon: isRetry ? 'pi pi-refresh' : 'pi pi-send'
      });
      this.confirmAction.set(() => {
        this.actionLoading.set(true);
        const request: SendRfqInvitationsRequest = {
          supplierIds: [winner.supplierId],
          portalBaseUrl: window.location.origin
        };

        this.rfqService.sendInvitations(scope.companyId, scope.branchId, id, request).subscribe({
          next: resp => {
            this.actionLoading.set(false);
            this.confirmDialogVisible.set(false);
            const summary = resp.sentCount > 0
              ? `Award notification email accepted by SMTP for ${winner.supplierName}.`
              : `Award notification failed: ${resp.messages.join(' ')}`;
            this.message.set({
              text: summary,
              severity: resp.sentCount > 0 ? 'success' : 'danger'
            });
            this.loadRfq(id);
          },
          error: err => {
            this.actionLoading.set(false);
            this.confirmDialogVisible.set(false);
            this.message.set({
              text: err?.error?.message || err?.message || 'Failed to send award notification.',
              severity: 'danger'
            });
            this.loadRfq(id);
          }
        });
      });
      this.confirmDialogVisible.set(true);
      return;
    }

    this.confirmDialogConfig.set({
      action: 'send-invitations',
      title: 'Send RFQ Invitations',
      message: `Send an invitation email to: ${recipientSummary}. Only pending or failed invitations are eligible. Successful invitations will not be resent. Sent means the SMTP transport accepted the message; inbox delivery is not confirmed.`,
      confirmLabel: 'Send Invitations',
      confirmSeverity: 'primary',
      cancelLabel: 'Cancel',
      confirmIcon: 'pi pi-send'
    });
    this.confirmAction.set(() => {
      this.actionLoading.set(true);
      const candidateSubdomain =
        this.tenantHostService?.currentSubdomain() ||
        this.tenantContextService?.subdomain() ||
        this.tenantContextService?.currentTenant()?.subdomain ||
        undefined;

      const request: SendRfqInvitationsRequest = {
        supplierIds: selectedSuppliers.map(supplier => supplier.supplierId),
        portalBaseUrl: window.location.origin,
        tenantSubdomain: candidateSubdomain
      };

      this.rfqService.sendInvitations(scope.companyId, scope.branchId, id, request).subscribe({
        next: resp => {
          this.actionLoading.set(false);
          this.confirmDialogVisible.set(false);
          const summary = `${resp.sentCount} accepted by SMTP; ${resp.failedCount} failed.`;
          this.message.set({
            text: [summary, ...resp.messages].join(' '),
            severity: resp.failedCount > 0
              ? (resp.sentCount > 0 ? 'warn' : 'danger')
              : (resp.sentCount > 0 ? 'success' : 'info')
          });
          if (resp.accessOptions || resp.standardLocalhostUrl || resp.lanIpUrl) {
            this.latestAccessOptions.set({
              standardLocalhostUrl: resp.accessOptions?.standardLocalhostUrl || resp.standardLocalhostUrl,
              tenantLocalhostUrl: resp.accessOptions?.tenantLocalhostUrl || resp.tenantLocalhostUrl,
              tenantLocalhostUnavailableReason: resp.accessOptions?.tenantLocalhostUnavailableReason || resp.tenantLocalhostUnavailableReason,
              externalTenantUrl: resp.accessOptions?.externalTenantUrl || resp.externalTenantUrl,
              externalTenantUnavailableReason: resp.accessOptions?.externalTenantUnavailableReason || resp.externalTenantUnavailableReason,
              lanIpUrl: resp.accessOptions?.lanIpUrl || resp.lanIpUrl,
              lanIpUnavailableReason: resp.accessOptions?.lanIpUnavailableReason || resp.lanIpUnavailableReason
            });
          }
          this.loadRfq(id);
        },
        error: err => {
          this.actionLoading.set(false);
          this.confirmDialogVisible.set(false);
          this.message.set({
            text: err?.error?.message || err?.message || 'Failed to send invitations.',
            severity: 'danger'
          });
          this.loadRfq(id);
        }
      });
    });
    this.confirmDialogVisible.set(true);
  }

  retryAwardNotificationFromQuote(quote: SupplierQuotationDto): void {
    if (!quote || quote.status !== 'Approved') return;
    this.sendSingleInvitation(quote.supplierId);
  }

  openPortalAccessModal(supplier?: RfqSupplierDto): void {
    if (supplier) {
      this.selectedSupplierForAccess.set(supplier);
    } else {
      this.selectedSupplierForAccess.set(null);
    }
    this.portalAccessModalVisible.set(true);
  }

  closePortalAccessModal(): void {
    this.portalAccessModalVisible.set(false);
    this.selectedSupplierForAccess.set(null);
  }

  // --- Manual Quotation Entry ---

  openManualQuoteModal(): void {
    const currentRfq = this.rfq();
    if (!currentRfq) return;

    this.manualSupplierId.set('');
    this.manualReference.set('');
    const today = new Date();
    this.manualDate.set(today.toISOString().split('T')[0]);
    this.manualValidUntil.set(currentRfq.closingDate ? currentRfq.closingDate.split('T')[0] : '');
    this.manualNotes.set('');

    const lines = currentRfq.lines.map(l => ({
      rfqLineId: l.id,
      productId: l.productId,
      unitOfMeasureId: l.unitOfMeasureId,
      productName: `${l.productCode} - ${l.productName}`,
      quantity: l.quantity,
      uomCode: l.unitOfMeasureCode,
      unitPrice: l.targetUnitPrice || 0,
      discountPct: 0,
      taxRate: 0,
      leadTimeDays: 7,
      technicalRemarks: ''
    }));
    this.manualLines.set(lines);
    this.manualQuoteModalVisible.set(true);
  }

  closeManualQuoteModal(): void {
    this.manualQuoteModalVisible.set(false);
  }

  submitManualQuotation(): void {
    const scope = this.getCurrentScope();
    const id = this.rfqId();
    if (!scope || !id) return;

    if (!this.manualSupplierId()) {
      this.message.set({ text: 'Please select the quotation supplier.', severity: 'warn' });
      return;
    }

    if (!this.manualReference().trim()) {
      this.message.set({ text: 'Supplier quotation reference is required.', severity: 'warn' });
      return;
    }

    if (!this.manualValidUntil()) {
      this.message.set({ text: 'Validity date is required.', severity: 'warn' });
      return;
    }

    const invalidLine = this.manualLines().find(line =>
      !(line.quantity > 0) ||
      !(line.unitPrice > 0) ||
      line.discountPct < 0 || line.discountPct > 100 ||
      line.taxRate < 0 || line.taxRate > 100 ||
      line.leadTimeDays < 0 || line.leadTimeDays > 365
    );

    if (invalidLine) {
      this.message.set({
        text: `Enter valid quantities, unit prices, discount (0-100), tax (0-100) and lead time (0-365) for ${invalidLine.productName}.`,
        severity: 'warn'
      });
      return;
    }

    this.actionLoading.set(true);
    const request: RecordManualQuotationRequest = {
      supplierId: this.manualSupplierId(),
      quotationReference: this.manualReference().trim(),
      quotationDate: new Date(this.manualDate()).toISOString(),
      validUntil: new Date(this.manualValidUntil()).toISOString(),
      currencyId: (this.rfq() as any)?.currencyId || '00000000-0000-0000-0000-000000000000',
      notes: this.manualNotes().trim() || undefined,
      lines: this.manualLines().map(l => ({
        rfqLineId: l.rfqLineId,
        productId: l.productId,
        quotedQuantity: l.quantity,
        unitOfMeasureId: l.unitOfMeasureId,
        unitPrice: l.unitPrice,
        discountPercentage: l.discountPct,
        taxRate: l.taxRate,
        leadTimeDays: l.leadTimeDays,
        technicalRemarks: l.technicalRemarks.trim() || undefined
      }))
    };

    this.rfqService.recordManualQuotation(scope.companyId, scope.branchId, id, request).subscribe({
      next: quote => {
        this.actionLoading.set(false);
        this.manualQuoteModalVisible.set(false);
        this.message.set({
          text: `Manual quotation ${quote.quotationReference} successfully recorded.`,
          severity: 'success'
        });
        this.loadRfq(id);
      },
      error: err => {
        this.actionLoading.set(false);
        this.message.set({
          text: err?.error?.message || err?.message || 'Failed to record manual quotation.',
          severity: 'danger'
        });
      }
    });
  }

  quotationForSupplier(supplierId: string): SupplierQuotationDto | null {
    const quotes = (this.rfq()?.quotations || []).filter(q => q.supplierId === supplierId);
    if (quotes.length === 0) return null;
    return [...quotes].sort(
      (a, b) => new Date(b.quotationDate).getTime() - new Date(a.quotationDate).getTime()
    )[0];
  }

  supplierQuotationLabel(supplierId: string): string {
    const quotation = this.quotationForSupplier(supplierId);
    if (quotation) return quotation.status as string;
    return 'Awaiting Quotation';
  }

  supplierQuotationBadgeClass(supplierId: string): string {
    const quotation = this.quotationForSupplier(supplierId);
    if (!quotation) return 'badge-light';
    switch (quotation.status) {
      case 'Approved': return 'badge-success';
      case 'Rejected': return 'badge-danger';
      case 'Draft': return 'badge-warning';
      default: return 'badge-info';
    }
  }

  quotationMaxLeadTime(quotation: SupplierQuotationDto): number | null {
    const values = (quotation.lines || [])
      .map(line => line.leadTimeDays)
      .filter((value): value is number => value !== null && value !== undefined);
    return values.length ? Math.max(...values) : null;
  }

  toggleQuotationDetail(quotation: SupplierQuotationDto): void {
    if (this.expandedQuotationId() === quotation.id) {
      this.expandedQuotationId.set(null);
      this.quotationDetail.set(null);
      this.quotationDetailLoading.set(false);
      return;
    }

    this.expandedQuotationId.set(quotation.id);
    this.quotationDetail.set(quotation);
    this.quotationDetailLoading.set(true);

    const scope = this.getCurrentScope();
    const id = this.rfqId();
    if (!scope || !id) {
      this.quotationDetailLoading.set(false);
      return;
    }

    this.rfqService.getQuotation(scope.companyId, scope.branchId, id, quotation.id).subscribe({
      next: detail => {
        if (this.expandedQuotationId() === detail.id) {
          this.quotationDetail.set(detail);
        }
        this.quotationDetailLoading.set(false);
      },
      error: () => {
        // Fall back to the quotation already returned with the RFQ details.
        this.quotationDetailLoading.set(false);
      }
    });
  }

  // --- Comparison Matrix & Quotation Approval ---

  loadComparisonMatrix(): void {
    const scope = this.getCurrentScope();
    const id = this.rfqId();
    if (!scope || !id || !this.canCompare()) return;

    this.matrixLoading.set(true);
    this.rfqService.getComparisonMatrix(scope.companyId, scope.branchId, id).subscribe({
      next: matrix => {
        this.comparisonMatrix.set(matrix);
        this.matrixLoading.set(false);
      },
      error: err => {
        this.matrixLoading.set(false);
        this.error.set(err?.error?.message || err?.message || 'Failed to load comparison matrix.');
      }
    });
  }

  getLowestOverallQuotationId(): string | null {
    const m = this.comparisonMatrix();
    if (!m || m.suppliers.length === 0) return null;
    let lowestId: string | null = null;
    let lowestAmt = Number.MAX_VALUE;
    for (const s of m.suppliers) {
      if (s.hasValidExchangeRate && s.baseTotalAmount > 0 && s.baseTotalAmount < lowestAmt) {
        lowestAmt = s.baseTotalAmount;
        lowestId = s.quotationId;
      }
    }
    return lowestId;
  }

  isLowestPriceForLine(row: RfqComparisonLineDto | any, quotationId: string): boolean {
    if (!row || !row.bids || row.bids.length === 0) return false;
    const currentBid = row.bids.find((b: any) => b.quotationId === quotationId);
    if (!currentBid || !(currentBid.unitPrice > 0)) return false;

    const supplier = this.comparisonMatrix()?.suppliers.find(s => s.quotationId === quotationId);
    if (supplier && !supplier.hasValidExchangeRate) return false;

    const validBids = row.bids.filter((b: any) => {
      if (!(b.unitPrice > 0)) return false;
      const s = this.comparisonMatrix()?.suppliers.find(sup => sup.quotationId === b.quotationId);
      return !s || s.hasValidExchangeRate !== false;
    });

    if (validBids.length === 0) return false;
    const minBasePrice = Math.min(...validBids.map((b: any) => b.baseUnitPrice));
    return Math.abs(currentBid.baseUnitPrice - minBasePrice) < 0.0001;
  }

  isTiedLowestPriceForLine(row: RfqComparisonLineDto | any, quotationId: string): boolean {
    if (!this.isLowestPriceForLine(row, quotationId)) return false;
    const currentBid = row.bids.find((b: any) => b.quotationId === quotationId);
    if (!currentBid) return false;

    const matchingBids = row.bids.filter((b: any) =>
      b.unitPrice > 0 && Math.abs(b.baseUnitPrice - currentBid.baseUnitPrice) < 0.0001
    );
    return matchingBids.length > 1;
  }

  setEvaluationSearch(term: string): void {
    this.evaluationFilterState.update(s => ({ ...s, searchTerm: term }));
  }

  setEvaluationStatus(status: string): void {
    this.evaluationFilterState.update(s => ({ ...s, statusFilter: status }));
  }

  setEvaluationExpiry(expiry: 'ALL' | 'ACTIVE' | 'EXPIRED'): void {
    this.evaluationFilterState.update(s => ({ ...s, expiryFilter: expiry }));
  }

  setEvaluationMaxLeadTime(leadTime: number | null): void {
    this.evaluationFilterState.update(s => ({ ...s, maxLeadTimeDays: leadTime }));
  }

  setEvaluationMinBaseAmount(amt: number | null): void {
    this.evaluationFilterState.update(s => ({ ...s, minBaseAmount: amt }));
  }

  setEvaluationMaxBaseAmount(amt: number | null): void {
    this.evaluationFilterState.update(s => ({ ...s, maxBaseAmount: amt }));
  }

  setEvaluationCurrency(currency: string): void {
    this.evaluationFilterState.update(s => ({ ...s, currencyFilter: currency }));
  }

  setEvaluationSort(sort: RfqEvaluationSortOption): void {
    this.evaluationFilterState.update(s => ({ ...s, sortBy: sort }));
  }

  setEvaluationViewMode(mode: 'grid' | 'list'): void {
    this.evaluationFilterState.update(s => ({ ...s, viewMode: mode }));
  }

  setEvaluationTab(tab: 'cards' | 'commercial' | 'matrix'): void {
    this.evaluationActiveTab.set(tab);
  }

  resetEvaluationFilters(): void {
    this.evaluationFilterState.set({
      searchTerm: '',
      statusFilter: 'ALL',
      expiryFilter: 'ALL',
      maxLeadTimeDays: null,
      minBaseAmount: null,
      maxBaseAmount: null,
      currencyFilter: 'ALL',
      sortBy: 'PRICE_ASC',
      viewMode: this.evaluationFilterState().viewMode
    });
  }

  hasActiveEvaluationFilters(): boolean {
    const f = this.evaluationFilterState();
    return !!(
      (f.searchTerm && f.searchTerm.trim()) ||
      f.statusFilter !== 'ALL' ||
      f.expiryFilter !== 'ALL' ||
      f.maxLeadTimeDays !== null ||
      f.minBaseAmount !== null ||
      f.maxBaseAmount !== null ||
      f.currencyFilter !== 'ALL'
    );
  }

  getLeadTimeDisplay(days?: number | null): string {
    if (days === null || days === undefined) return 'Not Specified';
    return `${days} ${days === 1 ? 'day' : 'days'}`;
  }

  getLowestBidForLine(row: any): string | null {
    if (!row || !row.bids || row.bids.length === 0) return null;
    let lowestId: string | null = null;
    let lowestPrice = Number.MAX_VALUE;
    for (const b of row.bids) {
      if (b.unitPrice > 0 && b.unitPrice < lowestPrice) {
        lowestPrice = b.unitPrice;
        lowestId = b.quotationId;
      }
    }
    return lowestId;
  }

  getBidForSupplier(row: RfqComparisonLineDto | any, quotationId: string): RfqComparisonLineBidDto | undefined {
    if (!row || !row.bids) return undefined;
    return row.bids.find((b: any) => b.quotationId === quotationId);
  }

  inspectQuotationFromMatrix(quotationId: string): void {
    const existingQuote = (this.rfq()?.quotations || []).find(q => q.id === quotationId);
    if (existingQuote) {
      this.toggleQuotationDetail(existingQuote);
      return;
    }

    const scope = this.getCurrentScope();
    const id = this.rfqId();
    if (!scope || !id) return;

    if (this.expandedQuotationId() === quotationId) {
      this.expandedQuotationId.set(null);
      this.quotationDetail.set(null);
      this.quotationDetailLoading.set(false);
      return;
    }

    this.expandedQuotationId.set(quotationId);
    this.quotationDetailLoading.set(true);

    this.rfqService.getQuotation(scope.companyId, scope.branchId, id, quotationId).subscribe({
      next: detail => {
        this.quotationDetail.set(detail);
        this.quotationDetailLoading.set(false);
      },
      error: err => {
        this.quotationDetailLoading.set(false);
        this.message.set({
          text: err?.error?.message || err?.message || 'Failed to load quotation details.',
          severity: 'danger'
        });
      }
    });
  }

  openDecisionModal(quotation: SupplierQuotationDto, type: 'approve' | 'reject'): void {
    this.selectedQuotation.set(quotation);
    this.decisionType.set(type);
    this.decisionRemarks.set('');
    this.decisionAwardRfq.set(true);
    this.decisionModalVisible.set(true);
  }

  openMatrixDecisionModal(quote: any, type: 'approve' | 'reject'): void {
    const scope = this.getCurrentScope();
    const id = this.rfqId();
    const existingQuote = (this.rfq()?.quotations || []).find(q => q.id === quote.quotationId);

    if (existingQuote && existingQuote.lines && existingQuote.lines.length > 0) {
      this.openDecisionModal(existingQuote, type);
      return;
    }

    if (scope && id) {
      this.actionLoading.set(true);
      this.rfqService.getQuotation(scope.companyId, scope.branchId, id, quote.quotationId).subscribe({
        next: fullQuote => {
          this.actionLoading.set(false);
          this.openDecisionModal(fullQuote, type);
        },
        error: () => {
          this.actionLoading.set(false);
          const fallback: SupplierQuotationDto = {
            id: quote.quotationId,
            tenantId: '',
            companyId: '',
            branchId: '',
            rfqId: id,
            rfqNumber: this.rfq()?.rfqNumber || '',
            supplierId: quote.supplierId,
            supplierCode: quote.supplierCode,
            supplierName: quote.supplierName,
            quotationReference: quote.quotationReference,
            quotationDate: quote.quotationDate,
            validUntil: quote.validUntil,
            isExpired: quote.isExpired,
            currencyId: quote.currencyId || '',
            currencyCode: quote.currencyCode,
            exchangeRate: quote.exchangeRate || 1,
            status: quote.status,
            subTotal: quote.totalAmount,
            discountAmount: 0,
            taxAmount: 0,
            totalAmount: quote.totalAmount,
            baseTotalAmount: quote.baseTotalAmount,
            isManualEntry: quote.isManualEntry || false,
            lines: []
          };
          this.openDecisionModal(fallback, type);
        }
      });
    } else {
      const fallback: SupplierQuotationDto = {
        id: quote.quotationId,
        tenantId: '',
        companyId: '',
        branchId: '',
        rfqId: id || '',
        rfqNumber: this.rfq()?.rfqNumber || '',
        supplierId: quote.supplierId,
        supplierCode: quote.supplierCode,
        supplierName: quote.supplierName,
        quotationReference: quote.quotationReference,
        quotationDate: quote.quotationDate,
        validUntil: quote.validUntil,
        isExpired: quote.isExpired,
        currencyId: quote.currencyId || '',
        currencyCode: quote.currencyCode,
        exchangeRate: quote.exchangeRate || 1,
        status: quote.status,
        subTotal: quote.totalAmount,
        discountAmount: 0,
        taxAmount: 0,
        totalAmount: quote.totalAmount,
        baseTotalAmount: quote.baseTotalAmount,
        isManualEntry: quote.isManualEntry || false,
        lines: []
      };
      this.openDecisionModal(fallback, type);
    }
  }

  closeDecisionModal(): void {
    this.decisionModalVisible.set(false);
    this.selectedQuotation.set(null);
  }

  submitDecision(): void {
    const scope = this.getCurrentScope();
    const id = this.rfqId();
    const quote = this.selectedQuotation();
    if (!scope || !id || !quote) return;

    this.actionLoading.set(true);

    if (this.decisionType() === 'approve') {
      const request: ApproveQuotationRequest = {
        remarks: this.decisionRemarks().trim() || undefined
      };

      this.rfqService.approveQuotation(scope.companyId, scope.branchId, id, quote.id, request).subscribe({
        next: approvedQuote => {
          this.actionLoading.set(false);
          this.closeDecisionModal();
          let notificationMsg = '';
          if (approvedQuote?.awardNotificationStatus === 'Sent') {
            notificationMsg = ` Award notification email was sent to ${approvedQuote.supplierName}.`;
          } else if (approvedQuote?.awardNotificationStatus === 'Failed') {
            notificationMsg = ` Note: Award email delivery failed (${approvedQuote.awardNotificationFailureReason || 'SMTP error'}). You can retry sending from the Invitations tab.`;
          }
          this.message.set({
            text: `Quotation ${quote.quotationReference} from ${quote.supplierName} was approved. RFQ is now Awarded!${notificationMsg}`,
            severity: approvedQuote?.awardNotificationStatus === 'Failed' ? 'warn' : 'success'
          });
          this.loadRfq(id);
          this.loadComparisonMatrix();
        },
        error: err => {
          this.actionLoading.set(false);
          this.message.set({
            text: err?.error?.message || err?.message || 'Failed to approve quotation.',
            severity: 'danger'
          });
        }
      });
    } else {
      if (!this.decisionRemarks().trim()) {
        this.actionLoading.set(false);
        this.message.set({ text: 'Rejection reason is required.', severity: 'warn' });
        return;
      }

      const request: RejectQuotationRequest = {
        remarks: this.decisionRemarks().trim() || undefined
      };

      this.rfqService.rejectQuotation(scope.companyId, scope.branchId, id, quote.id, request).subscribe({
        next: () => {
          this.actionLoading.set(false);
          this.closeDecisionModal();
          this.message.set({
            text: `Quotation ${quote.quotationReference} was rejected.`,
            severity: 'info'
          });
          this.loadRfq(id);
          this.loadComparisonMatrix();
        },
        error: err => {
          this.actionLoading.set(false);
          this.message.set({
            text: err?.error?.message || err?.message || 'Failed to reject quotation.',
            severity: 'danger'
          });
        }
      });
    }
  }

  // --- Confirmation Dialog Actions ---

  executeConfirmedAction(): void {
    const action = this.confirmAction();
    if (action) {
      action();
    }
  }

  cancelConfirmation(): void {
    this.confirmDialogVisible.set(false);
    this.confirmAction.set(null);
  }

  navigateBack(): void {
    this.router.navigate(['/app/procurement/rfqs']);
  }
}
