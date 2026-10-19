import { Component, inject, OnInit, signal, HostListener, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { WorkspaceScopeService } from '../../../../core/services/workspace-scope.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { Company, Branch } from '../../../../core/models/scope.models';

@Component({
  selector: 'app-tenant-scope-switcher',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './tenant-scope-switcher.component.html',
  styleUrl: './tenant-scope-switcher.component.scss'
})
export class TenantScopeSwitcherComponent implements OnInit {
  readonly scopeService = inject(WorkspaceScopeService);
  readonly authService = inject(AuthService);
  private readonly elementRef = inject(ElementRef);

  readonly companyMenuOpen = signal<boolean>(false);
  readonly branchMenuOpen = signal<boolean>(false);

  ngOnInit(): void {
    if (this.scopeService.companies().length === 0) {
      this.scopeService.initializeScope();
    }
  }

  toggleCompanyMenu(): void {
    this.companyMenuOpen.update(v => !v);
    this.branchMenuOpen.set(false);
  }

  toggleBranchMenu(): void {
    if (!this.scopeService.selectedCompany()) return;
    this.branchMenuOpen.update(v => !v);
    this.companyMenuOpen.set(false);
  }

  onSelectCompany(company: Company): void {
    this.companyMenuOpen.set(false);
    this.scopeService.selectCompany(company);
  }

  onSelectBranch(branch: Branch | null): void {
    this.branchMenuOpen.set(false);
    if (branch === null) {
      this.scopeService.selectAllBranches();
    } else {
      this.scopeService.selectBranch(branch);
    }
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.elementRef.nativeElement.contains(event.target)) {
      this.companyMenuOpen.set(false);
      this.branchMenuOpen.set(false);
    }
  }
}
