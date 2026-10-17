import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DashboardComponent } from './dashboard.component';

describe('DashboardComponent', () => {
  let component: DashboardComponent;
  let fixture: ComponentFixture<DashboardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
  });

  it('should create dashboard and render KPI cards', () => {
    fixture.detectChanges();
    expect(component.kpiCards.length).toBe(4);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.kpi-title')?.textContent).toContain('Inventory Valuation');
  });

  it('should initialize recent orders list', () => {
    expect(component.recentOrders.length).toBe(5);
  });
});
