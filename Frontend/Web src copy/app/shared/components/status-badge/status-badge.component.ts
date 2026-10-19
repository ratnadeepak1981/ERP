import { Component, input } from '@angular/core';
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
  readonly label = input.required<string>();
  readonly severity = input<BadgeSeverity>('neutral');
  readonly icon = input<string>();
  readonly size = input<'small' | 'medium'>('small');
}
