import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-error-state',
  standalone: true,
  imports: [CommonModule, ButtonModule],
  templateUrl: './error-state.component.html',
  styleUrl: './error-state.component.scss'
})
export class AppErrorStateComponent {
  readonly title = input<string>('Failed to load data');
  readonly message = input<string>('An error occurred while fetching information. Please try again.');
  readonly retryable = input<boolean>(true);
  readonly retry = output<void>();
}
