import { Injectable, signal } from '@angular/core';

export type PublicHomeTab = 'platform' | 'directory';

@Injectable({
  providedIn: 'root'
})
export class PublicNavService {
  /**
   * The currently active tab on the public homepage.
   * Default is 'platform'.
   */
  readonly activeTab = signal<PublicHomeTab>('platform');

  /**
   * Switch the active tab without routing or page reloading.
   */
  setActiveTab(tab: PublicHomeTab): void {
    this.activeTab.set(tab);
  }
}
