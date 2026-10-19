import unittest
from fastapi.testclient import TestClient

from app.main import app


class TestAuthErrorHandling(unittest.TestCase):
    """Verifies that authentication errors return proper HTTP 401 status without exposing raw tokens."""

    def setUp(self):
        self.client = TestClient(app, raise_server_exceptions=False)

    def test_missing_auth_header_returns_401(self):
        resp = self.client.get("/security/protected-test")
        self.assertEqual(resp.status_code, 401)
        data = resp.json()
        self.assertEqual(data["status"], "FAIL")
        self.assertEqual(data["error_code"], "AUTHENTICATION_FAILED")
        self.assertIn("correlation_id", data)

    def test_malformed_token_returns_401(self):
        resp = self.client.get(
            "/security/protected-test",
            headers={"Authorization": "Bearer not-a-real-jwt-token"},
        )
        self.assertEqual(resp.status_code, 401)
        data = resp.json()
        self.assertEqual(data["status"], "FAIL")
        self.assertEqual(data["error_code"], "AUTHENTICATION_FAILED")
        # Ensure the malformed token is not reflected back in the response message
        self.assertNotIn("not-a-real-jwt-token", data["message"])

    def test_handshake_endpoint_returns_401_on_invalid_token(self):
        # Validates that POST /security/handshake returns 401 on invalid or unverified tokens
        resp = self.client.post(
            "/security/handshake",
            headers={"Authorization": "Bearer not-a-real-token"},
        )
        self.assertEqual(resp.status_code, 401)
        data = resp.json()
        self.assertEqual(data["status"], "FAIL")
        self.assertEqual(data["error_code"], "AUTHENTICATION_FAILED")


if __name__ == "__main__":
    unittest.main()
