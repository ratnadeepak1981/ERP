import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { StorefrontComponent } from './storefront.component';

describe('StorefrontComponent', () => {
  let component: StorefrontComponent;
  let fixture: ComponentFixture<StorefrontComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StorefrontComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(StorefrontComponent);
    component = fixture.componentInstance;
  });

  it('should create storefront component and render products', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Alpha Manufacturing Storefront');
    expect(component.products().length).toBeGreaterThan(0);
  });
});
