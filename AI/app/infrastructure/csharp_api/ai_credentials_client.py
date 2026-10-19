import logging
import httpx
from typing import Any

from app.core.config import ERP_API_BASE_URL

logger = logging.getLogger(__name__)


class CSharpAICredentialsClient:
    """
    Communicates with the authoritative C# .NET API (ERP.API) for encrypted AI credential persistence.
    Python never connects to SQL Server or AIDatabase directly; all operations route through this client.
    Requests are authorized using a service-to-service JWT token (Service Agent role, AI.READINESS permission).
    """

    def __init__(self, base_url: str | None = None):
        self.base_url = (base_url or ERP_API_BASE_URL).rstrip("/")

    async def save_credential_async(
        self,
        service_token: str,
        provider_code: str,
        key_reference_code: str,
        encrypted_secret: str,
        encryption_algorithm: str = "RSA-4096-OAEP-SHA256",
        key_version: int = 1,
        environment: str = "Production",
        timeout: float = 10.0
    ) -> dict[str, Any]:
        """Saves an encrypted provider credential via C# ERP.API."""
        url = f"{self.base_url}/api/ai/credentials"
        headers = {
            "Authorization": f"Bearer {service_token.strip()}",
            "Content-Type": "application/json"
        }
        payload = {
            "providerCode": provider_code.strip().upper(),
            "keyReferenceCode": key_reference_code.strip(),
            "encryptedSecret": encrypted_secret.strip(),
            "encryptionAlgorithm": encryption_algorithm,
            "keyVersion": key_version,
            "environment": environment
        }

        try:
            async with httpx.AsyncClient(timeout=timeout) as client:
                resp = await client.post(url, headers=headers, json=payload)
                if resp.status_code == 200:
                    return {"success": True, "data": resp.json()}
                elif resp.status_code in (401, 403):
                    return {"success": False, "status_code": resp.status_code, "error": "Unauthorized service request."}
                else:
                    return {"success": False, "status_code": resp.status_code, "error": f"API error: {resp.status_code}"}
        except Exception as ex:
            logger.error("Failed to connect to C# ERP API at %s: %s", url, type(ex).__name__)
            return {"success": False, "error": f"Cannot connect to C# ERP API: {type(ex).__name__}"}

    async def get_credential_async(
        self,
        service_token: str,
        provider_code: str,
        environment: str = "Production",
        timeout: float = 10.0
    ) -> dict[str, Any]:
        """Retrieves an active encrypted credential via C# ERP.API."""
        url = f"{self.base_url}/api/ai/credentials/{provider_code.strip().upper()}?environment={environment}"
        headers = {
            "Authorization": f"Bearer {service_token.strip()}"
        }

        try:
            async with httpx.AsyncClient(timeout=timeout) as client:
                resp = await client.get(url, headers=headers)
                if resp.status_code == 200:
                    return {"success": True, "data": resp.json()}
                elif resp.status_code == 404:
                    return {"success": False, "status_code": 404, "error": "No saved credential found."}
                elif resp.status_code in (401, 403):
                    return {"success": False, "status_code": resp.status_code, "error": "Unauthorized service request."}
                else:
                    return {"success": False, "status_code": resp.status_code, "error": f"API error: {resp.status_code}"}
        except Exception as ex:
            logger.error("Failed to connect to C# ERP API at %s: %s", url, type(ex).__name__)
            return {"success": False, "error": f"Cannot connect to C# ERP API: {type(ex).__name__}"}
