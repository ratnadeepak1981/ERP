import { Component, OnInit, OnDestroy, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ActionDialogComponent } from '../../../../../shared/components/action-dialog/action-dialog.component';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../../shared/components/data-table/data-table.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AccessDeniedComponent } from '../../../../../shared/components/access-denied/access-denied.component';
import { SalesShipmentService } from '../../../../../core/services/sales-shipment.service';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { WorkspaceScopeService } from '../../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import {
  SalesShipmentDto,
  SalesShipmentStatus,
  FulfillmentMethod,
  getSalesShipmentStatusName,
  getSalesShipmentStatusSeverity
} from '../../../../../core/models/sales-shipment.models';
import { CustomerItem } from '../../../../../core/models/scope.models';
import { DataTableColumn, DataTableAction } from '../../../../../shared/models/data-table-config.model';

export type ShipmentStatusFilter = 'all' | 'draft' | 'dispatched' | 'delivered' | 'cancelled';

@Component({
  selector: 'app-sales-shipment-register',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ActionDialogComponent,
    AppPageHeaderComponent,
    AppDataTableComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AccessDeniedComponent
  ],
  templateUrl: './sales-shipment-register.component.html',
  styleUrl: './sales-shipment-register.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SalesShipmentRegisterComponent implements OnInit, OnDestroy {
  readonly shipmentService = inject(SalesShipmentService);
  readonly masterDataService = inject(MasterDataService);
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);

  private readonly subs = new Subscription();

  // Loading & Error States
  readonly loading = signal<boolean>(false);
  readonly actionInProgress = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly isAccessDenied = signal<boolean>(false);
  readonly accessDeniedMessage = signal<string>(
    "You don't have permission to access dispatch notes for this operating company and branch."
  );
  readonly message = signal<{ text: string; severity: 'success' | 'info' | 'warn' | 'danger' } | null>(null);

  // Data Signals
  readonly shipments = signal<SalesShipmentDto[]>([]);
  readonly customers = signal<CustomerItem[]>([]);

  // Search & Filter State
  readonly searchTerm = signal<string>('');
  readonly activeStatusFilter = signal<ShipmentStatusFilter>('all');

  // Deliver Dialog State
  readonly deliverDialogVisible = signal<boolean>(false);
  readonly deliverTargetShipment = signal<SalesShipmentDto | null>(null);
  readonly deliverRecipientName = signal<string>('');
  readonly deliverNotes = signal<string>('');

  // Breadcrumbs
  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Sales', routerLink: '/app/sales' },
    { label: 'Dispatch Notes' }
  ];

  // Status Filter Options
  readonly statusOptions = [
    { label: 'All', value: 'all' as ShipmentStatusFilter },
    { label: 'Draft', value: 'draft' as ShipmentStatusFilter },
    { label: 'Dispatched', value: 'dispatched' as ShipmentStatusFilter },
    { label: 'Delivered', value: 'delivered' as ShipmentStatusFilter },
    { label: 'Cancelled', value: 'cancelled' as ShipmentStatusFilter }
  ];

  // Permissions
  readonly canCreateShipment = computed<boolean>(() => {
    return this.authService.hasPermission('SALES_SHIPMENT.CREATE') || this.authService.isTenantAdmin() || this.authService.isPlatformAdmin();
  });

  readonly canDeliverShipment = computed<boolean>(() => {
    return this.authService.hasPermission('SALES_SHIPMENT.DELIVER') || this.authService.isTenantAdmin() || this.authService.isPlatformAdmin();
  });

  // Filtered Shipments
  readonly filteredShipments = computed<SalesShipmentDto[]>(() => {
    let list = this.shipments();
    const filter = this.activeStatusFilter();
    if (filter !== 'all') {
      const statusMap: Record<string, SalesShipmentStatus> = {
        draft: SalesShipmentStatus.Draft,
        dispatched: SalesShipmentStatus.Dispatched,
        delivered: SalesShipmentStatus.Delivered,
        cancelled: SalesShipmentStatus.Cancelled
      };
      const targetStatus = statusMap[filter];
      list = list.filter(s => Number(s.status) === targetStatus);
    }

    const term = this.searchTerm().trim().toLowerCase();
    if (term) {
      list = list.filter(s => {
        return (
          (s.shipmentNumber && s.shipmentNumber.toLowerCase().includes(term)) ||
          (s.orderNumber && s.orderNumber.toLowerCase().includes(term)) ||
          (s.customerName && s.customerName.toLowerCase().includes(term)) ||
          (s.carrierName && s.carrierName.toLowerCase().includes(term)) ||
          (s.trackingNumber && s.trackingNumber.toLowerCase().includes(term))
        );
      });
    }

    return list;
  });

  // Summary Metrics
  readonly draftCount = computed<number>(() => {
    return this.shipments().filter(s => Number(s.status) === SalesShipmentStatus.Draft).length;
  });

  readonly deliveredCount = computed<number>(() => {
    return this.shipments().filter(s => Number(s.status) === SalesShipmentStatus.Delivered).length;
  });

  // Table Columns
  readonly columns: DataTableColumn<SalesShipmentDto>[] = [
    {
      field: 'shipmentNumber',
      header: 'Dispatch #',
      sortable: true,
      width: '150px'
    },
    {
      field: 'orderNumber',
      header: 'Order #',
      sortable: true,
      width: '140px'
    },
    {
      field: 'customerName',
      header: 'Customer',
      sortable: true,
      width: '200px'
    },
    {
      field: 'shipmentDate',
      header: 'Date',
      sortable: true,
      width: '120px',
      format: (val: any) => (val ? new Date(val).toLocaleDateString() : '-')
    },
    {
      field: 'method',
      header: 'Method',
      width: '130px',
      format: (val: any) => this.getMethodName(val)
    },
    {
      field: 'items',
      header: 'Lines',
      width: '80px',
      align: 'center',
      format: (val: any) => String(Array.isArray(val) ? val.length : 0)
    },
    {
      field: 'status',
      header: 'Status',
      sortable: true,
      width: '130px',
      type: 'badge',
      badgeSeverityMap: (val: any) => getSalesShipmentStatusSeverity(val),
      format: (val: any) => getSalesShipmentStatusName(val)
    },
    {
      field: 'id',
      header: 'Actions',
      width: '260px',
      type: 'actions'
    }
  ];

  // Row Actions
  readonly tableActions: DataTableAction<SalesShipmentDto>[] = [
    {
      id: 'view',
      label: 'View',
      icon: 'pi pi-eye',
      severity: 'secondary',
      action: (shipment: SalesShipmentDto) => this.navigateToView(shipment)
    },
    {
      id: 'edit',
      label: 'Edit Draft',
      icon: 'pi pi-pencil',
      severity: 'secondary',
      visible: (shipment: SalesShipmentDto) => Number(shipment.status) === SalesShipmentStatus.Draft,
      action: (shipment: SalesShipmentDto) => this.navigateToEdit(shipment)
    },
    {
      id: 'deliver',
      label: 'Deliver & Issue Stock',
      icon: 'pi pi-check-circle',
      severity: 'success',
      visible: (shipment: SalesShipmentDto) =>
        (Number(shipment.status) === SalesShipmentStatus.Draft || Number(shipment.status) === SalesShipmentStatus.Dispatched) &&
        this.canDeliverShipment(),
      action: (shipment: SalesShipmentDto) => this.openDeliverDialog(shipment)
    }
  ];

  ngOnInit(): void {
    this.subs.add(
      this.scopeService.scopeChange$.subscribe(() => {
        this.loadData();
      })
    );
    this.loadData();
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  loadData(): void {
    const comp = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!comp?.id) {
      this.shipments.set([]);
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.isAccessDenied.set(false);

    forkJoin({
      customers: this.masterDataService.getCustomers().pipe(catchError(() => of([] as CustomerItem[]))),
      shipments: this.shipmentService.getShipments(comp.id, branch?.id || null).pipe(
        catchError(err => {
          if (err?.status === 403) {
            this.isAccessDenied.set(true);
          } else {
            this.error.set(err?.message || 'Failed to load dispatch notes.');
          }
          return of([] as SalesShipmentDto[]);
        })
      )
    }).subscribe({
      next: ({ customers, shipments }) => {
        this.customers.set(customers || []);
        const custMap = new Map((customers || []).map(c => [c.customerId, c.customerName]));

        const enriched = (shipments || []).map(s => ({
          ...s,
          customerName: custMap.get(s.customerId) || s.customerName || 'Customer'
        }));

        this.shipments.set(enriched);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  getMethodName(method: FulfillmentMethod | number): string {
    switch (Number(method)) {
      case FulfillmentMethod.CustomerPickup:
        return 'Customer Pickup';
      case FulfillmentMethod.StoreHandover:
        return 'Store Handover';
      case FulfillmentMethod.Delivery:
      default:
        return 'Delivery';
    }
  }

  setStatusFilter(filter: ShipmentStatusFilter): void {
    this.activeStatusFilter.set(filter);
  }

  navigateToNew(): void {
    this.router.navigate(['/app/sales/dispatches/new']);
  }

  navigateToView(shipment: SalesShipmentDto): void {
    this.router.navigate(['/app/sales/dispatches', shipment.id]);
  }

  navigateToEdit(shipment: SalesShipmentDto): void {
    this.router.navigate(['/app/sales/dispatches', shipment.id, 'edit']);
  }

  openDeliverDialog(shipment: SalesShipmentDto): void {
    this.deliverTargetShipment.set(shipment);
    this.deliverRecipientName.set(shipment.recipientName || '');
    this.deliverNotes.set('');
    this.deliverDialogVisible.set(true);
  }

  closeDeliverDialog(): void {
    this.deliverDialogVisible.set(false);
    this.deliverTargetShipment.set(null);
  }

  executeDelivery(): void {
    const s = this.deliverTargetShipment();
    const comp = this.scopeService.selectedCompany();
    const branch = this.scopeService.selectedBranch();
    if (!s || !comp?.id || !branch?.id) return;

    this.actionInProgress.set(true);
    this.shipmentService
      .deliverShipment(comp.id, branch.id, s.id, {
        recipientName: this.deliverRecipientName().trim() || undefined,
        notes: this.deliverNotes().trim() || undefined
      })
      .subscribe({
        next: updated => {
          this.actionInProgress.set(false);
          this.closeDeliverDialog();
          this.message.set({
            text: `Dispatch note ${updated.shipmentNumber} delivered successfully. Stock issue posted and sales invoice draft created.`,
            severity: 'success'
          });
          this.loadData();
          if (updated.invoiceId) {
            this.router.navigate(['/app/sales/invoices', updated.invoiceId]);
          }
        },
        error: err => {
          this.actionInProgress.set(false);
          this.message.set({
            text: err?.error?.message || err?.message || 'Failed to deliver shipment.',
            severity: 'danger'
          });
        }
      });
  }
}
