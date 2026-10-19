import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { AppStatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';

export interface DirectoryListing {
  id: string;
  name: string;
  industry: string;
  location: string;
  verified: boolean;
  productCount: number;
}

@Component({
  selector: 'app-directory',
  standalone: true,
  imports: [CommonModule, FormsModule, InputTextModule, ButtonModule, AppStatusBadgeComponent],
  templateUrl: './directory.component.html',
  styleUrl: './directory.component.scss'
})
export class DirectoryComponent {
  searchQuery: string = '';

  readonly listings: DirectoryListing[] = [
    { id: '1', name: 'Alpha Precision Steel Fabricators', industry: 'Metals & Precision', location: 'Colombo Industrial Zone', verified: true, productCount: 48 },
    { id: '2', name: 'Beta Industrial Polymers & Resins', industry: 'Chemicals & Polymers', location: 'Gampaha Manufacturing Park', verified: true, productCount: 112 },
    { id: '3', name: 'Gamma Electrical Assemblies Ltd', industry: 'Electronics & Power', location: 'Katunayake EPZ', verified: false, productCount: 29 },
    { id: '4', name: 'Delta Packaging Solutions', industry: 'Paper & Corrugated', location: 'Biyagama Free Trade Zone', verified: true, productCount: 64 }
  ];

  filteredListings(): DirectoryListing[] {
    if (!this.searchQuery) return this.listings;
    const q = this.searchQuery.toLowerCase();
    return this.listings.filter(item =>
      item.name.toLowerCase().includes(q) ||
      item.industry.toLowerCase().includes(q) ||
      item.location.toLowerCase().includes(q)
    );
  }
}
