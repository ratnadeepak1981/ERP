import logging
import httpx
from typing import Any

logger = logging.getLogger(__name__)


class ProviderDiscoveryService:
    """
    Connects to external LLM provider endpoints (Groq, Gemini) to test authentication
    and dynamically discover available models.
    Operates without storing or logging raw API keys.
    """

    GROQ_MODELS_URL = "https://api.groq.com/openai/v1/models"
    GEMINI_MODELS_URL = "https://generativelanguage.googleapis.com/v1beta/models"

    async def test_groq_connection(self, api_key: str, timeout: float = 10.0) -> dict[str, Any]:
        """Tests connectivity and authentication against the Groq API."""
        if not api_key or not api_key.strip():
            return {
                "success": False,
                "provider": "groq",
                "message": "Groq API key was not provided.",
                "error_category": "MISSING_API_KEY",
                "models": []
            }

        headers = {
            "Authorization": f"Bearer {api_key.strip()}",
            "Content-Type": "application/json"
        }

        try:
            async with httpx.AsyncClient(timeout=timeout) as client:
                resp = await client.get(self.GROQ_MODELS_URL, headers=headers)
                if resp.status_code == 200:
                    data = resp.json()
                    models = [m.get("id") for m in data.get("data", []) if m.get("id")]
                    return {
                        "success": True,
                        "provider": "groq",
                        "message": f"Successfully connected to Groq. Discovered {len(models)} models.",
                        "models": sorted(models),
                        "status_code": 200
                    }
                elif resp.status_code in (401, 403):
                    return {
                        "success": False,
                        "provider": "groq",
                        "message": "Groq authentication failed. Please verify your API key.",
                        "error_category": "AUTHENTICATION_FAILED",
                        "status_code": resp.status_code,
                        "models": []
                    }
                else:
                    return {
                        "success": False,
                        "provider": "groq",
                        "message": f"Groq API returned HTTP {resp.status_code}.",
                        "error_category": "PROVIDER_HTTP_ERROR",
                        "status_code": resp.status_code,
                        "models": []
                    }
        except httpx.TimeoutException:
            return {
                "success": False,
                "provider": "groq",
                "message": "Connection to Groq timed out.",
                "error_category": "TIMEOUT",
                "models": []
            }
        except Exception as ex:
            return {
                "success": False,
                "provider": "groq",
                "message": f"Connection to Groq failed: {type(ex).__name__}",
                "error_category": "NETWORK_OR_CLIENT_ERROR",
                "models": []
            }

    async def test_gemini_connection(self, api_key: str, timeout: float = 10.0) -> dict[str, Any]:
        """Tests connectivity and authentication against Google Gemini Generative Language API."""
        if not api_key or not api_key.strip():
            return {
                "success": False,
                "provider": "gemini",
                "message": "Google Gemini API key was not provided.",
                "error_category": "MISSING_API_KEY",
                "models": []
            }

        # Gemini uses query parameter 'key'
        params = {"key": api_key.strip()}

        try:
            async with httpx.AsyncClient(timeout=timeout) as client:
                resp = await client.get(self.GEMINI_MODELS_URL, params=params)
                if resp.status_code == 200:
                    data = resp.json()
                    raw_models = data.get("models", [])
                    # Filter models supporting generateContent and normalize name (remove 'models/' prefix)
                    models = []
                    for m in raw_models:
                        methods = m.get("supportedGenerationMethods", [])
                        if "generateContent" in methods:
                            name = m.get("name", "")
                            if name.startswith("models/"):
                                name = name[len("models/"):]
                            if name:
                                models.append(name)

                    return {
                        "success": True,
                        "provider": "gemini",
                        "message": f"Successfully connected to Google Gemini. Discovered {len(models)} content generation models.",
                        "models": sorted(models),
                        "status_code": 200
                    }
                elif resp.status_code in (400, 401, 403):
                    return {
                        "success": False,
                        "provider": "gemini",
                        "message": "Google Gemini authentication failed. Please verify your API key.",
                        "error_category": "AUTHENTICATION_FAILED",
                        "status_code": resp.status_code,
                        "models": []
                    }
                else:
                    return {
                        "success": False,
                        "provider": "gemini",
                        "message": f"Gemini API returned HTTP {resp.status_code}.",
                        "error_category": "PROVIDER_HTTP_ERROR",
                        "status_code": resp.status_code,
                        "models": []
                    }
        except httpx.TimeoutException:
            return {
                "success": False,
                "provider": "gemini",
                "message": "Connection to Google Gemini timed out.",
                "error_category": "TIMEOUT",
                "models": []
            }
        except Exception as ex:
            return {
                "success": False,
                "provider": "gemini",
                "message": f"Connection to Google Gemini failed: {type(ex).__name__}",
                "error_category": "NETWORK_OR_CLIENT_ERROR",
                "models": []
            }
