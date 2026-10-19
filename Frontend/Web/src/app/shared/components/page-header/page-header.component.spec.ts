import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppPageHeaderComponent } from './page-header.component';

describe('AppPageHeaderComponent', () => {
  let component: AppPageHeaderComponent;
  let fixture: ComponentFixture<AppPageHeaderComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppPageHeaderComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(AppPageHeaderComponent);
    component = fixture.componentInstance;
  });

  it('should create and render title and subtitle', () => {
    fixture.componentRef.setInput('title', 'Sales Orders');
    fixture.componentRef.setInput('subtitle', 'Manage customer orders and fulfillment');
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.page-title')?.textContent).toContain('Sales Orders');
    expect(compiled.querySelector('.page-subtitle')?.textContent).toContain('Manage customer orders');
  });
});
