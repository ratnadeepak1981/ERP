import {
  Component,
  OnInit,
  inject,
  signal,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { StockTransactionFormComponent } from '../stock-transaction-form/stock-transaction-form.component';
import { StockTransactionMode } from '../stock-transaction-form/stock-transaction-form.models';

@Component({
  selector: 'app-stock-receipt-page',
  standalone: true,
  imports: [CommonModule, StockTransactionFormComponent],
  templateUrl: './stock-receipt-page.component.html',
  styleUrl: './stock-receipt-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StockReceiptPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);

  readonly mode = signal<StockTransactionMode>('create');
  readonly receiptId = signal<string | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id && id !== 'new') {
      this.mode.set('view');
      this.receiptId.set(id);
    } else {
      this.mode.set('create');
      this.receiptId.set(null);
    }
  }
}
