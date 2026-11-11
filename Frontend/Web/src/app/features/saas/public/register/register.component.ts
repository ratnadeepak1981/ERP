import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { TenantRegistrationService } from '../../../../core/services/tenant-registration.service';
import { SubscriptionPlanService } from '../../../../core/services/subscription-plan.service';
import { SubscriptionPlan } from '../../../../core/models/subscription-plan.model';
import { CreateTenantRequest, TenantRegistrationResult } from '../../../../core/models/tenant-registration.model';
import { environment } from '../../../../../environments/environment';

export interface IndustryOption {
  value: string;
  label: string;
  icon: string;
}

export interface CompanyStructureOption {
  value: string;
  label: string;
  description: string;
}

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
    'swagger', 'auth', 'support', 'billing', 'login', 'root', 'dev', 'test',
    'register', 'portal', 'storefront', 'system', 'preview', 'staging', 'prod',
    'demo', 'mfa', 'oauth', 'webhook', 'status', 'health', 'cdn', 'dns',
    'mx', 'pop', 'smtp', 'static', 'ws', 'saas', 'security', 'monitoring',
    'audit', 'db', 'database', 'internal', 'gateway', 'backend'
  ]);

  private static readonly SubdomainRegex = /^[a-z0-9]([a-z0-9-]{1,61}[a-z0-9])?$/;
  private static readonly EmailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

  private readonly route = inject(ActivatedRoute);
  private readonly registrationService = inject(TenantRegistrationService);
  private readonly planService = inject(SubscriptionPlanService);

  // Industry Types supported by backend
  readonly industryOptions: IndustryOption[] = [
    { value: 'Retail', label: 'Retail & Supermarket Mart', icon: 'pi pi-shopping-cart' },
    { value: 'Wholesale', label: 'Wholesale & B2B Distribution', icon: 'pi pi-box' },
    { value: 'Manufacturing', label: 'Discrete & Process Manufacturing', icon: 'pi pi-cog' },
    { value: 'Services', label: 'Professional & Business Services', icon: 'pi pi-briefcase' },
    { value: 'Healthcare', label: 'Healthcare & Pharmaceuticals', icon: 'pi pi-heart' },
    { value: 'Logistics', label: 'Logistics, Supply Chain & Transport', icon: 'pi pi-truck' },
    { value: 'FoodAndBeverage', label: 'Food, Beverage & Hospitality', icon: 'pi pi-apple' },
    { value: 'Other', label: 'General Commercial Enterprise', icon: 'pi pi-building' }
  ];

  // Company Structure options supported by backend ('Single' | 'Multi')
  readonly companyStructureOptions: CompanyStructureOption[] = [
    {
      value: 'Single',
      label: 'Single Legal Entity (Standard)',
      description: 'Single company organization with multiple branch warehouses and locations.'
    },
    {
      value: 'Multi',
      label: 'Multi-Company Enterprise Group',
      description: 'Conglomerate structure supporting multiple holding and operating companies.'
    }
  ];

  // Selected Plan carried from plans step or selected inline
  readonly selectedPlanId = signal<string>('');
  readonly selectedPlan = signal<SubscriptionPlan | null>(null);
  readonly availablePlans = signal<SubscriptionPlan[]>([]);
  readonly isLoadingPlans = signal<boolean>(false);

  // Business Details Form State
  tenantName = '';
  subdomain = '';
  subdomainTouched = false;
  industryType = 'Retail';
  companyStructure = 'Single';
  eCommerceEnabled = false;

  // Primary Administrator Form State
  adminFullName = '';
  adminEmail = '';
  adminUsername = '';
  adminUsernameTouched = false;
  adminPassword = '';
  adminConfirmPassword = '';
  adminPhone = '';

  // Password Visibility Toggles
  showPassword = false;
  showConfirmPassword = false;

  // Inline Field Validation Errors
  readonly tenantNameError = signal<string | null>(null);
  readonly subdomainError = signal<string | null>(null);
  readonly subdomainStatus = signal<'idle' | 'valid' | 'invalid'>('idle');
  readonly adminFullNameError = signal<string | null>(null);
  readonly adminEmailError = signal<string | null>(null);
  readonly adminUsernameError = signal<string | null>(null);
  readonly adminPasswordError = signal<string | null>(null);
  readonly adminConfirmPasswordError = signal<string | null>(null);
  readonly planIdError = signal<string | null>(null);

  // Live URL Preview
  readonly previewHostname = signal<string>('organization.localhost:4200');
  readonly previewFullUrl = signal<string>('http://organization.localhost:4200');

  // API Submission & Confirmation State
  readonly isSubmitting = signal<boolean>(false);
  readonly apiError = signal<string | null>(null);
  readonly apiErrorDetails = signal<string | null>(null);
  readonly registrationResult = signal<TenantRegistrationResult | null>(null);

  // Safe registration confirmation metadata for next steps
  readonly registeredAdminUsername = signal<string>('');
  readonly registeredAdminEmail = signal<string>('');

  ngOnInit(): void {
    const planId = this.route.snapshot.queryParamMap.get('planId') || '';
    this.selectedPlanId.set(planId);

    // Retrieve plan object from route navigation state if available
    const navState = history.state?.plan;
    if (navState && navState.id === planId) {
      this.selectedPlan.set(navState);
    }

    // Load available plans
    this.loadSubscriptionPlans(planId);

    // Initial preview setup
    this.updatePreviewHostname();
  }

  loadSubscriptionPlans(preselectedId: string): void {
    this.isLoadingPlans.set(true);
    this.planService.getSubscriptionPlans().subscribe({
      next: (res) => {
        this.isLoadingPlans.set(false);
        const plans = res?.data || [];
        this.availablePlans.set(plans);

        if (preselectedId) {
          const match = plans.find(p => p.id === preselectedId);
          if (match) {
            this.selectedPlan.set(match);
          }
        } else if (plans.length > 0 && !this.selectedPlanId()) {
          // Preselect first plan if none was selected
          const firstPlan = plans[0];
          this.selectedPlanId.set(firstPlan.id);
          this.selectedPlan.set(firstPlan);
        }
      },
      error: () => {
        this.isLoadingPlans.set(false);
      }
    });
  }

  onPlanSelect(planId: string): void {
    this.selectedPlanId.set(planId);
    const plan = this.availablePlans().find(p => p.id === planId) || null;
    this.selectedPlan.set(plan);
    this.planIdError.set(null);
  }

  onTenantNameChange(): void {
    if (!this.subdomainTouched) {
      const slug = this.generateSlug(this.tenantName);
      this.subdomain = slug;
      this.validateSubdomain();
      this.updatePreviewHostname();
    }

    if (!this.adminUsernameTouched && !this.adminUsername && this.tenantName) {
      const userPrefix = this.generateSlug(this.tenantName).split('-')[0] || 'admin';
      this.adminUsername = `${userPrefix}_admin`;
    }

    if (this.tenantName.trim().length >= 2) {
      this.tenantNameError.set(null);
    }
  }

  onAdminEmailChange(): void {
    if (this.adminEmail && !this.adminUsernameTouched) {
      const parts = this.adminEmail.split('@');
      if (parts[0]) {
        this.adminUsername = parts[0].toLowerCase().replace(/[^a-z0-9_.]+/g, '_');
      }
    }

    if (this.adminEmail && RegisterComponent.EmailRegex.test(this.adminEmail.trim())) {
      this.adminEmailError.set(null);
    }
  }

  onAdminUsernameInput(): void {
    this.adminUsernameTouched = true;
    if (this.adminUsername.trim()) {
      this.adminUsernameError.set(null);
    }
  }

  onSubdomainInput(): void {
    this.subdomainTouched = true;
    this.subdomain = this.subdomain.toLowerCase().trim();
    this.validateSubdomain();
    this.updatePreviewHostname();
  }

  generateSlug(input: string): string {
    return input
      .toLowerCase()
      .trim()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-+|-+$/g, '')
      .slice(0, 50);
  }

  updatePreviewHostname(): void {
    const raw = this.subdomain.trim().toLowerCase();
    const slug = raw || 'organization';
    const previewUrl = this.buildTenantUrl(slug);
    this.previewFullUrl.set(previewUrl);

    try {
      const urlObj = new URL(previewUrl);
      this.previewHostname.set(urlObj.host);
    } catch {
      this.previewHostname.set(`${slug}.localhost:4200`);
    }
  }

  buildTenantUrl(slug: string): string {
    if (typeof window === 'undefined') {
      return `http://${slug}.localhost:4200`;
    }

    const hostname = window.location.hostname || 'localhost';
    const port = window.location.port ? `:${window.location.port}` : '';
    const protocol = window.location.protocol || 'http:';

    // 1. IP address LAN demo (e.g. 192.168.1.5 -> retail.192.168.1.5.nip.io:4200)
    if (/^(\d{1,3}\.){3}\d{1,3}$/.test(hostname)) {
      return `${protocol}//${slug}.${hostname}.nip.io${port}`;
    }

    // 2. Wildcard DNS LAN demo (.nip.io or .sslip.io)
    if (hostname.endsWith('.nip.io') || hostname.endsWith('.sslip.io')) {
      const parts = hostname.split('.');
      const ipParts = parts.filter(p => /^\d+$/.test(p)).join('.');
      if (ipParts) {
        return `${protocol}//${slug}.${ipParts}.nip.io${port}`;
      }
    }

    // 3. Localhost development (retail.localhost:4200)
    if (hostname === 'localhost' || hostname.endsWith('.localhost')) {
      return `${protocol}//${slug}.localhost${port}`;
    }

    // 4. Configured base domain (e.g. apexerp.com)
    const baseDomain = (environment as any).tenantBaseDomain || 'localhost';
    if (baseDomain === 'localhost') {
      return `${protocol}//${slug}.localhost${port}`;
    }

    return `${protocol}//${slug}.${baseDomain}${port}`;
  }

  validateSubdomain(): boolean {
    const raw = this.subdomain.trim().toLowerCase();

    if (!raw) {
      this.subdomainError.set(null);
      this.subdomainStatus.set('idle');
      return true;
    }

    if (raw.length < 3) {
      this.subdomainError.set('Subdomain must be at least 3 characters.');
      this.subdomainStatus.set('invalid');
      return false;
    }

    if (raw.length > 63) {
      this.subdomainError.set('Subdomain must not exceed 63 characters.');
      this.subdomainStatus.set('invalid');
      return false;
    }

    if (!RegisterComponent.SubdomainRegex.test(raw)) {
      this.subdomainError.set('Subdomain may only contain lowercase letters, numbers, and hyphens (cannot start or end with a hyphen).');
      this.subdomainStatus.set('invalid');
      return false;
    }

    if (RegisterComponent.ReservedSubdomains.has(raw)) {
      this.subdomainError.set(`'${raw}' is a reserved subdomain and cannot be used.`);
      this.subdomainStatus.set('invalid');
      return false;
    }

    this.subdomainError.set(null);
    this.subdomainStatus.set('valid');
    return true;
  }

  validatePassword(): boolean {
    // Password complexity check disabled for onboarding simplicity
    this.adminPasswordError.set(null);
    if (this.adminConfirmPassword) {
      this.validateConfirmPassword();
    }
    return true;
  }

  validateConfirmPassword(): boolean {
    if (this.adminPassword && this.adminConfirmPassword && this.adminPassword !== this.adminConfirmPassword) {
      this.adminConfirmPasswordError.set('Passwords do not match.');
      return false;
    }

    this.adminConfirmPasswordError.set(null);
    return true;
  }

  getPasswordStrengthScore(): number {
    return 0;
  }

  validateForm(): boolean {
    let isValid = true;

    // 1. Validate Organization Name
    if (!this.tenantName || !this.tenantName.trim()) {
      this.tenantNameError.set('Organization / Tenant Name is required.');
      isValid = false;
    } else if (this.tenantName.trim().length < 2) {
      this.tenantNameError.set('Organization name must be at least 2 characters.');
      isValid = false;
    } else {
      this.tenantNameError.set(null);
    }

    // 2. Validate Subdomain
    if (!this.validateSubdomain()) {
      isValid = false;
    }

    // 3. Validate Subscription Plan ID
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
    if (!this.adminEmail || !this.adminEmail.trim()) {
      this.adminEmailError.set('Administrator email address is required.');
      isValid = false;
    } else if (!RegisterComponent.EmailRegex.test(this.adminEmail.trim())) {
      this.adminEmailError.set('Please enter a valid email address.');
      isValid = false;
    } else {
      this.adminEmailError.set(null);
    }

    // 6. Validate Password & Confirmation (if entered)
    if (this.adminPassword || this.adminConfirmPassword) {
      if (!this.validatePassword()) {
        isValid = false;
      }
      if (!this.validateConfirmPassword()) {
        isValid = false;
      }
    }

    return isValid;
  }

  onRegister(): void {
    this.apiError.set(null);
    this.apiErrorDetails.set(null);

    if (!this.validateForm()) {
      return;
    }

    if (this.isSubmitting()) {
      return;
    }

    this.isSubmitting.set(true);

    const payload: CreateTenantRequest = {
      name: this.tenantName.trim(),
      subscriptionPlanId: this.selectedPlanId(),
      industryType: this.industryType,
      companyStructure: this.companyStructure,
      eCommerceEnabled: this.eCommerceEnabled,
      adminFullName: this.adminFullName.trim(),
      adminEmail: this.adminEmail.trim(),
      adminPhone: this.adminPhone?.trim() || undefined
    };

    if (this.subdomain && this.subdomain.trim()) {
      payload.subdomain = this.subdomain.trim().toLowerCase();
    }

    this.registrationService.registerTenant(payload).subscribe({
      next: (response) => {
        this.isSubmitting.set(false);
        const data = response?.data;
        if (data) {
          this.registrationResult.set(data);
          this.registeredAdminEmail.set(this.adminEmail.trim());
          this.registeredAdminUsername.set(this.adminUsername.trim() || this.adminEmail.split('@')[0]);
        } else {
          this.apiError.set('Registration response was empty.');
          this.apiErrorDetails.set(response?.message || 'Server did not return registration confirmation data.');
        }
      },
      error: (err) => {
        this.isSubmitting.set(false);
        const statusCode = err?.status ? `HTTP ${err.status}` : 'API Error';
        const serverMsg = err?.error?.message || err?.message || 'Tenant registration request failed.';

        if (err?.status === 409) {
          const lower = (serverMsg || '').toLowerCase();
          this.apiError.set('Registration Conflict');
          this.apiErrorDetails.set(serverMsg || 'A conflicting record was detected. Please review the details below.');

          if (lower.includes('subdomain') || lower.includes('hostname')) {
            this.subdomainError.set(serverMsg || 'Subdomain is already registered. Please choose another.');
            this.tenantNameError.set(null);
          } else if (lower.includes('organization') || lower.includes('tenant with this') || lower.includes('name already exists')) {
            this.tenantNameError.set(serverMsg || 'An organization with this name is already registered. Please choose a different name.');
            this.subdomainError.set(null);
          } else {
            this.tenantNameError.set(null);
            this.subdomainError.set(null);
          }
        } else {
          this.apiError.set(`Registration Request Failed (${statusCode})`);
          this.apiErrorDetails.set(serverMsg);
        }
      }
    });
  }

  getTenantPortalUrl(): string {
    const res = this.registrationResult();
    const host = res?.hostname || res?.domain?.hostname;
    const sub = res?.subdomain || res?.domain?.subdomain;

    if (host) {
      const port = (typeof window !== 'undefined' && window.location.port) ? `:${window.location.port}` : ':4200';
      const protocol = (typeof window !== 'undefined' && window.location.protocol) ? window.location.protocol : 'http:';
      return `${protocol}//${host}${port}/login`;
    }

    if (sub) {
      return this.buildTenantUrl(sub) + '/login';
    }

    return '/login';
  }

  private isValidGuid(val: string): boolean {
    const guidRegex = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
    return guidRegex.test(val);
  }
}
