import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';

export interface OverviewPillar {
  icon: string;
  title: string;
  badge: string;
  description: string;
  points: string[];
}

@Component({
  selector: 'app-overview',
  standalone: true,
  imports: [CommonModule, RouterLink, ButtonModule, CardModule],
  templateUrl: './overview.component.html',
  styleUrl: './overview.component.scss'
})
export class OverviewComponent {
  readonly pillars: OverviewPillar[] = [
    {
      icon: 'pi pi-shopping-cart',
      title: 'Procurement & Purchasing',
      badge: 'Supply Chain',
      description: 'Streamline upstream procurement workflows with structured purchasing controls and approvals.',
      points: [
        'Purchase Requisition and Order Lifecycle',
        'Vendor Catalog and Price Agreement Management',
        'Multi-Tier Procurement Authorization Thresholds'
      ]
    },
    {
      icon: 'pi pi-box',
      title: 'Inventory & Warehousing',
      badge: 'Operations',
      description: 'Unified visibility of stock levels across distributed storage locations, warehouses, and bin zones.',
      points: [
        'Multi-Warehouse and Multi-Zone Tracking',
        'FIFO, LIFO, and Moving Average Valuation Rules',
        'Real-Time Goods Receipt & Stock Movement Auditing'
      ]
    },
    {
      icon: 'pi pi-send',
      title: 'Sales & Fulfillment',
      badge: 'Distribution',
      description: 'End-to-end sales processing from quote and customer order to parcel shipment and dispatch.',
      points: [
        'Multi-Channel Sales Order Processing',
        'Direct Shipment and Logistics Tracking',
        'Customer Profile and Account Management'
      ]
    },
    {
      icon: 'pi pi-shield',
      title: 'Multi-Tenant Security & Isolation',
      badge: 'Enterprise SaaS',
      description: 'Rigorous architectural boundaries ensuring absolute separation of tenant business data.',
      points: [
        'Tenant-Aware Data Partitioning & Storage Modes',
        'RS256 Asymmetric Cryptographic Authentication',
        'Granular Permission-Based Role Enforcement'
      ]
    },
    {
      icon: 'pi pi-chart-line',
      title: 'AI Business Intelligence',
      badge: 'Next-Gen Analytics',
      description: 'Predictive intelligence integrated across your procurement and inventory operations.',
      points: [
        'Demand Forecasting and Reorder Recommendations',
        'Operational Anomaly Detection & Insights',
        'AI Query and Executive Trend Summaries'
      ]
    },
    {
      icon: 'pi pi-sitemap',
      title: 'Multi-Company & Branch Network',
      badge: 'Scalability',
      description: 'Hierarchical company structures connecting divisions, subsidiaries, and localized branches.',
      points: [
        'Consolidated Multi-Company Reporting',
        'Localized Branch-Level Inventory Control',
        'Flexible Expansion from Free to Dedicated Tiers'
      ]
    }
  ];
}
