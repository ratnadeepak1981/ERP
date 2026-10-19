import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppLookupDropdownComponent } from './lookup-dropdown.component';
import { LookupItem } from './lookup-item.model';

describe('AppLookupDropdownComponent', () => {
  let component: AppLookupDropdownComponent;
  let fixture: ComponentFixture<AppLookupDropdownComponent>;

  const mockItems: LookupItem[] = [
    { id: 1, code: 'RAW-001', name: 'Steel Sheet', category: 'Raw Materials', isEligible: true },
    { id: 2, code: 'RAW-002', name: 'Aluminium Rod', category: 'Raw Materials', isEligible: true },
    { id: 3, code: 'FIN-001', name: 'Finished Enclosure', category: 'Finished Goods', isEligible: false },
    { id: 4, code: 'SVC-001', name: 'Maintenance Service', category: 'Services', isEligible: true }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppLookupDropdownComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(AppLookupDropdownComponent);
    component = fixture.componentInstance;
    component.items = mockItems;
    fixture.detectChanges();
  });

  it('should create the lookup dropdown component', () => {
    expect(component).toBeTruthy();
  });

  it('should display placeholder when no item is selected', () => {
    component.placeholder = 'Select a product...';
    fixture.detectChanges();
    expect(component.displayText()).toBe('');
  });

  it('should filter items by search term (code or name)', () => {
    component.searchTerm.set('Steel');
    const filtered = component.filteredItems();
    expect(filtered.length).toBe(1);
    expect(filtered[0].code).toBe('RAW-001');
  });

  it('should select first matching item when Tab key is pressed during halfway typing', () => {
    component.open();
    component.onSearchChange('Alum'); // Halfway typed

    const selectSpy = vi.spyOn(component.selectItem, 'emit');
    const tabEvent = new KeyboardEvent('keydown', { key: 'Tab' });
    component.onSearchInputKeyDown(tabEvent);

    expect(component.selectedValue()).toBe('RAW-002');
    expect(selectSpy).toHaveBeenCalledWith(mockItems[1]);
    expect(component.isOpen()).toBe(false);
  });

  it('should navigate options using ArrowDown and ArrowUp', () => {
    component.open();
    expect(component.activeIndex()).toBe(0);

    const downEvent = new KeyboardEvent('keydown', { key: 'ArrowDown' });
    component.onSearchInputKeyDown(downEvent);
    expect(component.activeIndex()).toBe(1);

    const upEvent = new KeyboardEvent('keydown', { key: 'ArrowUp' });
    component.onSearchInputKeyDown(upEvent);
    expect(component.activeIndex()).toBe(0);
  });

  it('should filter items by category', () => {
    component.categoryFilter = 'Finished Goods';
    const filtered = component.filteredItems();
    expect(filtered.length).toBe(1);
    expect(filtered[0].code).toBe('FIN-001');
  });

  it('should filter out ineligible items when onlyEligible is true', () => {
    component.onlyEligible = true;
    const filtered = component.filteredItems();
    expect(filtered.every(i => i.isEligible !== false)).toBe(true);
    expect(filtered.some(i => i.code === 'FIN-001')).toBe(false);
  });

  it('should select item on selection and emit value', () => {
    const selectSpy = vi.spyOn(component.selectItem, 'emit');
    const valueSpy = vi.spyOn(component.valueChange, 'emit');

    component.select(mockItems[0]);

    expect(component.selectedValue()).toBe('RAW-001');
    expect(selectSpy).toHaveBeenCalledWith(mockItems[0]);
    expect(valueSpy).toHaveBeenCalledWith('RAW-001');
    expect(component.isOpen()).toBe(false);
  });

  it('should clear selection when onClear is called', () => {
    component.selectedValue.set('RAW-001');
    const clearSpy = vi.spyOn(component.clear, 'emit');
    const valueSpy = vi.spyOn(component.valueChange, 'emit');

    const fakeEvent = new MouseEvent('click');
    component.onClear(fakeEvent);

    expect(component.selectedValue()).toBeNull();
    expect(clearSpy).toHaveBeenCalled();
    expect(valueSpy).toHaveBeenCalledWith(null);
  });
});
