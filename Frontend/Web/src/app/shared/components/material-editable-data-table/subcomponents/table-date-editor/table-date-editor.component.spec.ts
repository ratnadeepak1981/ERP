import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TableDateEditorComponent } from './table-date-editor.component';

describe('TableDateEditorComponent', () => {
  let component: TableDateEditorComponent;
  let fixture: ComponentFixture<TableDateEditorComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TableDateEditorComponent]
    });
    fixture = TestBed.createComponent(TableDateEditorComponent);
    component = fixture.componentInstance;
  });

  it('should instantiate and format ISO date strings into YYYY-MM-DD', () => {
    component.value = '2026-11-15T00:00:00.000Z';
    expect(component.formattedValue).toBe('2026-11-15');
  });

  it('should format Date objects into YYYY-MM-DD', () => {
    component.value = new Date('2026-12-31T00:00:00.000Z');
    expect(component.formattedValue).toBe('2026-12-31');
  });

  it('should return empty string for null, undefined, or empty values', () => {
    component.value = null;
    expect(component.formattedValue).toBe('');
    component.value = undefined;
    expect(component.formattedValue).toBe('');
    component.value = '';
    expect(component.formattedValue).toBe('');
  });

  it('should emit valueChange with sanitized date string on change', () => {
    let emitted = '';
    component.valueChange.subscribe(v => (emitted = v));
    component.onDateChange('2027-05-20');
    expect(emitted).toBe('2027-05-20');
  });
});
