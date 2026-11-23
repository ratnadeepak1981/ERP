export interface CustomerAddressPortalDto {
  customerAddressId: string;
  addressId: string;
  addressName: string;
  addressLine1: string;
  addressLine2?: string;
  city: string;
  stateProvince?: string;
  postalCode?: string;
  countryId?: string;
  country?: string;
  addressTypeId: string;
  addressTypeCode: string;
  addressTypeName: string;
  isDefault: boolean;
  isActive: boolean;
}

export interface CreateCustomerAddressPortalRequest {
  addressName?: string;
  addressLine1: string;
  addressLine2?: string;
  city: string;
  stateProvince?: string;
  postalCode?: string;
  countryId?: string;
  country?: string;
  addressTypeId?: string;
  addressTypeCode?: string;
  isDefault?: boolean;
}

export interface UpdateCustomerAddressPortalRequest {
  addressName?: string;
  addressLine1: string;
  addressLine2?: string;
  city: string;
  stateProvince?: string;
  postalCode?: string;
  countryId?: string;
  country?: string;
  addressTypeId?: string;
  addressTypeCode?: string;
  isDefault?: boolean;
}

export interface PortalAddressTypeDto {
  addressTypeId: string;
  code: string;
  name: string;
}
