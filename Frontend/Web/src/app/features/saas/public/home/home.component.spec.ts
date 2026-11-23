import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { HomeComponent } from './home.component';
import { PublicNavService } from '../../../../core/services/public-nav.service';
import { TenantContextService } from '../../../../core/services/tenant-context.service';

describe('HomeComponent', () => {
  let component: HomeComponent;
  let fixture: ComponentFixture<HomeComponent>;
  let navService: PublicNavService;
  let tenantContext: TenantContextService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HomeComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    navService = TestBed.inject(PublicNavService);
    tenantContext = TestBed.inject(TenantContextService);
    navService.setActiveTab('platform');

    fixture = TestBed.createComponent(HomeComponent);
    component = fixture.componentInstance;
  });

  describe('Platform Marketing Mode (apexerp.com)', () => {
    beforeEach(() => {
      tenantContext.subdomain.set(null);
      tenantContext.currentTenant.set(null);
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

    it('should render Explore the Platform and View Subscription Plans buttons', () => {
      fixture.detectChanges();
      const compiled = fixture.nativeElement as HTMLElement;
      const links = Array.from(compiled.querySelectorAll('.hero-cta-group a, .hero-cta-group button'));
      const linkTexts = links.map(l => l.textContent?.trim());

      expect(linkTexts.some(t => t?.includes('Explore the Platform'))).toBe(true);
      expect(linkTexts.some(t => t?.includes('View Subscription Plans'))).toBe(true);
    });
  });

  describe('Tenant Subdomain Mode (nova.192.168.1.65.nip.io:4200)', () => {
    beforeEach(() => {
      tenantContext.subdomain.set('nova');
      tenantContext.currentTenant.set({
        isTenant: true,
        tenantId: '11111111-2222-3333-4444-555555555555',
        name: 'Nova Manufacturing',
        subdomain: 'nova',
        industryType: 'Discrete & Process Manufacturing',
        companyStructure: 'Single Company',
        eCommerceEnabled: true
      });
    });

    it('should render Section 1: Hero with tenant branding, headline and Operations Hub card', () => {
      fixture.detectChanges();
      const compiled = fixture.nativeElement as HTMLElement;

      const hero = compiled.querySelector('#home');
      expect(hero).not.toBeNull();
      expect(hero?.querySelector('.hero-title')?.textContent).toContain('Nova Manufacturing');
      expect(hero?.querySelector('.hero-badge')?.textContent).toContain('Discrete & Process Manufacturing');
      expect(hero?.querySelector('.card-caption')?.textContent).toContain('Nova Manufacturing Operations Hub');
      expect(hero?.textContent).toContain('Enterprise Capacity');
      expect(hero?.textContent).toContain('Active Live');
    });

    it('should render Section 2: About Us with story and milestone cards', () => {
      fixture.detectChanges();
      const compiled = fixture.nativeElement as HTMLElement;

      const about = compiled.querySelector('#about');
      expect(about).not.toBeNull();
      expect(about?.textContent).toContain('Who We Are');
      expect(about?.textContent).toContain('Single Company');
      expect(about?.textContent).toContain('Quality & Traceability');
    });

    it('should render Section 3: Product Categories & Featured Products with Storefront link', () => {
      fixture.detectChanges();
      const compiled = fixture.nativeElement as HTMLElement;

      const productsSection = compiled.querySelector('#products');
      expect(productsSection).not.toBeNull();
      expect(productsSection?.textContent).toContain('Product Categories');
      expect(productsSection?.textContent).toContain('View Entire Product Catalogue');
    });

    it('should render Section 4: Business Services grid', () => {
      fixture.detectChanges();
      const compiled = fixture.nativeElement as HTMLElement;

      const servicesSection = compiled.querySelector('#services');
      expect(servicesSection).not.toBeNull();
      expect(servicesSection?.textContent).toContain('Comprehensive Business Capabilities');
      expect(servicesSection?.textContent).toContain('Custom Fabrication');
      expect(servicesSection?.textContent).toContain('Quality Assurance');
    });

    it('should render Section 5: Supplier Registration & Onboarding with RFQ link', () => {
      fixture.detectChanges();
      const compiled = fixture.nativeElement as HTMLElement;

      const suppliersSection = compiled.querySelector('#suppliers');
      expect(suppliersSection).not.toBeNull();
      expect(suppliersSection?.textContent).toContain('Partner with Nova Manufacturing');
      expect(suppliersSection?.textContent).toContain('Vendor Network');

      const rfqLink = suppliersSection?.querySelector('a[routerLink="/public/rfq-submit"]');
      expect(rfqLink).not.toBeNull();
      expect(rfqLink?.textContent).toContain('Submit Active RFQ Quotation');
    });

    it('should render Section 6: Corporate Contact Information with operating hours', () => {
      fixture.detectChanges();
      const compiled = fixture.nativeElement as HTMLElement;

      const contactSection = compiled.querySelector('#contact');
      expect(contactSection).not.toBeNull();
      expect(contactSection?.textContent).toContain('Corporate Contact & Operating Hours');
      expect(contactSection?.textContent).toContain('Registered Head Office');
      expect(contactSection?.textContent).toContain('Inquiries & Sales Desk');
      expect(contactSection?.textContent).toContain('Monday – Friday: 08:00 – 17:30');
    });
  });
});
