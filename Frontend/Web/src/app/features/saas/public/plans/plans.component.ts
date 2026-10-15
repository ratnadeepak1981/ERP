import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { SubscriptionPlanService } from '../../../../core/services/subscription-plan.service';
import { SubscriptionPlan, formatBillingCycle } from '../../../../core/models/subscription-plan.model';
import { AppLoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';
import { AppErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { AppEmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';

@Component({
  selector: 'app-plans',
  standalone: true,
  imports: [
    CommonModule,
    ButtonModule,
    AppLoadingStateComponent,
    AppErrorStateComponent,
    AppEmptyStateComponent
  ],
  templateUrl: './plans.component.html',
  styleUrl: './plans.component.scss'
})
export class PlansComponent implements OnInit {
  private readonly planService = inject(SubscriptionPlanService);
  private readonly router = inject(Router);

  readonly loading = signal<boolean>(true);
  readonly errorMessage = signal<string | null>(null);
  readonly errorDetails = signal<string | null>(null);
  readonly plans = signal<SubscriptionPlan[]>([]);
  readonly selectedPlan = signal<SubscriptionPlan | null>(null);

  ngOnInit(): void {
    this.loadPlans();
  }

  loadPlans(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.errorDetails.set(null);

    this.planService.getSubscriptionPlans().subscribe({
      next: (response) => {
        this.loading.set(false);
        const data = response?.data ?? (Array.isArray(response) ? (response as unknown as SubscriptionPlan[]) : []);
        this.plans.set(data);
      },
      error: (err) => {
        this.loading.set(false);
        const statusCode = err?.status ? `HTTP ${err.status}` : 'Network / API Error';
        const serverMsg = err?.error?.message || err?.message || 'Unable to connect to the Subscription Plans API.';

        this.errorMessage.set(`Backend Subscription Plans Request Failed (${statusCode})`);
        this.errorDetails.set(
          err?.status === 401 || err?.status === 403
            ? `${serverMsg} Note: Endpoint /api/SubscriptionPlan is protected with [RequirePermission("SUBSCRIPTION.VIEW")]. Sign in with platform permissions or retry if session was updated.`
            : serverMsg
        );
      }
    });
  }

  selectPlan(plan: SubscriptionPlan): void {
    this.selectedPlan.set(plan);
    this.router.navigate(['/register'], {
      queryParams: { planId: plan.id },
      state: { plan }
    });
  }

  formatCycle(cycle: number): string {
    return formatBillingCycle(cycle);
  }
}
