import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { IssueStockPageComponent } from './issue-stock-page.component';
import { MasterDataService } from '../../../../../core/services/master-data.service';
import { InventoryService } from '../../../../../core/services/inventory.service';
import { AuthService } from '../../../../../core/auth/auth.service';

describe('IssueStockPageComponent', () => {
  let component: IssueStockPageComponent;
  let fixture: ComponentFixture<IssueStockPageComponent>;
  let masterDataService: MasterDataService;
  let inventoryService: InventoryService;
  let authService: AuthService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [IssueStockPageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => (key === 'id' ? 'new' : null)
              }
            }
          }
        }
      ]
    }).compileComponents();

    masterDataService = TestBed.inject(MasterDataService);
    inventoryService = TestBed.inject(InventoryService);
    authService = TestBed.inject(AuthService);

    vi.spyOn(authService, 'isTenantAdmin').mockReturnValue(true);
    vi.spyOn(masterDataService, 'getWarehouses').mockReturnValue(of([]));
    vi.spyOn(masterDataService, 'getProducts').mockReturnValue(of([]));

    fixture = TestBed.createComponent(IssueStockPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize in create mode for new issue', () => {
    expect(component).toBeTruthy();
    expect(component.mode()).toBe('create');
    expect(component.issueId()).toBeNull();
  });
});
