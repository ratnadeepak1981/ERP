import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { TenantRegistrationService } from '../../../../core/services/tenant-registration.service';
import { SubscriptionPlanService } from '../../../../core/services/subscription-plan.service';
import { SubscriptionPlan } from '../../../../core/models/subscription-plan.model';
import { CreateTenantRequest, TenantRegistrationResult } from '../../../../core/models/tenant-registration.model';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, InputTextModule, ButtonModule],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss'
})
export class RegisterComponent implements OnInit {
  private static readonly ReservedSubdomains = new Set([
    'admin', 'api', 'platform', 'app', 'www', 'mail', 'hangfire',
    'swagger', 'auth', 'support', 'billing', 'login', 'root', 'dev', 'test'
  ]);

  private static readonly SubdomainRegex = /^[a-z0-9]([a-z0-9-]{1,61}[a-z0-9])?$/;

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly registrationService = inject(TenantRegistrationService);
  private readonly planService = inject(SubscriptionPlanService);

  // Selected Plan carried from plans step
  readonly selectedPlanId = signal<string>('');
  readonly selectedPlan = signal<SubscriptionPlan | null>(null);

  // Business Details
  tenantName = '';
  subdomain = '';
  subdomainTouched = false;

  // Administrator Details
  adminFullName = '';
  adminEmail = '';
  adminPhone = '';

  // Validation Errors
  readonly tenantNameError = signal<string | null>(null);
  readonly subdomainError = signal<string | null>(null);
  readonly adminFullNameError = signal<string | null>(null);
  readonly adminEmailError = signal<string | null>(null);
  readonly planIdError = signal<string | null>(null);

  // Live URL Preview
  readonly previewHostname = signal<string>('organization.localhost:4200');

  // API State
  readonly isSubmitting = signal<boolean>(false);
  readonly apiError = signal<string | null>(null);
  readonly apiErrorDetails = signal<string | null>(null);
  readonly registrationResult = signal<TenantRegistrationResult | null>(null);

  ngOnInit(): void {
    const planId = this.route.snapshot.queryParamMap.get('planId') || '';
    this.selectedPlanId.set(planId);

    // Retrieve plan object from route navigation state if available
    const navState = history.state?.plan;
    if (navState && navState.id === planId) {
      this.selectedPlan.set(navState);
    } else if (planId) {
      // Fetch plan details from API if ID was passed directly in URL
      this.planService.getSubscriptionPlanById(planId).subscribe({
        next: (res) => {
          if (res?.data) {
            this.selectedPlan.set(res.data);
          }
        },
        error: () => {
          // Keep planId intact even if lookup fails
        }
      });
    }
  }

  onTenantNameChange(): void {
    if (!this.subdomainTouched) {
      const slug = this.tenantName
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, '-')
        .replace(/^-+|-+$/g, '')
        .slice(0, 30);
      this.subdomain = slug;
      this.updatePreviewHostname();
    }
  }

  onSubdomainInput(): void {
    this.subdomainTouched = true;
    this.validateSubdomain();
    this.updatePreviewHostname();
  }

  updatePreviewHostname(): void {
    const raw = this.subdomain.trim().toLowerCase();
    const host = raw ? `${raw}.localhost:4200` : 'organization.localhost:4200';
    this.previewHostname.set(host);
  }

  validateSubdomain(): boolean {
    const raw = this.subdomain.trim().toLowerCase();
    if (!raw) {
      this.subdomainError.set(null);
      return true;
    }

    if (raw.length < 3) {
      this.subdomainError.set('Subdomain must be at least 3 characters.');
      return false;
    }

    if (raw.length > 63) {
      this.subdomainError.set('Subdomain must not exceed 63 characters.');
      return false;
    }

    if (!RegisterComponent.SubdomainRegex.test(raw)) {
      this.subdomainError.set('Subdomain may only contain lowercase letters, numbers, and hyphens (cannot start or end with a hyphen).');
      return false;
    }

    if (RegisterComponent.ReservedSubdomains.has(raw)) {
      this.subdomainError.set(`'${raw}' is a reserved subdomain and cannot be used.`);
      return false;
    }

    this.subdomainError.set(null);
    return true;
  }

  validateForm(): boolean {
    let isValid = true;

    // 1. Validate Business Name (TenantRules.IsValidName)
    if (!this.tenantName || !this.tenantName.trim()) {
      this.tenantNameError.set('Organization / Tenant Name is required.');
      isValid = false;
    } else if (this.tenantName.trim().length < 2) {
      this.tenantNameError.set('Organization name must be at least 2 characters.');
      isValid = false;
    } else {
      this.tenantNameError.set(null);
    }

    // 2. Validate Subdomain (optional but if provided must meet rules)
    if (!this.validateSubdomain()) {
      isValid = false;
    }

    // 3. Validate Selected Plan ID (required by backend contract)
    if (!this.selectedPlanId() || !this.isValidGuid(this.selectedPlanId())) {
      this.planIdError.set('A valid subscription plan must be selected before registering.');
      isValid = false;
    } else {
      this.planIdError.set(null);
    }

    // 4. Validate Administrator Full Name
    if (!this.adminFullName || !this.adminFullName.trim()) {
      this.adminFullNameError.set('Primary administrator name is required.');
      isValid = false;
    } else {
      this.adminFullNameError.set(null);
    }

    // 5. Validate Administrator Email
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!this.adminEmail || !this.adminEmail.trim()) {
      this.adminEmailError.set('Administrator email address is required.');
      isValid = false;
    } else if (!emailRegex.test(this.adminEmail.trim())) {
      this.adminEmailError.set('Please enter a valid email address.');
      isValid = false;
    } else {
      this.adminEmailError.set(null);
    }

    return isValid;
  }

  onRegister(): void {
    this.apiError.set(null);
    this.apiErrorDetails.set(null);

    if (!this.validateForm()) {
      return;
    }

    // Prevent duplicate submissions
    if (this.isSubmitting()) {
      return;
    }

    this.isSubmitting.set(true);

    const payload: CreateTenantRequest = {
      name: this.tenantName.trim(),
      subscriptionPlanId: this.selectedPlanId()
    };

    if (this.subdomain && this.subdomain.trim()) {
      payload.subdomain = this.subdomain.trim().toLowerCase();
    }

    this.registrationService.registerTenant(payload).subscribe({
      next: (response) => {
        this.isSubmitting.set(false);
        if (response?.succeeded && response.data) {
          this.registrationResult.set(response.data);
        } else if (response?.data) {
          this.registrationResult.set(response.data);
        } else {
          this.apiError.set('Registration failed');
          this.apiErrorDetails.set(response?.message || 'Server did not return registration confirmation data.');
        }
      },
      error: (err) => {
        this.isSubmitting.set(false);
        const statusCode = err?.status ? `HTTP ${err.status}` : 'API Error';
        const serverMsg = err?.error?.message || err?.message || 'Tenant registration request failed.';

        this.apiError.set(`Tenant Registration Request Failed (${statusCode})`);
        this.apiErrorDetails.set(
          err?.status === 401 || err?.status === 403
            ? `${serverMsg} Endpoint /api/Tenant/tenant-registration-test is secured with [RequirePermission("TENANT.CREATE")]. Verify credentials or retry after logging in with platform authority.`
            : serverMsg
        );
      }
    });
  }

  private isValidGuid(val: string): boolean {
    const guidRegex = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
    return guidRegex.test(val);
  }
}
