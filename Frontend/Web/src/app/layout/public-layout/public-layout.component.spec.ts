import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { PublicLayoutComponent } from './public-layout.component';
import { PublicNavService } from '../../core/services/public-nav.service';
import { TenantContextService } from '../../core/services/tenant-context.service';

describe('PublicLayoutComponent', () => {
  let component: PublicLayoutComponent;
  let fixture: ComponentFixture<PublicLayoutComponent>;
  let navService: PublicNavService;
  let tenantContext: TenantContextService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PublicLayoutComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    navService = TestBed.inject(PublicNavService);
    tenantContext = TestBed.inject(TenantContextService);
    fixture = TestBed.createComponent(PublicLayoutComponent);
    component = fixture.componentInstance;
  });

  describe('Platform Mode (apexerp.com / localhost)', () => {
    beforeEach(() => {
      tenantContext.subdomain.set(null);
      tenantContext.currentTenant.set(null);
    });

    it('should create public layout and render brand title for platform', () => {
      fixture.detectChanges();
      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.querySelector('.brand-title')?.textContent).toContain('ApexERP');
      expect(compiled.querySelector('.brand-tag')?.textContent).toContain('SaaS Platform');
    });

    it('should render exactly two main navigation tabs: Platform and Business Directory', () => {
      fixture.detectChanges();
      const compiled = fixture.nativeElement as HTMLElement;
      const tabs = compiled.querySelectorAll('.public-nav button[role="tab"]');
      expect(tabs.length).toBe(2);
      expect(tabs[0].textContent).toContain('Platform');
      expect(tabs[1].textContent).toContain('Business Directory');
    });

    it('should switch active tab when Business Directory tab is clicked', () => {
      fixture.detectChanges();
      const compiled = fixture.nativeElement as HTMLElement;
      const directoryTab = compiled.querySelector('#tab-directory') as HTMLButtonElement;
      directoryTab.click();
      fixture.detectChanges();

      expect(navService.activeTab()).toBe('directory');
      expect(directoryTab.getAttribute('aria-selected')).toBe('true');
    });

    it('should render Sign In and View Plans actions in platform mode', () => {
      fixture.detectChanges();
      const compiled = fixture.nativeElement as HTMLElement;
      const signInLink = compiled.querySelector('a[routerLink="/login"]');
      const viewPlansLink = compiled.querySelector('a[routerLink="/plans"]');
      expect(signInLink).not.toBeNull();
      expect(signInLink?.textContent).toContain('Sign In');
      expect(viewPlansLink).not.toBeNull();
      expect(viewPlansLink?.textContent).toContain('View Plans');
    });

    it('should render SaaS platform footer in platform mode', () => {
      fixture.detectChanges();
      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.querySelector('.footer-logo')?.textContent).toContain('ApexERP Manufacturing Cloud');
      expect(compiled.textContent).toContain('Platform Navigation');
      expect(compiled.textContent).toContain('Subscription Plans');
    });
  });

  describe('Tenant Subdomain Mode (abc.apexerp.com / nova.nip.io)', () => {
    beforeEach(() => {
      tenantContext.subdomain.set('nova');
      tenantContext.currentTenant.set({
        isTenant: true,
        tenantId: '11111111-2222-3333-4444-555555555555',
        name: 'Nova Manufacturing',
        subdomain: 'nova',
        industryType: 'Discrete Manufacturing',
        companyStructure: 'Single Company',
        eCommerceEnabled: true
      });
    });

    it('should render tenant branding in header', () => {
      fixture.detectChanges();
      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.querySelector('.brand-title')?.textContent).toContain('Nova Manufacturing');
      expect(compiled.querySelector('.brand-tag')?.textContent).toContain('Discrete Manufacturing');
    });

    it('should render 6 accessible middle navigation links and remove static badge', () => {
      fixture.detectChanges();
      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.querySelector('.tenant-nav-badge')).toBeNull();

      const tenantNav = compiled.querySelector('.tenant-nav');
      expect(tenantNav).not.toBeNull();

      const navButtons = Array.from(tenantNav!.querySelectorAll('button.nav-tab'));
      const labels = navButtons.map(b => b.textContent?.trim());
      expect(labels).toContain('Home');
      expect(labels).toContain('Products');
      expect(labels).toContain('About Us');
      expect(labels).toContain('Services');
      expect(labels).toContain('Suppliers');
      expect(labels).toContain('Contact Us');
    });

    it('should hide View Plans and render Register button in tenant mode', () => {
      fixture.detectChanges();
      const compiled = fixture.nativeElement as HTMLElement;
      const viewPlansLink = compiled.querySelector('a[routerLink="/plans"]');
      const registerLink = compiled.querySelector('a[routerLink="/register"]');
      const signInLink = compiled.querySelector('a[routerLink="/login"]');

      expect(viewPlansLink).toBeNull();
      expect(registerLink).not.toBeNull();
      expect(registerLink?.textContent).toContain('Register');
      expect(signInLink).not.toBeNull();
      expect(signInLink?.textContent).toContain('Sign In');
    });

    it('should render tenant-specific corporate footer', () => {
      fixture.detectChanges();
      const compiled = fixture.nativeElement as HTMLElement;
      const tenantFooter = compiled.querySelector('.tenant-footer');
      expect(tenantFooter).not.toBeNull();
      expect(tenantFooter?.textContent).toContain('Nova Manufacturing');
      expect(tenantFooter?.textContent).toContain('Supplier RFQ Portal');
      expect(tenantFooter?.textContent).toContain('Discrete Manufacturing');
      expect(tenantFooter?.textContent).not.toContain('Subscription Plans');
    });
  });
});
