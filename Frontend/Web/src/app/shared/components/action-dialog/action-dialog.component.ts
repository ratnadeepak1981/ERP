// ActionDialogComponent - Reusable Fluent‑inspired action dialog
// This component abstracts the confirm/reject/cancel dialogs used across the app.

import { Component, Input, Output, EventEmitter } from '@angular/core';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
// import { InputTextareaModule } from 'primeng/inputtextarea';

@Component({
  selector: 'app-action-dialog',
  standalone: true,
  imports: [DialogModule, ButtonModule],
  template: `
    <p-dialog
      [(visible)]="visible"
      [modal]="true"
      [draggable]="false"
      [resizable]="false"
      [header]="title"
      [style]="{ width: dialogWidth, maxWidth: '94vw' }"
      [closable]="!loading"
    >
      <ng-content></ng-content>
      <ng-template #footer pTemplate="footer">
        <div class="dialog-actions-container">
          <p-button
            label="{{ cancelLabel }}"
            icon="pi pi-times"
            [outlined]="true"
            severity="secondary"
            size="small"
            (onClick)="handleCancel()"
            [disabled]="loading"
          ></p-button>
          <p-button
            [label]="confirmLabel"
            [icon]="confirmIcon"
            [severity]="confirmSeverity"
            size="small"
            (onClick)="handleConfirm()"
            [loading]="loading"
          ></p-button>
        </div>
      </ng-template>
    </p-dialog>
  `,
  styleUrls: ['./action-dialog.component.scss']
})
export class ActionDialogComponent {
  @Input() visible: boolean = false;
  @Output() visibleChange = new EventEmitter<boolean>();

  @Input() title: string = '';
  @Input() dialogWidth: string = '480px';
  @Input() loading: boolean = false;

  @Input() cancelLabel: string = 'Cancel';
  @Input() confirmLabel: string = 'Confirm';
  @Input() confirmIcon: string = 'pi pi-check';
  @Input() confirmSeverity: 'success' | 'danger' | 'secondary' = 'success';

  @Output() onCancel = new EventEmitter<void>();
  @Output() onConfirm = new EventEmitter<void>();

  // Emitters also update two‑way binding for `visible` when the dialog is closed via the cancel button.
  handleCancel(): void {
    this.onCancel.emit();
    this.visible = false;
    this.visibleChange.emit(this.visible);
  }

  handleConfirm(): void {
    this.onConfirm.emit();
  }
}
