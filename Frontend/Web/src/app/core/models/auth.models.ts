export interface LoginRequest {
  email: string;
  password: string;
  tenantCode?: string;
}

export interface RefreshTokenRequest {
  refreshToken?: string;
}

export interface LoginResponse {
  status: string;
  message: string;
  userId: string;
  username: string;
  email: string;
  tenantId?: string | null;
  roles: string[];
  permissions: string[];
  algorithm?: string;
  issuer?: string;
  audience?: string;
  token: string;
  accessToken?: string;
  accessTokenExpiresAt?: string;
  refreshToken?: string;
  refreshTokenExpiresAt?: string;
  sessionId?: string;
}

export interface UserSession {
  userId: string;
  username: string;
  email: string;
  tenantId?: string | null;
  roles: string[];
  permissions: string[];
  token: string;
  accessTokenExpiresAt?: string;
  sessionId?: string;
}

export interface SessionInfo {
  id: string;
  userId: string;
  username?: string;
  tenantId?: string | null;
  ipAddress?: string | null;
  userAgent?: string | null;
  deviceInfo?: string | null;
  createdAt: string;
  lastActivityAt: string;
  absoluteExpiresAt: string;
  isRevoked: boolean;
  isCurrent: boolean;
}

export interface ApiResponse<T = any> {
  succeeded: boolean;
  message: string;
  data: T;
  errors: string[];
  errorCode?: string | null;
  traceId?: string | null;
}

export interface SecurityStatusInfo {
  status?: string;
  issuer?: string;
  audience?: string;
  activeAlgorithm?: string;
  keyVaultConfigured?: boolean;
  timestamp?: string;
  [key: string]: any;
}

export interface DiagnosticStepResult {
  step: number;
  test: string;
  status: 'PASS' | 'FAIL';
  message: string;
}
