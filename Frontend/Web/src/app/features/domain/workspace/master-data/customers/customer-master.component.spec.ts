import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { CustomerMasterComponent } from './customer-master.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { of } from 'rxjs';
import { CustomerItem } from '../../../../../core/models/scope.models';

describe('CustomerMasterComponent', () => {
  let component: CustomerMasterComponent;
  let fixture: ComponentFixture<CustomerMasterComponent>;
  let masterDataService: MasterDataService;

  const mockCustomers: CustomerItem[] = [
    {
      customerId: 'c1',
      tenantId: 't1',
      customerCode: 'CUST-001',
      customerName: 'Ceylon Beverage Holdings',
      contactPerson: 'Sunil Perera',
      email: 'orders@ceylonbev.lk',
      city: 'Colombo',
      paymentTerms: 'Net 30',
      isActive: true
    },
    {
      customerId: 'c2',
      tenantId: 't1',
      customerCode: 'CUST-002',
      customerName: 'Lanka Industrial Distribution',
      contactPerson: 'Kamal Silva',
      email: 'kamal@lankaindustrial.lk',
      city: 'Kandy',
      paymentTerms: 'Net 15',
      isActive: false
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CustomerMasterComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        MasterDataService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerMasterComponent);
    component = fixture.componentInstance;
    masterDataService = TestBed.inject(MasterDataService);
  });

  it('should create and load customer records', () => {
    vi.spyOn(masterDataService, 'getCustomers').mockReturnValue(of(mockCustomers));
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.customers().length).toBe(2);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('CUST-001');
    expect(compiled.textContent).toContain('Ceylon Beverage Holdings');
    expect(compiled.textContent).toContain('Sunil Perera');
  });

  it('should filter customers by search query', () => {
    vi.spyOn(masterDataService, 'getCustomers').mockReturnValue(of(mockCustomers));
    fixture.detectChanges();

    component.searchTerm.set('Beverage');
    expect(component.filteredCustomers().length).toBe(1);
    expect(component.filteredCustomers()[0].customerCode).toBe('CUST-001');
  });

  it('should filter customers by active status', () => {
    vi.spyOn(masterDataService, 'getCustomers').mockReturnValue(of(mockCustomers));
    fixture.detectChanges();

    component.statusFilter.set('active');
    expect(component.filteredCustomers().length).toBe(1);
    expect(component.filteredCustomers()[0].customerCode).toBe('CUST-001');

    component.statusFilter.set('inactive');
    expect(component.filteredCustomers().length).toBe(1);
    expect(component.filteredCustomers()[0].customerCode).toBe('CUST-002');
  });
});
