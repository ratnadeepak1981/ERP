import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of, tap, catchError, throwError } from 'rxjs';
import { map } from 'rxjs/operators';
import { buildApiUrl } from '../utils/api-url.util';
import { ApiResponse } from '../models/auth.models';
import { AuthService } from '../auth/auth.service';
import {
  CartDto,
  CartItemDto,
  AddToCartRequest,
  UpdateCartQuantityRequest,
  PendingAddToCartAction,
  EcommerceCartStatus,
  CheckoutRequest,
  CheckoutResultDto
} from '../models/ecommerce-cart.models';

const PENDING_CART_ACTION_STORAGE_KEY = 'apex_pending_cart_action';

@Injectable({
  providedIn: 'root'
})
export class EcommerceCartService {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);

  // Authoritative in-memory state signals derived from backend CartDto
  readonly cart = signal<CartDto | null>(null);
  readonly activeCompanyId = signal<string | null>(null);
  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);

  // Reactive computed helpers
  readonly totalItems = computed<number>(() => this.cart()?.totalItems ?? 0);
  readonly subTotal = computed<number>(() => this.cart()?.subTotal ?? 0);
  readonly totalTax = computed<number>(() => this.cart()?.totalTax ?? 0);
  readonly grandTotal = computed<number>(() => this.cart()?.grandTotal ?? 0);
  readonly currency = computed<string>(() => this.cart()?.currency ?? 'USD');
  readonly lines = computed<CartItemDto[]>(() => this.cart()?.lines ?? []);
  readonly isEmpty = computed<boolean>(() => (this.cart()?.lines?.length ?? 0) === 0);
  readonly isCheckedOut = computed<boolean>(() => this.cart()?.status === EcommerceCartStatus.CheckedOut);

  // Track authenticated user identity to prevent cross-session cart leakage
  private lastAuthenticatedUserId: string | null = null;

  // Asynchronous state isolation & concurrency tracking
  private contextGeneration = 0;
  private requestIdSequence = 0;
  private latestCommittedRequestId = 0;
  private inFlightRequests = 0;

  /**
   * Sets or updates the active Selling Company context.
   * If the company context changes, in-memory cart state is cleared to maintain company isolation,
   * and context generation is bumped to invalidate any in-flight requests from the previous context.
   */
  setCompanyContext(companyId: string | null): void {
    const current = this.activeCompanyId();
    if (current !== companyId) {
      this.contextGeneration++;
      this.activeCompanyId.set(companyId);
      this.resetCart();
    }
  }

  /**
   * Clears the current in-memory cart state, errors, and loading indicators.
   * Invoked on logout, company switch, or customer identity change.
   * Also increments the context generation so outstanding async responses are discarded.
   */
  resetCart(): void {
    this.contextGeneration++;
    this.cart.set(null);
    this.error.set(null);
    this.loading.set(false);
    this.inFlightRequests = 0;
  }

  /**
   * Stores an intended Add-to-Cart action before redirecting an anonymous user to login/register.
   */
  storePendingAction(action: Omit<PendingAddToCartAction, 'timestamp'>): void {
    try {
      const payload: PendingAddToCartAction = {
        ...action,
        timestamp: Date.now()
      };
      sessionStorage.setItem(PENDING_CART_ACTION_STORAGE_KEY, JSON.stringify(payload));
    } catch {
      // Gracefully ignore storage failures
    }
  }

  /**
   * Retrieves the currently stored pending Add-to-Cart action, if any.
   * Stored action is valid for up to 30 minutes.
   */
  getPendingAction(): PendingAddToCartAction | null {
    try {
      const raw = sessionStorage.getItem(PENDING_CART_ACTION_STORAGE_KEY);
      if (!raw) {
        return null;
      }
      const parsed: PendingAddToCartAction = JSON.parse(raw);
      // Expire if older than 30 minutes
      if (Date.now() - parsed.timestamp > 30 * 60 * 1000) {
        this.clearPendingAction();
        return null;
      }
      return parsed;
    } catch {
      this.clearPendingAction();
      return null;
    }
  }

  /**
   * Clears the stored pending Add-to-Cart action.
   */
  clearPendingAction(): void {
    try {
      sessionStorage.removeItem(PENDING_CART_ACTION_STORAGE_KEY);
    } catch {
      // Gracefully ignore
    }
  }

  /**
   * Resumes and executes any stored pending Add-to-Cart action if authenticated.
   * Executes exactly once by immediately removing the stored action before dispatching.
   */
  resumePendingAction(): Observable<CartDto | null> {
    const pending = this.getPendingAction();
    if (!pending) {
      return of(null);
    }

    // Immediately remove to ensure it cannot be executed more than once
    this.clearPendingAction();

    if (!this.authService.isAuthenticated()) {
      return of(null);
    }

    const request: AddToCartRequest = {
      productId: pending.productId,
      quantity: pending.quantity,
      unitOfMeasureId: pending.unitOfMeasureId ?? null,
      productPackagingId: pending.productPackagingId ?? null
    };

    return this.addToCart(request, pending.companyId);
  }

  /**
   * Validates whether a response or error should be applied based on context snapshots.
   */
  private isResponseValid(snapshot: {
    requestId: number;
    contextGen: number;
    companyId: string;
    userId: string | null;
  }): boolean {
    // 1. Context generation must match
    if (snapshot.contextGen !== this.contextGeneration) {
      return false;
    }

    // 2. User must still be authenticated
    if (!this.authService.isAuthenticated()) {
      return false;
    }

    // 3. User identity must match the snapshot
    const currentUserId = this.authService.currentUser()?.userId ?? null;
    if (snapshot.userId !== currentUserId) {
      return false;
    }

    // 4. Active company must match snapshot company
    if (snapshot.companyId !== this.activeCompanyId()) {
      return false;
    }

    // 5. Older responses must not overwrite newer responses
    if (snapshot.requestId < this.latestCommittedRequestId) {
      return false;
    }

    return true;
  }

  private beginRequest(targetCompanyId: string): {
    requestId: number;
    contextGen: number;
    companyId: string;
    userId: string | null;
  } {
    this.inFlightRequests++;
    this.loading.set(true);
    this.error.set(null);

    return {
      requestId: ++this.requestIdSequence,
      contextGen: this.contextGeneration,
      companyId: targetCompanyId,
      userId: this.authService.currentUser()?.userId ?? null
    };
  }

  private completeRequest(): void {
    if (this.inFlightRequests > 0) {
      this.inFlightRequests--;
    }
    if (this.inFlightRequests === 0) {
      this.loading.set(false);
    }
  }

  /**
   * Retrieves the active shopping cart for the specified Selling Company.
   * Requires authenticated customer context.
   *
   * Endpoint: GET /api/ecommerce/cart?companyId={companyId}
   */
  getCart(companyId?: string): Observable<CartDto> {
    const targetCompanyId = this.resolveCompanyId(companyId);
    if (!this.validateAuthAndScope(targetCompanyId)) {
      return throwError(() => new Error(this.error() ?? 'Validation failed.'));
    }

    const snapshot = this.beginRequest(targetCompanyId);
    const params = new HttpParams().set('companyId', targetCompanyId);

    return this.http
      .get<ApiResponse<CartDto>>(buildApiUrl('/ecommerce/cart'), { params })
      .pipe(
        map(res => this.extractCartData(res)),
        tap(cart => {
          this.applySuccessResponse(cart, snapshot);
        }),
        catchError(err => this.handleErrorWithSnapshot(err, snapshot))
      );
  }

  /**
   * Adds an item to the active shopping cart.
   * Server resolves unit price, discounts, and sales tax authoritative.
   * Client NEVER submits price, tax, or total values.
   *
   * Endpoint: POST /api/ecommerce/cart/items?companyId={companyId}
   */
  addToCart(request: AddToCartRequest, companyId?: string): Observable<CartDto> {
    const targetCompanyId = this.resolveCompanyId(companyId);
    if (!this.validateAuthAndScope(targetCompanyId)) {
      return throwError(() => new Error(this.error() ?? 'Validation failed.'));
    }

    if (!request.productId || request.productId.trim() === '') {
      const err = 'ProductId is required to add an item to the cart.';
      this.error.set(err);
      return throwError(() => new Error(err));
    }

    if (request.quantity <= 0) {
      const err = 'Quantity must be greater than zero.';
      this.error.set(err);
      return throwError(() => new Error(err));
    }

    const snapshot = this.beginRequest(targetCompanyId);

    // Strictly limit payload properties to match backend AddToCartRequest
    const payload: AddToCartRequest = {
      productId: request.productId,
      quantity: request.quantity,
      unitOfMeasureId: request.unitOfMeasureId ?? null,
      productPackagingId: request.productPackagingId ?? null
    };

    const params = new HttpParams().set('companyId', targetCompanyId);

    return this.http
      .post<ApiResponse<CartDto>>(buildApiUrl('/ecommerce/cart/items'), payload, { params })
      .pipe(
        map(res => this.extractCartData(res)),
        tap(cart => {
          this.applySuccessResponse(cart, snapshot);
        }),
        catchError(err => this.handleErrorWithSnapshot(err, snapshot))
      );
  }

  /**
   * Updates the requested quantity of an existing cart line.
   * Passing quantity <= 0 causes the backend to remove the line.
   *
   * Endpoint: PUT /api/ecommerce/cart/items/{lineId}?companyId={companyId}
   */
  updateLineQuantity(lineId: string, quantity: number, companyId?: string): Observable<CartDto> {
    const targetCompanyId = this.resolveCompanyId(companyId);
    if (!this.validateAuthAndScope(targetCompanyId)) {
      return throwError(() => new Error(this.error() ?? 'Validation failed.'));
    }

    if (!lineId || lineId.trim() === '') {
      const err = 'LineId is required to update cart quantity.';
      this.error.set(err);
      return throwError(() => new Error(err));
    }

    const snapshot = this.beginRequest(targetCompanyId);
    const payload: UpdateCartQuantityRequest = { quantity };
    const params = new HttpParams().set('companyId', targetCompanyId);

    return this.http
      .put<ApiResponse<CartDto>>(buildApiUrl(`/ecommerce/cart/items/${lineId}`), payload, { params })
      .pipe(
        map(res => this.extractCartData(res)),
        tap(cart => {
          this.applySuccessResponse(cart, snapshot);
        }),
        catchError(err => this.handleErrorWithSnapshot(err, snapshot))
      );
  }

  /**
   * Removes a specific line from the active shopping cart.
   *
   * Endpoint: DELETE /api/ecommerce/cart/items/{lineId}?companyId={companyId}
   */
  removeLine(lineId: string, companyId?: string): Observable<CartDto> {
    const targetCompanyId = this.resolveCompanyId(companyId);
    if (!this.validateAuthAndScope(targetCompanyId)) {
      return throwError(() => new Error(this.error() ?? 'Validation failed.'));
    }

    if (!lineId || lineId.trim() === '') {
      const err = 'LineId is required to remove item from cart.';
      this.error.set(err);
      return throwError(() => new Error(err));
    }

    const snapshot = this.beginRequest(targetCompanyId);
    const params = new HttpParams().set('companyId', targetCompanyId);

    return this.http
      .delete<ApiResponse<CartDto>>(buildApiUrl(`/ecommerce/cart/items/${lineId}`), { params })
      .pipe(
        map(res => this.extractCartData(res)),
        tap(cart => {
          this.applySuccessResponse(cart, snapshot);
        }),
        catchError(err => this.handleErrorWithSnapshot(err, snapshot))
      );
  }

  /**
   * Clears all items from the active shopping cart while retaining the cart header.
   *
   * Endpoint: DELETE /api/ecommerce/cart?companyId={companyId}
   */
  clearCart(companyId?: string): Observable<CartDto> {
    const targetCompanyId = this.resolveCompanyId(companyId);
    if (!this.validateAuthAndScope(targetCompanyId)) {
      return throwError(() => new Error(this.error() ?? 'Validation failed.'));
    }

    const snapshot = this.beginRequest(targetCompanyId);
    const params = new HttpParams().set('companyId', targetCompanyId);

    return this.http
      .delete<ApiResponse<CartDto>>(buildApiUrl('/ecommerce/cart'), { params })
      .pipe(
        map(res => this.extractCartData(res)),
        tap(cart => {
          this.applySuccessResponse(cart, snapshot);
        }),
        catchError(err => this.handleErrorWithSnapshot(err, snapshot))
      );
  }

  /**
   * Dispatches authenticated customer checkout to create a Draft Sales Order.
   * Transmits delivery address, customer notes, requested delivery date, and UUID v4 idempotency key.
   * Strictly enforces Stage 1B context-isolation and stale-response protection.
   * On confirmed success, marks local cart checked out and updates cart state.
   *
   * Endpoint: POST /api/ecommerce/cart/checkout?companyId={companyId}
   */
  checkout(request: CheckoutRequest, companyId?: string): Observable<CheckoutResultDto> {
    const targetCompanyId = this.resolveCompanyId(companyId);
    if (!this.validateAuthAndScope(targetCompanyId)) {
      return throwError(() => new Error(this.error() ?? 'Validation failed.'));
    }

    if (!request) {
      const err = 'Checkout request payload is required.';
      this.error.set(err);
      return throwError(() => new Error(err));
    }

    const snapshot = this.beginRequest(targetCompanyId);
    const params = new HttpParams().set('companyId', targetCompanyId);

    return this.http
      .post<ApiResponse<CheckoutResultDto>>(buildApiUrl('/ecommerce/cart/checkout'), request, { params })
      .pipe(
        map(res => {
          if (!res || !res.data) {
            throw new Error(res?.message || 'Invalid server response for checkout.');
          }
          return res.data;
        }),
        tap(result => {
          this.completeRequest();
          if (this.isResponseValid(snapshot)) {
            this.latestCommittedRequestId = snapshot.requestId;
            // On successful checkout, mark local cart state as checked out with 0 items
            const current = this.cart();
            if (current) {
              this.cart.set({
                ...current,
                status: EcommerceCartStatus.CheckedOut,
                totalItems: 0,
                subTotal: 0,
                totalDiscount: 0,
                totalTax: 0,
                grandTotal: 0,
                lines: []
              });
            }
          }
        }),
        catchError(err => this.handleErrorWithSnapshot(err, snapshot))
      );
  }

  private applySuccessResponse(
    cart: CartDto,
    snapshot: {
      requestId: number;
      contextGen: number;
      companyId: string;
      userId: string | null;
    }
  ): void {
    this.completeRequest();

    if (this.isResponseValid(snapshot)) {
      this.latestCommittedRequestId = snapshot.requestId;
      this.cart.set(cart);
    }
  }

  private handleErrorWithSnapshot(
    err: any,
    snapshot: {
      requestId: number;
      contextGen: number;
      companyId: string;
      userId: string | null;
    }
  ): Observable<never> {
    this.completeRequest();

    const message =
      err?.error?.message ||
      err?.message ||
      'Shopping cart service encountered an error.';

    if (this.isResponseValid(snapshot)) {
      this.latestCommittedRequestId = snapshot.requestId;
      this.error.set(message);
    }

    return throwError(() => new Error(message));
  }

  /**
   * Resolves the effective Selling Company context:
   * Explicit parameter > activeCompanyId signal > empty string.
   */
  private resolveCompanyId(companyId?: string): string {
    if (companyId && companyId.trim() !== '') {
      return companyId.trim();
    }
    return this.activeCompanyId() ?? '';
  }

  /**
   * Validates authenticated customer status and company scope.
   * Guarantees that carts cannot be modified or accessed without an authenticated customer session.
   */
  private validateAuthAndScope(companyId: string): boolean {
    if (!this.authService.isAuthenticated()) {
      const msg = 'Customer authentication is required for shopping cart operations.';
      this.resetCart();
      this.error.set(msg);
      this.lastAuthenticatedUserId = null;
      return false;
    }

    // Verify customer identity consistency
    const currentUserId = this.authService.currentUser()?.userId ?? null;
    if (this.lastAuthenticatedUserId && this.lastAuthenticatedUserId !== currentUserId) {
      this.resetCart();
    }
    this.lastAuthenticatedUserId = currentUserId;

    if (!companyId || companyId.trim() === '') {
      const msg = 'A valid Selling Company context is required for cart operations.';
      this.error.set(msg);
      return false;
    }

    return true;
  }

  private extractCartData(res: ApiResponse<CartDto>): CartDto {
    if (!res || !res.data) {
      throw new Error(res?.message || 'Invalid server response for shopping cart.');
    }
    return res.data;
  }

  private handleError(err: any): Observable<never> {
    this.loading.set(false);
    const message =
      err?.error?.message ||
      err?.message ||
      'Shopping cart service encountered an error.';
    this.error.set(message);
    return throwError(() => new Error(message));
  }
}
