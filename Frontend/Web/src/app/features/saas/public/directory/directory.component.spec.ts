import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DirectoryComponent } from './directory.component';

describe('DirectoryComponent', () => {
  let component: DirectoryComponent;
  let fixture: ComponentFixture<DirectoryComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DirectoryComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(DirectoryComponent);
    component = fixture.componentInstance;
  });

  it('should create and filter listings by query', () => {
    expect(component.filteredListings().length).toBe(4);
    component.searchQuery = 'Polymers';
    expect(component.filteredListings().length).toBe(1);
    expect(component.filteredListings()[0].name).toContain('Beta Industrial Polymers');
  });
});
