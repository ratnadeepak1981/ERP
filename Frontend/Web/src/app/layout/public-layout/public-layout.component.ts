import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { ThemeService } from '../../core/services/theme.service';
import { PublicHomeTab, PublicNavService } from '../../core/services/public-nav.service';

@Component({
  selector: 'app-public-layout',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, ButtonModule],
  templateUrl: './public-layout.component.html',
  styleUrl: './public-layout.component.scss'
})
export class PublicLayoutComponent {
  readonly themeService = inject(ThemeService);
  readonly navService = inject(PublicNavService);
  private readonly router = inject(Router);

  currentYear = new Date().getFullYear();

  selectTab(tab: PublicHomeTab): void {
    this.navService.setActiveTab(tab);
    if (this.router.url !== '/' && !this.router.url.startsWith('/?')) {
      this.router.navigate(['/']);
    }
  }

  onTabKeydown(event: KeyboardEvent, currentTab: PublicHomeTab): void {
    if (event.key === 'ArrowRight' || event.key === 'ArrowLeft') {
      event.preventDefault();
      const nextTab: PublicHomeTab = currentTab === 'platform' ? 'directory' : 'platform';
      this.selectTab(nextTab);
      const nextElement = document.getElementById(`tab-${nextTab}`);
      nextElement?.focus();
    }
  }
}
