import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-access-denied',
  standalone: true,
  imports: [CommonModule, RouterLink, ButtonModule],
  template: `
    <div class="access-denied-page">
      <div class="denied-card erp-card erp-card-elevated">
        <div class="denied-icon">
          <i class="pi pi-lock"></i>
        </div>
        <h2>403 — Access Denied</h2>
        <p class="denied-desc">
          Your current security role does not have authorization to access this area.
        </p>

        @if (attemptedUrl) {
          <div class="context-box">
            <span class="box-label">Requested Destination:</span>
            <code>{{ attemptedUrl }}</code>
          </div>
        }

        @if (requiredRoles) {
          <div class="context-box roles-box">
            <span class="box-label">Required Role(s):</span>
            <span class="role-pill">{{ requiredRoles }}</span>
          </div>
        }

        @if (authService.currentUser()) {
          <div class="user-session-info">
            <span>Signed in as: <strong>{{ authService.currentUser()?.email }}</strong></span>
            <span>Assigned Roles: <em>{{ authService.roles().join(', ') || 'None' }}</em></span>
          </div>
        }

        <div class="denied-actions">
          <a routerLink="/" class="p-button p-button-outlined">Platform Home</a>
          <button type="button" class="p-button p-button-secondary" (click)="authService.logout()">
            Sign Out / Switch User
          </button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .access-denied-page {
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: calc(100vh - 150px);
      padding: var(--erp-space-8) var(--erp-space-4);
      background-color: var(--erp-bg-app);
    }
    .denied-card {
      max-width: 500px;
      width: 100%;
      text-align: center;
      padding: var(--erp-space-6);
    }
    .denied-icon {
      width: 60px;
      height: 60px;
      border-radius: var(--erp-radius-pill);
      background-color: var(--erp-danger-bg);
      color: var(--erp-danger);
      display: inline-flex;
      align-items: center;
      justify-content: center;
      font-size: 28px;
      margin-bottom: var(--erp-space-3);
    }
    h2 {
      font-size: 1.6rem;
      margin-bottom: var(--erp-space-2);
      color: var(--erp-text-main);
    }
    .denied-desc {
      color: var(--erp-text-secondary);
      font-size: 14px;
      margin-bottom: var(--erp-space-4);
    }
    .context-box {
      background-color: var(--erp-bg-secondary);
      border: 1px solid var(--erp-border);
      border-radius: var(--erp-radius-sm);
      padding: var(--erp-space-2) var(--erp-space-3);
      font-size: 13px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: var(--erp-space-3);
    }
    .box-label {
      font-weight: 600;
      color: var(--erp-text-muted);
    }
    .role-pill {
      background-color: var(--erp-primary-light);
      color: var(--erp-primary);
      padding: 2px 8px;
      border-radius: var(--erp-radius-pill);
      font-weight: 600;
      font-size: 12px;
    }
    .user-session-info {
      display: flex;
      flex-direction: column;
      gap: 4px;
      font-size: 12px;
      color: var(--erp-text-muted);
      margin: var(--erp-space-4) 0;
      padding-top: var(--erp-space-3);
      border-top: 1px solid var(--erp-border-subtle);
    }
    .denied-actions {
      display: flex;
      gap: var(--erp-space-3);
      justify-content: center;
      margin-top: var(--erp-space-4);
    }
  `]
})
export class AccessDeniedComponent {
  readonly authService = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  readonly attemptedUrl = this.route.snapshot.queryParams['attempted'] || '';
  readonly requiredRoles = this.route.snapshot.queryParams['required'] || '';
}
