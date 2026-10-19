import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  let service: ThemeService;

  beforeEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('should initialize with default theme clean-enterprise', () => {
    service = new ThemeService();
    expect(service.currentTheme()).toBe('clean-enterprise');
  });

  it('should restore valid saved theme from localStorage', () => {
    localStorage.setItem('erp_ui_theme_preference', 'fluent-dark');
    service = new ThemeService();
    expect(service.currentTheme()).toBe('fluent-dark');
  });

  it('should fallback to clean-enterprise if saved value is invalid', () => {
    localStorage.setItem('erp_ui_theme_preference', 'non-existent-theme' as any);
    service = new ThemeService();
    expect(service.currentTheme()).toBe('clean-enterprise');
  });

  it('should handle localStorage getItem throwing error gracefully', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('SecurityError: Access Denied');
    });
    service = new ThemeService();
    expect(service.currentTheme()).toBe('clean-enterprise');
  });

  it('should handle localStorage setItem throwing error gracefully', () => {
    service = new ThemeService();
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('QuotaExceededError');
    });
    service.setTheme('fluent-dark');
    expect(service.currentTheme()).toBe('fluent-dark');
  });

  it('should update theme and persist to localStorage', () => {
    service = new ThemeService();
    service.setTheme('fluent-dark');
    expect(service.currentTheme()).toBe('fluent-dark');
    expect(localStorage.getItem('erp_ui_theme_preference')).toBe('fluent-dark');
  });

  it('should toggle theme between clean-enterprise and fluent-dark', () => {
    service = new ThemeService();
    expect(service.currentTheme()).toBe('clean-enterprise');
    service.toggleTheme();
    expect(service.currentTheme()).toBe('fluent-dark');
    service.toggleTheme();
    expect(service.currentTheme()).toBe('clean-enterprise');
  });
});
