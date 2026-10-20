import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { AppPageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { AppDataTableComponent } from '../../../../shared/components/data-table/data-table.component';
import { AppStatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { AppErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { TableColumn } from '../../../../shared/models/table-column.model';
import { DemoSalesOrder } from './demonstration.model';

// Showcase Subsections
import { CatalogueSectionComponent } from './sections/catalogue-section.component';
import { ReadonlyTableSectionComponent } from './sections/readonly-table-section.component';
import { EditableTableSectionComponent } from './sections/editable-table-section.component';
import { ModularControlsSectionComponent } from './sections/modular-controls-section.component';
import { CheckboxPlaygroundSectionComponent } from './sections/checkbox-playground-section.component';
import { SharedControlsSectionComponent } from './sections/shared-controls-section.component';
import { AppLookupDropdownSectionComponent } from './sections/lookup-dropdown-section.component';
import { MaterialEditableTableSectionComponent } from './sections/material-editable-table-section.component';

export type ShowcaseTab =
  | 'catalogue'
  | 'readonly'
  | 'editable'
  | 'material-editable'
  | 'modular'
  | 'checkbox'
  | 'lookup'
  | 'shared';

@Component({
  selector: 'app-demonstration',
  standalone: true,
  imports: [
    CommonModule,
    ButtonModule,
    DialogModule,
    AppPageHeaderComponent,
    AppDataTableComponent,
    AppStatusBadgeComponent,
    AppErrorStateComponent,
    CatalogueSectionComponent,
    ReadonlyTableSectionComponent,
    EditableTableSectionComponent,
    MaterialEditableTableSectionComponent,
    ModularControlsSectionComponent,
    CheckboxPlaygroundSectionComponent,
    SharedControlsSectionComponent,
    AppLookupDropdownSectionComponent
  ],
  templateUrl: './demonstration.component.html',
  styleUrl: './demonstration.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DemonstrationComponent {
  readonly breadcrumbs = [
    { label: 'Workspace', routerLink: '/app/dashboard' },
    { label: 'UI Component Showcase' }
  ];

  readonly activeTab = signal<ShowcaseTab>('checkbox');

  // Simulation state signals for testing & backward compatibility
  readonly isSimulatingLoading = signal<boolean>(false);
  readonly isSimulatingEmpty = signal<boolean>(false);
  readonly isSimulatingError = signal<boolean>(false);

  get displayedData(): any[] {
    return this.isSimulatingEmpty() ? [] : new Array(8);
  }

  // Tab switcher
  setTab(tab: ShowcaseTab): void {
    this.activeTab.set(tab);
  }

  // Simulations
  toggleLoadingSimulation(): void {
    this.isSimulatingLoading.update(v => !v);
  }

  toggleEmptySimulation(): void {
    this.isSimulatingEmpty.update(v => !v);
  }

  toggleErrorSimulation(): void {
    this.isSimulatingError.update(v => !v);
  }

  onRetry(): void {
    this.isSimulatingError.set(false);
  }
}
