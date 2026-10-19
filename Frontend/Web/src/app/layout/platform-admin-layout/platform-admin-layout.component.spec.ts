import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { PlatformAdminLayoutComponent } from './platform-admin-layout.component';

describe('PlatformAdminLayoutComponent', () => {
  let component: PlatformAdminLayoutComponent;
  let fixture: ComponentFixture<PlatformAdminLayoutComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PlatformAdminLayoutComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PlatformAdminLayoutComponent);
    component = fixture.componentInstance;
  });

  it('should create and render Platform Admin brand', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.brand-title')?.textContent).toContain('ApexERP');
    expect(compiled.querySelector('.admin-tag')?.textContent).toContain('Platform Admin');
  });

  it('should toggle sidebar state', () => {
    expect(component.sidebarCollapsed()).toBe(false);
    component.toggleSidebar();
    expect(component.sidebarCollapsed()).toBe(true);
  });
});
