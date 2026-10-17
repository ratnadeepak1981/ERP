from typing import Any


class AiBaseException(Exception):
    """Base exception for all AI modular monolith errors."""

    def __init__(
        self,
        message: str,
        error_code: str = "INTERNAL_SERVER_ERROR",
        status_code: int = 500,
        details: dict[str, Any] | None = None,
    ) -> None:
        super().__init__(message)
        self.message = message
        self.error_code = error_code
        self.status_code = status_code
        self.details = details or {}


class InvalidRequestError(AiBaseException):
    """Raised when client input or request parameters are malformed or invalid."""

    def __init__(
        self,
        message: str,
        error_code: str = "INVALID_REQUEST",
        details: dict[str, Any] | None = None,
    ) -> None:
        super().__init__(
            message=message,
            error_code=error_code,
            status_code=400,
            details=details,
        )


class AuthenticationError(AiBaseException):
    """Raised when authentication credentials or JWT tokens are missing or invalid."""

    def __init__(
        self,
        message: str,
        error_code: str = "AUTHENTICATION_FAILED",
        details: dict[str, Any] | None = None,
    ) -> None:
        super().__init__(
            message=message,
            error_code=error_code,
            status_code=401,
            details=details,
        )


class AuthorizationError(AiBaseException):
    """Raised when the caller lacks required permissions or roles."""

    def __init__(
        self,
        message: str,
        error_code: str = "AUTHORIZATION_DENIED",
        details: dict[str, Any] | None = None,
    ) -> None:
        super().__init__(
            message=message,
            error_code=error_code,
            status_code=403,
            details=details,
        )


class TenantContextError(AiBaseException):
    """Raised when tenant identity is missing, mismatched, or violates isolation."""

    def __init__(
        self,
        message: str,
        error_code: str = "TENANT_CONTEXT_INVALID",
        details: dict[str, Any] | None = None,
    ) -> None:
        super().__init__(
            message=message,
            error_code=error_code,
            status_code=403,
            details=details,
        )


class ConfigurationError(AiBaseException):
    """Raised when system or provider configuration is missing or invalid."""

    def __init__(
        self,
        message: str,
        error_code: str = "CONFIGURATION_ERROR",
        details: dict[str, Any] | None = None,
    ) -> None:
        super().__init__(
            message=message,
            error_code=error_code,
            status_code=500,
            details=details,
        )


class ProviderConnectionError(AiBaseException):
    """Raised when an external AI provider times out or fails to connect."""

    def __init__(
        self,
        message: str,
        error_code: str = "PROVIDER_CONNECTION_FAILED",
        details: dict[str, Any] | None = None,
    ) -> None:
        super().__init__(
            message=message,
            error_code=error_code,
            status_code=502,
            details=details,
        )


class ProviderResponseError(AiBaseException):
    """Raised when an external AI provider returns an invalid or schema-violating payload."""

    def __init__(
        self,
        message: str,
        error_code: str = "PROVIDER_RESPONSE_INVALID",
        details: dict[str, Any] | None = None,
    ) -> None:
        super().__init__(
            message=message,
            error_code=error_code,
            status_code=502,
            details=details,
        )


class ErpApiIntegrationError(AiBaseException):
    """Raised when communication with the authoritative C# ERP API fails."""

    def __init__(
        self,
        message: str,
        error_code: str = "ERP_INTEGRATION_FAILED",
        status_code: int = 502,
        details: dict[str, Any] | None = None,
    ) -> None:
        super().__init__(
            message=message,
            error_code=error_code,
            status_code=status_code,
            details=details,
        )


class QuotaExceededError(AiBaseException):
    """Raised when token quota, rate limits, or subscription entitlement limits are reached."""

    def __init__(
        self,
        message: str,
        error_code: str = "QUOTA_EXCEEDED",
        details: dict[str, Any] | None = None,
    ) -> None:
        super().__init__(
            message=message,
            error_code=error_code,
            status_code=429,
            details=details,
        )
