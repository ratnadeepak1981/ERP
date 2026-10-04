from dataclasses import dataclass
from typing import Any


@dataclass(frozen=True)
class SecurityContext:

    subject: str | None
    tenant_id: str | None
    roles: list[str]
    permissions: list[str]

    @classmethod
    def from_claims(cls, claims: dict[str, Any]):
        roles = claims.get(
            "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
        )

        permissions = claims.get("permission")

        if isinstance(roles, str):
            roles = [roles]

        if roles is None:
            roles = []

        if isinstance(permissions, str):
            permissions = [permissions]

        if permissions is None:
            permissions = []

        return cls(
            subject=claims.get("sub"),
            tenant_id=claims.get("tenant_id"),
            roles=roles,
            permissions=permissions
        )