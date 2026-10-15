import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-empty-state',
  standalone: true,
  imports: [CommonModule, ButtonModule],
  templateUrl: './empty-state.component.html',
  styleUrl: './empty-state.component.scss'
})
export class AppEmptyStateComponent {
  readonly icon = input<string>('pi pi-inbox');
  readonly title = input<string>('No records found');
  readonly description = input<string>('There are no items to display at this time.');
  readonly actionLabel = input<string>();
  readonly actionIcon = input<string>('pi pi-plus');
  readonly action = output<void>();
}
