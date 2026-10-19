import logging
from typing import Any

from app.core.credential_store import credential_store
from app.core.service_token_provider import ServiceTokenProvider, service_token_provider
from app.core.vault_service import CredentialVaultService
from app.infrastructure.csharp_api.ai_credentials_client import CSharpAICredentialsClient

logger = logging.getLogger(__name__)


class AICredentialsService:
    """
    Coordinates AI credential resolution and persistence between the FastAPI AI service
    and the authoritative C# ERP.API (which persists to AIDatabase).
    
    Security invariants:
    - Raw API keys are never returned to callers or written to logs.
    - Decryption is strictly performed in-memory on demand using RSA-4096 private key from SecureVault.
    - When C# ERP.API is reachable and authenticated, it acts as authoritative store in AIDatabase.
    - Local memory/transient cache coordinates seamlessly across application restarts.
    """

    def __init__(
        self,
        vault_service: CredentialVaultService | None = None,
        csharp_client: CSharpAICredentialsClient | None = None,
        token_provider: ServiceTokenProvider | None = None
    ):
        self.vault_service = vault_service or CredentialVaultService()
        self.csharp_client = csharp_client or CSharpAICredentialsClient()
        self.token_provider = token_provider or service_token_provider

    async def save_credential(
        self,
        provider: str,
        key_reference_code: str,
        raw_api_key: str,
        default_model: str,
        service_token: str | None = None,
        environment: str = "Production"
    ) -> dict[str, Any]:
        """
        Encrypts raw key using RSA-4096 OAEP and persists ciphertext in AIDatabase via C# API.
        Also caches encrypted record in credential_store for instant local recovery.
        """
        if not raw_api_key or not raw_api_key.strip():
            raise ValueError("Raw API key cannot be empty.")

        provider_norm = provider.lower().strip()
        key_ref = key_reference_code.strip()

        # 1. Server-side RSA encryption
        encrypted_secret = self.vault_service.encrypt_credential(raw_api_key.strip())

        # 2. Persist in local store
        credential_store.save_credential(
            provider=provider_norm,
            key_reference_code=key_ref,
            encrypted_secret=encrypted_secret,
            default_model=default_model.strip(),
            environment=environment
        )

        # 3. Persist via C# ERP.API into AIDatabase using service token
        token_to_use = service_token
        if not token_to_use and self.token_provider:
            try:
                token_to_use = self.token_provider.generate_token()
            except Exception as ex:
                logger.warning("Could not generate service token for C# API persistence: %s", type(ex).__name__)

        csharp_persisted = False
        csharp_error = None
        if token_to_use:
            resp = await self.csharp_client.save_credential_async(
                service_token=token_to_use,
                provider_code=provider_norm,
                key_reference_code=key_ref,
                encrypted_secret=encrypted_secret,
                encryption_algorithm="RSA-4096-OAEP-SHA256",
                key_version=1,
                environment=environment
            )
            csharp_persisted = resp.get("success", False)
            csharp_error = resp.get("error")

        return {
            "success": True,
            "provider": provider_norm,
            "key_reference_code": key_ref,
            "csharp_persisted": csharp_persisted,
            "csharp_error": csharp_error
        }

    async def resolve_plaintext_key(
        self,
        provider: str,
        user_key: str | None = None,
        service_token: str | None = None,
        environment: str = "Production"
    ) -> str:
        """
        Resolves the plaintext API key in memory:
        1. If user supplied key directly, return it.
        2. Query C# ERP.API (AIDatabase) if service_token is provided or generated.
        3. Fall back to local encrypted store.
        4. Decrypt in memory using RSA private key.
        """
        if user_key and user_key.strip():
            return user_key.strip()

        provider_norm = provider.lower().strip()
        encrypted_secret = None

        # Try C# ERP.API first
        token_to_use = service_token
        if not token_to_use and self.token_provider:
            try:
                token_to_use = self.token_provider.generate_token()
            except Exception as ex:
                logger.debug("Could not generate service token for C# retrieval: %s", type(ex).__name__)

        if token_to_use:
            resp = await self.csharp_client.get_credential_async(
                service_token=token_to_use,
                provider_code=provider_norm,
                environment=environment
            )
            if resp.get("success") and resp.get("data"):
                encrypted_secret = resp["data"].get("encryptedSecret")

        # Fallback to local encrypted store
        if not encrypted_secret:
            stored = credential_store.get_credential(provider_norm)
            if stored and stored.get("encrypted_secret"):
                encrypted_secret = stored["encrypted_secret"]

        if not encrypted_secret:
            return ""

        try:
            return self.vault_service.decrypt_credential(encrypted_secret)
        except Exception as ex:
            logger.error("Failed to decrypt stored credential for provider %s: %s", provider_norm, type(ex).__name__)
            return ""


ai_credentials_service = AICredentialsService()
