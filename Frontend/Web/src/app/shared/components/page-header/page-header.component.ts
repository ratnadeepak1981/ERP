import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BreadcrumbModule } from 'primeng/breadcrumb';
import { MenuItem } from 'primeng/api';

@Component({
  selector: 'app-page-header',
  standalone: true,
  imports: [CommonModule, BreadcrumbModule],
  templateUrl: './page-header.component.html',
  styleUrl: './page-header.component.scss'
})
export class AppPageHeaderComponent {
  readonly title = input.required<string>();
  readonly subtitle = input<string>();
  readonly breadcrumbs = input<MenuItem[]>([]);
}

export { AppPageHeaderComponent as PageHeaderComponent };
