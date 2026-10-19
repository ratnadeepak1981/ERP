import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TenantAdminComponent } from './tenant-admin.component';

describe('TenantAdminComponent', () => {
  let component: TenantAdminComponent;
  let fixture: ComponentFixture<TenantAdminComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TenantAdminComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TenantAdminComponent);
    component = fixture.componentInstance;
  });

  it('should create tenant admin component', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h2')?.textContent).toContain('Tenant Administration Console');
  });
});
