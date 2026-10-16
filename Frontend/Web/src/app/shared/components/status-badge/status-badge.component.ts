import { Component, input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BadgeSeverity } from '../../models/status-badge.model';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './status-badge.component.html',
  styleUrl: './status-badge.component.scss'
})
export class AppStatusBadgeComponent {
  readonly label = input<string>();
  readonly status = input<string>();
  readonly severity = input<BadgeSeverity>();
  readonly icon = input<string>();
  readonly size = input<'small' | 'medium'>('small');

  readonly displaySeverity = computed<string>(() => {
    if (this.severity()) return this.severity()!;
    const st = (this.status() || '').toLowerCase();
    if (st === 'active' || st === 'operational' || st === 'success') return 'success';
    if (st === 'warning' || st === 'pending') return 'warning';
    if (st === 'danger' || st === 'fail' || st === 'failed' || st === 'error') return 'danger';
    if (st === 'info') return 'info';
    return 'neutral';
  });

  readonly displayLabel = computed<string>(() => {
    return this.label() || this.status() || '';
  });
}

export { AppStatusBadgeComponent as StatusBadgeComponent };
