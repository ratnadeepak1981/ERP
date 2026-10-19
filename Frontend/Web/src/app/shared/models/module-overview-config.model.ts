import { DataTableColumn, DataTableAction } from './data-table-config.model';

export type ProcessFlowPosition = 'aboveCards' | 'belowCards' | 'belowRecentTransactions';
export type CardColorVariant = 'blue' | 'amber' | 'green' | 'slate' | 'purple' | 'neutral' | string;

export type ProcessStageType = 'standard' | 'optional' | 'conditional';

export type ProcessStageStatus =
  | 'active'
  | 'current'
  | 'completed'
  | 'upcoming'
  | 'disabled'
  | 'not-applicable'
  | 'blocked'
  | 'planned';

export interface DashboardCardConfig {
  id: string;
  title: string;
  icon: string;
  /** Numerical count or formatted string (e.g. currency, ratio) from real authorized API data */
  value: string | number;
  description?: string;
  route?: string;
  queryParams?: Record<string, any>;
  requiredPermissions?: string[];
  colorVariant?: CardColorVariant;
  enabled?: boolean;
  ariaLabel?: string;
  tooltip?: string;
}

export interface ProcessStageConfig {
  id: string;
  /** Short identifier pill or code, e.g. 'PR', 'PO', 'GRN', 'Stock' */
  code?: string;
  /** Descriptive stage label, e.g. 'Requisition', 'Purchase order', 'Goods Receipt', 'Stock Posting' */
  label: string;
  description?: string;
  /** Icon name (e.g. 'cart', 'file-text', 'package', 'boxes', 'check') or PrimeIcon class ('pi pi-...') */
  icon?: string;
  route?: string;
  queryParams?: Record<string, any>;
  requiredPermissions?: string[];
  /** Optional status or metric count if supported by real API (e.g. '3 Drafts' or '12') */
  badge?: string | number;
  /** Status indicates operational state: 'active' | 'current' | 'completed' | 'upcoming' | 'disabled' | 'not-applicable' | 'blocked' | 'planned' */
  status?: ProcessStageStatus;
  /** Explicit status label text to display, e.g. 'Completed', 'Current', 'Upcoming', 'When applicable', 'Optional' */
  statusLabel?: string;
  /** Stage category: standard (default), optional (e.g. PR), or conditional (e.g. stock posting) */
  stageType?: ProcessStageType;
  /** Indicates stage is optional in the business process */
  isOptional?: boolean;
  /** Indicates stage is conditional (e.g. applies when transaction affects stock) */
  isConditional?: boolean;
  /** Short explanation for conditional stage, e.g. 'When applicable' or 'Applies when transaction affects stock' */
  conditionalExplanation?: string;
  /** Alternative path flag (e.g. Direct PO vs PR) */
  isAlternative?: boolean;
  /** Groups alternative paths together, e.g. 'entry' */
  pathGroup?: string;
  /** Specific path identifier, e.g. 'requisition' or 'direct-po' */
  pathId?: string;
  /** Id of the stage where alternative paths converge */
  convergesTo?: string;
  /** Explanation if the stage is blocked */
  blockedReason?: string;
  /** Whether stage is enabled */
  enabled?: boolean;
  /** Whether stage is a planned future capability rather than currently implemented */
  planned?: boolean;
}

export interface ProcessBranchConfig {
  id: string;
  label?: string;
  stages: ProcessStageConfig[];
  convergesToStageId?: string;
}

export interface ProcessFlowConfig {
  enabled: boolean;
  title?: string;
  subtitle?: string;
  position?: ProcessFlowPosition;
  stages: ProcessStageConfig[];
  /** Visual presentation mode: 'horizontal' (default) | 'compact' | 'branching' */
  layout?: 'horizontal' | 'compact' | 'branching';
  /** Whether to show planned/future stages in the operational overview. Defaults to true with disabled/planned treatment. */
  showPlannedStages?: boolean;
  /** Active path id when showing specific transaction path */
  activePathId?: string;
  /** Optional branch configurations */
  branches?: ProcessBranchConfig[];
}

export interface RecentTransactionsConfig<T extends Record<string, any> = Record<string, any>> {
  title?: string;
  subtitle?: string;
  /** Configurable record limit. Default is 10. */
  limit: number;
  showSearch?: boolean;
  showFilters?: boolean;
  showViewAll?: boolean;
  viewAllRoute?: string;
  viewAllLabel?: string;
  searchPlaceholder?: string;
  emptyTitle?: string;
  emptyDescription?: string;
  createActionLabel?: string;
  createActionIcon?: string;
  createActionRoute?: string;
  createActionQueryParams?: Record<string, any>;
  createActionPermission?: string;
}

export interface ModuleNavigationLink {
  label: string;
  icon?: string;
  route?: string;
  queryParams?: Record<string, any>;
  requiredPermissions?: string[];
  severity?: 'primary' | 'secondary' | 'success' | 'info' | 'warn' | 'warning' | 'danger';
  outlined?: boolean;
  disabled?: boolean;
}

export interface ModuleOverviewConfig<T extends Record<string, any> = Record<string, any>> {
  moduleName: string;
  moduleDescription?: string;
  breadcrumbs?: { label: string; routerLink?: string }[];
  dashboardCards: DashboardCardConfig[];
  processFlow?: ProcessFlowConfig;
  recentTransactions: RecentTransactionsConfig<T>;
  navigationLinks?: ModuleNavigationLink[];
}
