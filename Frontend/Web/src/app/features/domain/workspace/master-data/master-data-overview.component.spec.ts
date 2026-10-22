import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { MasterDataOverviewComponent } from './master-data-overview.component';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';

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
        WorkspaceScopeService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(MasterDataOverviewComponent);
    component = fixture.componentInstance;
  });

  it('should create and render all master data cards from target architecture', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
    expect(component.masterCards.length).toBe(9);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.banner-text h2')?.textContent).toContain('Supply Chain Masters');
    expect(compiled.textContent).toContain('Product Master');
    expect(compiled.textContent).toContain('Customer Master');
    expect(compiled.textContent).toContain('Supplier Master');
    expect(compiled.textContent).toContain('Units of Measure');
    expect(compiled.textContent).toContain('Warehouse & Locations');
  });
});
