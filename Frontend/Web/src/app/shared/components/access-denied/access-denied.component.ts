import { Component, input, output, inject, computed } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-access-denied',
  standalone: true,
  imports: [CommonModule, ButtonModule],
  template: `
    <div [class.access-denied-page]="!inline()" [class.access-denied-inline]="inline()">
      <div class="denied-card erp-card" [class.erp-card-elevated]="!inline()">
        <div class="denied-icon" aria-hidden="true">
          <i class="pi pi-shield"></i>
        </div>

        <h2 class="denied-title">{{ effectiveTitle() }}</h2>

        <p class="denied-desc">{{ effectiveMessage() }}</p>

        <!-- Context Details (Optional / Clean, never exposing raw internal URLs or stack traces) -->
        @if (attemptedDestination()) {
          <div class="context-box">
            <span class="box-label">Requested Area:</span>
            <code class="destination-code">{{ attemptedDestination() }}</code>
          </div>
        }

        @if (effectiveRequiredRoles()) {
          <div class="context-box roles-box">
            <span class="box-label">Required Role / Permission:</span>
            <span class="role-pill">{{ effectiveRequiredRoles() }}</span>
          </div>
        }

        @if (showSessionInfo() && authService.currentUser()) {
          <div class="user-session-info">
            <span>Signed in as: <strong>{{ authService.currentUser()?.email }}</strong></span>
            <span>Assigned Roles: <em>{{ authService.roles().join(', ') || 'Standard User' }}</em></span>
          </div>
        }

        <!-- Actions Bar -->
        <div class="denied-actions">
          @if (showBackAction()) {
            <button
              type="button"
              class="p-button p-button-sm p-button-outlined"
              (click)="onBack()"
              title="Go back to previous page"
            >
              <i class="pi pi-arrow-left p-button-icon-left"></i>
              <span class="p-button-label">Go Back</span>
            </button>
          }

          @if (showHomeAction()) {
            <button
              type="button"
              class="p-button p-button-sm p-button-primary"
              (click)="onHome()"
              title="Return to your workspace home"
            >
              <i class="pi pi-home p-button-icon-left"></i>
              <span class="p-button-label">{{ homeActionLabel() }}</span>
            </button>
          }

          @if (showSignOutAction() && authService.isAuthenticated()) {
            <button
              type="button"
              class="p-button p-button-sm p-button-secondary p-button-text"
              (click)="authService.logout()"
              title="Sign out or switch accounts"
            >
              <i class="pi pi-sign-out p-button-icon-left"></i>
              <span class="p-button-label">Switch Account</span>
            </button>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .access-denied-page {
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: calc(100vh - 160px);
      padding: var(--erp-space-8, 32px) var(--erp-space-4, 16px);
      background-color: var(--erp-bg-app, #f8fafc);
    }

    .access-denied-inline {
      display: flex;
      align-items: center;
      justify-content: center;
      padding: var(--erp-space-6, 24px) var(--erp-space-4, 16px);
      width: 100%;
    }

    .denied-card {
      max-width: 520px;
      width: 100%;
      text-align: center;
      padding: var(--erp-space-6, 24px);
      background: var(--erp-bg-surface, #ffffff);
      border: 1px solid var(--erp-border, #e2e8f0);
      border-radius: var(--erp-radius-md, 8px);
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
    }

    .erp-card-elevated {
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
    }

    .denied-icon {
      width: 56px;
      height: 56px;
      border-radius: 9999px;
      background-color: #fee2e2;
      color: #dc2626;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      font-size: 26px;
      margin-bottom: var(--erp-space-3, 12px);
    }

    .denied-title {
      font-size: 1.4rem;
      font-weight: 600;
      margin: 0 0 var(--erp-space-2, 8px) 0;
      color: var(--erp-text-main, #0f172a);
    }

    .denied-desc {
      color: var(--erp-text-secondary, #475569);
      font-size: 13.5px;
      line-height: 1.5;
      margin: 0 0 var(--erp-space-4, 16px) 0;
    }

    .context-box {
      background-color: var(--erp-bg-secondary, #f8fafc);
      border: 1px solid var(--erp-border, #e2e8f0);
      border-radius: var(--erp-radius-sm, 4px);
      padding: 6px 12px;
      font-size: 12.5px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
      margin-bottom: var(--erp-space-3, 12px);
      text-align: left;
    }

    .box-label {
      font-weight: 600;
      color: var(--erp-text-muted, #64748b);
      font-size: 12px;
    }

    .destination-code {
      font-family: var(--erp-font-mono, monospace);
      background: #f1f5f9;
      padding: 2px 6px;
      border-radius: 3px;
      color: var(--erp-text-main, #0f172a);
      font-size: 12px;
    }

    .role-pill {
      background-color: #eff6fc;
      color: #0f6cbd;
      border: 1px solid #c7e0f4;
      padding: 2px 8px;
      border-radius: 9999px;
      font-weight: 600;
      font-size: 11.5px;
    }

    .user-session-info {
      display: flex;
      flex-direction: column;
      gap: 3px;
      font-size: 11.5px;
      color: var(--erp-text-muted, #64748b);
      margin: var(--erp-space-4, 16px) 0;
      padding-top: var(--erp-space-3, 12px);
      border-top: 1px solid var(--erp-border, #e2e8f0);
      text-align: center;
    }

    .denied-actions {
      display: flex;
      gap: var(--erp-space-3, 12px);
      justify-content: center;
      align-items: center;
      flex-wrap: wrap;
      margin-top: var(--erp-space-4, 16px);
    }
  `]
})
export class AccessDeniedComponent {
  readonly authService = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly location = inject(Location);

  // Reusable Component Contract Inputs
  readonly title = input<string>('Access restricted');
  readonly message = input<string>(
    "You don't have permission to access this page. Contact your administrator if you believe this is an error."
  );
  readonly showBackAction = input<boolean>(true);
  readonly showHomeAction = input<boolean>(true);
  readonly showSignOutAction = input<boolean>(false);
  readonly showSessionInfo = input<boolean>(true);
  readonly homeUrl = input<string | null>(null);
  readonly homeLabel = input<string | null>(null);
  readonly inline = input<boolean>(false);
  readonly requiredRole = input<string | null>(null);
  readonly attemptedUrlInput = input<string | null>(null);

  // Outputs for custom action overrides
  readonly back = output<void>();
  readonly home = output<void>();

  // Route Query Params for Standalone Route Access (/access-denied?attempted=...&required=...)
  private readonly queryTitle = this.route?.snapshot?.queryParams?.['title'];
  private readonly queryMessage = this.route?.snapshot?.queryParams?.['message'];
  private readonly queryAttempted = this.route?.snapshot?.queryParams?.['attempted'];
  private readonly queryRequired = this.route?.snapshot?.queryParams?.['required'];

  readonly effectiveTitle = computed(() => {
    return this.title() || this.queryTitle || 'Access restricted';
  });

  readonly effectiveMessage = computed(() => {
    return (
      this.message() ||
      this.queryMessage ||
      "You don't have permission to access this page. Contact your administrator if you believe this is an error."
    );
  });

  readonly attemptedDestination = computed(() => {
    return this.attemptedUrlInput() || this.queryAttempted || null;
  });

  readonly effectiveRequiredRoles = computed(() => {
    return this.requiredRole() || this.queryRequired || null;
  });

  readonly defaultHomeUrl = computed(() => {
    if (this.authService.isPlatformAdmin()) {
      return '/platform-admin/dashboard';
    }
    if (this.authService.isPlatformStaff()) {
      return '/platform-staff';
    }
    if (this.authService.isTenantAdmin()) {
      return '/tenant-admin/console';
    }
    if (this.authService.isTenantStaff() || this.authService.isAuthenticated()) {
      return '/app/dashboard';
    }
    return '/';
  });

  readonly effectiveHomeUrl = computed(() => {
    return this.homeUrl() || this.defaultHomeUrl();
  });

  readonly homeActionLabel = computed(() => {
    if (this.homeLabel()) return this.homeLabel()!;
    if (this.authService.isTenantAdmin()) return 'Tenant Admin Console';
    if (this.authService.isPlatformAdmin() || this.authService.isPlatformStaff()) return 'Platform Workspace';
    return 'Workspace Home';
  });

  onBack(): void {
    this.back.emit();
    try {
      this.location.back();
    } catch {
      this.router.navigate([this.effectiveHomeUrl()]);
    }
  }

  onHome(): void {
    this.home.emit();
    this.router.navigate([this.effectiveHomeUrl()]);
  }
}

export { AccessDeniedComponent as AppAccessDeniedComponent };
