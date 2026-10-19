import { Component, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormControl, FormGroup, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { AppCheckboxComponent } from '../../../../../shared/components/checkbox/checkbox.component';
import { ThemeService } from '../../../../../core/services/theme.service';
import {
  CheckboxShape,
  CheckboxSize,
  CheckboxColorToken
} from '../../../../../shared/models/checkbox-config.model';

@Component({
  selector: 'app-checkbox-playground-section',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, ButtonModule, AppCheckboxComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="checkbox-playground-container">
      <!-- 1. Dedicated Fluent Checkbox Showcase (Matching Design Reference) -->
      <div class="erp-card fluent-showcase-card">
        <div class="showcase-header">
          <div>
            <div class="title-with-pill">
              <h3 class="main-title">Checkbox Components</h3>
              <span class="fluent-pill">Fluent-inspired</span>
            </div>
            <p class="subtitle">Reusable checkbox component &middot; Consistent across all ERP modules</p>
          </div>
          <div class="theme-btn-box">
            <p-button
              [label]="themeService.currentTheme() === 'clean-enterprise' ? 'Fluent Dark Mode' : 'Clean Enterprise Light'"
              [icon]="themeService.currentTheme() === 'clean-enterprise' ? 'pi pi-moon' : 'pi pi-sun'"
              [outlined]="true"
              size="small"
              (onClick)="themeService.toggleTheme()"
            ></p-button>
          </div>
        </div>

        <!-- 1. Checkbox states -->
        <div class="showcase-block">
          <h4 class="block-title">1. Checkbox states</h4>
          <div class="states-row">
            <app-checkbox [checked]="true" label="Checked" size="regular"></app-checkbox>
            <app-checkbox [checked]="false" label="Unchecked" size="regular"></app-checkbox>
            <app-checkbox [indeterminate]="true" label="Indeterminate" size="regular"></app-checkbox>
            <app-checkbox [checked]="false" [disabled]="true" label="Disabled" size="regular"></app-checkbox>
          </div>
        </div>

        <!-- 2. Shape options -->
        <div class="showcase-block">
          <h4 class="block-title">2. Shape options</h4>
          <div class="radio-shape-group">
            <label class="fluent-radio-option">
              <input
                type="radio"
                name="shapeSelection"
                value="rounded-square"
                [checked]="shapeChoice() === 'rounded-square'"
                (change)="shapeChoice.set('rounded-square')"
              />
              <span>Rounded square &mdash; recommended for ApexERP</span>
            </label>
            <label class="fluent-radio-option">
              <input
                type="radio"
                name="shapeSelection"
                value="square"
                [checked]="shapeChoice() === 'square'"
                (change)="shapeChoice.set('square')"
              />
              <span>Square &mdash; sharper, traditional enterprise look</span>
            </label>
          </div>
          <div class="shape-example-box">
            <app-checkbox
              [shape]="shapeChoice()"
              [checked]="true"
              label="Selected example"
              size="regular"
            ></app-checkbox>
          </div>
        </div>

        <!-- 3. Sizes -->
        <div class="showcase-block">
          <h4 class="block-title">3. Sizes</h4>
          <div class="sizes-row">
            <app-checkbox size="compact" [checked]="true" label="Compact"></app-checkbox>
            <app-checkbox size="regular" [checked]="true" label="Regular"></app-checkbox>
            <app-checkbox size="large" [checked]="true" label="Large"></app-checkbox>
          </div>
        </div>

        <!-- 4. ERP form examples -->
        <div class="showcase-block">
          <h4 class="block-title">4. ERP form examples</h4>
          <div class="form-examples-card">
            <div class="form-checks-stack">
              <app-checkbox
                [checked]="enableNotifications()"
                (checkedChange)="enableNotifications.set($event)"
                label="Enable notifications"
                size="regular"
              ></app-checkbox>

              <app-checkbox
                [checked]="includeInactive()"
                (checkedChange)="includeInactive.set($event)"
                label="Include inactive products"
                size="regular"
              ></app-checkbox>

              <app-checkbox
                [checked]="confirmOrderDetails()"
                (checkedChange)="confirmOrderDetails.set($event)"
                label="I confirm the purchase order details are correct"
                size="regular"
              ></app-checkbox>
            </div>
            <p class="form-instruction-text">
              Select the confirmation checkbox before submitting the purchase order.
            </p>
          </div>
        </div>

        <!-- 5. Table selection -->
        <div class="showcase-block">
          <h4 class="block-title">5. Table selection</h4>
          <div class="table-selection-container">
            <table class="showcase-mini-table">
              <thead>
                <tr>
                  <th style="width: 44px; text-align: center;">
                    <app-checkbox
                      [checked]="isTableAllSelected()"
                      [indeterminate]="isTableIndeterminate()"
                      (checkedChange)="toggleTableAll($event)"
                      ariaLabel="Select all table products"
                      size="compact"
                    ></app-checkbox>
                  </th>
                  <th>Product</th>
                  <th style="text-align: right; width: 120px;">Status</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td style="text-align: center;">
                    <app-checkbox
                      [checked]="rowSteelSelected()"
                      (checkedChange)="rowSteelSelected.set($event)"
                      ariaLabel="Select Steel Sheet"
                      size="compact"
                    ></app-checkbox>
                  </td>
                  <td>
                    <span class="product-name">Steel Sheet</span>
                  </td>
                  <td style="text-align: right;">
                    <span class="status-active">Active</span>
                  </td>
                </tr>
                <tr>
                  <td style="text-align: center;">
                    <app-checkbox
                      [checked]="rowAlumSelected()"
                      (checkedChange)="rowAlumSelected.set($event)"
                      ariaLabel="Select Aluminium Rod"
                      size="compact"
                    ></app-checkbox>
                  </td>
                  <td>
                    <span class="product-name">Aluminium Rod</span>
                  </td>
                  <td style="text-align: right;">
                    <span class="status-active">Active</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- 2. Semantic Color Tokens & Validation Error Verification -->
      <div class="erp-card semantic-colors-card">
        <h4 class="block-title">6. Validated Semantic Color Tokens & Errors</h4>
        <p class="text-sm text-secondary">
          Configurable enterprise color tokens for contextual workflows.
        </p>

        <div class="colors-row">
          <app-checkbox colorToken="primary" [checked]="true" label="Primary (Fluent Blue)" size="regular"></app-checkbox>
          <app-checkbox colorToken="success" [checked]="true" label="Success (Approved)" size="regular"></app-checkbox>
          <app-checkbox colorToken="warning" [checked]="true" label="Warning (Review required)" size="regular"></app-checkbox>
          <app-checkbox colorToken="danger" [checked]="true" label="Danger (Critical exception)" size="regular"></app-checkbox>
          <app-checkbox [checked]="false" error="Mandatory field confirmation" label="Validation Error" size="regular"></app-checkbox>
        </div>
      </div>

      <!-- 3. Reactive Forms Integration (ControlValueAccessor) -->
      <div class="erp-card reactive-forms-card">
        <h4 class="block-title">7. Reactive Forms Integration (ControlValueAccessor)</h4>
        <p class="text-sm text-secondary">
          Full two-way synchronization with Angular's <code>FormGroup</code>, validation state tracking, and disabled pipeline.
        </p>

        <form [formGroup]="demoForm" (ngSubmit)="onSubmitForm()" class="reactive-form-box">
          <div class="form-checkboxes">
            <app-checkbox
              formControlName="requireApproval"
              label="Require two-tier executive approval for orders over $10,000"
              size="regular"
            ></app-checkbox>

            <app-checkbox
              formControlName="autoDispatch"
              label="Auto-dispatch purchase requisitions to preferred vendor"
              size="regular"
            ></app-checkbox>

            <app-checkbox
              formControlName="termsConsent"
              [error]="demoForm.get('termsConsent')?.touched && demoForm.get('termsConsent')?.invalid ? 'You must accept the terms of business' : undefined"
              label="I agree to the Enterprise Master Subscription Agreement (Required)"
              size="regular"
            ></app-checkbox>
          </div>

          <div class="form-actions-row">
            <p-button
              type="submit"
              label="Validate Form State"
              icon="pi pi-check"
              size="small"
              [disabled]="demoForm.invalid"
            ></p-button>

            <span class="form-status-pill" [class.valid]="demoForm.valid" [class.invalid]="demoForm.invalid">
              Form Status: {{ demoForm.valid ? 'VALID' : 'INVALID' }}
            </span>
          </div>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .checkbox-playground-container {
      display: flex;
      flex-direction: column;
      gap: 16px;

      .fluent-showcase-card {
        padding: 24px;
        background: #ffffff;
        border: 1px solid var(--erp-border, #e2e8f0);
        border-radius: 6px;

        .showcase-header {
          display: flex;
          justify-content: space-between;
          align-items: flex-start;
          margin-bottom: 24px;
          padding-bottom: 16px;
          border-bottom: 1px solid var(--erp-border, #f1f5f9);

          .title-with-pill {
            display: flex;
            align-items: center;
            gap: 10px;

            .main-title {
              margin: 0;
              font-size: 18px;
              font-weight: 700;
              color: var(--erp-text-main, #0f172a);
            }

            .fluent-pill {
              font-size: 11px;
              font-weight: 600;
              color: #0f6cbd;
              background-color: #eff6fc;
              border: 1px solid #cbe2f7;
              padding: 2px 8px;
              border-radius: 12px;
            }
          }

          .subtitle {
            margin: 4px 0 0;
            font-size: 12px;
            color: var(--erp-text-secondary, #64748b);
          }
        }

        .showcase-block {
          margin-bottom: 24px;

          .block-title {
            margin: 0 0 12px;
            font-size: 14px;
            font-weight: 600;
            color: var(--erp-text-main, #0f172a);
          }

          .states-row {
            display: flex;
            gap: 32px;
            flex-wrap: wrap;
          }

          .radio-shape-group {
            display: flex;
            flex-direction: column;
            gap: 8px;
            margin-bottom: 12px;

            .fluent-radio-option {
              display: inline-flex;
              align-items: center;
              gap: 8px;
              font-size: 13px;
              color: var(--erp-text-main, #0f172a);
              cursor: pointer;

              input[type="radio"] {
                accent-color: #0f6cbd;
                cursor: pointer;
              }
            }
          }

          .shape-example-box {
            padding-left: 2px;
          }

          .sizes-row {
            display: flex;
            gap: 32px;
            align-items: center;
            flex-wrap: wrap;
          }

          .form-examples-card {
            padding: 16px;
            background: #ffffff;
            border: 1px solid var(--erp-border, #e2e8f0);
            border-radius: 6px;

            .form-checks-stack {
              display: flex;
              flex-direction: column;
              gap: 12px;
            }

            .form-instruction-text {
              margin: 12px 0 0;
              font-size: 11.5px;
              color: var(--erp-text-secondary, #64748b);
            }
          }

          .table-selection-container {
            border: 1px solid var(--erp-border, #e2e8f0);
            border-radius: 6px;
            overflow: hidden;

            .showcase-mini-table {
              width: 100%;
              border-collapse: collapse;
              font-size: 13px;

              th, td {
                padding: 10px 14px;
                border-bottom: 1px solid var(--erp-border, #e2e8f0);
              }

              th {
                background: #f8fafc;
                font-weight: 600;
                color: var(--erp-text-secondary, #475569);
                text-align: left;
              }

              td {
                background: #ffffff;

                .product-name {
                  font-weight: 500;
                  color: var(--erp-text-main, #0f172a);
                }

                .status-active {
                  color: #0f6cbd;
                  font-size: 12px;
                  font-weight: 500;
                }
              }

              tr:last-child td {
                border-bottom: none;
              }
            }
          }
        }
      }

      .semantic-colors-card, .reactive-forms-card {
        padding: 20px 24px;
        background: #ffffff;
        border: 1px solid var(--erp-border, #e2e8f0);
        border-radius: 6px;

        .block-title {
          margin: 0 0 4px;
          font-size: 14px;
          font-weight: 600;
          color: var(--erp-text-main, #0f172a);
        }

        .colors-row {
          display: flex;
          gap: 24px;
          flex-wrap: wrap;
          margin-top: 14px;
        }

        .reactive-form-box {
          margin-top: 14px;
          display: flex;
          flex-direction: column;
          gap: 12px;

          .form-checkboxes {
            display: flex;
            flex-direction: column;
            gap: 10px;
          }

          .form-actions-row {
            display: flex;
            align-items: center;
            gap: 16px;
            margin-top: 8px;

            .form-status-pill {
              font-size: 11px;
              font-weight: 600;
              padding: 3px 8px;
              border-radius: 4px;

              &.valid {
                background: #dcfce7;
                color: #15803d;
              }

              &.invalid {
                background: #fee2e2;
                color: #b91c1c;
              }
            }
          }
        }
      }
    }
  `]
})
export class CheckboxPlaygroundSectionComponent {
  readonly themeService = inject(ThemeService);

  readonly shapeChoice = signal<CheckboxShape>('rounded-square');

  // Form example checks
  readonly enableNotifications = signal<boolean>(true);
  readonly includeInactive = signal<boolean>(false);
  readonly confirmOrderDetails = signal<boolean>(false);

  // Mini table selection states
  readonly rowSteelSelected = signal<boolean>(true);
  readonly rowAlumSelected = signal<boolean>(false);

  readonly isTableAllSelected = computed(() => this.rowSteelSelected() && this.rowAlumSelected());
  readonly isTableIndeterminate = computed(() =>
    (this.rowSteelSelected() || this.rowAlumSelected()) && !(this.rowSteelSelected() && this.rowAlumSelected())
  );

  toggleTableAll(checked: boolean): void {
    this.rowSteelSelected.set(checked);
    this.rowAlumSelected.set(checked);
  }

  // Reactive form
  readonly demoForm = new FormGroup({
    requireApproval: new FormControl(true),
    autoDispatch: new FormControl(false),
    termsConsent: new FormControl(false, Validators.requiredTrue)
  });

  onSubmitForm(): void {
    if (this.demoForm.valid) {
      alert(`Reactive form submitted successfully:\n${JSON.stringify(this.demoForm.value, null, 2)}`);
    }
  }
}
