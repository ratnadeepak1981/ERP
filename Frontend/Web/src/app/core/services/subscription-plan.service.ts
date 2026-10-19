import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/auth.models';
import { SubscriptionPlan } from '../models/subscription-plan.model';
import { buildApiUrl } from '../utils/api-url.util';

@Injectable({
  providedIn: 'root'
})
export class SubscriptionPlanService {
  private readonly http = inject(HttpClient);

  getSubscriptionPlans(): Observable<ApiResponse<SubscriptionPlan[]>> {
    return this.http.get<ApiResponse<SubscriptionPlan[]>>(buildApiUrl('/SubscriptionPlan'));
  }

  getSubscriptionPlanById(id: string): Observable<ApiResponse<SubscriptionPlan>> {
    return this.http.get<ApiResponse<SubscriptionPlan>>(buildApiUrl(`/SubscriptionPlan/${id}`));
  }
}
