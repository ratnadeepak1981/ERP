import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { AppStatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppEmptyStateComponent } from '../../../../../shared/components/empty-state/empty-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AccessDeniedComponent } from '../../../../../shared/components/access-denied/access-denied.component';

@Component({
  selector: 'app-shared-controls-section',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    InputTextModule,
    DialogModule,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppEmptyStateComponent,
    AppErrorStateComponent,
    AccessDeniedComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="shared-controls-container">
      <div class="section-intro erp-card">
        <h3>Shared Buttons, Status Badges, Form Controls & System States</h3>
        <p class="text-sm text-secondary">
          Overview of shared enterprise UI primitives across actions, data display, feedback notifications, and state boundaries.
        </p>
      </div>

      <div class="sections-grid">
        <!-- 1. Enterprise Action Buttons -->
        <div class="control-box erp-card">
          <h4>1. Buttons & Action Primitives</h4>
          <p class="text-xs text-muted">Primary, secondary, outlined, text, danger, and icon-supported variants.</p>

          <div class="btn-group-row">
            <p-button label="Primary Blue" icon="pi pi-check" size="small"></p-button>
            <p-button label="Secondary Outlined" [outlined]="true" severity="secondary" size="small"></p-button>
            <p-button label="Success Action" severity="success" size="small"></p-button>
            <p-button label="Warning Action" severity="warn" size="small"></p-button>
            <p-button label="Destructive Danger" icon="pi pi-trash" severity="danger" size="small"></p-button>
            <p-button label="Text Action" [text]="true" size="small"></p-button>
            <p-button icon="pi pi-refresh" [rounded]="true" [text]="true" size="small"></p-button>
          </div>
        </div>

        <!-- 2. Status Badges -->
        <div class="control-box erp-card">
          <h4>2. WCAG-Compliant Status Badges</h4>
          <p class="text-xs text-muted">Semantic badges with high contrast borders and companion icons.</p>

          <div class="badge-group-row">
            <app-status-badge label="Active / Operational" severity="success" icon="pi pi-check"></app-status-badge>
            <app-status-badge label="Pending Review" severity="warning" icon="pi pi-clock"></app-status-badge>
            <app-status-badge label="Cancelled / Rejected" severity="danger" icon="pi pi-times"></app-status-badge>
            <app-status-badge label="Shipped / In Transit" severity="info" icon="pi pi-send"></app-status-badge>
            <app-status-badge label="Draft / Inactive" severity="neutral" icon="pi pi-pencil"></app-status-badge>
          </div>
        </div>

        <!-- 3. Form Controls & Floating Labels -->
        <div class="control-box erp-card">
          <h4>3. Enterprise Input Controls</h4>
          <p class="text-xs text-muted">Compact form inputs with accessible focus rings and token borders.</p>

          <div class="form-inputs-row">
            <div class="input-field">
              <label class="field-label">Tenant Identifier</label>
              <input pInputText type="text" value="TENANT-COLOMBO-01" class="demo-input" />
            </div>

            <div class="input-field">
              <label class="field-label">Fiscal Year Budget (USD)</label>
              <input pInputText type="number" value="150000" class="demo-input" />
            </div>

            <div class="input-field">
              <label class="field-label">Audit Log Remarks</label>
              <input pInputText type="text" placeholder="Enter optional notes..." class="demo-input" />
            </div>
          </div>
        </div>

        <!-- 4. Enterprise Dialog / Modal -->
        <div class="control-box erp-card">
          <h4>4. Enterprise Overlays & Modals</h4>
          <p class="text-xs text-muted">PrimeNG Dialog with clean enterprise typography and action bar.</p>

          <div>
            <p-button
              label="Launch Sample Modal"
              icon="pi pi-external-link"
              size="small"
              [outlined]="true"
              (onClick)="demoModalVisible.set(true)"
            ></p-button>
          </div>

          <p-dialog
            header="Executive Approval Required"
            [(visible)]="demoModalVisible"
            [modal]="true"
            [style]="{ width: '440px' }"
            [draggable]="false"
          >
            <p class="text-sm">
              This action requires two-factor authorization from the Financial Controller branch authority.
            </p>
            <ng-template pTemplate="footer">
              <p-button label="Cancel" [text]="true" severity="secondary" (onClick)="demoModalVisible.set(false)"></p-button>
              <p-button label="Confirm Authorization" icon="pi pi-check" (onClick)="demoModalVisible.set(false)"></p-button>
            </ng-template>
          </p-dialog>
        </div>

        <!-- 5. System States: Loading, Empty, Error, Access Denied -->
        <div class="control-box erp-card full-width">
          <div class="states-header">
            <h4>5. System State Boundaries (Loading, Empty, Error, Access-Denied)</h4>
            <div class="state-pills">
              <button type="button" class="pill-btn" [class.active]="activeStateTab() === 'loading'" (click)="activeStateTab.set('loading')">Loading</button>
              <button type="button" class="pill-btn" [class.active]="activeStateTab() === 'empty'" (click)="activeStateTab.set('empty')">Empty</button>
              <button type="button" class="pill-btn" [class.active]="activeStateTab() === 'error'" (click)="activeStateTab.set('error')">Error</button>
              <button type="button" class="pill-btn" [class.active]="activeStateTab() === 'access'" (click)="activeStateTab.set('access')">Access Denied</button>
            </div>
          </div>

          <div class="state-container-preview">
            @switch (activeStateTab()) {
              @case ('loading') {
                <app-loading-state message="Simulated asynchronous ledger data synchronization..."></app-loading-state>
              }
              @case ('empty') {
                <app-empty-state
                  title="No Supplier Invoices Found"
                  description="There are no pending accounts payable records for the selected accounting period."
                ></app-empty-state>
              }
              @case ('error') {
                <app-error-state
                  title="Failed to Connect to ERP Microservice"
                  message="Simulated boundary timeout (HTTP 504 Gateway Timeout). Click retry to re-establish connection."
                  (retry)="onRetryState()"
                ></app-error-state>
              }
              @case ('access') {
                <app-access-denied
                  attemptedRoute="/app/security/role-permissions"
                  requiredRoles="Platform Admin, Security Manager"
                ></app-access-denied>
              }
            }
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .shared-controls-container {
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-4, 16px);

      .sections-grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 16px;

        @media (max-width: 900px) {
          grid-template-columns: 1fr;
        }

        .control-box {
          display: flex;
          flex-direction: column;
          gap: 12px;

          &.full-width {
            grid-column: 1 / -1;
          }

          h4 {
            margin: 0;
            font-size: 14px;
          }

          .btn-group-row, .badge-group-row {
            display: flex;
            flex-wrap: wrap;
            gap: 8px;
            align-items: center;
          }

          .form-inputs-row {
            display: flex;
            gap: 12px;
            flex-wrap: wrap;

            .input-field {
              display: flex;
              flex-direction: column;
              gap: 4px;
              flex: 1;
              min-width: 180px;

              .field-label {
                font-size: 11.5px;
                font-weight: 500;
                color: var(--erp-text-secondary, #475569);
              }

              .demo-input {
                height: 32px;
                font-size: 13px;
                border-radius: var(--erp-radius-sm, 4px);
              }
            }
          }

          .states-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            gap: 12px;
            flex-wrap: wrap;

            .state-pills {
              display: flex;
              gap: 6px;

              .pill-btn {
                padding: 4px 10px;
                font-size: 12px;
                border-radius: var(--erp-radius-pill, 9999px);
                border: 1px solid var(--erp-border, #e2e8f0);
                background: var(--erp-bg-surface, #ffffff);
                color: var(--erp-text-secondary, #475569);
                cursor: pointer;

                &.active {
                  background: var(--erp-primary, #0f6cbd);
                  color: #ffffff;
                  border-color: var(--erp-primary, #0f6cbd);
                  font-weight: 600;
                }
              }
            }
          }

          .state-container-preview {
            padding: 16px;
            background: var(--erp-bg-secondary, #f1f5f9);
            border-radius: var(--erp-radius-sm, 4px);
            border: 1px solid var(--erp-border, #e2e8f0);
            min-height: 180px;
            display: flex;
            align-items: center;
            justify-content: center;
          }
        }
      }
    }
  `]
})
export class SharedControlsSectionComponent {
  readonly demoModalVisible = signal<boolean>(false);
  readonly activeStateTab = signal<'loading' | 'empty' | 'error' | 'access'>('loading');

  onRetryState(): void {
    alert('Simulated retry executed successfully!');
  }
}
