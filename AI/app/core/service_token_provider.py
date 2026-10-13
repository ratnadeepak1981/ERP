import datetime
import hashlib
import logging
import uuid
from pathlib import Path
from typing import Any

import jwt
from cryptography.hazmat.primitives.serialization import load_pem_private_key

from app.core.config import SECURE_VAULT

logger = logging.getLogger(__name__)


class ServiceTokenProvider:
    """
    Generates short-lived, RS256-signed service-to-service JWT tokens
    for authenticating from the Python AI service to the C# .NET ERP.API.
    
    Security requirements enforced:
    - RS256 algorithm with RSA private key from SecureVault/ServerVault/private_key.pem.
    - Issuer: 'CSharpAuthServer', Audience: 'ERP'.
    - Service identity: 'erp-ai-readiness-client'.
    - Identity type: 'service'.
    - Role: 'Service Agent'.
    - Permission: 'AI.READINESS'.
    - Subject: deterministic UUID derived from MD5(service:{serviceName}) to match C# TokenService.
    - Expiry: default 5 minutes (strictly adhering to service token policy <= 30 mins).
    """

    def __init__(self, private_key_path: Path | None = None):
        self.private_key_path = private_key_path or (SECURE_VAULT / "ServerVault" / "private_key.pem")

    def generate_token(
        self,
        service_name: str = "erp-ai-readiness-client",
        role: str = "Service Agent",
        permission: str = "AI.READINESS",
        lifetime_minutes: int = 5
    ) -> str:
        if not self.private_key_path.exists():
            raise FileNotFoundError(f"RSA private key not found at {self.private_key_path}")

        priv_bytes = self.private_key_path.read_bytes()
        private_key = load_pem_private_key(priv_bytes, password=None)

        # Deterministic service GUID namespace matching C# TokenService
        h = hashlib.md5(f"service:{service_name}".encode("utf-8")).digest()
        service_guid = str(uuid.UUID(bytes=h))

        now = datetime.datetime.now(datetime.timezone.utc)
        exp = now + datetime.timedelta(minutes=lifetime_minutes)

        payload: dict[str, Any] = {
            "sub": service_guid,
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name": f"service:{service_name}",
            "name": f"service:{service_name}",
            "client_id": service_name,
            "identity_type": "service",
            "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": role,
            "role": role,
            "permission": permission,
            "iss": "CSharpAuthServer",
            "aud": "ERP",
            "nbf": int(now.timestamp()),
            "exp": int(exp.timestamp()),
            "iat": int(now.timestamp())
        }

        return jwt.encode(payload, private_key, algorithm="RS256")


service_token_provider = ServiceTokenProvider()
