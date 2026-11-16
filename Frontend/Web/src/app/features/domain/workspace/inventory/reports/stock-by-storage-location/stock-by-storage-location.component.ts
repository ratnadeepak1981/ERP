import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StockLocationReportViewComponent } from '../stock-location-report-view.component';

@Component({
  selector: 'app-stock-by-storage-location',
  standalone: true,
  imports: [CommonModule, StockLocationReportViewComponent],
  template: `<app-stock-location-report-view mode="storage"></app-stock-location-report-view>`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StockByStorageLocationComponent {}
