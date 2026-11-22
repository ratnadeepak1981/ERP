export interface PublicEcommerceProductDto {
  productId: string;
  productCode: string;
  projectName?: string;
  productName: string;
  onlineDescription?: string;
  displayOrder: number;
  categoryId: string;
  categoryName?: string;
  brandId?: string;
  brandName?: string;
  unitOfMeasure: string;
  sellingPrice: number;
  currency: string;
  inStock?: boolean;
  leadTime?: string;
  imageUrl?: string;
}

export interface PublicCatalogueFilter {
  searchTerm?: string | null;
  categoryId?: string | null;
  brandId?: string | null;
}

export interface ProductCategorySummary {
  id: string;
  name: string;
  description?: string;
  icon?: string;
  itemCount?: number;
}
