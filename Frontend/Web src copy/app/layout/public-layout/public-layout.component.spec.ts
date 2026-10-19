import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PublicLayoutComponent } from './public-layout.component';
import { PublicNavService } from '../../core/services/public-nav.service';

describe('PublicLayoutComponent', () => {
  let component: PublicLayoutComponent;
  let fixture: ComponentFixture<PublicLayoutComponent>;
  let navService: PublicNavService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PublicLayoutComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    navService = TestBed.inject(PublicNavService);
    fixture = TestBed.createComponent(PublicLayoutComponent);
    component = fixture.componentInstance;
  });

  it('should create public layout and render brand title', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.brand-title')?.textContent).toContain('ApexERP');
  });

  it('should render exactly two main navigation tabs: Platform and Business Directory', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const tabs = compiled.querySelectorAll('.public-nav button[role="tab"]');
    expect(tabs.length).toBe(2);
    expect(tabs[0].textContent).toContain('Platform');
    expect(tabs[1].textContent).toContain('Business Directory');
  });

  it('should switch active tab when Business Directory tab is clicked', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const directoryTab = compiled.querySelector('#tab-directory') as HTMLButtonElement;
    directoryTab.click();
    fixture.detectChanges();

    expect(navService.activeTab()).toBe('directory');
    expect(directoryTab.getAttribute('aria-selected')).toBe('true');
  });

  it('should render Sign In action', () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const signInLink = compiled.querySelector('a[routerLink="/login"]');
    expect(signInLink).not.toBeNull();
    expect(signInLink?.textContent).toContain('Sign In');
  });
});
