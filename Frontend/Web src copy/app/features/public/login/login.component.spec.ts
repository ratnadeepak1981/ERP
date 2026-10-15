import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { LoginComponent } from './login.component';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
  });

  it('should create login component', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h2')?.textContent).toContain('Tenant Sign In');
  });

  it('should allow quick fill of test credentials', () => {
    component.onQuickFill('admin@platform.local', '123');
    expect(component.email).toBe('admin@platform.local');
    expect(component.password).toBe('123');
  });

  it('should display error when submitting empty fields', () => {
    component.email = '';
    component.password = '';
    component.onLogin();
    expect(component.errorMessage()).toContain('Please provide both email and password');
  });
});
