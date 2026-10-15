import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppStatusBadgeComponent } from './status-badge.component';

describe('AppStatusBadgeComponent', () => {
  let component: AppStatusBadgeComponent;
  let fixture: ComponentFixture<AppStatusBadgeComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppStatusBadgeComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(AppStatusBadgeComponent);
    component = fixture.componentInstance;
  });

  it('should create and render label', () => {
    fixture.componentRef.setInput('label', 'Active');
    fixture.componentRef.setInput('severity', 'success');
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.badge-label')?.textContent).toContain('Active');
    expect(compiled.querySelector('.erp-badge')?.classList.contains('severity-success')).toBe(true);
  });

  it('should render warning badge', () => {
    fixture.componentRef.setInput('label', 'Pending Approval');
    fixture.componentRef.setInput('severity', 'warning');
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.erp-badge')?.classList.contains('severity-warning')).toBe(true);
  });
});
