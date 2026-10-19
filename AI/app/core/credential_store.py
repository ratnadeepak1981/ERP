import os
import json
import logging
from pathlib import Path
from pydantic import BaseModel, Field

from app.core.config import SECURE_VAULT

logger = logging.getLogger(__name__)


class EncryptedCredentialRecord(BaseModel):
    provider: str
    key_reference_code: str
    encrypted_secret: str
    default_model: str
    environment: str = "Production"
    updated_at: str


class CredentialPersistenceStore:
    """
    Persistence store for encrypted provider credentials and metadata.
    Coordinates between encrypted store and AIDatabase authorized persistence.
    Stores only encrypted secrets; raw secrets are strictly forbidden.
    """

    def __init__(self, store_file: Path | None = None):
        self.store_file = store_file or (SECURE_VAULT / "credentials_store.enc.json")

    def _read_all(self) -> dict[str, dict]:
        if not self.store_file.exists():
            return {}
        try:
            content = self.store_file.read_text(encoding="utf-8")
            return json.loads(content)
        except Exception as ex:
            logger.error("Failed to read credential store: %s", type(ex).__name__)
            return {}

    def _write_all(self, data: dict[str, dict]) -> None:
        self.store_file.parent.mkdir(parents=True, exist_ok=True)
        self.store_file.write_text(json.dumps(data, indent=2), encoding="utf-8")

    def save_credential(
        self,
        provider: str,
        key_reference_code: str,
        encrypted_secret: str,
        default_model: str,
        environment: str = "Production"
    ) -> None:
        """Persists encrypted credential record."""
        from datetime import datetime, timezone

        provider_norm = provider.lower().strip()
        data = self._read_all()
        data[provider_norm] = {
            "provider": provider_norm,
            "key_reference_code": key_reference_code.strip(),
            "encrypted_secret": encrypted_secret.strip(),
            "default_model": default_model.strip(),
            "environment": environment.strip(),
            "updated_at": datetime.now(timezone.utc).isoformat()
        }
        self._write_all(data)

    def get_credential(self, provider: str) -> dict | None:
        """Retrieves stored record for provider (returns encrypted secret, never plaintext)."""
        provider_norm = provider.lower().strip()
        data = self._read_all()
        return data.get(provider_norm)

    def has_credential(self, provider: str) -> bool:
        provider_norm = provider.lower().strip()
        data = self._read_all()
        return provider_norm in data and bool(data[provider_norm].get("encrypted_secret"))


credential_store = CredentialPersistenceStore()
