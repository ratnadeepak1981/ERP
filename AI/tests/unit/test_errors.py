import unittest
from app.core.errors import (
    AiBaseException,
    InvalidRequestError,
    AuthenticationError,
    AuthorizationError,
    TenantContextError,
    ConfigurationError,
    ProviderConnectionError,
    ProviderResponseError,
    ErpApiIntegrationError,
    QuotaExceededError,
)


class TestErrors(unittest.TestCase):
    """Verifies that the exception hierarchy maps properly to HTTP status codes and codes."""

    def test_base_exception_defaults(self):
        err = AiBaseException("Internal failure")
        self.assertEqual(err.status_code, 500)
        self.assertEqual(err.error_code, "INTERNAL_SERVER_ERROR")
        self.assertEqual(err.message, "Internal failure")
        self.assertEqual(err.details, {})

    def test_invalid_request_error(self):
        err = InvalidRequestError("Missing parameter", details={"param": "name"})
        self.assertEqual(err.status_code, 400)
        self.assertEqual(err.error_code, "INVALID_REQUEST")
        self.assertEqual(err.details["param"], "name")

    def test_authentication_error(self):
        err = AuthenticationError("Invalid token")
        self.assertEqual(err.status_code, 401)
        self.assertEqual(err.error_code, "AUTHENTICATION_FAILED")

    def test_authorization_error(self):
        err = AuthorizationError("Permission denied")
        self.assertEqual(err.status_code, 403)
        self.assertEqual(err.error_code, "AUTHORIZATION_DENIED")

    def test_tenant_context_error(self):
        err = TenantContextError("Missing tenant context")
        self.assertEqual(err.status_code, 403)
        self.assertEqual(err.error_code, "TENANT_CONTEXT_INVALID")

    def test_configuration_error(self):
        err = ConfigurationError("Invalid key path")
        self.assertEqual(err.status_code, 500)
        self.assertEqual(err.error_code, "CONFIGURATION_ERROR")

    def test_provider_connection_error(self):
        err = ProviderConnectionError("Gateway timed out")
        self.assertEqual(err.status_code, 502)
        self.assertEqual(err.error_code, "PROVIDER_CONNECTION_FAILED")

    def test_provider_response_error(self):
        err = ProviderResponseError("Invalid JSON from LLM")
        self.assertEqual(err.status_code, 502)
        self.assertEqual(err.error_code, "PROVIDER_RESPONSE_INVALID")

    def test_erp_api_integration_error(self):
        err = ErpApiIntegrationError("C# ERP returned 503")
        self.assertEqual(err.status_code, 502)
        self.assertEqual(err.error_code, "ERP_INTEGRATION_FAILED")

    def test_quota_exceeded_error(self):
        err = QuotaExceededError("Rate limit exceeded")
        self.assertEqual(err.status_code, 429)
        self.assertEqual(err.error_code, "QUOTA_EXCEEDED")


if __name__ == "__main__":
    unittest.main()
