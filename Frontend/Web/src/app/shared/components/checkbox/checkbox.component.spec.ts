import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppCheckboxComponent } from './checkbox.component';
import { Component } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';

@Component({
  standalone: true,
  imports: [AppCheckboxComponent, ReactiveFormsModule],
  template: `<app-checkbox [formControl]="control" label="Agree to terms"></app-checkbox>`
})
class TestReactiveCheckboxHostComponent {
  control = new FormControl(false);
}

describe('AppCheckboxComponent', () => {
  let component: AppCheckboxComponent;
  let fixture: ComponentFixture<AppCheckboxComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppCheckboxComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(AppCheckboxComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the checkbox component with default settings', () => {
    expect(component).toBeTruthy();
    expect(component.checked).toBe(false);
    expect(component.shape).toBe('rounded-square');
    expect(component.size).toBe('standard');
    expect(component.colorToken).toBe('primary');
  });

  it('should toggle checked state on click and emit event', () => {
    const spy = vi.fn();
    component.checkedChange.subscribe(spy);

    component.toggle();
    expect(component.checked).toBe(true);
    expect(spy).toHaveBeenCalledWith(true);

    component.toggle();
    expect(component.checked).toBe(false);
    expect(spy).toHaveBeenCalledWith(false);
  });

  it('should not toggle when disabled', () => {
    component.disabled = true;
    component.toggle();
    expect(component.checked).toBe(false);
  });

  it('should not toggle when readonly', () => {
    component.readonly = true;
    component.toggle();
    expect(component.checked).toBe(false);
  });

  it('should transition from indeterminate to checked on toggle', () => {
    component.indeterminate = true;
    const checkedSpy = vi.fn();
    const indetSpy = vi.fn();
    component.checkedChange.subscribe(checkedSpy);
    component.indeterminateChange.subscribe(indetSpy);

    component.toggle();
    expect(component.indeterminate).toBe(false);
    expect(component.checked).toBe(true);
    expect(indetSpy).toHaveBeenCalledWith(false);
    expect(checkedSpy).toHaveBeenCalledWith(true);
  });

  it('should respond to Space and Enter key events', () => {
    const toggleSpy = vi.spyOn(component, 'toggle');
    const spaceEvent = new KeyboardEvent('keydown', { key: ' ' });
    component.onKeyDown(spaceEvent);
    expect(toggleSpy).toHaveBeenCalled();

    const enterEvent = new KeyboardEvent('keydown', { key: 'Enter' });
    component.onKeyDown(enterEvent);
    expect(toggleSpy).toHaveBeenCalledTimes(2);
  });

  it('should accept bulk configuration via config input', () => {
    component.config = {
      shape: 'square',
      size: 'large',
      colorToken: 'success',
      label: 'Accept Invoice',
      disabled: true
    };

    expect(component.shape).toBe('square');
    expect(component.size).toBe('large');
    expect(component.colorToken).toBe('success');
    expect(component.label).toBe('Accept Invoice');
    expect(component.disabled).toBe(true);
  });

  it('should support compact and regular sizes and warning color token', () => {
    fixture.componentRef.setInput('size', 'compact');
    fixture.componentRef.setInput('colorToken', 'warning');
    fixture.detectChanges();

    const wrapperEl = fixture.nativeElement.querySelector('.erp-checkbox-wrapper');
    const boxEl = fixture.nativeElement.querySelector('.erp-checkbox-box');

    expect(wrapperEl.classList.contains('size-small')).toBe(true);
    expect(boxEl.classList.contains('color-warning')).toBe(true);

    fixture.componentRef.setInput('size', 'regular');
    fixture.detectChanges();
    expect(wrapperEl.classList.contains('size-standard')).toBe(true);
  });
});

describe('AppCheckboxComponent Reactive Form Integration', () => {
  let fixture: ComponentFixture<TestReactiveCheckboxHostComponent>;
  let host: TestReactiveCheckboxHostComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TestReactiveCheckboxHostComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(TestReactiveCheckboxHostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should synchronize with reactive form control', () => {
    expect(host.control.value).toBe(false);

    host.control.setValue(true);
    fixture.detectChanges();

    const checkboxEl = fixture.nativeElement.querySelector('.erp-checkbox-box');
    expect(checkboxEl.classList.contains('checked')).toBe(true);
  });
});
