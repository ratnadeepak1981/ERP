export interface RegisterIndividualCustomerRequest {
  tenantId?: string;
  firstName: string;
  lastName: string;
  email: string;
  mobileNumber?: string;
  addressLine1?: string;
  addressLine2?: string;
  city?: string;
  postalCode?: string;
  country?: string;
  password: string;
  confirmPassword: string;
}

export interface RegisterCompanyCustomerRequest {
  tenantId?: string;
  companyName: string;
  tradingName?: string;
  companyRegistrationNumber?: string;
  taxNumber?: string;
  companyEmail?: string;
  companyPhone?: string;
  addressLine1?: string;
  addressLine2?: string;
  city?: string;
  postalCode?: string;
  country?: string;
  contactPersonName: string;
  jobTitle?: string;
  email: string;
  mobileNumber?: string;
  password: string;
  confirmPassword: string;
}

export interface CustomerRegistrationResult {
  customerId: string;
  customerCode: string;
  companyName: string;
  contactId: string;
  userId: string;
  email: string;
  isExistingCompanyMatched: boolean;
  requiresAuthorization: boolean;
  canLoginImmediately: boolean;
  message: string;
}
