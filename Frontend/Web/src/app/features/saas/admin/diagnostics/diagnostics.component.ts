import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { LoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { AuthService } from '../../../../core/auth/auth.service';
import { DiagnosticStepResult } from '../../../../core/models/auth.models';

interface EndpointTest {
  name: string;
  url: string;
  requirement: string;
  status: 'PENDING' | 'RUNNING' | 'PASS' | 'FAIL';
  response?: any;
  error?: string;
}

@Component({
  selector: 'app-diagnostics',
  standalone: true,
  imports: [
    CommonModule,
    ButtonModule,
    PageHeaderComponent,
    StatusBadgeComponent,
    LoadingStateComponent,
    ErrorStateComponent
  ],
  templateUrl: './diagnostics.component.html',
  styleUrl: './diagnostics.component.scss'
})
export class DiagnosticsComponent implements OnInit {
  readonly authService = inject(AuthService);

  readonly loading = signal<boolean>(false);
  readonly errorMessage = signal<string>('');
  readonly diagnosticSteps = signal<DiagnosticStepResult[]>([]);

  readonly endpointTests = signal<EndpointTest[]>([
    {
      name: 'Security Engine Status',
      url: '/api/Security/status',
      requirement: 'Anonymous / Public',
      status: 'PENDING'
    },
    {
      name: 'JWT Protected Verification',
      url: '/api/Security/protected-test',
      requirement: '[Authorize] with Bearer Token',
      status: 'PENDING'
    },
    {
      name: 'Platform Admin Role Authorization',
      url: '/api/Security/platform-admin-test',
      requirement: '[Authorize(Roles = "Platform Admin")]',
      status: 'PENDING'
    }
  ]);

  ngOnInit(): void {
    this.runAllTests();
  }

  runAllTests(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    // Test 1: Public status
    this.updateTestStatus(0, 'RUNNING');
    this.authService.getSecurityStatus().subscribe({
      next: (res: any) => {
        this.updateTestStatus(0, 'PASS', res?.data || res);
      },
      error: (err: any) => {
        this.updateTestStatus(0, 'FAIL', null, err?.error?.message || err?.message || 'Failed to reach status endpoint');
      }
    });

    // Test 2: Protected check
    this.updateTestStatus(1, 'RUNNING');
    this.authService.checkProtectedTest().subscribe({
      next: (res: any) => {
        this.updateTestStatus(1, 'PASS', res?.data || res);
      },
      error: (err: any) => {
        this.updateTestStatus(1, 'FAIL', null, err?.error?.message || err?.message || 'Protected check rejected');
      }
    });

    // Test 3: Platform Admin test
    this.updateTestStatus(2, 'RUNNING');
    this.authService.checkPlatformAdminTest().subscribe({
      next: (res: any) => {
        this.updateTestStatus(2, 'PASS', res?.data || res);
      },
      error: (err: any) => {
        this.updateTestStatus(2, 'FAIL', null, err?.error?.message || err?.message || 'Platform Admin role check failed');
      }
    });

    // Full 9-step cryptographic diagnostics
    this.authService.runDiagnostic().subscribe({
      next: (res: any) => {
        this.loading.set(false);
        if (res?.succeeded && Array.isArray(res.data)) {
          this.diagnosticSteps.set(res.data);
        } else if (Array.isArray(res)) {
          this.diagnosticSteps.set(res);
        }
      },
      error: (err: any) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.message || err?.message || 'Server diagnostic challenge returned an error');
      }
    });
  }

  private updateTestStatus(index: number, status: EndpointTest['status'], response?: any, error?: string): void {
    this.endpointTests.update(tests => {
      const copy = [...tests];
      copy[index] = { ...copy[index], status, response, error };
      return copy;
    });
  }
}
