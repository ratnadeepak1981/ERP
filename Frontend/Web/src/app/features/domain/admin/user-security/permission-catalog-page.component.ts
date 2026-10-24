import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { MenuItem } from 'primeng/api';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppLoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { UserSecurityService } from '../../../../core/services/user-security.service';
import { PermissionItem } from '../../../../core/models/security.models';

export interface ModulePermissionGroup {
  category: string;
  count: number;
  items: PermissionItem[];
}

@Component({
  selector: 'app-permission-catalog-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    ButtonModule,
    InputTextModule,
    TooltipModule,
    AppPageHeaderComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent
  ],
  templateUrl: './permission-catalog-page.component.html',
  styleUrl: './permission-catalog-page.component.scss'
})
export class PermissionCatalogPageComponent implements OnInit {
  readonly securityService = inject(UserSecurityService);

  readonly permissions = signal<PermissionItem[]>([]);
  readonly loading = signal<boolean>(true);
  readonly errorMessage = signal<string | null>(null);

  readonly searchQuery = signal<string>('');
  readonly selectedCategory = signal<string>('ALL');

  readonly breadcrumbs: MenuItem[] = [
    { label: 'Tenant Administration', routerLink: '/tenant-admin' },
    { label: 'Capability & Permission Catalog' }
  ];

  readonly categories = computed(() => {
    const set = new Set<string>();
    for (const p of this.permissions()) {
      set.add(p.category || 'General Capabilities');
    }
    return Array.from(set).sort();
  });

  readonly totalCount = computed(() => this.permissions().length);
  readonly moduleCount = computed(() => this.categories().length);

  readonly filteredPermissions = computed(() => {
    let list = this.permissions();

    if (this.selectedCategory() !== 'ALL') {
      list = list.filter(p => (p.category || 'General Capabilities') === this.selectedCategory());
    }

    const q = this.searchQuery().trim().toLowerCase();
    if (q) {
      list = list.filter(p =>
        p.code.toLowerCase().includes(q) ||
        p.name.toLowerCase().includes(q) ||
        (p.description && p.description.toLowerCase().includes(q)) ||
        (p.category && p.category.toLowerCase().includes(q))
      );
    }

    return list;
  });

  readonly groupedPermissions = computed(() => {
    const map = new Map<string, PermissionItem[]>();

    for (const p of this.filteredPermissions()) {
      const cat = p.category || 'General Capabilities';
      if (!map.has(cat)) {
        map.set(cat, []);
      }
      map.get(cat)!.push(p);
    }

    const groups: ModulePermissionGroup[] = [];
    for (const [category, items] of map.entries()) {
      groups.push({
        category,
        count: items.length,
        items
      });
    }

    return groups.sort((a, b) => a.category.localeCompare(b.category));
  });

  ngOnInit(): void {
    this.loadPermissions();
  }

  loadPermissions(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.securityService.getPermissions().subscribe({
      next: (list) => {
        this.permissions.set(list);
        this.loading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err?.message || 'Failed to load system capability permissions.');
        this.loading.set(false);
      }
    });
  }
}
