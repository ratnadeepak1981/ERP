import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { PlatformStaffComponent } from './platform-staff.component';

describe('PlatformStaffComponent', () => {
  let component: PlatformStaffComponent;
  let fixture: ComponentFixture<PlatformStaffComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PlatformStaffComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PlatformStaffComponent);
    component = fixture.componentInstance;
  });

  it('should create platform staff component', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h2')?.textContent).toContain('Platform Staff Workspace');
  });
});
