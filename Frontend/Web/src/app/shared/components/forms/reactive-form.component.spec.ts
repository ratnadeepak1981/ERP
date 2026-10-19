import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AppReactiveFormComponent } from './reactive-form.component';
import { FormFieldConfig } from './form-field-config.model';

describe('AppReactiveFormComponent', () => {
  let component: AppReactiveFormComponent;
  let fixture: ComponentFixture<AppReactiveFormComponent>;
  let form: FormGroup;

  const mockFields: FormFieldConfig[] = [
    {
      name: 'orderNumber',
      label: 'PO Number',
      type: 'text',
      placeholder: 'Auto-generated or enter PO number',
      required: true
    },
    {
      name: 'supplierId',
      label: 'Supplier',
      type: 'select',
      required: true,
      options: [
        { label: 'Northern Supplies', value: 'sup-1' },
        { label: 'City Wholesale', value: 'sup-2' }
      ]
    },
    {
      name: 'orderDate',
      label: 'Order Date',
      type: 'date',
      required: true
    },
    {
      name: 'notes',
      label: 'Instructions / Notes',
      type: 'textarea',
      hint: 'Special delivery instructions'
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppReactiveFormComponent, ReactiveFormsModule]
    }).compileComponents();

    fixture = TestBed.createComponent(AppReactiveFormComponent);
    component = fixture.componentInstance;

    form = new FormGroup({
      orderNumber: new FormControl('', [Validators.required]),
      supplierId: new FormControl('', [Validators.required]),
      orderDate: new FormControl('2026-10-18', [Validators.required]),
      notes: new FormControl('')
    });

    component.form = form;
    component.fields = mockFields;
    fixture.detectChanges();
  });

  it('should create reactive form component and render field groups', () => {
    expect(component).toBeTruthy();
    const compiled = fixture.nativeElement as HTMLElement;
    const fields = compiled.querySelectorAll('.form-field-group');
    expect(fields.length).toBe(4);
    expect(compiled.textContent).toContain('PO Number');
    expect(compiled.textContent).toContain('Supplier');
    expect(compiled.textContent).toContain('Order Date');
    expect(compiled.textContent).toContain('Instructions / Notes');
  });

  it('should display required asterisk for required fields', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const orderNumField = compiled.querySelector('[data-testid="form-field-orderNumber"]');
    expect(orderNumField?.querySelector('.required-star')).toBeTruthy();

    const notesField = compiled.querySelector('[data-testid="form-field-notes"]');
    expect(notesField?.querySelector('.required-star')).toBeFalsy();
  });

  it('should show field validation error message when touched and invalid', () => {
    form.get('orderNumber')?.setValue('');
    form.get('orderNumber')?.markAsTouched();
    fixture.detectChanges();

    expect(component.isFieldInvalid('orderNumber')).toBe(true);
    expect(component.getFieldError('orderNumber')).toBe('This field is required.');

    const errorEl = fixture.nativeElement.querySelector('[data-testid="error-orderNumber"]');
    expect(errorEl).toBeTruthy();
    expect(errorEl.textContent).toContain('This field is required.');
  });

  it('should show server-side validation error message when provided', () => {
    component.serverErrors = {
      orderNumber: 'PO Number already exists in the selected operating company.'
    };
    fixture.detectChanges();

    expect(component.isFieldInvalid('orderNumber')).toBe(true);
    expect(component.getFieldError('orderNumber')).toBe(
      'PO Number already exists in the selected operating company.'
    );

    const errorEl = fixture.nativeElement.querySelector('[data-testid="error-orderNumber"]');
    expect(errorEl).toBeTruthy();
    expect(errorEl.textContent).toContain('PO Number already exists in the selected operating company.');
  });

  it('should respect readonly mode by rendering readonly inputs and hiding required stars', () => {
    component.readonly = true;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const textInput = compiled.querySelector('#input-orderNumber') as HTMLInputElement;
    expect(textInput.readOnly).toBe(true);

    const requiredStars = compiled.querySelectorAll('.required-star');
    expect(requiredStars.length).toBe(0);
  });
});
