import unittest
from unittest.mock import MagicMock, patch
from fastapi.testclient import TestClient

from app.main import app
from app.integration.handshake_service import HandshakeService


class TestHandshakeAuthorization(unittest.TestCase):
    """
    Focused unit tests for /security/handshake authorization rules:
    - Service tokens with correct identity, role, and permission are accepted (HTTP 200).
    - Human-user tokens are rejected (HTTP 403).
    - Service tokens missing AI.READINESS are rejected (HTTP 403).
    - Service tokens missing Service Agent role are rejected (HTTP 403).
    - Invalid or expired tokens are rejected (HTTP 401).
    """

    def setUp(self):
        self.client = TestClient(app, raise_server_exceptions=False)

    @patch("app.integration.handshake_service.HandshakeService.validate_token")
    def test_handshake_accepts_valid_service_agent_token(self, mock_validate):
        mock_validate.return_value = {
            "status": "PASS",
            "stage": "SecurityContext creation",
            "message": "C# RS256 JWT successfully validated by FastAPI",
            "security_context": {
                "subject": "00000000-0000-0000-0000-000000000001",
                "tenant_id": None,
                "roles": ["Service Agent"],
                "permissions": ["AI.READINESS"],
                "identity_type": "service",
                "client_id": "erp-ai-readiness-client",
            },
        }

        resp = self.client.post(
            "/security/handshake",
            headers={"Authorization": "Bearer valid-service-token"},
        )
        self.assertEqual(resp.status_code, 200)
        data = resp.json()
        self.assertEqual(data["status"], "PASS")
        self.assertEqual(data["security_context"]["identity_type"], "service")
        self.assertEqual(data["security_context"]["client_id"], "erp-ai-readiness-client")

    @patch("app.integration.handshake_service.HandshakeService.validate_token")
    def test_handshake_rejects_human_user_token(self, mock_validate):
        mock_validate.return_value = {
            "status": "FAIL",
            "stage": "Authorization",
            "message": "Only service identities are permitted to perform handshake",
        }

        resp = self.client.post(
            "/security/handshake",
            headers={"Authorization": "Bearer human-user-token"},
        )
        self.assertEqual(resp.status_code, 403)
        data = resp.json()
        self.assertEqual(data["status"], "FAIL")
        self.assertEqual(data["error_code"], "AUTHORIZATION_DENIED")
        self.assertIn("Only service identities are permitted", data["message"])

    @patch("app.integration.handshake_service.HandshakeService.validate_token")
    def test_handshake_rejects_service_token_missing_readiness_permission(self, mock_validate):
        mock_validate.return_value = {
            "status": "FAIL",
            "stage": "Authorization",
            "message": "Missing required service permission: AI.READINESS",
        }

        resp = self.client.post(
            "/security/handshake",
            headers={"Authorization": "Bearer service-token-missing-permission"},
        )
        self.assertEqual(resp.status_code, 403)
        data = resp.json()
        self.assertEqual(data["status"], "FAIL")
        self.assertEqual(data["error_code"], "AUTHORIZATION_DENIED")
        self.assertIn("AI.READINESS", data["message"])

    @patch("app.integration.handshake_service.HandshakeService.validate_token")
    def test_handshake_rejects_service_token_missing_service_agent_role(self, mock_validate):
        mock_validate.return_value = {
            "status": "FAIL",
            "stage": "Authorization",
            "message": "Missing required service role: Service Agent",
        }

        resp = self.client.post(
            "/security/handshake",
            headers={"Authorization": "Bearer service-token-missing-role"},
        )
        self.assertEqual(resp.status_code, 403)
        data = resp.json()
        self.assertEqual(data["status"], "FAIL")
        self.assertEqual(data["error_code"], "AUTHORIZATION_DENIED")
        self.assertIn("Service Agent", data["message"])

    @patch("app.integration.handshake_service.HandshakeService.validate_token")
    def test_handshake_rejects_expired_or_invalid_signature_with_401(self, mock_validate):
        mock_validate.return_value = {
            "status": "FAIL",
            "stage": "JWT validation",
            "message": "Signature verification failed",
        }

        resp = self.client.post(
            "/security/handshake",
            headers={"Authorization": "Bearer expired-token"},
        )
        self.assertEqual(resp.status_code, 401)
        data = resp.json()
        self.assertEqual(data["status"], "FAIL")
        self.assertEqual(data["error_code"], "AUTHENTICATION_FAILED")
        self.assertEqual(data["message"], "Token authentication failed.")

    def test_handshake_service_validate_token_authorization_logic(self):
        # Directly test HandshakeService.validate_token logic
        mock_validator = MagicMock()
        mock_validator.is_key_found.return_value = True
        mock_key = MagicMock()
        mock_key.key_size = 4096
        mock_validator.load_key.return_value = mock_key

        service = HandshakeService(public_key_validator=mock_validator)

        with patch("app.integration.handshake_service.JwtValidator") as mock_jwt_cls:
            mock_jwt_instance = MagicMock()
            mock_jwt_cls.return_value = mock_jwt_instance

            # 1. Non-service identity rejected
            mock_jwt_instance.validate.return_value = {
                "status": "PASS",
                "claims": {
                    "sub": "user-guid",
                    "identity_type": "user",
                    "client_id": "erp-user",
                    "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": ["Platform Admin"],
                    "permission": ["AI.READINESS"],
                },
            }
            res = service.validate_token("dummy-token")
            self.assertEqual(res["status"], "FAIL")
            self.assertEqual(res["stage"], "Authorization")
            self.assertIn("Only service identities", res["message"])

            # 2. Missing client_id rejected
            mock_jwt_instance.validate.return_value = {
                "status": "PASS",
                "claims": {
                    "sub": "service-guid",
                    "identity_type": "service",
                    "client_id": None,
                    "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": ["Service Agent"],
                    "permission": ["AI.READINESS"],
                },
            }
            res = service.validate_token("dummy-token")
            self.assertEqual(res["status"], "FAIL")
            self.assertEqual(res["stage"], "Authorization")
            self.assertIn("client identifier is missing", res["message"])

            # 3. Missing Service Agent role rejected
            mock_jwt_instance.validate.return_value = {
                "status": "PASS",
                "claims": {
                    "sub": "service-guid",
                    "identity_type": "service",
                    "client_id": "erp-ai-readiness-client",
                    "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": ["Other Role"],
                    "permission": ["AI.READINESS"],
                },
            }
            res = service.validate_token("dummy-token")
            self.assertEqual(res["status"], "FAIL")
            self.assertEqual(res["stage"], "Authorization")
            self.assertIn("Service Agent", res["message"])

            # 4. Missing AI.READINESS permission rejected
            mock_jwt_instance.validate.return_value = {
                "status": "PASS",
                "claims": {
                    "sub": "service-guid",
                    "identity_type": "service",
                    "client_id": "erp-ai-readiness-client",
                    "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": ["Service Agent"],
                    "permission": ["OTHER.PERMISSION"],
                },
            }
            res = service.validate_token("dummy-token")
            self.assertEqual(res["status"], "FAIL")
            self.assertEqual(res["stage"], "Authorization")
            self.assertIn("AI.READINESS", res["message"])

            # 5. Full valid service token passes
            mock_jwt_instance.validate.return_value = {
                "status": "PASS",
                "claims": {
                    "sub": "service-guid",
                    "identity_type": "service",
                    "client_id": "erp-ai-readiness-client",
                    "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": ["Service Agent"],
                    "permission": ["AI.READINESS"],
                },
            }
            res = service.validate_token("dummy-token")
            self.assertEqual(res["status"], "PASS")
            self.assertEqual(res["security_context"]["identity_type"], "service")
            self.assertEqual(res["security_context"]["client_id"], "erp-ai-readiness-client")


if __name__ == "__main__":
    unittest.main()
