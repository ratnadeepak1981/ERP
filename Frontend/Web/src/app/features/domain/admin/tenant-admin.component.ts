import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-tenant-admin',
  standalone: true,
  imports: [CommonModule, RouterLink, ButtonModule, PageHeaderComponent, StatusBadgeComponent],
  template: `
    <div class="tenant-admin-page">
      <header class="tenant-header erp-card">
        <div class="header-left">
          <div class="tenant-badge">
            <i class="pi pi-building"></i>
          </div>
          <div>
            <h2>Tenant Administration Console</h2>
            <p>Organization settings, branches, user access roles, and subscription quotas.</p>
          </div>
        </div>
        <div class="header-right">
          <div class="scope-pill">
            <i class="pi pi-shield"></i>
            <span>Role: <strong>{{ authService.roles()[0] || 'Tenant Admin' }}</strong></span>
          </div>
          <button type="button" class="p-button p-button-sm p-button-outlined" (click)="authService.logout()">
            Sign Out
          </button>
        </div>
      </header>

      <div class="tenant-admin-grid">
        <!-- Organization Profile Card -->
        <div class="admin-card erp-card">
          <div class="card-head">
            <i class="pi pi-id-card"></i>
            <h3>Organization Profile</h3>
          </div>
          <div class="profile-info-grid">
            <div class="info-row">
              <span class="label">Organization Name:</span>
              <strong>Alpha Manufacturing Ltd</strong>
            </div>
            <div class="info-row">
              <span class="label">Tenant Code:</span>
              <code class="code-tag">ALPHA</code>
            </div>
            <div class="info-row">
              <span class="label">Active Subscription:</span>
              <span class="plan-tag">Enterprise Pro</span>
            </div>
            <div class="info-row">
              <span class="label">Operating Branches:</span>
              <span>2 Branches (Colombo Main, Kandy Plant)</span>
            </div>
          </div>
        </div>

        <!-- Quota & Subscription Limits -->
        <div class="admin-card erp-card">
          <div class="card-head">
            <i class="pi pi-sliders-h"></i>
            <h3>Subscription &amp; Capacity</h3>
          </div>
          <div class="quota-bars">
            <div class="quota-item">
              <div class="quota-label">
                <span>Active ERP Users</span>
                <strong>42 / 50 Allowed</strong>
              </div>
              <div class="bar-bg"><div class="bar-fill" style="width: 84%;"></div></div>
            </div>
            <div class="quota-item">
              <div class="quota-label">
                <span>Monthly Orders</span>
                <strong>1,240 / 5,000</strong>
              </div>
              <div class="bar-bg"><div class="bar-fill" style="width: 25%;"></div></div>
            </div>
          </div>
        </div>

        <!-- Rapid Navigation -->
        <div class="admin-card erp-card full-width">
          <div class="card-head">
            <i class="pi pi-external-link"></i>
            <h3>Tenant Workspaces</h3>
          </div>
          <div class="links-row">
            <a routerLink="/app/dashboard" class="workspace-btn primary">
              <i class="pi pi-desktop"></i>
              <div>
                <strong>Tenant Operational ERP</strong>
                <span>Open Master Data, Inventory, Sales &amp; Procurement workspace</span>
              </div>
            </a>
            <a routerLink="/storefront" class="workspace-btn">
              <i class="pi pi-shopping-bag"></i>
              <div>
                <strong>Tenant Public Storefront</strong>
                <span>Customer catalog &amp; public e-commerce store</span>
              </div>
            </a>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .tenant-admin-page {
      padding: var(--erp-space-5);
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-5);
      background-color: var(--erp-bg-app);
      min-height: 100vh;
    }
    .tenant-header {
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
    .tenant-badge {
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
    }
    .scope-pill {
      background-color: var(--erp-bg-secondary);
      border: 1px solid var(--erp-border);
      padding: 4px 10px;
      border-radius: var(--erp-radius-pill);
      font-size: 12px;
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .tenant-admin-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: var(--erp-space-4);
      @media (max-width: 860px) {
        grid-template-columns: 1fr;
      }
    }
    .full-width {
      grid-column: 1 / -1;
    }
    .admin-card {
      .card-head {
        display: flex;
        align-items: center;
        gap: var(--erp-space-2);
        margin-bottom: var(--erp-space-3);
        color: var(--erp-primary);
        h3 {
          margin: 0;
          font-size: 15px;
          color: var(--erp-text-main);
        }
      }
    }
    .profile-info-grid {
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-2);
      .info-row {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding-bottom: var(--erp-space-2);
        border-bottom: 1px solid var(--erp-border-subtle);
        font-size: 13px;
        .label {
          color: var(--erp-text-muted);
        }
        .code-tag {
          font-family: var(--erp-font-mono);
          font-weight: 700;
          color: var(--erp-primary);
        }
        .plan-tag {
          background-color: var(--erp-primary-light);
          color: var(--erp-primary);
          padding: 2px 6px;
          border-radius: var(--erp-radius-sm);
          font-size: 12px;
          font-weight: 600;
        }
      }
    }
    .quota-bars {
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-3);
    }
    .quota-item {
      display: flex;
      flex-direction: column;
      gap: 4px;
      .quota-label {
        display: flex;
        justify-content: space-between;
        font-size: 12px;
        color: var(--erp-text-main);
      }
      .bar-bg {
        height: 8px;
        border-radius: var(--erp-radius-pill);
        background-color: var(--erp-bg-secondary);
        overflow: hidden;
        .bar-fill {
          height: 100%;
          background: linear-gradient(90deg, var(--erp-primary), var(--erp-primary-hover));
          border-radius: var(--erp-radius-pill);
        }
      }
    }
    .links-row {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: var(--erp-space-3);
      @media (max-width: 650px) {
        grid-template-columns: 1fr;
      }
    }
    .workspace-btn {
      display: flex;
      align-items: center;
      gap: var(--erp-space-3);
      padding: var(--erp-space-4);
      border: 1px solid var(--erp-border);
      border-radius: var(--erp-radius-md);
      background-color: var(--erp-bg-surface);
      text-decoration: none;
      transition: all 0.15s ease;
      i {
        font-size: 24px;
        color: var(--erp-primary);
      }
      strong {
        display: block;
        font-size: 14px;
        color: var(--erp-text-main);
        margin-bottom: 2px;
      }
      span {
        font-size: 12px;
        color: var(--erp-text-muted);
      }
      &:hover {
        border-color: var(--erp-primary);
        box-shadow: var(--erp-shadow-xs);
        transform: translateY(-1px);
      }
      &.primary {
        background-color: var(--erp-primary-light);
        border-color: var(--erp-primary);
      }
    }
  `]
})
export class TenantAdminComponent {
  readonly authService = inject(AuthService);
}
