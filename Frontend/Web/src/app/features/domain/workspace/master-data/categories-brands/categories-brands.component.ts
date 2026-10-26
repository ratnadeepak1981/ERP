import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AppPageHeaderComponent } from '../../../../../shared/components/page-header/page-header.component';
import { AppStatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';
import { AppLoadingStateComponent } from '../../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../../shared/components/error-state/error-state.component';
import { AppEmptyStateComponent } from '../../../../../shared/components/empty-state/empty-state.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { CategoryItem, BrandItem } from '../../../../../core/models/scope.models';

@Component({
  selector: 'app-categories-brands',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    AppPageHeaderComponent,
    AppStatusBadgeComponent,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AppEmptyStateComponent
  ],
  templateUrl: './categories-brands.component.html',
  styleUrl: './categories-brands.component.scss'
})
export class CategoriesBrandsComponent implements OnInit {
  private readonly masterDataService = inject(MasterDataService);

  readonly loading = signal<boolean>(true);
  readonly error = signal<string | null>(null);
  readonly message = signal<{ text: string; severity: 'success' | 'warn' | 'error' } | null>(null);

  readonly activeTab = signal<'categories' | 'brands'>('categories');
  readonly categories = signal<CategoryItem[]>([]);
  readonly brands = signal<BrandItem[]>([]);
  readonly searchTerm = signal<string>('');

  // Inline Category Form State (NO POPUP)
  readonly showCategoryForm = signal<boolean>(false);
  readonly isEditingCategory = signal<boolean>(false);
  readonly editingCategoryId = signal<string | null>(null);
  catCode = '';
  catName = '';
  catDescription = '';
  readonly categoryFormSaving = signal<boolean>(false);

  // Inline Brand Form State (NO POPUP)
  readonly showBrandForm = signal<boolean>(false);
  readonly isEditingBrand = signal<boolean>(false);
  readonly editingBrandId = signal<string | null>(null);
  brandCode = '';
  brandName = '';
  brandDescription = '';
  readonly brandFormSaving = signal<boolean>(false);

  // Inline Confirmation Banner State (NO POPUP)
  readonly pendingAction = signal<{
    type: 'deactivate' | 'reactivate';
    target: 'category' | 'brand';
    id: string;
    name: string;
  } | null>(null);

  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Supply Chain Masters', routerLink: '/app/master-data' },
    { label: 'Categories & Brands' }
  ];

  readonly filteredCategories = computed(() => {
    const q = this.searchTerm().trim().toLowerCase();
    const list = this.categories();
    if (!q) return list;
    return list.filter(c => c.categoryCode.toLowerCase().includes(q) || c.categoryName.toLowerCase().includes(q));
  });

  readonly filteredBrands = computed(() => {
    const q = this.searchTerm().trim().toLowerCase();
    const list = this.brands();
    if (!q) return list;
    return list.filter(b => b.brandCode.toLowerCase().includes(q) || b.brandName.toLowerCase().includes(q));
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.error.set(null);

    this.masterDataService.getCategories().subscribe({
      next: (cats) => {
        this.categories.set(cats);
        this.masterDataService.getBrands().subscribe({
          next: (brands) => {
            this.brands.set(brands);
            this.loading.set(false);
          },
          error: (err) => {
            this.error.set(err?.message || 'Failed to load brands.');
            this.loading.set(false);
          }
        });
      },
      error: (err) => {
        this.error.set(err?.message || 'Failed to load categories.');
        this.loading.set(false);
      }
    });
  }

  // --- Category Actions ---
  openCreateCategory(): void {
    this.isEditingCategory.set(false);
    this.editingCategoryId.set(null);
    this.catCode = '';
    this.catName = '';
    this.catDescription = '';
    this.showCategoryForm.set(true);
    this.message.set(null);
  }

  openEditCategory(cat: CategoryItem): void {
    this.isEditingCategory.set(true);
    this.editingCategoryId.set(cat.categoryId);
    this.catCode = cat.categoryCode;
    this.catName = cat.categoryName;
    this.catDescription = cat.description || '';
    this.showCategoryForm.set(true);
    this.message.set(null);
  }

  cancelCategoryForm(): void {
    this.showCategoryForm.set(false);
  }

  saveCategory(): void {
    if (!this.catName.trim()) return;
    this.categoryFormSaving.set(true);

    if (this.isEditingCategory()) {
      const id = this.editingCategoryId();
      if (!id) return;
      this.masterDataService.updateCategory(id, {
        categoryName: this.catName.trim(),
        description: this.catDescription.trim() || undefined
      }).subscribe({
        next: (updated) => {
          this.categoryFormSaving.set(false);
          this.showCategoryForm.set(false);
          this.message.set({ text: `Category "${updated.categoryName}" updated successfully.`, severity: 'success' });
          this.loadData();
        },
        error: (err) => {
          this.categoryFormSaving.set(false);
          this.message.set({ text: err?.error?.message || 'Failed to update category.', severity: 'error' });
        }
      });
    } else {
      if (!this.catCode.trim()) return;
      this.masterDataService.createCategory({
        categoryCode: this.catCode.trim(),
        categoryName: this.catName.trim(),
        description: this.catDescription.trim() || undefined
      }).subscribe({
        next: (created) => {
          this.categoryFormSaving.set(false);
          this.showCategoryForm.set(false);
          this.message.set({ text: `Category "${created.categoryName}" created successfully.`, severity: 'success' });
          this.loadData();
        },
        error: (err) => {
          this.categoryFormSaving.set(false);
          this.message.set({ text: err?.error?.message || 'Failed to create category.', severity: 'error' });
        }
      });
    }
  }

  // --- Brand Actions ---
  openCreateBrand(): void {
    this.isEditingBrand.set(false);
    this.editingBrandId.set(null);
    this.brandCode = '';
    this.brandName = '';
    this.brandDescription = '';
    this.showBrandForm.set(true);
    this.message.set(null);
  }

  openEditBrand(brand: BrandItem): void {
    this.isEditingBrand.set(true);
    this.editingBrandId.set(brand.brandId);
    this.brandCode = brand.brandCode;
    this.brandName = brand.brandName;
    this.brandDescription = brand.description || '';
    this.showBrandForm.set(true);
    this.message.set(null);
  }

  cancelBrandForm(): void {
    this.showBrandForm.set(false);
  }

  saveBrand(): void {
    if (!this.brandName.trim()) return;
    this.brandFormSaving.set(true);

    if (this.isEditingBrand()) {
      const id = this.editingBrandId();
      if (!id) return;
      this.masterDataService.updateBrand(id, {
        brandName: this.brandName.trim(),
        description: this.brandDescription.trim() || undefined
      }).subscribe({
        next: (updated) => {
          this.brandFormSaving.set(false);
          this.showBrandForm.set(false);
          this.message.set({ text: `Brand "${updated.brandName}" updated successfully.`, severity: 'success' });
          this.loadData();
        },
        error: (err) => {
          this.brandFormSaving.set(false);
          this.message.set({ text: err?.error?.message || 'Failed to update brand.', severity: 'error' });
        }
      });
    } else {
      if (!this.brandCode.trim()) return;
      this.masterDataService.createBrand({
        brandCode: this.brandCode.trim(),
        brandName: this.brandName.trim(),
        description: this.brandDescription.trim() || undefined
      }).subscribe({
        next: (created) => {
          this.brandFormSaving.set(false);
          this.showBrandForm.set(false);
          this.message.set({ text: `Brand "${created.brandName}" created successfully.`, severity: 'success' });
          this.loadData();
        },
        error: (err) => {
          this.brandFormSaving.set(false);
          this.message.set({ text: err?.error?.message || 'Failed to create brand.', severity: 'error' });
        }
      });
    }
  }

  // --- Inline Confirmation Workflow (NO POPUP) ---
  requestAction(type: 'deactivate' | 'reactivate', target: 'category' | 'brand', id: string, name: string): void {
    this.pendingAction.set({ type, target, id, name });
  }

  cancelAction(): void {
    this.pendingAction.set(null);
  }

  confirmAction(): void {
    const act = this.pendingAction();
    if (!act) return;
    this.pendingAction.set(null);

    if (act.target === 'category') {
      if (act.type === 'deactivate') {
        this.masterDataService.deactivateCategory(act.id).subscribe({
          next: () => {
            this.message.set({ text: `Category "${act.name}" deactivated.`, severity: 'warn' });
            this.loadData();
          },
          error: (err) => this.message.set({ text: err?.error?.message || 'Failed to deactivate category.', severity: 'error' })
        });
      } else {
        this.masterDataService.reactivateCategory(act.id).subscribe({
          next: () => {
            this.message.set({ text: `Category "${act.name}" reactivated successfully.`, severity: 'success' });
            this.loadData();
          },
          error: (err) => this.message.set({ text: err?.error?.message || 'Failed to reactivate category.', severity: 'error' })
        });
      }
    } else {
      if (act.type === 'deactivate') {
        this.masterDataService.deactivateBrand(act.id).subscribe({
          next: () => {
            this.message.set({ text: `Brand "${act.name}" deactivated.`, severity: 'warn' });
            this.loadData();
          },
          error: (err) => this.message.set({ text: err?.error?.message || 'Failed to deactivate brand.', severity: 'error' })
        });
      } else {
        this.masterDataService.reactivateBrand(act.id).subscribe({
          next: () => {
            this.message.set({ text: `Brand "${act.name}" reactivated successfully.`, severity: 'success' });
            this.loadData();
          },
          error: (err) => this.message.set({ text: err?.error?.message || 'Failed to reactivate brand.', severity: 'error' })
        });
      }
    }
  }
}
