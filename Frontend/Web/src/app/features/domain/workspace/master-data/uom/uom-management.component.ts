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
import { UnitOfMeasureItem, UnitOfMeasureConversionItem } from '../../../../../core/models/scope.models';

@Component({
  selector: 'app-uom-management',
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
  templateUrl: './uom-management.component.html',
  styleUrl: './uom-management.component.scss'
})
export class UomManagementComponent implements OnInit {
  private readonly masterDataService = inject(MasterDataService);

  readonly loading = signal<boolean>(true);
  readonly error = signal<string | null>(null);

  readonly activeTab = signal<'units' | 'conversions' | 'calculator'>('units');
  readonly units = signal<UnitOfMeasureItem[]>([]);
  readonly conversions = signal<UnitOfMeasureConversionItem[]>([]);
  readonly searchTerm = signal<string>('');

  // Unit modal
  readonly showUnitModal = signal<boolean>(false);
  readonly isEditingUnit = signal<boolean>(false);
  readonly editingUnitId = signal<string | null>(null);
  unitCode = '';
  unitName = '';
  unitDescription = '';

  // Conversion modal
  readonly showConversionModal = signal<boolean>(false);
  convFromUnitId = '';
  convToUnitId = '';
  convFactor = 1;

  // Calculator
  calcFromUnitId = '';
  calcToUnitId = '';
  calcQuantity = 1;
  readonly calcResult = signal<{ convertedQuantity: number; effectiveFactor: number; isInverse: boolean } | null>(null);
  readonly calcError = signal<string | null>(null);

  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'Supply Chain Masters', routerLink: '/app/master-data' },
    { label: 'Units of Measure & Conversions' }
  ];

  readonly filteredUnits = computed(() => {
    const q = this.searchTerm().trim().toLowerCase();
    const list = this.units();
    if (!q) return list;
    return list.filter(u => u.code.toLowerCase().includes(q) || u.name.toLowerCase().includes(q));
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.error.set(null);

    this.masterDataService.getUnitsOfMeasure().subscribe({
      next: (units) => {
        this.units.set(units);
        this.masterDataService.getUomConversions().subscribe({
          next: (convs) => {
            this.conversions.set(convs);
            this.loading.set(false);
          },
          error: (err) => {
            this.error.set(err?.message || 'Failed to load unit conversions.');
            this.loading.set(false);
          }
        });
      },
      error: (err) => {
        this.error.set(err?.message || 'Failed to load units of measure.');
        this.loading.set(false);
      }
    });
  }

  getUnitCode(id: string): string {
    return this.units().find(u => u.unitOfMeasureId === id)?.code || id;
  }

  getUnitName(id: string): string {
    return this.units().find(u => u.unitOfMeasureId === id)?.name || id;
  }

  // --- Unit actions ---
  openCreateUnitModal(): void {
    this.isEditingUnit.set(false);
    this.editingUnitId.set(null);
    this.unitCode = '';
    this.unitName = '';
    this.unitDescription = '';
    this.showUnitModal.set(true);
  }

  openEditUnitModal(unit: UnitOfMeasureItem): void {
    this.isEditingUnit.set(true);
    this.editingUnitId.set(unit.unitOfMeasureId);
    this.unitCode = unit.code;
    this.unitName = unit.name;
    this.unitDescription = unit.description || '';
    this.showUnitModal.set(true);
  }

  closeUnitModal(): void {
    this.showUnitModal.set(false);
  }

  saveUnit(): void {
    if (!this.unitName.trim()) return;

    if (this.isEditingUnit()) {
      const id = this.editingUnitId();
      if (!id) return;
      this.masterDataService.updateUnitOfMeasure(id, {
        name: this.unitName.trim(),
        description: this.unitDescription.trim() || undefined
      }).subscribe({
        next: () => {
          this.closeUnitModal();
          this.loadData();
        },
        error: (err) => alert(err?.error?.message || 'Failed to update unit of measure.')
      });
    } else {
      if (!this.unitCode.trim()) return;
      this.masterDataService.createUnitOfMeasure({
        code: this.unitCode.trim(),
        name: this.unitName.trim(),
        description: this.unitDescription.trim() || undefined
      }).subscribe({
        next: () => {
          this.closeUnitModal();
          this.loadData();
        },
        error: (err) => alert(err?.error?.message || 'Failed to create unit of measure.')
      });
    }
  }

  deactivateUnit(unit: UnitOfMeasureItem): void {
    if (confirm(`Deactivate unit of measure "${unit.name}" (${unit.code})?`)) {
      this.masterDataService.deactivateUnitOfMeasure(unit.unitOfMeasureId).subscribe({
        next: () => this.loadData(),
        error: (err) => alert(err?.error?.message || 'Failed to deactivate unit of measure.')
      });
    }
  }

  // --- Conversion actions ---
  openCreateConversionModal(): void {
    const list = this.units();
    this.convFromUnitId = list[0]?.unitOfMeasureId || '';
    this.convToUnitId = list[1]?.unitOfMeasureId || '';
    this.convFactor = 1;
    this.showConversionModal.set(true);
  }

  closeConversionModal(): void {
    this.showConversionModal.set(false);
  }

  saveConversion(): void {
    if (!this.convFromUnitId || !this.convToUnitId || this.convFromUnitId === this.convToUnitId) {
      alert('Source and target units must be different.');
      return;
    }
    if (this.convFactor <= 0) {
      alert('Conversion factor must be greater than zero.');
      return;
    }

    this.masterDataService.createUomConversion({
      fromUnitId: this.convFromUnitId,
      toUnitId: this.convToUnitId,
      conversionFactor: this.convFactor
    }).subscribe({
      next: () => {
        this.closeConversionModal();
        this.loadData();
      },
      error: (err) => alert(err?.error?.message || 'Failed to create unit conversion.')
    });
  }

  // --- Conversion calculator ---
  calculateConversion(): void {
    if (!this.calcFromUnitId || !this.calcToUnitId) return;
    this.calcError.set(null);
    this.calcResult.set(null);

    this.masterDataService.convertQuantity(this.calcFromUnitId, this.calcToUnitId, this.calcQuantity).subscribe({
      next: (res) => {
        this.calcResult.set({
          convertedQuantity: res.convertedQuantity,
          effectiveFactor: res.effectiveFactor,
          isInverse: res.isInverse
        });
      },
      error: (err) => {
        this.calcError.set(err?.error?.message || 'Conversion path not found or invalid.');
      }
    });
  }
}
