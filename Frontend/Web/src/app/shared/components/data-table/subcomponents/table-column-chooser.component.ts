import {
  Component,
  Input,
  Output,
  EventEmitter,
  ChangeDetectionStrategy,
  signal,
  ElementRef,
  HostListener,
  inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { DataTableColumn } from '../../../models/data-table-config.model';
import { AppCheckboxComponent } from '../../checkbox/checkbox.component';

@Component({
  selector: 'app-table-column-chooser',
  standalone: true,
  imports: [CommonModule, ButtonModule, AppCheckboxComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="column-chooser-wrapper">
      <p-button
        label="Columns"
        icon="pi pi-sliders-h"
        [outlined]="true"
        size="small"
        severity="secondary"
        (onClick)="toggleOverlay($event)"
        aria-haspopup="true"
        [attr.aria-expanded]="isOpen()"
      ></p-button>

      @if (isOpen()) {
        <div class="column-chooser-panel erp-card erp-card-elevated" (click)="$event.stopPropagation()">
          <div class="panel-header">
            <span class="panel-title">Visible Columns</span>
            <button type="button" class="close-btn" (click)="close()">
              <i class="pi pi-times"></i>
            </button>
          </div>

          <div class="panel-body">
            @for (col of manageableColumns(); track col.field) {
              <div class="col-item">
                <app-checkbox
                  [checked]="col.visible !== false"
                  (checkedChange)="onToggleColumn(col, $event)"
                  [label]="col.header"
                  size="small"
                ></app-checkbox>
              </div>
            }
          </div>

          <div class="panel-footer">
            <p-button
              label="Reset"
              size="small"
              [text]="true"
              severity="secondary"
              (onClick)="resetDefaults()"
            ></p-button>
            <p-button
              label="Show All"
              size="small"
              [text]="true"
              (onClick)="showAll()"
            ></p-button>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .column-chooser-wrapper {
      position: relative;
      display: inline-flex;

      .column-chooser-panel {
        position: absolute;
        top: calc(100% + 4px);
        right: 0;
        z-index: 1050;
        width: 220px;
        background: var(--erp-bg-surface, #ffffff);
        border: 1px solid var(--erp-border, #e2e8f0);
        border-radius: var(--erp-radius-md, 6px);
        box-shadow: var(--erp-shadow-md);
        padding: 0;
        text-align: left;

        .panel-header {
          display: flex;
          align-items: center;
          justify-content: space-between;
          padding: 8px 12px;
          border-bottom: 1px solid var(--erp-border, #e2e8f0);

          .panel-title {
            font-size: 12.5px;
            font-weight: 600;
            color: var(--erp-text-main, #0f172a);
          }

          .close-btn {
            background: transparent;
            border: none;
            color: var(--erp-text-muted, #64748b);
            cursor: pointer;
            padding: 2px;
            display: flex;
            align-items: center;

            &:hover {
              color: var(--erp-text-main, #0f172a);
            }
          }
        }

        .panel-body {
          max-height: 220px;
          overflow-y: auto;
          padding: 8px 12px;
          display: flex;
          flex-direction: column;
          gap: 6px;

          .col-item {
            display: flex;
            align-items: center;
          }
        }

        .panel-footer {
          display: flex;
          align-items: center;
          justify-content: space-between;
          padding: 6px 12px;
          background: var(--erp-bg-secondary, #f1f5f9);
          border-top: 1px solid var(--erp-border, #e2e8f0);
          border-bottom-left-radius: var(--erp-radius-md, 6px);
          border-bottom-right-radius: var(--erp-radius-md, 6px);
        }
      }
    }
  `]
})
export class TableColumnChooserComponent {
  private readonly elementRef = inject(ElementRef);

  @Input({ required: true }) columns: DataTableColumn[] = [];
  @Output() columnsChange = new EventEmitter<DataTableColumn[]>();

  readonly isOpen = signal<boolean>(false);

  manageableColumns(): DataTableColumn[] {
    return this.columns.filter(c => c.type !== 'actions');
  }

  toggleOverlay(event: Event): void {
    event.stopPropagation();
    this.isOpen.update(o => !o);
  }

  close(): void {
    this.isOpen.set(false);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.isOpen() && !this.elementRef.nativeElement.contains(event.target)) {
      this.close();
    }
  }

  onToggleColumn(col: DataTableColumn, isVisible: boolean): void {
    col.visible = isVisible;
    this.columnsChange.emit([...this.columns]);
  }

  showAll(): void {
    this.columns.forEach(c => (c.visible = true));
    this.columnsChange.emit([...this.columns]);
  }

  resetDefaults(): void {
    this.columns.forEach(c => (c.visible = true));
    this.columnsChange.emit([...this.columns]);
  }
}
