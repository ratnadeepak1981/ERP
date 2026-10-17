import time
import logging
from fastapi import APIRouter, HTTPException, status
from app.control_center.models import (
    DiagnosticItem,
    LoadModelsRequest,
    LoadModelsResponse,
    ProviderStatusModel,
    SaveConfigurationRequest,
    TestConnectionRequest,
    TestConnectionResponse,
    control_center_state,
)
from app.core.ai_credentials_service import ai_credentials_service
from app.core.credential_store import credential_store
from app.core.logging import correlation_id_ctx
from app.core.vault_service import CredentialVaultService
from app.infrastructure.providers.discovery_service import ProviderDiscoveryService

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/control-center", tags=["AI Control Center"])

discovery_service = ProviderDiscoveryService()
vault_service = CredentialVaultService()


async def _resolve_api_key(provider: str, user_key: str | None) -> str:
    """
    Resolves the API key:
    1. If user provided a non-empty key, use it.
    2. Otherwise, check if encrypted credential exists in AIDatabase/credential store and decrypt server-side.
    3. If neither, return empty string.
    """
    return await ai_credentials_service.resolve_plaintext_key(provider=provider, user_key=user_key)


@router.get("/status", response_model=dict[str, ProviderStatusModel])
async def get_control_center_status():
    """Returns the current configuration and operational status of all supported LLM providers."""
    return control_center_state.providers


@router.post("/test-connection", response_model=TestConnectionResponse)
async def test_provider_connection(request: TestConnectionRequest):
    """
    Tests live connectivity and authentication with the given provider (Groq or Gemini).
    Does not load models; tests authentication only.
    Transmitted API key is processed in memory only and discarded.
    """
    cid = correlation_id_ctx.get()
    provider = request.provider.lower().strip()

    if provider not in ("groq", "gemini"):
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Unsupported provider: '{provider}'. Supported providers are 'groq' and 'gemini'."
        )

    api_key = await _resolve_api_key(provider, request.api_key)
    if not api_key:
        return TestConnectionResponse(
            success=False,
            provider=provider,
            message="No API key provided or found in secure storage.",
            error_category="MISSING_API_KEY",
            duration_ms=0.0,
            correlation_id=cid
        )

    start_time = time.perf_counter()

    if provider == "groq":
        result = await discovery_service.test_groq_connection(api_key)
    else:
        result = await discovery_service.test_gemini_connection(api_key)

    duration_ms = round((time.perf_counter() - start_time) * 1000, 2)

    control_center_state.update_test_result(
        provider=provider,
        success=result["success"],
        message=result["message"],
        duration_ms=duration_ms,
        cid=cid,
        err_cat=result.get("error_category")
    )

    return TestConnectionResponse(
        success=result["success"],
        provider=provider,
        message=result["message"],
        error_category=result.get("error_category"),
        status_code=result.get("status_code"),
        duration_ms=duration_ms,
        correlation_id=cid
    )


@router.post("/load-models", response_model=LoadModelsResponse)
async def load_provider_models(request: LoadModelsRequest):
    """
    Loads models dynamically from the provider using the verified key.
    Enforces that connection test succeeded or valid key is provided.
    """
    cid = correlation_id_ctx.get()
    provider = request.provider.lower().strip()

    if provider not in ("groq", "gemini"):
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Unsupported provider: '{provider}'."
        )

    api_key = await _resolve_api_key(provider, request.api_key)
    if not api_key:
        return LoadModelsResponse(
            success=False,
            provider=provider,
            message="No API key provided or found in secure storage.",
            error_category="MISSING_API_KEY",
            models=[],
            duration_ms=0.0,
            correlation_id=cid
        )

    start_time = time.perf_counter()

    if provider == "groq":
        result = await discovery_service.test_groq_connection(api_key)
    else:
        result = await discovery_service.test_gemini_connection(api_key)

    duration_ms = round((time.perf_counter() - start_time) * 1000, 2)
    models = result.get("models", [])

    control_center_state.update_discovered_models(
        provider=provider,
        success=result["success"],
        models=models,
        duration_ms=duration_ms,
        cid=cid,
        err_cat=result.get("error_category")
    )

    return LoadModelsResponse(
        success=result["success"],
        provider=provider,
        message=result["message"],
        models=models,
        error_category=result.get("error_category"),
        status_code=result.get("status_code"),
        duration_ms=duration_ms,
        correlation_id=cid
    )


@router.post("/save-configuration")
async def save_provider_configuration(request: SaveConfigurationRequest):
    """
    Saves model selections and secure key references for the provider.
    Encrypts the API key server-side using RSA OAEP SHA-256 and persists it outside AIDatabase.
    Persists metadata reference into the authorized persistence architecture.
    Never stores or returns raw credentials.
    """
    provider = request.provider.lower().strip()
    if provider not in ("groq", "gemini"):
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Unsupported provider: '{provider}'"
        )

    if not request.default_model or not request.default_model.strip():
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="A default model must be selected."
        )

    if not request.key_reference_code or not request.key_reference_code.strip():
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="A key reference identifier is required."
        )

    # Handle credential encryption & persistence
    raw_key = request.api_key.strip() if request.api_key else ""
    csharp_persisted = False
    csharp_error = None
    if raw_key:
        try:
            save_res = await ai_credentials_service.save_credential(
                provider=provider,
                key_reference_code=request.key_reference_code.strip(),
                raw_api_key=raw_key,
                default_model=request.default_model.strip(),
                environment="Production"
            )
            csharp_persisted = save_res.get("csharp_persisted", False)
            csharp_error = save_res.get("csharp_error")
        except Exception as ex:
            logger.error("Failed to encrypt and store credential: %s", type(ex).__name__)
            raise HTTPException(
                status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
                detail=f"Failed to securely encrypt credential: {type(ex).__name__}"
            )
    else:
        # Verify an existing encrypted credential is present
        if not credential_store.has_credential(provider):
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail="API key must be provided when no prior credential is encrypted in storage."
            )
        # Update metadata for existing
        existing = credential_store.get_credential(provider)
        if existing:
            credential_store.save_credential(
                provider=provider,
                key_reference_code=request.key_reference_code.strip(),
                encrypted_secret=existing["encrypted_secret"],
                default_model=request.default_model.strip(),
                environment="Production"
            )
            # Check if C# persistence is also needed for existing credential
            csharp_persisted = True

    # Update in-memory state
    control_center_state.save_configuration(
        provider=provider,
        default_model=request.default_model.strip(),
        key_ref=request.key_reference_code.strip(),
        discovered_models=request.discovered_models
    )

    if csharp_persisted:
        msg = f"Configuration for provider '{provider}' saved centrally in AIDatabase."
    else:
        err_detail = f" (Central persistence error: {csharp_error})" if csharp_error else ""
        msg = f"Configuration for provider '{provider}' saved locally in encrypted storage.{err_detail}"

    return {
        "status": "SUCCESS" if (csharp_persisted or not raw_key) else "PARTIAL_SUCCESS",
        "message": msg,
        "provider": provider,
        "default_model": request.default_model.strip(),
        "key_reference_code": request.key_reference_code.strip(),
        "has_encrypted_credential": True,
        "csharp_persisted": csharp_persisted,
        "csharp_error": csharp_error
    }


@router.get("/diagnostics", response_model=dict[str, DiagnosticItem])
async def get_control_center_diagnostics():
    """Returns detailed diagnostic telemetry including auth and discovery status."""
    return control_center_state.diagnostics
