import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TenantsComponent } from './tenants.component';

describe('TenantsComponent', () => {
  let component: TenantsComponent;
  let fixture: ComponentFixture<TenantsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TenantsComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(TenantsComponent);
    component = fixture.componentInstance;
  });

  it('should create tenants component and render tenant rows', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
    expect(component.tenants().length).toBeGreaterThan(0);
  });
});
