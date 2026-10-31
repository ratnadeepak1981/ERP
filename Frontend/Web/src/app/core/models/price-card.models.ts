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
