import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppLoadingStateComponent } from './loading-state.component';

describe('AppLoadingStateComponent', () => {
  let component: AppLoadingStateComponent;
  let fixture: ComponentFixture<AppLoadingStateComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppLoadingStateComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(AppLoadingStateComponent);
    component = fixture.componentInstance;
  });

  it('should create and render message', () => {
    fixture.componentRef.setInput('message', 'Fetching records...');
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.loading-message')?.textContent).toContain('Fetching records...');
  });
});
