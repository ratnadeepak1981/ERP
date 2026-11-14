import { ComponentFixture, TestBed } from '@angular/core/testing';
import {
  ConfirmationDialogComponent,
  ConfirmationDialogConfig,
  ConfirmationDialogResult
} from './confirmation-dialog.component';

describe('ConfirmationDialogComponent', () => {
  let component: ConfirmationDialogComponent;
  let fixture: ComponentFixture<ConfirmationDialogComponent>;

  const baseConfig: ConfirmationDialogConfig = {
    action: 'approve',
    title: 'Approve Requisition',
    message: 'Are you sure you want to approve this requisition?',
    subMessage: 'Requisition total: $1,500.00',
    commentLabel: 'Approval Remarks',
    commentPlaceholder: 'Enter approval comments...',
    commentRequired: false,
    confirmLabel: 'Confirm Approval',
    confirmIcon: 'pi pi-check',
    confirmSeverity: 'success',
    cancelLabel: 'Cancel'
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ConfirmationDialogComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(ConfirmationDialogComponent);
    component = fixture.componentInstance;
    component.config = { ...baseConfig };
    component.visible = true;
    fixture.detectChanges();
  });

  it('should create the dialog component', () => {
    expect(component).toBeTruthy();
  });

  it('should render dialog header, title, and prompt message', () => {
    const el: HTMLElement = fixture.nativeElement;
    const title = el.querySelector('#confirmDialogTitle');
    const prompt = el.querySelector('.dialog-prompt');
    const submessage = el.querySelector('.dialog-submessage');

    expect(title?.textContent).toContain('Approve Requisition');
    expect(prompt?.textContent).toContain('Are you sure you want to approve this requisition?');
    expect(submessage?.textContent).toContain('Requisition total: $1,500.00');
  });

  it('should render correct button labels and styling', () => {
    const el: HTMLElement = fixture.nativeElement;
    const cancelBtn = el.querySelector('.erp-btn-secondary');
    const confirmBtn = el.querySelector('.erp-btn-success');

    expect(cancelBtn?.textContent).toContain('Cancel');
    expect(confirmBtn?.textContent).toContain('Confirm Approval');
    expect(confirmBtn?.querySelector('.pi-check')).toBeTruthy();
  });

  it('should allow approval with optional empty comment', () => {
    let emittedResult: ConfirmationDialogResult | null = null;
    component.confirmed.subscribe((res: ConfirmationDialogResult) => {
      emittedResult = res;
    });

    component.comment = '';
    component.handleConfirm();

    expect(emittedResult).toEqual({
      action: 'approve',
      comment: ''
    });
  });

  it('should pass trimmed comment to confirmed output', () => {
    let emittedResult: ConfirmationDialogResult | null = null;
    component.confirmed.subscribe((res: ConfirmationDialogResult) => {
      emittedResult = res;
    });

    component.comment = '   Authorized with priority delivery   ';
    component.handleConfirm();

    expect(emittedResult).toEqual({
      action: 'approve',
      comment: 'Authorized with priority delivery'
    });
  });

  it('should block submission and show validation error if comment is mandatory and empty', () => {
    component.config = {
      ...baseConfig,
      action: 'reject',
      title: 'Reject Requisition',
      commentRequired: true,
      commentLabel: 'Reason for Rejection'
    };
    fixture.detectChanges();

    let confirmedCalled = false;
    component.confirmed.subscribe(() => {
      confirmedCalled = true;
    });

    component.comment = '';
    component.handleConfirm();
    fixture.detectChanges();

    expect(confirmedCalled).toBe(false);
    expect(component.validationError).toBe('Reason for Rejection is required.');

    const el: HTMLElement = fixture.nativeElement;
    const errorText = el.querySelector('.field-error-text');
    expect(errorText?.textContent).toContain('Reason for Rejection is required.');
  });

  it('should reject whitespace-only comment when comment is mandatory', () => {
    component.config = {
      ...baseConfig,
      action: 'reject',
      commentRequired: true,
      commentLabel: 'Rejection Reason'
    };
    fixture.detectChanges();

    let confirmedCalled = false;
    component.confirmed.subscribe(() => {
      confirmedCalled = true;
    });

    component.comment = '    \n\t   ';
    component.handleConfirm();

    expect(confirmedCalled).toBe(false);
    expect(component.validationError).toBe('Rejection Reason is required.');
  });

  it('should emit cancelled event and close when cancel button is clicked', () => {
    let cancelEmitted = false;
    component.cancelled.subscribe(() => {
      cancelEmitted = true;
    });

    component.handleCancel();

    expect(cancelEmitted).toBe(true);
    expect(component.visible).toBe(false);
  });

  it('should disable buttons and show spinner when loading', () => {
    component.loading = true;
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    const cancelBtn = el.querySelector('.erp-btn-secondary') as HTMLButtonElement;
    const confirmBtn = el.querySelector('.erp-btn-success') as HTMLButtonElement;

    expect(cancelBtn.disabled).toBe(true);
    expect(confirmBtn.disabled).toBe(true);
    expect(confirmBtn.querySelector('.pi-spinner')).toBeTruthy();
  });

  it('should display inline error banner when errorMessage is set and preserve comment', () => {
    component.comment = 'Budget overrun note';
    component.errorMessage = 'Requisition failed approval: budget limit exceeded.';
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    const errorBanner = el.querySelector('.modal-error-banner');

    expect(errorBanner).toBeTruthy();
    expect(errorBanner?.textContent).toContain('Requisition failed approval: budget limit exceeded.');
    expect(component.comment).toBe('Budget overrun note');
  });

  it('should handle Escape key to close dialog when not loading', () => {
    let cancelEmitted = false;
    component.cancelled.subscribe(() => {
      cancelEmitted = true;
    });

    const escapeEvent = new KeyboardEvent('keydown', { key: 'Escape' });
    component.onEscapeKey(escapeEvent);

    expect(cancelEmitted).toBe(true);
    expect(component.visible).toBe(false);
  });

  it('should not close on Escape key when loading is in progress', () => {
    component.loading = true;
    let cancelEmitted = false;
    component.cancelled.subscribe(() => {
      cancelEmitted = true;
    });

    const escapeEvent = new KeyboardEvent('keydown', { key: 'Escape' });
    component.onEscapeKey(escapeEvent);

    expect(cancelEmitted).toBe(false);
    expect(component.visible).toBe(true);
  });
});
