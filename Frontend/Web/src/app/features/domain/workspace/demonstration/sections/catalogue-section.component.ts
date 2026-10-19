import { Component, ChangeDetectionStrategy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { AppStatusBadgeComponent } from '../../../../../shared/components/status-badge/status-badge.component';

export interface ComponentCatalogueItem {
  name: string;
  category: 'Navigation' | 'Forms' | 'Actions' | 'Feedback' | 'Data Display' | 'Overlays' | 'Layout' | 'Access Control';
  location: string;
  library: 'PrimeNG + Token Wrapper' | 'Shared Custom Angular' | 'PrimeNG Core';
  reuseStatus: 'Reusable & Implemented' | 'New Showcase Component' | 'Available via PrimeNG' | 'Proposed Specification';
  inputs: string;
  outputs: string;
  themeIntegration: string;
  accessibility: string;
  recommendedImprovements: string;
}

@Component({
  selector: 'app-catalogue-section',
  standalone: true,
  imports: [CommonModule, FormsModule, InputTextModule, ButtonModule, AppStatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="catalogue-section-container">
      <div class="catalogue-toolbar">
        <div class="search-box">
          <i class="pi pi-search search-icon"></i>
          <input
            pInputText
            type="text"
            [(ngModel)]="filterQuery"
            placeholder="Search component catalogue..."
            class="catalogue-search-input"
          />
        </div>

        <div class="category-pills">
          <button
            type="button"
            class="category-pill"
            [class.active]="selectedCategory() === 'All'"
            (click)="selectedCategory.set('All')"
          >
            All ({{ items.length }})
          </button>
          @for (cat of categories; track cat) {
            <button
              type="button"
              class="category-pill"
              [class.active]="selectedCategory() === cat"
              (click)="selectedCategory.set(cat)"
            >
              {{ cat }}
            </button>
          }
        </div>
      </div>

      <div class="catalogue-cards-grid">
        @for (item of filteredItems(); track item.name) {
          <div class="catalogue-card erp-card erp-card-elevated">
            <div class="card-header">
              <div class="title-group">
                <span class="category-tag">{{ item.category }}</span>
                <h4 class="component-name">{{ item.name }}</h4>
              </div>
              <app-status-badge
                [label]="item.reuseStatus"
                [severity]="item.reuseStatus === 'Reusable & Implemented' || item.reuseStatus === 'New Showcase Component' ? 'success' : 'info'"
                size="small"
              ></app-status-badge>
            </div>

            <div class="card-meta">
              <div class="meta-row">
                <span class="meta-label">Location:</span>
                <code class="meta-code">{{ item.location }}</code>
              </div>
              <div class="meta-row">
                <span class="meta-label">Library:</span>
                <span class="meta-val">{{ item.library }}</span>
              </div>
            </div>

            <div class="card-specs">
              <div class="spec-block">
                <strong>Inputs:</strong>
                <code>{{ item.inputs }}</code>
              </div>
              <div class="spec-block">
                <strong>Outputs:</strong>
                <code>{{ item.outputs }}</code>
              </div>
            </div>

            <div class="card-details">
              <div class="detail-row">
                <i class="pi pi-palette" title="Theme"></i>
                <span><strong>Theme:</strong> {{ item.themeIntegration }}</span>
              </div>
              <div class="detail-row">
                <i class="pi pi-eye" title="Accessibility"></i>
                <span><strong>A11y:</strong> {{ item.accessibility }}</span>
              </div>
              <div class="detail-row recommendation">
                <i class="pi pi-info-circle" title="Recommendation"></i>
                <span><strong>Next Steps:</strong> {{ item.recommendedImprovements }}</span>
              </div>
            </div>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .catalogue-section-container {
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-4, 16px);

      .catalogue-toolbar {
        display: flex;
        flex-direction: column;
        gap: var(--erp-space-3, 12px);
        background: var(--erp-bg-surface, #ffffff);
        border: 1px solid var(--erp-border, #e2e8f0);
        border-radius: var(--erp-radius-md, 6px);
        padding: 12px 16px;

        .search-box {
          position: relative;
          max-width: 400px;

          .search-icon {
            position: absolute;
            left: 10px;
            top: 50%;
            transform: translateY(-50%);
            color: var(--erp-text-muted, #64748b);
            font-size: 13px;
          }

          .catalogue-search-input {
            width: 100%;
            padding-left: 32px;
            height: 32px;
            font-size: 13px;
            border-radius: var(--erp-radius-sm, 4px);
            border: 1px solid var(--erp-border, #e2e8f0);
            background: var(--erp-bg-surface, #ffffff);
            color: var(--erp-text-main, #0f172a);
          }
        }

        .category-pills {
          display: flex;
          flex-wrap: wrap;
          gap: 6px;

          .category-pill {
            padding: 4px 10px;
            font-size: 12px;
            border-radius: var(--erp-radius-pill, 9999px);
            border: 1px solid var(--erp-border, #e2e8f0);
            background: var(--erp-bg-surface, #ffffff);
            color: var(--erp-text-secondary, #475569);
            cursor: pointer;
            transition: all 0.15s;

            &:hover {
              border-color: var(--erp-primary, #0f6cbd);
              color: var(--erp-primary, #0f6cbd);
            }

            &.active {
              background: var(--erp-primary, #0f6cbd);
              color: #ffffff;
              border-color: var(--erp-primary, #0f6cbd);
              font-weight: 600;
            }
          }
        }
      }

      .catalogue-cards-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(360px, 1fr));
        gap: 16px;

        .catalogue-card {
          display: flex;
          flex-direction: column;
          gap: 12px;
          border-left: 3px solid var(--erp-primary, #0f6cbd);

          .card-header {
            display: flex;
            align-items: flex-start;
            justify-content: space-between;
            gap: 8px;

            .title-group {
              display: flex;
              flex-direction: column;
              gap: 2px;

              .category-tag {
                font-size: 11px;
                font-weight: 600;
                text-transform: uppercase;
                letter-spacing: 0.05em;
                color: var(--erp-primary, #0f6cbd);
              }

              .component-name {
                margin: 0;
                font-size: 15px;
                color: var(--erp-text-main, #0f172a);
              }
            }
          }

          .card-meta {
            display: flex;
            flex-direction: column;
            gap: 4px;
            background: var(--erp-bg-secondary, #f1f5f9);
            padding: 6px 10px;
            border-radius: var(--erp-radius-sm, 4px);
            font-size: 11.5px;

            .meta-row {
              display: flex;
              gap: 6px;

              .meta-label {
                color: var(--erp-text-secondary, #475569);
                font-weight: 500;
              }

              .meta-code {
                color: var(--erp-primary, #0f6cbd);
                word-break: break-all;
              }

              .meta-val {
                color: var(--erp-text-main, #0f172a);
              }
            }
          }

          .card-specs {
            display: flex;
            flex-direction: column;
            gap: 4px;
            font-size: 12px;

            .spec-block {
              code {
                display: block;
                background: var(--erp-bg-secondary, #f1f5f9);
                padding: 4px 8px;
                border-radius: var(--erp-radius-sm, 4px);
                font-family: var(--erp-font-mono);
                font-size: 11px;
                color: var(--erp-text-main, #0f172a);
                margin-top: 2px;
                word-break: break-all;
              }
            }
          }

          .card-details {
            display: flex;
            flex-direction: column;
            gap: 6px;
            font-size: 12px;
            border-top: 1px solid var(--erp-border-subtle, #edf2f7);
            padding-top: 8px;

            .detail-row {
              display: flex;
              align-items: flex-start;
              gap: 8px;
              color: var(--erp-text-secondary, #475569);

              i {
                color: var(--erp-primary, #0f6cbd);
                margin-top: 2px;
                font-size: 12px;
              }

              &.recommendation {
                i {
                  color: var(--erp-warning, #b74700);
                }
              }
            }
          }
        }
      }
    }
  `]
})
export class CatalogueSectionComponent {
  filterQuery: string = '';
  readonly selectedCategory = signal<string>('All');

  readonly categories = [
    'Navigation',
    'Forms',
    'Actions',
    'Feedback',
    'Data Display',
    'Overlays',
    'Layout',
    'Access Control'
  ];

  readonly items: ComponentCatalogueItem[] = [
    {
      name: 'AppDataTableComponent (Read-Only)',
      category: 'Data Display',
      location: 'src/app/shared/components/data-table/data-table.component.ts',
      library: 'PrimeNG + Token Wrapper',
      reuseStatus: 'Reusable & Implemented',
      inputs: 'data, columns, config, loading, paginator, rows, selectionMode, sortMode',
      outputs: 'selectionChange, rowClick, queryChange',
      themeIntegration: 'Uses --erp-border, --erp-bg-secondary, --erp-primary-light row highlight, high-density Excel typography',
      accessibility: 'ARIA role="grid", tabindex="0", keyboard Enter/Space row selection, visible focus ring',
      recommendedImprovements: 'Add server-side virtual scrolling adapter for datasets > 10,000 records'
    },
    {
      name: 'AppEditableDataTableComponent',
      category: 'Data Display',
      location: 'src/app/shared/components/data-table/editable-data-table.component.ts',
      library: 'PrimeNG + Token Wrapper',
      reuseStatus: 'New Showcase Component',
      inputs: 'data, columns, config, allowAddRow, allowDeleteRow, defaultNewRow, rowKey',
      outputs: 'save, cancelEdits, rowDelete, dataChange, selectionChange',
      themeIntegration: 'Amber dirty-state bar (--erp-warning), clear distinction for editable vs readonly cells, theme tokens',
      accessibility: 'Cell-level input focus trap, keyboard navigation, clear ARIA validation error announcements',
      recommendedImprovements: 'Implement batch undo/redo history stack across multiple cell modifications'
    },
    {
      name: 'AppCheckboxComponent',
      category: 'Forms',
      location: 'src/app/shared/components/checkbox/checkbox.component.ts',
      library: 'Shared Custom Angular',
      reuseStatus: 'New Showcase Component',
      inputs: 'config, shape, size, colorToken, disabled, readonly, indeterminate, label, error',
      outputs: 'checkedChange, indeterminateChange',
      themeIntegration: 'Full token binding: --erp-primary, --erp-radius-sm (rounded-square: 4px), :focus-visible ring',
      accessibility: 'WCAG AA role="checkbox", aria-checked ("true"|"false"|"mixed"), keyboard Space/Enter trigger',
      recommendedImprovements: 'Add form validation error tooltip popover for tight table row constraints'
    },
    {
      name: 'TableSearchComponent',
      category: 'Actions',
      location: 'src/app/shared/components/data-table/subcomponents/table-search.component.ts',
      library: 'Shared Custom Angular',
      reuseStatus: 'New Showcase Component',
      inputs: 'placeholder, value, debounceTimeMs, ariaLabel',
      outputs: 'searchChange, searchClear',
      themeIntegration: 'Compact 32px height, Fluent subtle border, clear-button hover, --erp-primary focus outline',
      accessibility: 'Accessible search icon, Esc key to clear search, aria-label binding',
      recommendedImprovements: 'Add configurable regex or search-field selector dropdown'
    },
    {
      name: 'TableFilterComponent',
      category: 'Data Display',
      location: 'src/app/shared/components/data-table/subcomponents/table-filter.component.ts',
      library: 'Shared Custom Angular',
      reuseStatus: 'New Showcase Component',
      inputs: 'column (DataTableColumn), value',
      outputs: 'filterChange, filterClear',
      themeIntegration: 'Excel-style popover panel, elevated shadow (--erp-shadow-md), active accent state',
      accessibility: 'Accessible filter trigger, keyboard Enter to apply, click outside to close',
      recommendedImprovements: 'Add multi-condition compound filtering (AND/OR rules)'
    },
    {
      name: 'TablePaginationComponent',
      category: 'Data Display',
      location: 'src/app/shared/components/data-table/subcomponents/table-pagination.component.ts',
      library: 'Shared Custom Angular',
      reuseStatus: 'New Showcase Component',
      inputs: 'first, rows, totalRecords, rowsPerPageOptions',
      outputs: 'pageChange',
      themeIntegration: 'Subtle border divider, active page pill (--erp-primary), compact typography',
      accessibility: 'ARIA aria-current="page", accessible previous/next/first/last buttons, keyboard navigation',
      recommendedImprovements: 'Add direct "Jump to page #" numeric quick input'
    },
    {
      name: 'TableColumnChooserComponent',
      category: 'Actions',
      location: 'src/app/shared/components/data-table/subcomponents/table-column-chooser.component.ts',
      library: 'Shared Custom Angular',
      reuseStatus: 'New Showcase Component',
      inputs: 'columns (DataTableColumn[])',
      outputs: 'columnsChange',
      themeIntegration: 'Popout overlay with AppCheckbox checklist, Reset Defaults and Show All triggers',
      accessibility: 'Keyboard accessible overlay, aria-expanded toggle, focus retention',
      recommendedImprovements: 'Add drag-and-drop column reordering handle'
    },
    {
      name: 'AppStatusBadgeComponent',
      category: 'Feedback',
      location: 'src/app/shared/components/status-badge/status-badge.component.ts',
      library: 'Shared Custom Angular',
      reuseStatus: 'Reusable & Implemented',
      inputs: 'label, status, severity (success|warning|danger|info|neutral), icon, size',
      outputs: 'None (Display only)',
      themeIntegration: 'High-contrast semantic tints (--erp-success-bg, --erp-warning-border, etc.) for WCAG compliance',
      accessibility: 'Text contrast > 4.5:1, icon companion to avoid color-only status conveyance',
      recommendedImprovements: 'Support animated pulsing dot indicator for in-progress operations'
    },
    {
      name: 'AppPageHeaderComponent',
      category: 'Layout',
      location: 'src/app/shared/components/page-header/page-header.component.ts',
      library: 'Shared Custom Angular',
      reuseStatus: 'Reusable & Implemented',
      inputs: 'title, subtitle, breadcrumbs (BreadcrumbItem[])',
      outputs: 'None (Content projection for [actions])',
      themeIntegration: 'Standardized page header typography, breadcrumb hierarchy, responsive gap',
      accessibility: 'Hierarchical <h1> tag, breadcrumb aria-label="Breadcrumb"',
      recommendedImprovements: 'Add optional back-button anchor for deep nested drilldowns'
    },
    {
      name: 'AppEmptyStateComponent',
      category: 'Feedback',
      location: 'src/app/shared/components/empty-state/empty-state.component.ts',
      library: 'Shared Custom Angular',
      reuseStatus: 'Reusable & Implemented',
      inputs: 'title, description, icon',
      outputs: 'None (Action button projection)',
      themeIntegration: 'Centered layout with --erp-text-muted and subtle icon container',
      accessibility: 'Clear descriptive messaging for screen readers',
      recommendedImprovements: 'Add action button input callback for primary CTA'
    },
    {
      name: 'AppErrorStateComponent',
      category: 'Feedback',
      location: 'src/app/shared/components/error-state/error-state.component.ts',
      library: 'Shared Custom Angular',
      reuseStatus: 'Reusable & Implemented',
      inputs: 'title, message, icon',
      outputs: 'retry',
      themeIntegration: 'Semantic danger accent (--erp-danger), high visibility retry action button',
      accessibility: 'role="alert" for immediate screen reader announcement',
      recommendedImprovements: 'Add expandable technical trace-id accordion for engineering support'
    },
    {
      name: 'AppLoadingStateComponent',
      category: 'Feedback',
      location: 'src/app/shared/components/loading-state/loading-state.component.ts',
      library: 'Shared Custom Angular',
      reuseStatus: 'Reusable & Implemented',
      inputs: 'message, spinnerSize',
      outputs: 'None (Display only)',
      themeIntegration: 'PrimeNG spinner with --erp-primary tint and subtle text',
      accessibility: 'aria-live="polite", aria-busy="true" status indicator',
      recommendedImprovements: 'Add skeleton placeholder rows matching exact table column widths'
    },
    {
      name: 'AppAccessDeniedComponent',
      category: 'Access Control',
      location: 'src/app/shared/components/access-denied/access-denied.component.ts',
      library: 'Shared Custom Angular',
      reuseStatus: 'Reusable & Implemented',
      inputs: 'attemptedRoute, requiredRoles',
      outputs: 'None (Back to safe route CTA)',
      themeIntegration: 'Consistent 403 Forbidden display with shield illustration and RBAC explanation',
      accessibility: 'High contrast text, accessible return link button',
      recommendedImprovements: 'Include "Request Access" workflow button emitting privilege request'
    },
    {
      name: 'TenantLayoutComponent & Shell Navigation',
      category: 'Navigation',
      location: 'src/app/layout/tenant-layout/tenant-layout.component.ts',
      library: 'Shared Custom Angular',
      reuseStatus: 'Reusable & Implemented',
      inputs: 'None (Shell layout with router-outlet)',
      outputs: 'None',
      themeIntegration: 'Collapsible sidebar (64px / 256px), theme toggle trigger, tenant header bar',
      accessibility: 'Keyboard navigation throughout nav links, aria-expanded sidebar toggle',
      recommendedImprovements: 'Add keyboard shortcut (Ctrl+B) to toggle sidebar collapse'
    },
    {
      name: 'PrimeNG Dialog (Enterprise Wrapped)',
      category: 'Overlays',
      location: 'src/styles/_primeng.scss',
      library: 'PrimeNG + Token Wrapper',
      reuseStatus: 'Available via PrimeNG',
      inputs: 'header, visible, modal, draggable, resizable, style',
      outputs: 'visibleChange, onHide',
      themeIntegration: 'Overridden in _primeng.scss: --erp-bg-surface, --erp-shadow-lg, clean header/footer borders',
      accessibility: 'Focus trap within modal, Esc key closes dialog, accessible header title',
      recommendedImprovements: 'Create standalone AppConfirmationDialogComponent wrapper'
    }
  ];

  filteredItems(): ComponentCatalogueItem[] {
    const q = this.filterQuery.trim().toLowerCase();
    const cat = this.selectedCategory();

    return this.items.filter(item => {
      const matchCat = cat === 'All' || item.category === cat;
      const matchQuery =
        !q ||
        item.name.toLowerCase().includes(q) ||
        item.category.toLowerCase().includes(q) ||
        item.location.toLowerCase().includes(q) ||
        item.themeIntegration.toLowerCase().includes(q);

      return matchCat && matchQuery;
    });
  }
}
