import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { MasterDataOverviewComponent } from './master-data-overview.component';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { TenantSetupService } from '../../../../core/services/tenant-setup.service';

describe('MasterDataOverviewComponent', () => {
  let component: MasterDataOverviewComponent;
  let fixture: ComponentFixture<MasterDataOverviewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MasterDataOverviewComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        WorkspaceScopeService,
        TenantSetupService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(MasterDataOverviewComponent);
    component = fixture.componentInstance;
  });

  it('should create and render all master data cards from target architecture', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
    expect(component.masterCards.length).toBe(14);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.banner-text h2')?.textContent).toContain('Supply Chain Masters');
    expect(compiled.textContent).toContain('Product Master');
    expect(compiled.textContent).toContain('Customer Master');
    expect(compiled.textContent).toContain('Supplier Master');
    expect(compiled.textContent).toContain('Units of Measure');
    expect(compiled.textContent).toContain('Warehouse & Locations');
  });

  it('should toggle master data setup panel inline when button is clicked', () => {
    fixture.detectChanges();
    expect(component.showSetupPanel()).toBe(false);
    component.toggleSetupPanel();
    expect(component.showSetupPanel()).toBe(true);
    expect(component.selectedProfileCode).toBe('retail');
    expect(component.availableProfiles[0].code).toBe('retail');
    component.closeSetupDialog();
    expect(component.showSetupPanel()).toBe(false);
  });
});
