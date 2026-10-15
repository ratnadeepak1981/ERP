import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { OverviewComponent } from './overview.component';

describe('OverviewComponent', () => {
  let component: OverviewComponent;
  let fixture: ComponentFixture<OverviewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OverviewComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(OverviewComponent);
    component = fixture.componentInstance;
  });

  it('should create overview component', () => {
    expect(component).toBeTruthy();
  });

  it('should render platform overview heading and subtitle', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.hero-title')?.textContent).toContain('Enterprise Operations');
    expect(compiled.querySelector('.hero-subtitle')?.textContent).toContain('ApexERP unifies procurement');
  });

  it('should render all 6 core capability pillars', () => {
    expect(component.pillars.length).toBe(6);
    const titles = component.pillars.map(p => p.title);
    expect(titles).toContain('Procurement & Purchasing');
    expect(titles).toContain('Inventory & Warehousing');
    expect(titles).toContain('Sales & Fulfillment');
    expect(titles).toContain('Multi-Tenant Security & Isolation');
    expect(titles).toContain('AI Business Intelligence');
    expect(titles).toContain('Multi-Company & Branch Network');
  });

  it('should render View Subscription Plans button linking to /plans', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const plansLinks = Array.from(compiled.querySelectorAll('a')).filter(
      a => a.textContent?.includes('View Subscription Plans')
    );

    expect(plansLinks.length).toBeGreaterThanOrEqual(1);
    plansLinks.forEach(link => {
      expect(link.getAttribute('routerLink') || link.getAttribute('href')).toContain('/plans');
    });
  });
});
