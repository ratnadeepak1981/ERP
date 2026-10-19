import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, ActivatedRoute } from '@angular/router';
import { StockReceiptPageComponent } from './stock-receipt-page.component';

describe('StockReceiptPageComponent', () => {
  let component: StockReceiptPageComponent;
  let fixture: ComponentFixture<StockReceiptPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StockReceiptPageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => (key === 'id' ? 'new' : null)
              }
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(StockReceiptPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize in create mode for new receipt', () => {
    expect(component).toBeTruthy();
    expect(component.mode()).toBe('create');
    expect(component.receiptId()).toBeNull();
  });
});
