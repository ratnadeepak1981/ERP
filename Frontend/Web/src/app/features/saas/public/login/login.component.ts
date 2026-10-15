import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { PasswordModule } from 'primeng/password';
import { AuthService } from '../../../../core/auth/auth.service';
import { sanitizeReturnUrl } from '../../../../core/utils/return-url.util';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, InputTextModule, ButtonModule, PasswordModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  tenantCode = '';
  email = '';
  password = '';

  readonly loading = signal<boolean>(false);
  readonly errorMessage = signal<string>('');
  readonly returnUrl = signal<string>('');

  ngOnInit(): void {
    const returnUrlParam = this.route.snapshot.queryParams['returnUrl'];
    if (returnUrlParam) {
      this.returnUrl.set(returnUrlParam);
    }
  }

  onLogin(event?: Event): void {
    if (event) {
      event.preventDefault();
    }
    if (!this.email || !this.password) {
      this.errorMessage.set('Please provide both email and password.');
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');

    this.authService.login({ email: this.email, password: this.password }).subscribe({
      next: (response) => {
        this.loading.set(false);
        if (response?.succeeded) {
          this.navigateAfterLogin();
        } else {
          this.errorMessage.set(response?.message || 'Login failed.');
        }
      },
      error: (err) => {
        this.loading.set(false);
        const serverMsg = err?.error?.message || err?.message || 'Unable to connect to authentication server.';
        this.errorMessage.set(serverMsg);
      }
    });
  }

  onQuickFill(email: string, pass: string): void {
    this.email = email;
    this.password = pass;
    this.errorMessage.set('');
  }

  onDevQuickLogin(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.authService.loginTest('admin', '123').subscribe({
      next: (response) => {
        this.loading.set(false);
        if (response?.succeeded) {
          this.navigateAfterLogin();
        } else {
          this.errorMessage.set(response?.message || 'Test authentication failed.');
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.message || err?.message || 'Dev login endpoint failed.');
      }
    });
  }

  private navigateAfterLogin(): void {
    const rawReturnUrl = this.returnUrl();
    const safeUrl = sanitizeReturnUrl(rawReturnUrl);
    if (safeUrl) {
      this.router.navigateByUrl(safeUrl);
      return;
    }

    if (this.authService.isPlatformAdmin()) {
      this.router.navigate(['/platform-admin']);
    } else if (this.authService.isPlatformStaff()) {
      this.router.navigate(['/platform-staff']);
    } else if (this.authService.isTenantAdmin()) {
      this.router.navigate(['/tenant-admin']);
    } else if (this.authService.isCustomer()) {
      this.router.navigate(['/customer-portal']);
    } else {
      this.router.navigate(['/app/dashboard']);
    }
  }
}
