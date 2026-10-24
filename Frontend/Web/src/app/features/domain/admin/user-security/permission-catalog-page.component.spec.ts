import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { PermissionCatalogPageComponent } from './permission-catalog-page.component';
import { UserSecurityService } from '../../../../core/services/user-security.service';
import { PermissionItem } from '../../../../core/models/security.models';

describe('PermissionCatalogPageComponent', () => {
  let component: PermissionCatalogPageComponent;
  let fixture: ComponentFixture<PermissionCatalogPageComponent>;
  let securityService: UserSecurityService;

  const mockPermissions: PermissionItem[] = [
    { id: '1', code: 'PRODUCT.VIEW', name: 'View Products', category: 'Master Data', description: 'Read products' },
    { id: '2', code: 'INVENTORY.VIEW', name: 'View Stock', category: 'Inventory Management', description: 'Read inventory' },
    { id: '3', code: 'PUTAWAY.POST', name: 'Post Putaway', category: 'Inventory Management', description: 'Post putaway' }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PermissionCatalogPageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        UserSecurityService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PermissionCatalogPageComponent);
    component = fixture.componentInstance;
    securityService = TestBed.inject(UserSecurityService);

    vi.spyOn(securityService, 'getPermissions').mockReturnValue(of(mockPermissions));
  });

  it('should initialize and load permissions', () => {
    fixture.detectChanges();
    expect(component.permissions().length).toBe(3);
    expect(component.totalCount()).toBe(3);
    expect(component.moduleCount()).toBe(2);
    expect(component.categories()).toEqual(['Inventory Management', 'Master Data']);
  });

  it('should group permissions by module', () => {
    fixture.detectChanges();
    const groups = component.groupedPermissions();
    expect(groups.length).toBe(2);
    const invGroup = groups.find(g => g.category === 'Inventory Management');
    expect(invGroup?.items.length).toBe(2);
  });

  it('should filter permissions by search query and category', () => {
    fixture.detectChanges();
    component.searchQuery.set('PUTAWAY');
    expect(component.filteredPermissions().length).toBe(1);
    expect(component.filteredPermissions()[0].code).toBe('PUTAWAY.POST');

    component.searchQuery.set('');
    component.selectedCategory.set('Master Data');
    expect(component.filteredPermissions().length).toBe(1);
    expect(component.filteredPermissions()[0].code).toBe('PRODUCT.VIEW');
  });
});
