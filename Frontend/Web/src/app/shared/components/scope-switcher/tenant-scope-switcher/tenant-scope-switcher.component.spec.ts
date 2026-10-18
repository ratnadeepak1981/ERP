import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TenantScopeSwitcherComponent } from './tenant-scope-switcher.component';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';

describe('TenantScopeSwitcherComponent', () => {
  let component: TenantScopeSwitcherComponent;
  let fixture: ComponentFixture<TenantScopeSwitcherComponent>;
  let scopeService: WorkspaceScopeService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TenantScopeSwitcherComponent],
      providers: [
        WorkspaceScopeService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TenantScopeSwitcherComponent);
    component = fixture.componentInstance;
    scopeService = TestBed.inject(WorkspaceScopeService);
  });

  it('should create scope switcher component', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.scope-header-label')?.textContent).toContain('ACTIVE TENANT SCOPE');
  });

  it('should toggle company and branch menus', () => {
    expect(component.companyMenuOpen()).toBe(false);
    component.toggleCompanyMenu();
    expect(component.companyMenuOpen()).toBe(true);

    component.toggleCompanyMenu();
    expect(component.companyMenuOpen()).toBe(false);
  });

  it('should display selected company name when company is set', () => {
    scopeService.companies.set([
      { id: 'c1', tenantId: 't1', name: 'ABC Trading (Pvt) Ltd', code: 'ABC', isActive: true }
    ]);
    scopeService.selectedCompany.set(scopeService.companies()[0]);

    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('ABC Trading (Pvt) Ltd');
  });
});
