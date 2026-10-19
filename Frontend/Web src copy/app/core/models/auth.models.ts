export interface LoginRequest {
  email: string;
  password: string;
  tenantCode?: string;
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
  algorithm: string;
  issuer: string;
  audience: string;
  token: string;
}

export interface UserSession {
  userId: string;
  username: string;
  email: string;
  tenantId?: string | null;
  roles: string[];
  permissions: string[];
  token: string;
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
