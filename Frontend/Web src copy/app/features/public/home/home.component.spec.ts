import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { HomeComponent } from './home.component';
import { PublicNavService } from '../../../core/services/public-nav.service';

describe('HomeComponent', () => {
  let component: HomeComponent;
  let fixture: ComponentFixture<HomeComponent>;
  let navService: PublicNavService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HomeComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    navService = TestBed.inject(PublicNavService);
    // Reset to default platform tab before each test
    navService.setActiveTab('platform');

    fixture = TestBed.createComponent(HomeComponent);
    component = fixture.componentInstance;
  });

  it('should initialize on Platform tab by default and render main heading', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;

    expect(navService.activeTab()).toBe('platform');
    expect(compiled.querySelector('#tabpanel-platform')).not.toBeNull();
    expect(compiled.querySelector('.hero-title')?.textContent).toContain('One Connected Platform. Smarter Business Decisions.');
    expect(compiled.querySelector('.hero-badge')?.textContent).toContain('ApexERP — AI-Powered Business Management Platform');
  });

  it('should render all 5 core platform capabilities matching the specification', () => {
    expect(component.capabilities.length).toBe(5);
    const titles = component.capabilities.map(c => c.title);
    expect(titles).toEqual([
      'Procurement & Purchasing',
      'Inventory & Warehousing',
      'Sales & Order Fulfillment',
      'Business Operations',
      'AI Business Intelligence'
    ]);
  });

  it('should render section headings for growth and decisions', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Everything Your Business Needs');
    expect(compiled.textContent).toContain('Built for Growing Businesses');
    expect(compiled.textContent).toContain('Make Better Business Decisions with ApexERP');
  });

  it('should switch content to Business Directory on the same page when activeTab is directory', () => {
    fixture.detectChanges();

    component.switchToDirectory();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(navService.activeTab()).toBe('directory');
    expect(compiled.querySelector('#tabpanel-platform')).toBeNull();
    expect(compiled.querySelector('#tabpanel-directory')).not.toBeNull();
    expect(compiled.querySelector('h1')?.textContent).toContain('Manufacturing & Supplier Directory');
    expect(compiled.querySelector('h2')?.textContent).toContain('Business Discovery Network In Development');
    expect(compiled.querySelector('.status-desc')?.textContent).toContain('onboarding verified industrial fabricators');
  });

  it('should switch back to Platform content when switchToPlatform is called', () => {
    component.switchToDirectory();
    fixture.detectChanges();

    component.switchToPlatform();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(navService.activeTab()).toBe('platform');
    expect(compiled.querySelector('#tabpanel-platform')).not.toBeNull();
    expect(compiled.querySelector('#tabpanel-directory')).toBeNull();
  });

  it('should render correct accessible ARIA tabpanel attributes', () => {
    fixture.detectChanges();
    const platformPanel = fixture.nativeElement.querySelector('#tabpanel-platform');
    expect(platformPanel.getAttribute('role')).toBe('tabpanel');
    expect(platformPanel.getAttribute('aria-labelledby')).toBe('tab-platform');

    component.switchToDirectory();
    fixture.detectChanges();
    const directoryPanel = fixture.nativeElement.querySelector('#tabpanel-directory');
    expect(directoryPanel.getAttribute('role')).toBe('tabpanel');
    expect(directoryPanel.getAttribute('aria-labelledby')).toBe('tab-directory');
  });
});
