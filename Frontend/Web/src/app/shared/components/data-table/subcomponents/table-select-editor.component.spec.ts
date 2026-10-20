import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TableSelectEditorComponent } from './table-select-editor.component';
import { SelectOption } from '../../../models/data-table-config.model';

describe('TableSelectEditorComponent', () => {
  let component: TableSelectEditorComponent;
  let fixture: ComponentFixture<TableSelectEditorComponent>;

  const mockOptions: SelectOption[] = [
    {
      label: 'CBL-001 — Power Cable 2M',
      value: 'CBL-001',
      subtitle: 'Heavy duty copper cable',
      badge: 'Cables',
      rawItem: { code: 'CBL-001', name: 'Power Cable 2M', description: 'Heavy duty copper cable' }
    },
    {
      label: 'ADP-002 — USB-C Adapter',
      value: 'ADP-002',
      subtitle: 'High speed adapter',
      badge: 'Accessories',
      rawItem: { code: 'ADP-002', name: 'USB-C Adapter', description: 'High speed adapter' }
    },
    {
      label: 'WID-003 — Standard Widget',
      value: 'WID-003',
      subtitle: 'Standard factory widget',
      badge: 'Components',
      rawItem: { code: 'WID-003', name: 'Standard Widget', description: 'Standard factory widget' }
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TableSelectEditorComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(TableSelectEditorComponent);
    component = fixture.componentInstance;
    component.options = [...mockOptions];
    fixture.detectChanges();
  });

  it('should create the select editor component', () => {
    expect(component).toBeTruthy();
    expect(component.isOpen()).toBe(true);
  });

  it('should filter options by label, subtitle, badge and rawItem fields', () => {
    // Filter by label
    component.onSearchInput('Power');
    expect(component.filteredOptions().length).toBe(1);
    expect(component.filteredOptions()[0].value).toBe('CBL-001');

    // Filter by subtitle/description
    component.onSearchInput('copper');
    expect(component.filteredOptions().length).toBe(1);
    expect(component.filteredOptions()[0].value).toBe('CBL-001');

    // Filter by badge
    component.onSearchInput('Accessories');
    expect(component.filteredOptions().length).toBe(1);
    expect(component.filteredOptions()[0].value).toBe('ADP-002');

    // Clear search
    component.clearSearch();
    expect(component.filteredOptions().length).toBe(3);
    expect(component.searchTerm()).toBe('');
  });

  it('should not automatically commit or highlight the first option on typing', () => {
    component.onSearchInput('Adapter');
    // activeIndex must be -1 (distinct state: filtered but not highlighted)
    expect(component.activeIndex()).toBe(-1);
    expect(component.value).toBeNull();
  });

  it('should navigate highlighted options with ArrowDown and ArrowUp', () => {
    component.onSearchInput('');
    expect(component.activeIndex()).toBe(-1);

    // ArrowDown moves to 0
    component.onInputKeyDown(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    expect(component.activeIndex()).toBe(0);

    // ArrowDown moves to 1
    component.onInputKeyDown(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    expect(component.activeIndex()).toBe(1);

    // ArrowUp moves back to 0
    component.onInputKeyDown(new KeyboardEvent('keydown', { key: 'ArrowUp' }));
    expect(component.activeIndex()).toBe(0);
  });

  it('should select and commit highlighted option on Enter', () => {
    let committedValue: any = null;
    let selectedOption: any = null;
    component.commit.subscribe(val => (committedValue = val));
    component.selectOptionEvent.subscribe(opt => (selectedOption = opt));

    // Highlight option 1 (ADP-002)
    component.activeIndex.set(1);
    component.onInputKeyDown(new KeyboardEvent('keydown', { key: 'Enter' }));

    expect(committedValue).toBe('ADP-002');
    expect(selectedOption?.value).toBe('ADP-002');
    expect(component.isOpen()).toBe(false);
  });

  it('should not arbitrarily select the first option on Enter when no option is highlighted', () => {
    let committedValue: any = null;
    component.commit.subscribe(val => (committedValue = val));

    // activeIndex is -1 (no option highlighted)
    component.activeIndex.set(-1);
    component.onInputKeyDown(new KeyboardEvent('keydown', { key: 'Enter' }));

    // Committed value must remain current value (null), not ADP or CBL
    expect(committedValue).toBeNull();
  });

  it('should commit highlighted option on Tab and emit tabOut with selectedOption', () => {
    let tabOutEvent: any = null;
    component.tabOut.subscribe(e => (tabOutEvent = e));

    // Highlight option 2 (WID-003)
    component.activeIndex.set(2);
    component.onInputKeyDown(new KeyboardEvent('keydown', { key: 'Tab' }));

    expect(tabOutEvent).toBeTruthy();
    expect(tabOutEvent.selectedOption?.value).toBe('WID-003');
    expect(tabOutEvent.shiftKey).toBe(false);
    expect(component.value).toBe('WID-003');
    expect(component.isOpen()).toBe(false);
  });

  it('should not arbitrarily commit first option on Tab when no option is highlighted', () => {
    let tabOutEvent: any = null;
    component.tabOut.subscribe(e => (tabOutEvent = e));

    component.activeIndex.set(-1);
    component.onInputKeyDown(new KeyboardEvent('keydown', { key: 'Tab' }));

    expect(tabOutEvent).toBeTruthy();
    expect(tabOutEvent.selectedOption).toBeUndefined();
    expect(component.value).toBeNull();
  });

  it('should select and commit option when clicked with mouse', () => {
    let committedValue: any = null;
    component.commit.subscribe(val => (committedValue = val));

    component.selectOption(mockOptions[0]);

    expect(committedValue).toBe('CBL-001');
    expect(component.value).toBe('CBL-001');
    expect(component.isOpen()).toBe(false);
  });

  it('should close popup on first Escape and cancel on second Escape', () => {
    let cancelCalled = false;
    component.cancel.subscribe(() => (cancelCalled = true));

    expect(component.isOpen()).toBe(true);

    // 1st Escape: closes popup without cancelling
    component.onInputKeyDown(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(component.isOpen()).toBe(false);
    expect(cancelCalled).toBe(false);

    // 2nd Escape: cancels editing
    component.onInputKeyDown(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(cancelCalled).toBe(true);
  });

  it('should adapt panel minimum width to container cell width', () => {
    component.adjustPosition();
    expect(component.panelMinWidth()).toBeDefined();
    expect(parseInt(component.panelMinWidth(), 10)).toBeGreaterThanOrEqual(220);
  });

  describe('Keyboard Navigation & Auto-Match Regression Tests', () => {
    it('should automatically match and commit single exact match by code on Enter without arrow keys', () => {
      let committedValue: any = null;
      component.commit.subscribe(val => (committedValue = val));

      component.onSearchInput('CBL-001');
      expect(component.activeIndex()).toBe(-1);

      component.onInputKeyDown(new KeyboardEvent('keydown', { key: 'Enter' }));

      expect(committedValue).toBe('CBL-001');
      expect(component.value).toBe('CBL-001');
      expect(component.isOpen()).toBe(false);
    });

    it('should automatically match and commit single exact match by code on Tab without arrow keys', () => {
      let tabOutEvent: any = null;
      component.tabOut.subscribe(e => (tabOutEvent = e));

      component.onSearchInput('ADP-002');
      expect(component.activeIndex()).toBe(-1);

      component.onInputKeyDown(new KeyboardEvent('keydown', { key: 'Tab' }));

      expect(tabOutEvent).toBeTruthy();
      expect(tabOutEvent.selectedOption?.value).toBe('ADP-002');
      expect(component.value).toBe('ADP-002');
      expect(component.isOpen()).toBe(false);
    });

    it('should reject ambiguous search terms on Enter without arrow keys', () => {
      let committedValue: any = null;
      component.commit.subscribe(val => (committedValue = val));

      // 'a' matches multiple options (ADP-002, Cable, Standard Widget)
      component.onSearchInput('a');
      expect(component.filteredOptions().length).toBeGreaterThan(1);
      expect(component.activeIndex()).toBe(-1);

      component.onInputKeyDown(new KeyboardEvent('keydown', { key: 'Enter' }));

      expect(committedValue).toBeNull();
      expect(component.value).toBeNull();
    });

    it('should reject non-matching search terms on Enter and keep editor open or uncommitted', () => {
      let committedValue: any = null;
      component.commit.subscribe(val => (committedValue = val));

      component.onSearchInput('NON-EXISTENT-CODE');
      expect(component.filteredOptions().length).toBe(0);
      expect(component.activeIndex()).toBe(-1);

      component.onInputKeyDown(new KeyboardEvent('keydown', { key: 'Enter' }));

      expect(committedValue).toBeNull();
      expect(component.value).toBeNull();
    });
  });
});
