import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { ThemeService } from '../../core/services/theme.service';
import { PublicHomeTab, PublicNavService } from '../../core/services/public-nav.service';
import { TenantContextService } from '../../core/services/tenant-context.service';

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
  readonly tenantContext = inject(TenantContextService);
  private readonly router = inject(Router);

  currentYear = new Date().getFullYear();
  readonly activeSection = signal<string>('home');
  readonly mobileMenuOpen = signal<boolean>(false);

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

  toggleMobileMenu(): void {
    this.mobileMenuOpen.update(v => !v);
  }

  closeMobileMenu(): void {
    this.mobileMenuOpen.set(false);
  }

  scrollToSection(sectionId: string, event?: Event): void {
    if (event) {
      event.preventDefault();
    }
    this.activeSection.set(sectionId);
    this.closeMobileMenu();

    const isHomeRoute = this.router.url === '/' || this.router.url.startsWith('/#') || this.router.url.startsWith('/?');

    if (isHomeRoute) {
      if (sectionId === 'home') {
        window.scrollTo({ top: 0, behavior: 'smooth' });
        return;
      }
      const element = document.getElementById(sectionId);
      if (element) {
        element.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }
    } else {
      this.router.navigate(['/'], { fragment: sectionId }).then(() => {
        setTimeout(() => {
          if (sectionId === 'home') {
            window.scrollTo({ top: 0, behavior: 'smooth' });
          } else {
            const element = document.getElementById(sectionId);
            element?.scrollIntoView({ behavior: 'smooth', block: 'start' });
          }
        }, 120);
      });
    }
  }
}

