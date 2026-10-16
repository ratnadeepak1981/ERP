import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { CustomerPortalComponent } from './customer-portal.component';

describe('CustomerPortalComponent', () => {
  let component: CustomerPortalComponent;
  let fixture: ComponentFixture<CustomerPortalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CustomerPortalComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerPortalComponent);
    component = fixture.componentInstance;
  });

  it('should create customer portal component and render orders', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h2')?.textContent).toContain('Customer Private Portal');
    expect(component.recentOrders().length).toBeGreaterThan(0);
  });
});
