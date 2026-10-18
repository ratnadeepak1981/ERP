import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TableViewSwitcherComponent, TableViewMode } from './table-view-switcher.component';
import { By } from '@angular/platform-browser';

describe('TableViewSwitcherComponent', () => {
  let component: TableViewSwitcherComponent;
  let fixture: ComponentFixture<TableViewSwitcherComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TableViewSwitcherComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(TableViewSwitcherComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the component', () => {
    expect(component).toBeTruthy();
  });

  it('should default to table view mode and render 3 buttons by default', () => {
    expect(component.view).toBe('table');
    const buttons = fixture.debugElement.queryAll(By.css('button.view-btn'));
    expect(buttons.length).toBe(3);
  });

  it('should mark the active button with active class and aria-pressed="true"', () => {
    component.view = 'table';
    fixture.detectChanges();

    const tableBtn = fixture.debugElement.query(By.css('[data-testid="view-btn-table"]'));
    const listBtn = fixture.debugElement.query(By.css('[data-testid="view-btn-list"]'));
    const cardBtn = fixture.debugElement.query(By.css('[data-testid="view-btn-card"]'));

    expect(tableBtn.nativeElement.classList.contains('active')).toBe(true);
    expect(tableBtn.attributes['aria-pressed']).toBe('true');
    expect(listBtn.nativeElement.classList.contains('active')).toBe(false);
    expect(listBtn.attributes['aria-pressed']).toBe('false');
    expect(cardBtn.nativeElement.classList.contains('active')).toBe(false);
  });

  it('should emit viewChange when clicking an inactive mode button', () => {
    const spy = vi.spyOn(component.viewChange, 'emit');

    const listBtn = fixture.debugElement.query(By.css('[data-testid="view-btn-list"]'));
    listBtn.nativeElement.click();

    expect(spy).toHaveBeenCalledWith('list');
    expect(component.view).toBe('list');
  });

  it('should not emit viewChange when clicking the already active mode', () => {
    component.view = 'table';
    fixture.detectChanges();

    const spy = vi.spyOn(component.viewChange, 'emit');
    const tableBtn = fixture.debugElement.query(By.css('[data-testid="view-btn-table"]'));
    tableBtn.nativeElement.click();

    expect(spy).not.toHaveBeenCalled();
  });

  it('should only render available modes when availableViews is restricted', () => {
    fixture.componentRef.setInput('availableViews', ['table', 'card']);
    fixture.detectChanges();

    const buttons = fixture.debugElement.queryAll(By.css('button.view-btn'));
    expect(buttons.length).toBe(2);

    const tableBtn = fixture.debugElement.query(By.css('[data-testid="view-btn-table"]'));
    const listBtn = fixture.debugElement.query(By.css('[data-testid="view-btn-list"]'));
    const cardBtn = fixture.debugElement.query(By.css('[data-testid="view-btn-card"]'));

    expect(tableBtn).toBeTruthy();
    expect(listBtn).toBeFalsy();
    expect(cardBtn).toBeTruthy();
  });

  it('should have accessible labels and tooltips on every mode button', () => {
    const tableBtn = fixture.debugElement.query(By.css('[data-testid="view-btn-table"]'));
    const listBtn = fixture.debugElement.query(By.css('[data-testid="view-btn-list"]'));
    const cardBtn = fixture.debugElement.query(By.css('[data-testid="view-btn-card"]'));

    expect(tableBtn.attributes['aria-label']).toBe('Table view');
    expect(listBtn.attributes['aria-label']).toBe('List view');
    expect(cardBtn.attributes['aria-label']).toBe('Cards view');
  });
});
