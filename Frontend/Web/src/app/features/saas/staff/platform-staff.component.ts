import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-platform-staff',
  standalone: true,
  imports: [CommonModule, RouterLink, ButtonModule, PageHeaderComponent, StatusBadgeComponent],
  template: `
    <div class="platform-staff-page">
      <header class="staff-header erp-card">
        <div class="header-left">
          <div class="staff-badge">
            <i class="pi pi-users"></i>
          </div>
          <div>
            <h2>Platform Staff Workspace</h2>
            <p>Operational queues, tenant assistance, and platform customer support dispatch.</p>
          </div>
        </div>
        <div class="header-right">
          <span class="user-meta">Logged in: <strong>{{ authService.currentUser()?.email }}</strong></span>
          <button type="button" class="p-button p-button-sm p-button-outlined" (click)="authService.logout()">
            Sign Out
          </button>
        </div>
      </header>

      <div class="staff-grid">
        <div class="staff-card erp-card">
          <div class="card-head">
            <i class="pi pi-inbox"></i>
            <h3>Active Support Queues</h3>
          </div>
          <p>Tenant inquiries awaiting technical triage and onboarding assistance.</p>
          <div class="queue-list">
            <div class="queue-item">
              <span class="tenant-tag">ALPHA</span>
              <span class="item-desc">Warehouse barcode scanner integration issue</span>
              <app-status-badge status="warning" label="Pending Response"></app-status-badge>
            </div>
            <div class="queue-item">
              <span class="tenant-tag">GAMMA</span>
              <span class="item-desc">Dedicated database migration verification</span>
              <app-status-badge status="active" label="In Progress"></app-status-badge>
            </div>
            <div class="queue-item">
              <span class="tenant-tag">BETA</span>
              <span class="item-desc">User limit expansion request</span>
              <app-status-badge status="info" label="New Inquiry"></app-status-badge>
            </div>
          </div>
        </div>

        <div class="staff-card erp-card">
          <div class="card-head">
            <i class="pi pi-compass"></i>
            <h3>Staff Quick Actions</h3>
          </div>
          <div class="actions-list">
            <a routerLink="/platform-admin" class="action-btn">
              <i class="pi pi-shield"></i>
              <span>Elevate to Platform Admin (Role Permitted)</span>
            </a>
            <a routerLink="/app/dashboard" class="action-btn">
              <i class="pi pi-desktop"></i>
              <span>Open Tenant Operational Workspace</span>
            </a>
            <a routerLink="/storefront" class="action-btn">
              <i class="pi pi-shopping-bag"></i>
              <span>Preview Tenant Storefront</span>
            </a>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .platform-staff-page {
      padding: var(--erp-space-5);
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-5);
      background-color: var(--erp-bg-app);
      min-height: 100vh;
    }
    .staff-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      flex-wrap: wrap;
      gap: var(--erp-space-3);
    }
    .header-left {
      display: flex;
      align-items: center;
      gap: var(--erp-space-3);
    }
    .staff-badge {
      width: 44px;
      height: 44px;
      border-radius: var(--erp-radius-md);
      background-color: var(--erp-primary-light);
      color: var(--erp-primary);
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 20px;
    }
    h2 {
      font-size: 1.4rem;
      margin: 0;
      color: var(--erp-text-main);
    }
    p {
      margin: 0;
      font-size: 13px;
      color: var(--erp-text-secondary);
    }
    .header-right {
      display: flex;
      align-items: center;
      gap: var(--erp-space-3);
      font-size: 13px;
    }
    .staff-grid {
      display: grid;
      grid-template-columns: 1.5fr 1fr;
      gap: var(--erp-space-4);
      @media (max-width: 860px) {
        grid-template-columns: 1fr;
      }
    }
    .staff-card {
      .card-head {
        display: flex;
        align-items: center;
        gap: var(--erp-space-2);
        margin-bottom: var(--erp-space-2);
        color: var(--erp-primary);
        h3 {
          margin: 0;
          font-size: 15px;
          color: var(--erp-text-main);
        }
      }
    }
    .queue-list {
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-2);
      margin-top: var(--erp-space-3);
    }
    .queue-item {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: var(--erp-space-2) var(--erp-space-3);
      border: 1px solid var(--erp-border-subtle);
      border-radius: var(--erp-radius-sm);
      background-color: var(--erp-bg-surface);
      font-size: 13px;
      .tenant-tag {
        font-family: var(--erp-font-mono);
        font-weight: 700;
        color: var(--erp-primary);
      }
      .item-desc {
        color: var(--erp-text-main);
        flex: 1;
        margin: 0 var(--erp-space-3);
      }
    }
    .actions-list {
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-2);
      margin-top: var(--erp-space-3);
    }
    .action-btn {
      display: flex;
      align-items: center;
      gap: var(--erp-space-2);
      padding: var(--erp-space-3);
      border: 1px solid var(--erp-border);
      border-radius: var(--erp-radius-sm);
      color: var(--erp-text-main);
      text-decoration: none;
      font-size: 13px;
      font-weight: 600;
      transition: all 0.15s ease;
      &:hover {
        border-color: var(--erp-primary);
        color: var(--erp-primary);
        background-color: var(--erp-primary-light);
      }
    }
  `]
})
export class PlatformStaffComponent {
  readonly authService = inject(AuthService);
}
