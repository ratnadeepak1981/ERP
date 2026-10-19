import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ProgressSpinnerModule } from 'primeng/progressspinner';

@Component({
  selector: 'app-loading-state',
  standalone: true,
  imports: [CommonModule, ProgressSpinnerModule],
  templateUrl: './loading-state.component.html',
  styleUrl: './loading-state.component.scss'
})
export class AppLoadingStateComponent {
  readonly message = input<string>('Loading data...');
  readonly size = input<number>(40);
  readonly inline = input<boolean>(false);
}

export { AppLoadingStateComponent as LoadingStateComponent };
