import unittest
from unittest.mock import AsyncMock, patch
from fastapi.testclient import TestClient

from app.main import app
from app.control_center.models import control_center_state
from app.core.credential_store import credential_store
from app.core.vault_service import CredentialVaultService


class TestControlCenter(unittest.TestCase):
    """Unit tests for the FastAPI AI Control Center endpoints."""

    def setUp(self):
        self.client = TestClient(app)
        self.vault_service = CredentialVaultService()

    def test_get_control_center_status(self):
        response = self.client.get("/control-center/status")
        self.assertEqual(response.status_code, 200)
        data = response.json()
        self.assertIn("groq", data)
        self.assertIn("gemini", data)
        self.assertIn("is_configured", data["groq"])
        self.assertIn("default_model", data["groq"])
        self.assertIn("is_configured", data["gemini"])
        self.assertIn("default_model", data["gemini"])
        self.assertIn("has_encrypted_credential", data["groq"])

    def test_get_control_center_diagnostics(self):
        response = self.client.get("/control-center/diagnostics")
        self.assertEqual(response.status_code, 200)
        data = response.json()
        self.assertIn("groq", data)
        self.assertIn("gemini", data)
        self.assertIn("provider", data["groq"])
        self.assertIn("auth_status", data["groq"])
        self.assertIn("discovery_status", data["groq"])

    def test_test_connection_invalid_provider(self):
        payload = {
            "provider": "unsupported_provider",
            "api_key": "some_test_key"
        }
        response = self.client.post("/control-center/test-connection", json=payload)
        self.assertEqual(response.status_code, 400)
        self.assertIn("Unsupported provider", response.json()["detail"])

    @patch("app.control_center.router.discovery_service.test_groq_connection")
    def test_test_connection_groq_success(self, mock_test_groq):
        mock_test_groq.return_value = {
            "success": True,
            "message": "Groq connection and authentication successful.",
            "status_code": 200,
            "error_category": None
        }

        payload = {
            "provider": "groq",
            "api_key": "gsk_test_fake_mock_key"
        }
        response = self.client.post("/control-center/test-connection", json=payload)
        self.assertEqual(response.status_code, 200)
        data = response.json()
        self.assertTrue(data["success"])
        self.assertEqual(data["provider"], "groq")
        self.assertNotIn("gsk_test_fake_mock_key", str(data))

    @patch("app.control_center.router.discovery_service.test_groq_connection")
    def test_test_connection_groq_auth_failure(self, mock_test_groq):
        mock_test_groq.return_value = {
            "success": False,
            "message": "Invalid API Key provided (HTTP 401).",
            "models": [],
            "status_code": 401,
            "error_category": "AUTHENTICATION_FAILED"
        }

        payload = {
            "provider": "groq",
            "api_key": "invalid_key"
        }
        response = self.client.post("/control-center/test-connection", json=payload)
        self.assertEqual(response.status_code, 200)
        data = response.json()
        self.assertFalse(data["success"])
        self.assertEqual(data["error_category"], "AUTHENTICATION_FAILED")
        self.assertEqual(data["status_code"], 401)

    @patch("app.control_center.router.discovery_service.test_gemini_connection")
    def test_test_connection_gemini_success(self, mock_test_gemini):
        mock_test_gemini.return_value = {
            "success": True,
            "message": "Gemini connection and authentication successful.",
            "status_code": 200,
            "error_category": None
        }

        payload = {
            "provider": "gemini",
            "api_key": "AIzaSy_fake_test_key"
        }
        response = self.client.post("/control-center/test-connection", json=payload)
        self.assertEqual(response.status_code, 200)
        data = response.json()
        self.assertTrue(data["success"])
        self.assertEqual(data["provider"], "gemini")
        self.assertNotIn("AIzaSy_fake_test_key", str(data))

    @patch("app.control_center.router.discovery_service.test_groq_connection")
    def test_load_models_groq_success(self, mock_test_groq):
        mock_test_groq.return_value = {
            "success": True,
            "message": "Discovered models.",
            "models": ["llama-3.3-70b-versatile", "mixtral-8x7b-32768"],
            "status_code": 200,
            "error_category": None
        }

        payload = {
            "provider": "groq",
            "api_key": "gsk_test_fake_mock_key"
        }
        response = self.client.post("/control-center/load-models", json=payload)
        self.assertEqual(response.status_code, 200)
        data = response.json()
        self.assertTrue(data["success"])
        self.assertEqual(data["provider"], "groq")
        self.assertEqual(len(data["models"]), 2)
        self.assertIn("llama-3.3-70b-versatile", data["models"])
        self.assertNotIn("gsk_test_fake_mock_key", str(data))

    @patch("app.control_center.router.discovery_service.test_gemini_connection")
    def test_load_models_gemini_success(self, mock_test_gemini):
        mock_test_gemini.return_value = {
            "success": True,
            "message": "Discovered models.",
            "models": ["gemini-1.5-flash", "gemini-2.0-flash"],
            "status_code": 200,
            "error_category": None
        }

        payload = {
            "provider": "gemini",
            "api_key": "AIzaSy_fake_test_key"
        }
        response = self.client.post("/control-center/load-models", json=payload)
        self.assertEqual(response.status_code, 200)
        data = response.json()
        self.assertTrue(data["success"])
        self.assertEqual(data["provider"], "gemini")
        self.assertEqual(len(data["models"]), 2)
        self.assertIn("gemini-1.5-flash", data["models"])
        self.assertNotIn("AIzaSy_fake_test_key", str(data))

    @patch("app.core.ai_credentials_service.CSharpAICredentialsClient.save_credential_async")
    def test_save_configuration_with_api_key_encrypts_and_persists(self, mock_save_csharp):
        mock_save_csharp.return_value = {
            "success": True,
            "data": {
                "status": "SUCCESS",
                "message": "Credential saved in AIDatabase."
            }
        }
        raw_key = "gsk_super_secret_test_key_value"
        payload = {
            "provider": "groq",
            "default_model": "llama-3.3-70b-versatile",
            "key_reference_code": "GROQ_API_KEY",
            "api_key": raw_key,
            "discovered_models": ["llama-3.3-70b-versatile", "llama-3.1-8b-instant"]
        }
        response = self.client.post("/control-center/save-configuration", json=payload)
        self.assertEqual(response.status_code, 200)
        data = response.json()
        self.assertEqual(data["status"], "SUCCESS")
        self.assertEqual(data["provider"], "groq")
        self.assertEqual(data["default_model"], "llama-3.3-70b-versatile")
        self.assertEqual(data["key_reference_code"], "GROQ_API_KEY")
        self.assertTrue(data["has_encrypted_credential"])
        self.assertTrue(data["csharp_persisted"])
        # Ensure raw key is never returned in response
        self.assertNotIn(raw_key, str(data))

        # Verify credential in store is encrypted (not plaintext)
        stored = credential_store.get_credential("groq")
        self.assertIsNotNone(stored)
        self.assertNotEqual(stored["encrypted_secret"], raw_key)
        self.assertNotIn(raw_key, stored["encrypted_secret"])

        # Verify decryption yields the original key
        decrypted = self.vault_service.decrypt_credential(stored["encrypted_secret"])
        self.assertEqual(decrypted, raw_key)

    @patch("app.core.ai_credentials_service.CSharpAICredentialsClient.save_credential_async")
    def test_save_configuration_csharp_offline_reports_partial_success(self, mock_save_csharp):
        mock_save_csharp.return_value = {
            "success": False,
            "error": "Cannot connect to C# ERP API: ConnectError"
        }
        raw_key = "gsk_offline_test_key_value"
        payload = {
            "provider": "groq",
            "default_model": "llama-3.3-70b-versatile",
            "key_reference_code": "GROQ_API_KEY",
            "api_key": raw_key,
            "discovered_models": ["llama-3.3-70b-versatile"]
        }
        response = self.client.post("/control-center/save-configuration", json=payload)
        self.assertEqual(response.status_code, 200)
        data = response.json()
        self.assertEqual(data["status"], "PARTIAL_SUCCESS")
        self.assertFalse(data["csharp_persisted"])
        self.assertIn("saved locally", data["message"])
        self.assertIn("ConnectError", data["csharp_error"])

    def test_save_configuration_missing_model_fails(self):
        payload = {
            "provider": "groq",
            "default_model": "   ",
            "key_reference_code": "GROQ_API_KEY"
        }
        response = self.client.post("/control-center/save-configuration", json=payload)
        self.assertEqual(response.status_code, 400)
        self.assertIn("A default model must be selected", response.json()["detail"])

    def test_save_configuration_missing_key_ref_fails(self):
        payload = {
            "provider": "groq",
            "default_model": "llama-3.3-70b-versatile",
            "key_reference_code": "   "
        }
        response = self.client.post("/control-center/save-configuration", json=payload)
        self.assertEqual(response.status_code, 400)
        self.assertIn("key reference identifier is required", response.json()["detail"])

    @patch("app.core.ai_credentials_service.CSharpAICredentialsClient.get_credential_async")
    @patch("app.control_center.router.discovery_service.test_groq_connection")
    def test_reusable_encrypted_credential_without_reentering_key(self, mock_test_groq, mock_get_csharp):
        mock_get_csharp.return_value = {"success": False, "status_code": 404}
        # 1. Save an encrypted credential first
        raw_key = "gsk_reusable_test_key"
        save_payload = {
            "provider": "groq",
            "default_model": "llama-3.3-70b-versatile",
            "key_reference_code": "GROQ_API_KEY",
            "api_key": raw_key,
            "discovered_models": ["llama-3.3-70b-versatile"]
        }
        self.client.post("/control-center/save-configuration", json=save_payload)

        # 2. Test connection with api_key=None should automatically decrypt saved credential
        mock_test_groq.return_value = {
            "success": True,
            "message": "Connected with saved credential.",
            "status_code": 200,
            "error_category": None
        }

        test_payload = {
            "provider": "groq",
            "api_key": None
        }
        response = self.client.post("/control-center/test-connection", json=test_payload)
        self.assertEqual(response.status_code, 200)
        self.assertTrue(response.json()["success"])
        # Verify mock received decrypted key
        mock_test_groq.assert_called_with(raw_key)


if __name__ == "__main__":
    unittest.main()
