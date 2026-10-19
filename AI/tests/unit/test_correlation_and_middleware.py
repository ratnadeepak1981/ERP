import unittest
from fastapi.testclient import TestClient

from app.main import app
from app.core.logging import CORRELATION_HEADER


class TestCorrelationAndMiddleware(unittest.TestCase):
    """Verifies correlation ID generation, propagation, response headers, and safe error responses."""

    def setUp(self):
        self.client = TestClient(app, raise_server_exceptions=False)

    def test_health_generates_correlation_id_if_missing(self):
        resp = self.client.get("/health")
        self.assertEqual(resp.status_code, 200)
        self.assertIn(CORRELATION_HEADER, resp.headers)
        self.assertTrue(len(resp.headers[CORRELATION_HEADER]) > 0)

    def test_health_preserves_supplied_correlation_id(self):
        custom_cid = "test-corr-id-12345"
        resp = self.client.get("/health", headers={CORRELATION_HEADER: custom_cid})
        self.assertEqual(resp.status_code, 200)
        self.assertEqual(resp.headers[CORRELATION_HEADER], custom_cid)

    def test_unhandled_route_or_error_returns_clean_correlation_payload(self):
        # Requesting protected endpoint without token triggers centralized error handling
        resp = self.client.get("/security/protected-test")
        self.assertEqual(resp.status_code, 401)
        data = resp.json()
        self.assertEqual(data["status"], "FAIL")
        self.assertEqual(data["error_code"], "AUTHENTICATION_FAILED")
        self.assertIn("correlation_id", data)
        self.assertIn("timestamp", data)
        # Verify no traceback / stack trace or server internals leaked
        self.assertNotIn("traceback", str(data).lower())
        self.assertNotIn("c:\\", str(data).lower())
        self.assertNotIn("z:\\", str(data).lower())

    def test_tenant_context_clean_after_request(self):
        from app.core.logging import tenant_id_ctx
        # Verify tenant_id_ctx is clean before request
        self.assertEqual(tenant_id_ctx.get(), "")
        resp = self.client.get("/security/protected-test")
        self.assertEqual(resp.status_code, 401)
        # Verify tenant_id_ctx remains clean after failed request
        self.assertEqual(tenant_id_ctx.get(), "")


if __name__ == "__main__":
    unittest.main()
