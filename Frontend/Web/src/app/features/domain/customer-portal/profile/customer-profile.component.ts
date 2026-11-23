import { Component, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../../../core/auth/auth.service';
import { TenantContextService } from '../../../../core/services/tenant-context.service';

@Component({
  selector: 'app-customer-profile',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="customer-profile-page">
      <div class="page-title-row">
        <div>
          <h1>Customer Account Profile</h1>
          <p>Review your verified customer details, access roles, and tenant associations.</p>
        </div>
      </div>

      <div class="profile-card erp-card">
        <div class="profile-head">
          <div class="avatar-wrap">
            <i [class]="isOrganisation() ? 'pi pi-building' : 'pi pi-user'"></i>
          </div>
          <div class="head-info">
            <h2>{{ userEmail() }}</h2>
            <div class="pill-row">
              <span class="role-pill" [class.b2b]="isOrganisation()">
                {{ isOrganisation() ? 'Organisation B2B Customer' : 'Individual Retail Customer' }}
              </span>
              <span class="status-pill">Active Verified Account</span>
            </div>
          </div>
        </div>

        <div class="profile-details-grid">
          <div class="detail-block">
            <span class="detail-label">Associated Tenant</span>
            <span class="detail-value">{{ tenantName() }}</span>
          </div>

          <div class="detail-block">
            <span class="detail-label">Sign-In Email</span>
            <span class="detail-value font-mono">{{ userEmail() }}</span>
          </div>

          <div class="detail-block">
            <span class="detail-label">Security Identifier</span>
            <span class="detail-value font-mono">{{ userId() }}</span>
          </div>

          <div class="detail-block">
            <span class="detail-label">Portal Scope</span>
            <span class="detail-value">{{ isOrganisation() ? 'Corporate Purchasing & Price Card Agreement' : 'Standard Retail Customer' }}</span>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .customer-profile-page {
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-4);
    }

    .page-title-row {
      h1 {
        font-size: 1.4rem;
        font-weight: 700;
        margin: 0;
        color: var(--erp-text-main);
      }

      p {
        font-size: 0.88rem;
        color: var(--erp-text-secondary);
        margin: 2px 0 0 0;
      }
    }

    .profile-card {
      padding: var(--erp-space-5);
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-5);

      .profile-head {
        display: flex;
        align-items: center;
        gap: var(--erp-space-4);
        padding-bottom: var(--erp-space-4);
        border-bottom: 1px solid var(--erp-border-subtle);

        .avatar-wrap {
          width: 60px;
          height: 60px;
          border-radius: var(--erp-radius-md);
          background-color: var(--erp-primary-light);
          color: var(--erp-primary);
          display: flex;
          align-items: center;
          justify-content: center;
          font-size: 28px;
        }

        .head-info {
          display: flex;
          flex-direction: column;
          gap: 4px;

          h2 {
            font-size: 1.25rem;
            margin: 0;
            color: var(--erp-text-main);
          }

          .pill-row {
            display: flex;
            align-items: center;
            gap: var(--erp-space-2);
            flex-wrap: wrap;

            .role-pill {
              font-size: 0.72rem;
              font-weight: 700;
              text-transform: uppercase;
              padding: 2px 8px;
              border-radius: 12px;
              background-color: var(--erp-primary-light);
              color: var(--erp-primary);

              &.b2b {
                background-color: rgba(139, 92, 246, 0.15);
                color: #7c3aed;
              }
            }

            .status-pill {
              font-size: 0.72rem;
              font-weight: 600;
              padding: 2px 8px;
              border-radius: 12px;
              background-color: rgba(16, 185, 129, 0.15);
              color: #10b981;
            }
          }
        }
      }

      .profile-details-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
        gap: var(--erp-space-4);

        .detail-block {
          display: flex;
          flex-direction: column;
          gap: 4px;

          .detail-label {
            font-size: 0.78rem;
            color: var(--erp-text-secondary);
            font-weight: 500;
          }

          .detail-value {
            font-size: 0.95rem;
            font-weight: 600;
            color: var(--erp-text-main);

            &.font-mono {
              font-family: var(--erp-font-mono);
              font-size: 0.88rem;
            }
          }
        }
      }
    }
  `]
})
export class CustomerProfileComponent {
  readonly authService = inject(AuthService);
  readonly tenantContext = inject(TenantContextService);

  readonly userEmail = computed(() => {
    return this.authService.currentUser()?.email || 'Customer';
  });

  readonly userId = computed(() => {
    return this.authService.currentUser()?.userId || 'N/A';
  });

  readonly isOrganisation = computed(() => {
    return this.authService.isCustomerCompany();
  });

  readonly tenantName = computed(() => {
    return this.tenantContext.currentTenant()?.name || this.tenantContext.subdomain() || 'Alpha Manufacturing Ltd';
  });
}
