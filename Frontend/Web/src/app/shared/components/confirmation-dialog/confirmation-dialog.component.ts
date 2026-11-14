import {
  Component,
  Input,
  Output,
  EventEmitter,
  OnChanges,
  SimpleChanges,
  HostListener,
  ElementRef,
  ViewChild
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export type ConfirmationButtonSeverity = 'success' | 'danger' | 'primary' | 'secondary';

export interface ConfirmationDialogConfig {
  action: string;
  title: string;
  message: string;
  subMessage?: string;
  commentLabel?: string;
  commentPlaceholder?: string;
  commentRequired?: boolean;
  commentMinLength?: number;
  commentRows?: number;
  confirmLabel: string;
  confirmIcon?: string;
  confirmSeverity?: ConfirmationButtonSeverity;
  cancelLabel?: string;
}

export interface ConfirmationDialogResult {
  action: string;
  comment: string;
}

@Component({
  selector: 'app-confirmation-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './confirmation-dialog.component.html',
  styleUrls: ['./confirmation-dialog.component.scss']
})
export class ConfirmationDialogComponent implements OnChanges {
  @Input() visible: boolean = false;
  @Output() visibleChange = new EventEmitter<boolean>();

  @Input() config: ConfirmationDialogConfig | null = null;
  @Input() loading: boolean = false;
  @Input() errorMessage: string | null = null;

  @Output() cancelled = new EventEmitter<void>();
  @Output() confirmed = new EventEmitter<ConfirmationDialogResult>();

  @ViewChild('commentInput') commentInput?: ElementRef<HTMLTextAreaElement>;

  comment: string = '';
  validationError: string | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible']) {
      if (this.visible) {
        // When opening dialog, reset validation errors.
        // We do NOT clear comment if returning from an error state,
        // but if opened freshly without error, reset comment.
        if (!this.errorMessage) {
          this.comment = '';
        }
        this.validationError = null;

        // Auto-focus the textarea on next microtask
        setTimeout(() => {
          this.commentInput?.nativeElement?.focus();
        }, 50);
      } else {
        this.validationError = null;
      }
    }
  }

  @HostListener('keydown.escape', ['$event'])
  onEscapeKey(event: Event): void {
    if (this.visible && !this.loading) {
      event.preventDefault();
      this.handleCancel();
    }
  }

  onCommentChange(): void {
    if (this.validationError) {
      this.validateComment();
    }
  }

  private validateComment(): boolean {
    const trimmed = (this.comment || '').trim();
    if (this.config?.commentRequired && !trimmed) {
      this.validationError = `${this.config.commentLabel || 'Comment'} is required.`;
      return false;
    }
    if (this.config?.commentMinLength && trimmed.length < this.config.commentMinLength) {
      this.validationError = `${this.config.commentLabel || 'Comment'} must be at least ${this.config.commentMinLength} characters.`;
      return false;
    }
    this.validationError = null;
    return true;
  }

  handleCancel(): void {
    if (this.loading) return;
    this.cancelled.emit();
    this.close();
  }

  handleConfirm(): void {
    if (this.loading) return;
    if (!this.validateComment()) return;

    this.confirmed.emit({
      action: this.config?.action || '',
      comment: (this.comment || '').trim()
    });
  }

  close(): void {
    this.visible = false;
    this.visibleChange.emit(false);
  }

  get defaultCommentLabel(): string {
    return this.config?.commentLabel || 'Remarks';
  }

  get defaultPlaceholder(): string {
    return this.config?.commentPlaceholder || 'Enter remarks...';
  }

  get defaultConfirmLabel(): string {
    return this.config?.confirmLabel || 'Confirm';
  }

  get defaultCancelLabel(): string {
    return this.config?.cancelLabel || 'Cancel';
  }

  get confirmButtonClass(): string {
    const sev = this.config?.confirmSeverity || 'primary';
    switch (sev) {
      case 'success':
        return 'erp-btn erp-btn-success';
      case 'danger':
        return 'erp-btn erp-btn-danger';
      case 'secondary':
        return 'erp-btn erp-btn-secondary';
      default:
        return 'erp-btn erp-btn-primary';
    }
  }
}
