import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DemonstrationComponent } from './demonstration.component';

describe('DemonstrationComponent', () => {
  let component: DemonstrationComponent;
  let fixture: ComponentFixture<DemonstrationComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DemonstrationComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(DemonstrationComponent);
    component = fixture.componentInstance;
  });

  it('should create and render showcase title', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('UI Component Showcase');
  });

  it('should toggle simulation states', () => {
    expect(component.isSimulatingEmpty()).toBe(false);
    component.toggleEmptySimulation();
    expect(component.isSimulatingEmpty()).toBe(true);
    expect(component.displayedData.length).toBe(0);
    component.toggleEmptySimulation();
    expect(component.isSimulatingEmpty()).toBe(false);
    expect(component.displayedData.length).toBe(8);
  });
});
