export enum ExchangeRateType {
  Daily = 1,
  Monthly = 2
}

export interface ExchangeRateItem {
  exchangeRateId: string;
  tenantId: string;
  fromCurrencyId: string;
  fromCurrencyCode: string;
  toCurrencyId: string;
  toCurrencyCode: string;
  rateType: ExchangeRateType | number | string;
  effectiveDate: string;
  rate: number;
  isActive: boolean;
  createdAt: string;
  modifiedAt?: string | null;
}

export interface ExchangeRateRequest {
  fromCurrencyId: string;
  toCurrencyId: string;
  rateType: ExchangeRateType;
  effectiveDate: string;
  rate: number;
}
