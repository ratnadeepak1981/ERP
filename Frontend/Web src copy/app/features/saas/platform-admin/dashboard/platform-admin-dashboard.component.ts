import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { AuthService } from '../../../core/auth/auth.service';

interface PlatformMetric {
  title: string;
  value: string;
  change: string;
  isPositive: boolean;
  icon: string;
  badgeText: string;
}

@Component({
  selector: 'app-platform-admin-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, ButtonModule, PageHeaderComponent, StatusBadgeComponent],
  templateUrl: './platform-admin-dashboard.component.html',
  styleUrl: './platform-admin-dashboard.component.scss'
})
export class PlatformAdminDashboardComponent implements OnInit {
  readonly authService = inject(AuthService);

  readonly backendStatus = signal<string>('Checking...');
  readonly algorithm = signal<string>('RS256');

  readonly metrics: PlatformMetric[] = [
    {
      title: 'Total Active Tenants',
      value: '24 Organizations',
      change: '+3 this month',
      isPositive: true,
      icon: 'pi pi-building',
      badgeText: 'Multi-Tenant'
    },
    {
      title: 'Platform MRR',
      value: '$48,250',
      change: '+12.4% vs last mo',
      isPositive: true,
      icon: 'pi pi-dollar',
      badgeText: 'SaaS Revenue'
    },
    {
      title: 'Active Subscriptions',
      value: '38 Active Plans',
      change: 'Tier 1 Enterprise: 16',
      isPositive: true,
      icon: 'pi pi-credit-card',
      badgeText: 'Billing Active'
    },
    {
      title: 'Auth & Key Vault',
      value: 'RS256 Asymmetric',
      change: '4096-bit SecureVault',
      isPositive: true,
      icon: 'pi pi-shield',
      badgeText: 'Verified'
    }
  ];

  ngOnInit(): void {
    this.authService.getSecurityStatus().subscribe({
      next: (res) => {
        if (res?.succeeded) {
          this.backendStatus.set('Operational');
        } else {
          this.backendStatus.set('Degraded');
        }
      },
      error: () => {
        this.backendStatus.set('Standby / Dev Mode');
      }
    });
  }
}
