import { Injectable, signal } from '@angular/core';

export type AppTheme = 'clean-enterprise' | 'fluent-dark';

@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  private readonly STORAGE_KEY = 'erp_ui_theme_preference';
  readonly currentTheme = signal<AppTheme>('clean-enterprise');

  constructor() {
    this.initTheme();
  }

  private initTheme(): void {
    try {
      const savedTheme = localStorage.getItem(this.STORAGE_KEY) as AppTheme | null;
      if (savedTheme === 'clean-enterprise' || savedTheme === 'fluent-dark') {
        this.setTheme(savedTheme);
      } else {
        this.setTheme('clean-enterprise');
      }
    } catch {
      // Fallback if localStorage is inaccessible (e.g. security sandbox)
      this.setTheme('clean-enterprise');
    }
  }

  setTheme(theme: AppTheme): void {
    this.currentTheme.set(theme);
    try {
      localStorage.setItem(this.STORAGE_KEY, theme);
    } catch {
      // Ignore storage write errors
    }

    if (typeof document !== 'undefined') {
      const root = document.documentElement;
      root.classList.remove('theme-clean-enterprise', 'theme-fluent-dark');
      root.classList.add(`theme-${theme}`);
    }
  }

  toggleTheme(): void {
    const nextTheme: AppTheme = this.currentTheme() === 'clean-enterprise' ? 'fluent-dark' : 'clean-enterprise';
    this.setTheme(nextTheme);
  }
}
