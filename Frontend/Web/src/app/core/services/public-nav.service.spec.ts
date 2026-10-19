import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { PublicNavService } from './public-nav.service';

describe('PublicNavService', () => {
  let service: PublicNavService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(PublicNavService);
  });

  it('should initialize with platform as default active tab', () => {
    expect(service.activeTab()).toBe('platform');
  });

  it('should switch active tab to directory and back to platform', () => {
    service.setActiveTab('directory');
    expect(service.activeTab()).toBe('directory');

    service.setActiveTab('platform');
    expect(service.activeTab()).toBe('platform');
  });
});
