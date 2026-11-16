import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StockLocationReportViewComponent } from '../stock-location-report-view.component';

@Component({
  selector: 'app-stock-by-staging-location',
  standalone: true,
  imports: [CommonModule, StockLocationReportViewComponent],
  template: `<app-stock-location-report-view mode="staging"></app-stock-location-report-view>`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StockByStagingLocationComponent {}
