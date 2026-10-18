import {
  Component,
  Input,
  Output,
  EventEmitter,
  inject,
  computed,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { TooltipModule } from 'primeng/tooltip';
import { AuthService } from '../../../core/auth/auth.service';
import { ProcessStageConfig, ProcessStageStatus } from '../../models/module-overview-config.model';

@Component({
  selector: 'app-process-flow',
  standalone: true,
  imports: [CommonModule, TooltipModule],
  templateUrl: './process-flow.component.html',
  styleUrl: './process-flow.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppProcessFlowComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router, { optional: true });

  @Input() stages: ProcessStageConfig[] = [];
  @Input() title: string = 'Business process flow';
  @Input() subtitle: string = '';
  @Input() layout: 'horizontal' | 'compact' | 'branching' = 'horizontal';
  @Input() showPlannedStages: boolean = true;
  @Input() activePathId?: string;

  @Output() stageClick = new EventEmitter<ProcessStageConfig>();

  readonly eligibleStages = computed<ProcessStageConfig[]>(() => {
    return (this.stages || [])
      .filter(stage => {
        if (stage.enabled === false) return false;
        if (!this.showPlannedStages && (stage.planned || stage.status === 'planned')) {
          return false;
        }
        if (this.activePathId && stage.pathId && stage.pathId !== this.activePathId) {
          return false;
        }
        return this.isPermitted(stage);
      });
  });

  readonly hasAlternativePaths = computed<boolean>(() => {
    const list = this.eligibleStages();
    return list.some(s => s.isAlternative || (s.pathGroup && s.pathGroup.length > 0) || !!s.convergesTo);
  });

  isPermitted(stage: ProcessStageConfig): boolean {
    if (stage.enabled === false) return false;
    if (!stage.requiredPermissions || stage.requiredPermissions.length === 0) return true;
    if (
      this.authService.isTenantAdmin() ||
      this.authService.isPlatformAdmin() ||
      this.authService.isPlatformStaff()
    ) {
      return true;
    }
    const userPerms = this.authService.permissions();
    return stage.requiredPermissions.some(p => userPerms.includes(p));
  }

  isInteractive(stage: ProcessStageConfig): boolean {
    if (stage.status === 'disabled' || stage.status === 'planned' || stage.planned) {
      return false;
    }
    return (!!stage.route || this.stageClick.observed);
  }

  onStageClick(stage: ProcessStageConfig): void {
    if (!this.isInteractive(stage)) return;

    this.stageClick.emit(stage);

    if (stage.route && this.router) {
      this.router.navigate([stage.route], {
        queryParams: stage.queryParams
      });
    }
  }

  getStageStatus(stage: ProcessStageConfig): ProcessStageStatus {
    if (stage.status) {
      return stage.status;
    }
    if (stage.planned) {
      return 'planned';
    }
    return 'upcoming';
  }

  isCompleted(stage: ProcessStageConfig): boolean {
    return this.getStageStatus(stage) === 'completed';
  }

  isCurrent(stage: ProcessStageConfig): boolean {
    const status = this.getStageStatus(stage);
    return status === 'current' || status === 'active';
  }

  isPlanned(stage: ProcessStageConfig): boolean {
    return stage.planned === true || this.getStageStatus(stage) === 'planned';
  }

  isBlocked(stage: ProcessStageConfig): boolean {
    return this.getStageStatus(stage) === 'blocked';
  }

  getStatusDisplayLabel(stage: ProcessStageConfig): string {
    if (stage.statusLabel) {
      return stage.statusLabel;
    }
    if (this.isCompleted(stage)) {
      return 'Completed';
    }
    if (this.isCurrent(stage)) {
      return 'Current';
    }
    if (this.isBlocked(stage)) {
      return 'Blocked';
    }
    if (this.isPlanned(stage)) {
      return 'Planned';
    }
    if (stage.status === 'not-applicable') {
      return 'Not applicable';
    }
    if (stage.isOptional || stage.stageType === 'optional') {
      return 'Optional';
    }
    if (stage.isConditional || stage.stageType === 'conditional') {
      return 'When applicable';
    }
    return 'Upcoming';
  }

  getStageCssClasses(stage: ProcessStageConfig): Record<string, boolean> {
    const status = this.getStageStatus(stage);
    return {
      clickable: this.isInteractive(stage),
      disabled: !this.isInteractive(stage),
      [`status-${status}`]: true,
      'stage-optional': !!(stage.isOptional || stage.stageType === 'optional'),
      'stage-conditional': !!(stage.isConditional || stage.stageType === 'conditional'),
      'stage-alternative': !!stage.isAlternative,
      'stage-planned': this.isPlanned(stage),
      'stage-blocked': this.isBlocked(stage)
    };
  }

  getStageTooltip(stage: ProcessStageConfig): string | undefined {
    if (stage.description) {
      return stage.description;
    }
    if (stage.blockedReason && this.isBlocked(stage)) {
      return stage.blockedReason;
    }
    if (stage.conditionalExplanation && (stage.isConditional || stage.stageType === 'conditional')) {
      return stage.conditionalExplanation;
    }
    if (this.isInteractive(stage)) {
      return `Navigate to ${stage.label}`;
    }
    return undefined;
  }

  resolveIcon(stage: ProcessStageConfig): string {
    const icon = (stage.icon || '').toLowerCase();
    if (icon.includes('cart') || icon.includes('po') || icon.includes('order')) return 'cart';
    if (icon.includes('file') || icon.includes('doc') || icon.includes('pr') || icon.includes('req')) return 'file-text';
    if (icon.includes('package') || icon.includes('grn') || icon.includes('receipt')) return 'package';
    if (icon.includes('box') || icon.includes('stock') || icon.includes('inventory')) return 'boxes';
    if (icon.includes('alert') || icon.includes('blocked') || icon.includes('warn')) return 'blocked';

    const id = (stage.id || '').toLowerCase();
    const label = (stage.label || '').toLowerCase();
    if (id.includes('po') || label.includes('purchase order')) return 'cart';
    if (id.includes('pr') || label.includes('requisition')) return 'file-text';
    if (id.includes('grn') || label.includes('receipt')) return 'package';
    if (id.includes('stock') || label.includes('inventory') || label.includes('posting')) return 'boxes';

    return 'default';
  }

  isAlternativeTransition(current: ProcessStageConfig, next: ProcessStageConfig | undefined): boolean {
    if (!next) return false;
    if (current.isAlternative && next.isAlternative) return true;
    if (current.pathGroup && current.pathGroup === next.pathGroup && next.isAlternative) return true;
    return false;
  }

  isConvergingTransition(current: ProcessStageConfig, next: ProcessStageConfig | undefined): boolean {
    if (!next) return false;
    if (current.convergesTo && current.convergesTo === next.id) return true;
    if (current.isAlternative && !next.isAlternative && current.pathGroup && !next.pathGroup) return true;
    return false;
  }
}
