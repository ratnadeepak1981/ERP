import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppErrorStateComponent } from './error-state.component';

describe('AppErrorStateComponent', () => {
  let component: AppErrorStateComponent;
  let fixture: ComponentFixture<AppErrorStateComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppErrorStateComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(AppErrorStateComponent);
    component = fixture.componentInstance;
  });

  it('should create and render error message', () => {
    fixture.componentRef.setInput('message', 'Network connection timed out');
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.error-message')?.textContent).toContain('Network connection timed out');
  });
});
