import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { PublicNavService } from '../../../core/services/public-nav.service';

export interface PlatformCapability {
  icon: string;
  title: string;
  description: string;
}

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterLink, ButtonModule, CardModule],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent {
  readonly navService = inject(PublicNavService);

  readonly capabilities: PlatformCapability[] = [
    {
      icon: 'pi pi-shopping-cart',
      title: 'Procurement & Purchasing',
      description: 'Manage purchase requisitions, supplier information, purchasing workflows and approvals.'
    },
    {
      icon: 'pi pi-box',
      title: 'Inventory & Warehousing',
      description: 'Track stock across multiple warehouses and locations, with inventory valuation and stock movement visibility.'
    },
    {
      icon: 'pi pi-send',
      title: 'Sales & Order Fulfillment',
      description: 'Manage the sales lifecycle, from customer orders to dispatch and delivery tracking.'
    },
    {
      icon: 'pi pi-sitemap',
      title: 'Business Operations',
      description: 'Connect business processes, company structures and branch operations through a unified platform.'
    },
    {
      icon: 'pi pi-chart-line',
      title: 'AI Business Intelligence',
      description: 'Gain insights into purchasing, inventory, sales performance, demand patterns and business impact.'
    }
  ];

  switchToDirectory(): void {
    this.navService.setActiveTab('directory');
  }

  switchToPlatform(): void {
    this.navService.setActiveTab('platform');
  }
}
