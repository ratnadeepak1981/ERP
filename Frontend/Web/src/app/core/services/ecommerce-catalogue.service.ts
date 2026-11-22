import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map, of, catchError } from 'rxjs';
import { buildApiUrl } from '../utils/api-url.util';
import { ApiResponse } from '../models/auth.models';
import { PublicEcommerceProductDto, PublicCatalogueFilter } from '../models/ecommerce-catalogue.models';

@Injectable({
  providedIn: 'root'
})
export class EcommerceCatalogueService {
  private readonly http = inject(HttpClient);

  /**
   * Retrieves published products for the specified selling company.
   * Public / anonymous endpoint (masks internal costs, supplier and margins).
   */
  getPublicCatalogue(
    companyId: string,
    filter?: PublicCatalogueFilter
  ): Observable<PublicEcommerceProductDto[]> {
    if (!companyId || companyId === '00000000-0000-0000-0000-000000000000') {
      return of([]);
    }

    let params = new HttpParams();
    if (filter?.searchTerm) {
      params = params.set('searchTerm', filter.searchTerm);
    }
    if (filter?.categoryId) {
      params = params.set('categoryId', filter.categoryId);
    }
    if (filter?.brandId) {
      params = params.set('brandId', filter.brandId);
    }

    return this.http
      .get<ApiResponse<PublicEcommerceProductDto[] | { items: PublicEcommerceProductDto[] }>>(
        buildApiUrl(`/ecommerce/catalogue/${companyId}/products`),
        { params }
      )
      .pipe(
        map(res => {
          if (res?.data) {
            if (Array.isArray(res.data)) {
              return res.data;
            } else if ((res.data as any).items && Array.isArray((res.data as any).items)) {
              return (res.data as any).items;
            }
          }
          return [];
        }),
        catchError(() => of([]))
      );
  }

  /**
   * Retrieves public details for a single published product.
   */
  getPublicProductDetail(companyId: string, productId: string): Observable<PublicEcommerceProductDto | null> {
    if (!companyId || !productId) {
      return of(null);
    }

    return this.http
      .get<ApiResponse<PublicEcommerceProductDto>>(
        buildApiUrl(`/ecommerce/catalogue/${companyId}/products/${productId}`)
      )
      .pipe(
        map(res => res?.data ?? null),
        catchError(() => of(null))
      );
  }
}
