import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router, ActivatedRoute } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { PriceCardPageComponent } from './price-card-page.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { AuthService } from '../../../../../core/auth/auth.service';
import {
  PriceCardItem,
  PriceCardType
} from '../../../../../core/models/price-card.models';
import {
  ProductItem,
  UnitOfMeasureItem,
  CurrencyItem
} from '../../../../../core/models/scope.models';

describe('PriceCardPageComponent', () => {
  let component: PriceCardPageComponent;
  let fixture: ComponentFixture<PriceCardPageComponent>;
  let masterDataService: MasterDataService;
  let router: Router;

  const mockProducts: ProductItem[] = [
    {
      productId: 'p-01',
      tenantId: 'tenant-1',
      productCode: 'STEEL-ROD-01',
      productName: 'Stainless Steel Rod 10mm',
      categoryId: 'cat-01',
      unitOfMeasure: 'MTR',
      unitOfMeasureId: 'uom-01',
      isManufacturable: false,
      isPurchasable: true,
      isSellable: true,
      canConsumeInProduction: true
    },
    {
      productId: 'p-02',
      tenantId: 'tenant-1',
      productCode: 'COPPER-WIRE-02',
      productName: 'Insulated Copper Wire 2.5mm',
      categoryId: 'cat-01',
      unitOfMeasure: 'ROLL',
      unitOfMeasureId: 'uom-02',
      isManufacturable: false,
      isPurchasable: true,
      isSellable: true,
      canConsumeInProduction: true
    }
  ] as unknown as ProductItem[];

  const mockUoms: UnitOfMeasureItem[] = [
    {
      unitOfMeasureId: 'uom-01',
      tenantId: 'tenant-1',
      code: 'MTR',
      name: 'Meter',
      isActive: true
    },
    {
      unitOfMeasureId: 'uom-02',
      tenantId: 'tenant-1',
      code: 'ROLL',
      name: 'Roll',
      isActive: true
    }
  ];

  const mockCurrencies: CurrencyItem[] = [
    {
      currencyId: 'curr-01',
      tenantId: 'tenant-1',
      currencyCode: 'USD',
      currencyName: 'US Dollar',
      symbol: '$',
      decimalPlaces: 2,
      isActive: true
    }
  ];

  const mockPriceCard: PriceCardItem = {
    priceCardId: 'pc-100',
    tenantId: 'tenant-1',
    priceCardCode: 'PC-PUR-2026',
    priceCardName: 'Annual Raw Material Pricing',
    priceCardType: PriceCardType.Purchase,
    currency: 'USD',
    effectiveFrom: '2026-01-01T00:00:00Z',
    effectiveTo: '2026-12-31T23:59:59Z',
    description: 'Contracted prices for metals',
    isActive: true,
    lineCount: 1,
    lines: [
      {
        priceCardLineId: 'line-001',
        tenantId: 'tenant-1',
        priceCardId: 'pc-100',
        productId: 'p-01',
        productCode: 'STEEL-ROD-01',
        productName: 'Stainless Steel Rod 10mm',
        unitOfMeasureId: 'uom-01',
        unitOfMeasureCode: 'MTR',
        unitOfMeasureName: 'Meter',
        unitPrice: 25.5,
        minimumQuantity: 10,
        effectiveFrom: '2026-01-01T00:00:00Z',
        effectiveTo: null,
        isActive: true
      }
    ]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PriceCardPageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        MasterDataService,
        AuthService,
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              data: { mode: 'create' },
              paramMap: {
                get: (key: string) => (key === 'id' ? null : null)
              }
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PriceCardPageComponent);
    component = fixture.componentInstance;
    masterDataService = TestBed.inject(MasterDataService);
    router = TestBed.inject(Router);

    vi.spyOn(masterDataService, 'getProducts').mockReturnValue(of(mockProducts));
    vi.spyOn(masterDataService, 'getUnitsOfMeasure').mockReturnValue(of(mockUoms));
    vi.spyOn(masterDataService, 'getCurrencies').mockReturnValue(of(mockCurrencies));
    vi.spyOn(masterDataService, 'getPriceCard').mockReturnValue(of(mockPriceCard));
  });

  it('should initialize in create mode with empty header form and reference lookups', () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.mode()).toBe('create');
    expect(component.headerForm.get('priceCardCode')?.enabled).toBe(true);
    expect(component.headerForm.get('priceCardType')?.enabled).toBe(true);
    expect(component.products().length).toBe(2);
    expect(component.uoms().length).toBe(2);
  });

  it('should reject invalid dates when Effective To is earlier than Effective From', () => {
    fixture.detectChanges();

    component.headerForm.patchValue({
      priceCardCode: 'PC-ERR',
      priceCardName: 'Invalid Dates Test',
      priceCardType: PriceCardType.Purchase,
      currency: 'USD',
      effectiveFrom: '2026-05-10',
      effectiveTo: '2026-05-01'
    });

    component.saveCard();

    expect(component.message()?.severity).toBe('error');
    expect(component.message()?.text).toContain('Effective To date cannot be earlier');
  });

  it('should detect duplicate product and UOM lines before submitting', () => {
    fixture.detectChanges();

    component.headerForm.patchValue({
      priceCardCode: 'PC-DUP',
      priceCardName: 'Duplicate Test',
      priceCardType: PriceCardType.Purchase,
      currency: 'USD',
      effectiveFrom: '2026-01-01'
    });

    component.lines.set([
      {
        priceCardLineId: 'l1',
        tenantId: 't1',
        priceCardId: 'pc-dup',
        productId: 'p-01',
        productCode: 'STEEL-ROD-01',
        unitOfMeasureId: 'uom-01',
        unitPrice: 10,
        minimumQuantity: 1,
        isActive: true
      },
      {
        priceCardLineId: 'l2',
        tenantId: 't1',
        priceCardId: 'pc-dup',
        productId: 'p-01',
        productCode: 'STEEL-ROD-01',
        unitOfMeasureId: 'uom-01',
        unitPrice: 15,
        minimumQuantity: 5,
        isActive: true
      }
    ]);

    component.saveCard();

    expect(component.message()?.severity).toBe('error');
    expect(component.message()?.text).toContain('Duplicate line detected');
  });

  it('should reject negative unit prices', () => {
    fixture.detectChanges();

    component.headerForm.patchValue({
      priceCardCode: 'PC-NEG',
      priceCardName: 'Negative Test',
      priceCardType: PriceCardType.Purchase,
      currency: 'USD',
      effectiveFrom: '2026-01-01'
    });

    component.lines.set([
      {
        priceCardLineId: 'l1',
        tenantId: 't1',
        priceCardId: 'pc-neg',
        productId: 'p-01',
        productCode: 'STEEL-ROD-01',
        unitOfMeasureId: 'uom-01',
        unitPrice: -5,
        minimumQuantity: 1,
        isActive: true
      }
    ]);

    component.saveCard();

    expect(component.message()?.severity).toBe('error');
    expect(component.message()?.text).toContain('invalid or negative unit price');
  });

  it('should submit valid create payload to masterDataService', () => {
    const createSpy = vi
      .spyOn(masterDataService, 'createPriceCard')
      .mockReturnValue(of(mockPriceCard));
    const navSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    fixture.detectChanges();

    component.headerForm.patchValue({
      priceCardCode: 'PC-NEW-001',
      priceCardName: 'New Metals Price Card',
      priceCardType: PriceCardType.Purchase,
      currency: 'USD',
      effectiveFrom: '2026-01-01'
    });

    component.lines.set([
      {
        priceCardLineId: 'l1',
        tenantId: 't1',
        priceCardId: 'pc-new',
        productId: 'p-01',
        productCode: 'STEEL-ROD-01',
        unitOfMeasureId: 'uom-01',
        unitPrice: 32.5,
        minimumQuantity: 5,
        isActive: true
      }
    ]);

    component.saveCard();

    expect(createSpy).toHaveBeenCalledWith(
      expect.objectContaining({
        priceCardCode: 'PC-NEW-001',
        priceCardName: 'New Metals Price Card',
        priceCardType: PriceCardType.Purchase,
        currency: 'USD'
      })
    );
    expect(navSpy).toHaveBeenCalledWith(['/app/master-data/price-cards', mockPriceCard.priceCardId]);
  });
});
