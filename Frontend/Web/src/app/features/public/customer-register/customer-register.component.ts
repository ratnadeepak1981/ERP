import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { CustomerRegistrationService } from '../../../core/services/customer-registration.service';
import { TenantContextService } from '../../../core/services/tenant-context.service';
import { ThemeService } from '../../../core/services/theme.service';
import {
  RegisterIndividualCustomerRequest,
  RegisterCompanyCustomerRequest,
  CustomerRegistrationResult
} from '../../../core/models/customer-registration.models';

export type CustomerAccountType = 'individual' | 'organisation';

@Component({
  selector: 'app-customer-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './customer-register.component.html',
  styleUrl: './customer-register.component.scss'
})
export class CustomerRegisterComponent implements OnInit {
  private readonly registrationService = inject(CustomerRegistrationService);
  readonly tenantContext = inject(TenantContextService);
  readonly themeService = inject(ThemeService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  // Account Type Selection: 'individual' (Retail) vs 'organisation' (B2B)
  readonly accountType = signal<CustomerAccountType>('individual');

  // Async & UI State
  readonly loading = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successResult = signal<CustomerRegistrationResult | null>(null);
  readonly returnUrl = signal<string | null>(null);

  // Tenant / Company Branding
  readonly tenantName = computed(() => {
    return this.tenantContext.currentTenant()?.name || this.tenantContext.subdomain() || 'ApexERP';
  });

  readonly tenantIndustry = computed(() => {
    return this.tenantContext.currentTenant()?.industryType || 'Precision Components & Materials';
  });

  // Individual Customer Form Model
  indFirstName = '';
  indLastName = '';
  indEmail = '';
  indMobileNumber = '';
  indAddressLine1 = '';
  indAddressLine2 = '';
  indCity = '';
  indPostalCode = '';
  indCountry = 'Sri Lanka';
  indPassword = '';
  indConfirmPassword = '';

  // Organisation / B2B Customer Form Model
  orgCompanyName = '';
  orgTradingName = '';
  orgRegistrationNumber = '';
  orgTaxNumber = '';
  orgCompanyEmail = '';
  orgCompanyPhone = '';
  orgAddressLine1 = '';
  orgAddressLine2 = '';
  orgCity = '';
  orgPostalCode = '';
  orgCountry = 'Sri Lanka';
  orgContactPersonName = '';
  orgJobTitle = '';
  orgEmail = '';
  orgMobileNumber = '';
  orgPassword = '';
  orgConfirmPassword = '';

  ngOnInit(): void {
    const returnUrlParam = this.route.snapshot.queryParamMap.get('returnUrl');
    if (returnUrlParam) {
      this.returnUrl.set(returnUrlParam);
    }

    const typeParam = this.route.snapshot.queryParamMap.get('type');
    if (typeParam === 'organisation' || typeParam === 'company' || typeParam === 'b2b') {
      this.accountType.set('organisation');
    }
  }

  setAccountType(type: CustomerAccountType): void {
    this.accountType.set(type);
    this.errorMessage.set(null);
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  onSubmit(): void {
    this.errorMessage.set(null);

    if (this.accountType() === 'individual') {
      this.submitIndividual();
    } else {
      this.submitOrganisation();
    }
  }

  private submitIndividual(): void {
    if (!this.indFirstName.trim() || !this.indLastName.trim()) {
      this.errorMessage.set('First name and last name are required.');
      return;
    }

    if (!this.indEmail.trim() || !this.indEmail.includes('@')) {
      this.errorMessage.set('A valid email address is required (this will be your login username).');
      return;
    }

    if (!this.indPassword || this.indPassword.length < 6) {
      this.errorMessage.set('Password must be at least 6 characters long.');
      return;
    }

    if (this.indPassword !== this.indConfirmPassword) {
      this.errorMessage.set('Passwords do not match.');
      return;
    }

    this.loading.set(true);

    const payload: RegisterIndividualCustomerRequest = {
      tenantId: this.tenantContext.currentTenant()?.tenantId || undefined,
      firstName: this.indFirstName.trim(),
      lastName: this.indLastName.trim(),
      email: this.indEmail.trim().toLowerCase(),
      mobileNumber: this.indMobileNumber.trim() || undefined,
      addressLine1: this.indAddressLine1.trim() || undefined,
      addressLine2: this.indAddressLine2.trim() || undefined,
      city: this.indCity.trim() || undefined,
      postalCode: this.indPostalCode.trim() || undefined,
      country: this.indCountry.trim() || undefined,
      password: this.indPassword,
      confirmPassword: this.indConfirmPassword
    };

    this.registrationService.registerIndividual(payload).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res?.succeeded && res.data) {
          this.successResult.set(res.data);
        } else {
          this.errorMessage.set(res?.message || 'Registration failed. Please verify your details.');
        }
      },
      error: (err) => {
        this.loading.set(false);
        const backendMsg = err.error?.message || err.error?.errors?.[0] || 'Registration failed. Please verify that this email is not already registered.';
        this.errorMessage.set(backendMsg);
      }
    });
  }

  private submitOrganisation(): void {
    if (!this.orgCompanyName.trim()) {
      this.errorMessage.set('Organisation / Company legal name is required.');
      return;
    }

    if (!this.orgContactPersonName.trim()) {
      this.errorMessage.set('Contact person full name is required.');
      return;
    }

    if (!this.orgEmail.trim() || !this.orgEmail.includes('@')) {
      this.errorMessage.set('A valid personal business email address is required (this will be your login username).');
      return;
    }

    if (!this.orgPassword || this.orgPassword.length < 6) {
      this.errorMessage.set('Password must be at least 6 characters long.');
      return;
    }

    if (this.orgPassword !== this.orgConfirmPassword) {
      this.errorMessage.set('Passwords do not match.');
      return;
    }

    this.loading.set(true);

    const payload: RegisterCompanyCustomerRequest = {
      tenantId: this.tenantContext.currentTenant()?.tenantId || undefined,
      companyName: this.orgCompanyName.trim(),
      tradingName: this.orgTradingName.trim() || undefined,
      companyRegistrationNumber: this.orgRegistrationNumber.trim() || undefined,
      taxNumber: this.orgTaxNumber.trim() || undefined,
      companyEmail: this.orgCompanyEmail.trim() || undefined,
      companyPhone: this.orgCompanyPhone.trim() || undefined,
      addressLine1: this.orgAddressLine1.trim() || undefined,
      addressLine2: this.orgAddressLine2.trim() || undefined,
      city: this.orgCity.trim() || undefined,
      postalCode: this.orgPostalCode.trim() || undefined,
      country: this.orgCountry.trim() || undefined,
      contactPersonName: this.orgContactPersonName.trim(),
      jobTitle: this.orgJobTitle.trim() || undefined,
      email: this.orgEmail.trim().toLowerCase(),
      mobileNumber: this.orgMobileNumber.trim() || undefined,
      password: this.orgPassword,
      confirmPassword: this.orgConfirmPassword
    };

    this.registrationService.registerCompany(payload).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res?.succeeded && res.data) {
          this.successResult.set(res.data);
        } else {
          this.errorMessage.set(res?.message || 'Organisation registration failed. Please verify your details.');
        }
      },
      error: (err) => {
        this.loading.set(false);
        const backendMsg = err.error?.message || err.error?.errors?.[0] || 'Organisation registration failed. Please verify that this email is not already registered.';
        this.errorMessage.set(backendMsg);
      }
    });
  }

  navigateToLogin(): void {
    const returnUrl = this.returnUrl();
    if (returnUrl) {
      this.router.navigate(['/login'], { queryParams: { returnUrl } });
    } else {
      this.router.navigate(['/login']);
    }
  }
}
