import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CustomerAddressService } from '../../../../core/services/customer-address.service';
import {
  CustomerAddressPortalDto,
  CreateCustomerAddressPortalRequest,
  UpdateCustomerAddressPortalRequest,
  PortalAddressTypeDto
} from '../../../../core/models/customer-address.models';

@Component({
  selector: 'app-customer-addresses',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="customer-addresses-page">
      <div class="page-title-row">
        <div>
          <h1>Saved Addresses</h1>
          <p>Manage verified delivery, billing, and operational destinations connected to Address Master.</p>
        </div>
        @if (!showForm()) {
          <button type="button" class="erp-button erp-button-primary" (click)="openCreateForm()">
            <i class="pi pi-plus"></i> Add New Address
          </button>
        }
      </div>

      <!-- Feedback Alert Banner -->
      @if (feedbackMessage()) {
        <div class="feedback-banner" [class.feedback-success]="feedbackMessage()?.type === 'success'" [class.feedback-error]="feedbackMessage()?.type === 'error'">
          <i [class]="feedbackMessage()?.type === 'success' ? 'pi pi-check-circle' : 'pi pi-exclamation-triangle'"></i>
          <span>{{ feedbackMessage()?.text }}</span>
          <button type="button" class="close-btn" (click)="feedbackMessage.set(null)">
            <i class="pi pi-times"></i>
          </button>
        </div>
      }

      <!-- Inline Add/Edit Form Section (Zero Popups) -->
      @if (showForm()) {
        <div class="address-form-card erp-card">
          <div class="form-card-header">
            <h3>{{ editingId() ? 'Edit Address' : 'Add New Address' }}</h3>
            <p>Enter the verified address details. This address will be stored in Address Master and linked to your customer profile.</p>
          </div>

          <form (ngSubmit)="saveAddress()" class="address-form-grid">
            <div class="form-group full-width">
              <label for="addressName">Address Label / Name</label>
              <input
                id="addressName"
                type="text"
                name="addressName"
                class="erp-form-control"
                placeholder="e.g. Corporate HQ, Main Delivery Dock, Branch Bay"
                [(ngModel)]="formData.addressName"
              />
            </div>

            <div class="form-group">
              <label for="addressTypeCode">Address Type *</label>
              <select
                id="addressTypeCode"
                name="addressTypeCode"
                class="erp-form-control"
                [(ngModel)]="formData.addressTypeCode"
                required
              >
                <option value="DELIVERY">Delivery Address</option>
                <option value="BILLING">Billing Address</option>
                <option value="HEAD_OFFICE">Head Office</option>
                <option value="BRANCH_OFFICE">Branch Office</option>
                <option value="WAREHOUSE">Warehouse / Dock</option>
                <option value="HOME">Home Address</option>
              </select>
            </div>

            <div class="form-group">
              <label for="country">Country</label>
              <input
                id="country"
                type="text"
                name="country"
                class="erp-form-control"
                placeholder="e.g. Sri Lanka, United States"
                [(ngModel)]="formData.country"
              />
            </div>

            <div class="form-group full-width">
              <label for="addressLine1">Address Line 1 *</label>
              <input
                id="addressLine1"
                type="text"
                name="addressLine1"
                class="erp-form-control"
                placeholder="Street address, building number, P.O. Box"
                [(ngModel)]="formData.addressLine1"
                required
              />
            </div>

            <div class="form-group full-width">
              <label for="addressLine2">Address Line 2</label>
              <input
                id="addressLine2"
                type="text"
                name="addressLine2"
                class="erp-form-control"
                placeholder="Apartment, suite, unit, floor"
                [(ngModel)]="formData.addressLine2"
              />
            </div>

            <div class="form-group">
              <label for="city">City *</label>
              <input
                id="city"
                type="text"
                name="city"
                class="erp-form-control"
                placeholder="City or Town"
                [(ngModel)]="formData.city"
                required
              />
            </div>

            <div class="form-group">
              <label for="stateProvince">State / Province</label>
              <input
                id="stateProvince"
                type="text"
                name="stateProvince"
                class="erp-form-control"
                placeholder="Province or State"
                [(ngModel)]="formData.stateProvince"
              />
            </div>

            <div class="form-group">
              <label for="postalCode">Postal / Zip Code</label>
              <input
                id="postalCode"
                type="text"
                name="postalCode"
                class="erp-form-control"
                placeholder="Postal Code"
                [(ngModel)]="formData.postalCode"
              />
            </div>

            <div class="form-group checkbox-group full-width">
              <label class="checkbox-label">
                <input
                  type="checkbox"
                  name="isDefault"
                  [(ngModel)]="formData.isDefault"
                />
                <span>Set as default address for this address classification</span>
              </label>
            </div>

            <div class="form-actions full-width">
              <button
                type="button"
                class="erp-button erp-button-secondary"
                (click)="cancelForm()"
                [disabled]="isSaving()"
              >
                Cancel
              </button>
              <button
                type="submit"
                class="erp-button erp-button-primary"
                [disabled]="isSaving() || !formData.addressLine1.trim() || !formData.city.trim()"
              >
                @if (isSaving()) {
                  <i class="pi pi-spin pi-spinner"></i>
                  <span>Saving...</span>
                } @else {
                  <i class="pi pi-check"></i>
                  <span>{{ editingId() ? 'Update Address' : 'Save Address' }}</span>
                }
              </button>
            </div>
          </form>
        </div>
      }

      <!-- Loading State -->
      @if (isLoading()) {
        <div class="loading-state erp-card">
          <i class="pi pi-spin pi-spinner"></i>
          <p>Loading your saved addresses from Address Master...</p>
        </div>
      } @else if (addresses().length === 0 && !showForm()) {
        <!-- Empty State -->
        <div class="empty-state erp-card">
          <div class="empty-icon">
            <i class="pi pi-map"></i>
          </div>
          <h3>No Addresses Saved Yet</h3>
          <p>You haven't saved any delivery or billing addresses yet. Add your primary business or residential address to expedite order checkout.</p>
          <button type="button" class="erp-button erp-button-primary" (click)="openCreateForm()">
            <i class="pi pi-plus"></i> Add First Address
          </button>
        </div>
      } @else {
        <!-- Populated Grid -->
        <div class="addresses-grid">
          @for (addr of addresses(); track addr.customerAddressId || addr.addressId) {
            <div class="address-card erp-card" [class.is-default-card]="addr.isDefault">
              <div class="card-top">
                <div class="label-box">
                  <i class="pi pi-map-marker"></i>
                  <strong>{{ addr.addressName || addr.addressTypeName }}</strong>
                </div>
                <div class="pill-group">
                  <span class="type-badge">{{ addr.addressTypeName }}</span>
                  @if (addr.isDefault) {
                    <span class="default-pill">Default</span>
                  }
                </div>
              </div>

              <div class="address-body">
                <p class="line">{{ addr.addressLine1 }}</p>
                @if (addr.addressLine2) {
                  <p class="line">{{ addr.addressLine2 }}</p>
                }
                <p class="city-postal">
                  {{ addr.city }}{{ addr.stateProvince ? ', ' + addr.stateProvince : '' }}{{ addr.postalCode ? ' ' + addr.postalCode : '' }}
                </p>
                @if (addr.country) {
                  <p class="country">{{ addr.country }}</p>
                }
              </div>

              <!-- Contextual Confirmation for Deactivation -->
              @if (confirmDeleteId() === addr.customerAddressId) {
                <div class="delete-confirm-box">
                  <p>Remove this address from your account?</p>
                  <div class="confirm-actions">
                    <button type="button" class="erp-button erp-button-secondary erp-button-sm" (click)="cancelDelete()">
                      Cancel
                    </button>
                    <button type="button" class="erp-button erp-button-danger erp-button-sm" (click)="confirmDelete(addr.customerAddressId)">
                      Confirm Remove
                    </button>
                  </div>
                </div>
              } @else {
                <div class="card-actions">
                  @if (!addr.isDefault && addr.customerAddressId && addr.customerAddressId !== '00000000-0000-0000-0000-000000000000') {
                    <button
                      type="button"
                      class="erp-button erp-button-secondary erp-button-sm"
                      (click)="onSetDefault(addr)"
                      title="Set as default address"
                    >
                      <i class="pi pi-star"></i> Set Default
                    </button>
                  }
                  @if (addr.customerAddressId && addr.customerAddressId !== '00000000-0000-0000-0000-000000000000') {
                    <button
                      type="button"
                      class="erp-button erp-button-secondary erp-button-sm"
                      (click)="openEditForm(addr)"
                    >
                      <i class="pi pi-pencil"></i> Edit
                    </button>
                    <button
                      type="button"
                      class="erp-button erp-button-secondary erp-button-sm text-danger"
                      (click)="promptDelete(addr.customerAddressId)"
                    >
                      <i class="pi pi-trash"></i>
                    </button>
                  }
                </div>
              }
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .customer-addresses-page {
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-4);
    }

    .page-title-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      flex-wrap: wrap;
      gap: var(--erp-space-3);

      h1 {
        font-size: 1.4rem;
        font-weight: 700;
        margin: 0;
        color: var(--erp-text-main);
      }

      p {
        font-size: 0.88rem;
        color: var(--erp-text-secondary);
        margin: 2px 0 0 0;
      }
    }

    .feedback-banner {
      display: flex;
      align-items: center;
      gap: var(--erp-space-2);
      padding: var(--erp-space-3) var(--erp-space-4);
      border-radius: var(--erp-radius-md, 8px);
      font-size: 0.88rem;

      &.feedback-success {
        background-color: #d1fae5;
        color: #065f46;
        border: 1px solid #a7f3d0;
      }

      &.feedback-error {
        background-color: #fee2e2;
        color: #991b1b;
        border: 1px solid #fecaca;
      }

      .close-btn {
        margin-left: auto;
        background: none;
        border: none;
        cursor: pointer;
        color: inherit;
        opacity: 0.7;
        &:hover { opacity: 1; }
      }
    }

    .address-form-card {
      padding: var(--erp-space-5);
      border: 1px solid var(--erp-border-color, #e5e7eb);
      background-color: var(--erp-bg-surface, #ffffff);

      .form-card-header {
        margin-bottom: var(--erp-space-4);
        h3 {
          margin: 0;
          font-size: 1.15rem;
          color: var(--erp-text-main);
        }
        p {
          margin: 4px 0 0 0;
          font-size: 0.85rem;
          color: var(--erp-text-secondary);
        }
      }

      .address-form-grid {
        display: grid;
        grid-template-columns: repeat(2, 1fr);
        gap: var(--erp-space-3);

        @media (max-width: 640px) {
          grid-template-columns: 1fr;
        }

        .full-width {
          grid-column: 1 / -1;
        }

        .form-group {
          display: flex;
          flex-direction: column;
          gap: 4px;

          label {
            font-size: 0.82rem;
            font-weight: 600;
            color: var(--erp-text-main);
          }

          .erp-form-control {
            padding: 8px 12px;
            border: 1px solid var(--erp-border-color, #d1d5db);
            border-radius: var(--erp-radius-sm, 6px);
            font-size: 0.88rem;
            background-color: var(--erp-bg-card, #ffffff);
            color: var(--erp-text-main);
            outline: none;
            transition: border-color 0.15s ease-in-out;

            &:focus {
              border-color: var(--erp-primary);
              box-shadow: 0 0 0 2px rgba(37, 99, 235, 0.15);
            }
          }

          &.checkbox-group {
            margin-top: var(--erp-space-2);
            .checkbox-label {
              display: flex;
              align-items: center;
              gap: 8px;
              cursor: pointer;
              font-size: 0.85rem;
              font-weight: normal;
              color: var(--erp-text-main);
            }
          }
        }

        .form-actions {
          display: flex;
          justify-content: flex-end;
          gap: var(--erp-space-3);
          margin-top: var(--erp-space-3);
          padding-top: var(--erp-space-3);
          border-top: 1px solid var(--erp-border-color, #e5e7eb);
        }
      }
    }

    .loading-state, .empty-state {
      padding: var(--erp-space-6);
      text-align: center;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: var(--erp-space-3);
      color: var(--erp-text-secondary);

      i {
        font-size: 2rem;
        color: var(--erp-primary);
      }

      h3 {
        margin: 0;
        font-size: 1.15rem;
        color: var(--erp-text-main);
      }

      p {
        max-width: 440px;
        margin: 0;
        font-size: 0.88rem;
      }
    }

    .addresses-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
      gap: var(--erp-space-4);
    }

    .address-card {
      padding: var(--erp-space-4);
      display: flex;
      flex-direction: column;
      gap: var(--erp-space-3);
      border: 1px solid var(--erp-border-color, #e5e7eb);
      background-color: var(--erp-bg-surface, #ffffff);
      transition: box-shadow 0.2s ease, border-color 0.2s ease;

      &.is-default-card {
        border-left: 4px solid var(--erp-primary);
      }

      .card-top {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--erp-space-2);

        .label-box {
          display: flex;
          align-items: center;
          gap: var(--erp-space-2);
          color: var(--erp-primary);

          strong {
            color: var(--erp-text-main);
            font-size: 0.95rem;
          }
        }

        .pill-group {
          display: flex;
          align-items: center;
          gap: 6px;

          .type-badge {
            font-size: 0.7rem;
            padding: 2px 6px;
            border-radius: 4px;
            background-color: var(--erp-bg-muted, #f3f4f6);
            color: var(--erp-text-secondary, #4b5563);
          }

          .default-pill {
            font-size: 0.7rem;
            font-weight: 600;
            padding: 2px 8px;
            border-radius: 12px;
            background-color: rgba(37, 99, 235, 0.1);
            color: var(--erp-primary);
          }
        }
      }

      .address-body {
        font-size: 0.88rem;
        color: var(--erp-text-secondary);
        line-height: 1.45;

        .line {
          margin: 0;
        }

        .city-postal, .country {
          margin: 0;
          font-weight: 500;
          color: var(--erp-text-main);
        }
      }

      .delete-confirm-box {
        margin-top: auto;
        padding: var(--erp-space-2);
        background-color: #fee2e2;
        border-radius: var(--erp-radius-sm, 6px);
        font-size: 0.82rem;
        color: #991b1b;
        display: flex;
        flex-direction: column;
        gap: 6px;

        p {
          margin: 0;
          font-weight: 600;
        }

        .confirm-actions {
          display: flex;
          justify-content: flex-end;
          gap: 6px;
        }
      }

      .card-actions {
        margin-top: auto;
        display: flex;
        align-items: center;
        justify-content: flex-end;
        gap: var(--erp-space-2);
        padding-top: var(--erp-space-2);
        border-top: 1px dashed var(--erp-border-color, #e5e7eb);
      }
    }

    .erp-button-danger {
      background-color: #ef4444;
      color: #ffffff;
      border: 1px solid #dc2626;
      &:hover { background-color: #dc2626; }
    }

    .text-danger {
      color: #ef4444 !important;
      &:hover { background-color: #fee2e2 !important; }
    }
  `]
})
export class CustomerAddressesComponent implements OnInit {
  private readonly addressService = inject(CustomerAddressService);

  readonly addresses = signal<CustomerAddressPortalDto[]>([]);
  readonly addressTypes = signal<PortalAddressTypeDto[]>([]);
  readonly isLoading = signal<boolean>(true);
  readonly isSaving = signal<boolean>(false);
  readonly showForm = signal<boolean>(false);
  readonly editingId = signal<string | null>(null);
  readonly confirmDeleteId = signal<string | null>(null);
  readonly feedbackMessage = signal<{ type: 'success' | 'error'; text: string } | null>(null);

  formData = {
    addressName: '',
    addressTypeCode: 'DELIVERY',
    addressLine1: '',
    addressLine2: '',
    city: '',
    stateProvince: '',
    postalCode: '',
    country: '',
    isDefault: false
  };

  ngOnInit(): void {
    this.loadAddresses();
    this.loadAddressTypes();
  }

  loadAddresses(): void {
    this.isLoading.set(true);
    this.addressService.getAddresses().subscribe({
      next: data => {
        this.addresses.set(data);
        this.isLoading.set(false);
      },
      error: err => {
        this.isLoading.set(false);
        this.feedbackMessage.set({
          type: 'error',
          text: err?.error?.message || 'Failed to load addresses from Address Master.'
        });
      }
    });
  }

  loadAddressTypes(): void {
    this.addressService.getAddressTypes().subscribe({
      next: types => this.addressTypes.set(types),
      error: () => {}
    });
  }

  openCreateForm(): void {
    this.editingId.set(null);
    this.formData = {
      addressName: '',
      addressTypeCode: 'DELIVERY',
      addressLine1: '',
      addressLine2: '',
      city: '',
      stateProvince: '',
      postalCode: '',
      country: '',
      isDefault: this.addresses().length === 0
    };
    this.showForm.set(true);
    this.confirmDeleteId.set(null);
  }

  openEditForm(addr: CustomerAddressPortalDto): void {
    this.editingId.set(addr.customerAddressId);
    this.formData = {
      addressName: addr.addressName || '',
      addressTypeCode: addr.addressTypeCode || 'DELIVERY',
      addressLine1: addr.addressLine1 || '',
      addressLine2: addr.addressLine2 || '',
      city: addr.city || '',
      stateProvince: addr.stateProvince || '',
      postalCode: addr.postalCode || '',
      country: addr.country || '',
      isDefault: addr.isDefault
    };
    this.showForm.set(true);
    this.confirmDeleteId.set(null);
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editingId.set(null);
  }

  saveAddress(): void {
    if (!this.formData.addressLine1.trim() || !this.formData.city.trim()) {
      this.feedbackMessage.set({
        type: 'error',
        text: 'Address Line 1 and City are required fields.'
      });
      return;
    }

    this.isSaving.set(true);
    this.feedbackMessage.set(null);

    const editId = this.editingId();
    if (editId) {
      const updateReq: UpdateCustomerAddressPortalRequest = {
        addressName: this.formData.addressName.trim() || undefined,
        addressTypeCode: this.formData.addressTypeCode,
        addressLine1: this.formData.addressLine1.trim(),
        addressLine2: this.formData.addressLine2.trim() || undefined,
        city: this.formData.city.trim(),
        stateProvince: this.formData.stateProvince.trim() || undefined,
        postalCode: this.formData.postalCode.trim() || undefined,
        country: this.formData.country.trim() || undefined,
        isDefault: this.formData.isDefault
      };

      this.addressService.updateAddress(editId, updateReq).subscribe({
        next: () => {
          this.isSaving.set(false);
          this.showForm.set(false);
          this.editingId.set(null);
          this.feedbackMessage.set({
            type: 'success',
            text: 'Address updated successfully in Address Master.'
          });
          this.loadAddresses();
        },
        error: err => {
          this.isSaving.set(false);
          this.feedbackMessage.set({
            type: 'error',
            text: err?.error?.message || 'Failed to update address.'
          });
        }
      });
    } else {
      const createReq: CreateCustomerAddressPortalRequest = {
        addressName: this.formData.addressName.trim() || undefined,
        addressTypeCode: this.formData.addressTypeCode,
        addressLine1: this.formData.addressLine1.trim(),
        addressLine2: this.formData.addressLine2.trim() || undefined,
        city: this.formData.city.trim(),
        stateProvince: this.formData.stateProvince.trim() || undefined,
        postalCode: this.formData.postalCode.trim() || undefined,
        country: this.formData.country.trim() || undefined,
        isDefault: this.formData.isDefault
      };

      this.addressService.createAddress(createReq).subscribe({
        next: () => {
          this.isSaving.set(false);
          this.showForm.set(false);
          this.feedbackMessage.set({
            type: 'success',
            text: 'New address added and linked to your customer profile.'
          });
          this.loadAddresses();
        },
        error: err => {
          this.isSaving.set(false);
          this.feedbackMessage.set({
            type: 'error',
            text: err?.error?.message || 'Failed to add address.'
          });
        }
      });
    }
  }

  onSetDefault(addr: CustomerAddressPortalDto): void {
    this.addressService.setDefault(addr.customerAddressId).subscribe({
      next: () => {
        this.feedbackMessage.set({
          type: 'success',
          text: `Address marked as default for ${addr.addressTypeName}.`
        });
        this.loadAddresses();
      },
      error: err => {
        this.feedbackMessage.set({
          type: 'error',
          text: err?.error?.message || 'Failed to update default address.'
        });
      }
    });
  }

  promptDelete(id: string): void {
    this.confirmDeleteId.set(id);
  }

  cancelDelete(): void {
    this.confirmDeleteId.set(null);
  }

  confirmDelete(id: string): void {
    this.addressService.deleteAddress(id).subscribe({
      next: () => {
        this.confirmDeleteId.set(null);
        this.feedbackMessage.set({
          type: 'success',
          text: 'Address unlinked from your account. Historical transactions remain preserved.'
        });
        this.loadAddresses();
      },
      error: err => {
        this.feedbackMessage.set({
          type: 'error',
          text: err?.error?.message || 'Failed to unlink address.'
        });
      }
    });
  }
}
