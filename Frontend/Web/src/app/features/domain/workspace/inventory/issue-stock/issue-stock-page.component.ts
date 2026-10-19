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
  selector: 'app-issue-stock-page',
  standalone: true,
  imports: [CommonModule, StockTransactionFormComponent],
  templateUrl: './issue-stock-page.component.html',
  styleUrl: './issue-stock-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class IssueStockPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);

  readonly mode = signal<StockTransactionMode>('create');
  readonly issueId = signal<string | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id && id !== 'new') {
      this.mode.set('view');
      this.issueId.set(id);
    } else {
      this.mode.set('create');
      this.issueId.set(null);
    }
  }
}
