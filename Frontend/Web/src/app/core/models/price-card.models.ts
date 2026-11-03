import { PagedResult } from './scope.models';

export enum PriceCardType {
  Purchase = 1,
  Sales = 2
}

export interface PriceCardLineItem {
  priceCardLineId: string;
  tenantId: string;
  priceCardId: string;
  productId: string;
  productCode?: string;
  productName?: string;
  unitOfMeasureId?: string;
  unitOfMeasureCode?: string;
  unitOfMeasureName?: string;
  unitPrice: number;
  minimumQuantity: number;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  isActive: boolean;
  // UI helper for editable grid row tracking
  lineId?: string;
}

export interface PriceCardItem {
  priceCardId: string;
  tenantId: string;
  priceCardCode: string;
  priceCardName: string;
  priceCardType: PriceCardType;
  currency: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  description?: string | null;
  isActive: boolean;
  lineCount: number;
  lines: PriceCardLineItem[];
}

export interface CreatePriceCardLineDto {
  productId: string;
  unitOfMeasureId?: string | null;
  unitPrice: number;
  minimumQuantity?: number;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
}

export interface CreatePriceCardDto {
  priceCardCode: string;
  priceCardName: string;
  priceCardType: PriceCardType;
  currency: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  description?: string | null;
  lines?: CreatePriceCardLineDto[];
}

export interface UpdatePriceCardDto {
  priceCardName: string;
  currency: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  description?: string | null;
  lines?: CreatePriceCardLineDto[];
}

export interface PriceCardQueryParams {
  type?: PriceCardType;
  search?: string;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface ResolvePriceRequest {
  companyId?: string | null;
  branchId?: string | null;
  supplierId?: string | null;
  productId: string;
  priceCardId?: string | null;
  unitOfMeasureId?: string | null;
  currency?: string | null;
  transactionDate?: string | null;
}

export interface PriceResolutionResult {
  found: boolean;
  priceCardId?: string | null;
  priceCardCode?: string | null;
  priceCardName?: string | null;
  priceCardType?: PriceCardType | null;
  productId: string;
  resolvedUomId?: string | null;
  unitPrice: number;
  currency: string;
  exchangeRate: number;
  source?: string | null;
  sourceRecordId?: string | null;
  priceCardLineId?: string | null;
  supplierProductPriceId?: string | null;
  message?: string | null;
}
