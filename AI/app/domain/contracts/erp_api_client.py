from typing import Any, Protocol, runtime_checkable


@runtime_checkable
class IErpApiClient(Protocol):
    """
    Contract for outbound communication from Python AI to the authoritative C# .NET ERP API.
    Enforces security boundary: Python never accesses ERP DBs directly; all operations
    are routed via this contract with the authenticated caller's credentials.
    """

    async def get_async(
        self,
        endpoint: str,
        bearer_token: str,
        params: dict[str, Any] | None = None,
    ) -> dict[str, Any]:
        """Performs an authenticated GET query against the C# ERP API."""
        ...

    async def post_async(
        self,
        endpoint: str,
        bearer_token: str,
        payload: dict[str, Any],
    ) -> dict[str, Any]:
        """Performs an authenticated POST action against the C# ERP API."""
        ...
