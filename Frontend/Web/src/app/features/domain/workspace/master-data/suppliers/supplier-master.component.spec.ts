import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { SupplierMasterComponent } from './supplier-master.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { of } from 'rxjs';
import { SupplierItem } from '../../../../../core/models/scope.models';

describe('SupplierMasterComponent', () => {
  let component: SupplierMasterComponent;
  let fixture: ComponentFixture<SupplierMasterComponent>;
  let masterDataService: MasterDataService;

  const mockSuppliers: SupplierItem[] = [
    {
      supplierId: 's1',
      tenantId: 't1',
      supplierCode: 'SUP-001',
      supplierName: 'Alpha Global Metals',
      contactName: 'Jane Smith',
      email: 'jane@alphametals.com',
      city: 'Colombo',
      isActive: true
    },
    {
      supplierId: 's2',
      tenantId: 't1',
      supplierCode: 'SUP-002',
      supplierName: 'Beta Chemical Corp',
      contactName: 'Bob Vance',
      email: 'bob@betachem.com',
      city: 'Kandy',
      isActive: false
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SupplierMasterComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        MasterDataService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SupplierMasterComponent);
    component = fixture.componentInstance;
    masterDataService = TestBed.inject(MasterDataService);
  });

  it('should create and load suppliers list', () => {
    vi.spyOn(masterDataService, 'getSuppliers').mockReturnValue(of(mockSuppliers));
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.suppliers().length).toBe(2);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('SUP-001');
    expect(compiled.textContent).toContain('Alpha Global Metals');
    expect(compiled.textContent).toContain('Jane Smith');
  });

  it('should filter suppliers by search term', () => {
    vi.spyOn(masterDataService, 'getSuppliers').mockReturnValue(of(mockSuppliers));
    fixture.detectChanges();

    component.searchTerm.set('Chemical');
    expect(component.filteredSuppliers().length).toBe(1);
    expect(component.filteredSuppliers()[0].supplierCode).toBe('SUP-002');
  });
});
