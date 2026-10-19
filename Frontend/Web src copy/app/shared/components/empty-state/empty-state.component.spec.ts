import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppEmptyStateComponent } from './empty-state.component';

describe('AppEmptyStateComponent', () => {
  let component: AppEmptyStateComponent;
  let fixture: ComponentFixture<AppEmptyStateComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppEmptyStateComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(AppEmptyStateComponent);
    component = fixture.componentInstance;
  });

  it('should create and render title and description', () => {
    fixture.componentRef.setInput('title', 'No Sales Orders');
    fixture.componentRef.setInput('description', 'Create your first sales order to begin.');
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.empty-title')?.textContent).toContain('No Sales Orders');
    expect(compiled.querySelector('.empty-description')?.textContent).toContain('Create your first sales order');
  });
});
