from datetime import datetime, timezone
from typing import Any
from pydantic import BaseModel, Field

from app.core.credential_store import credential_store


class ProviderStatusModel(BaseModel):
    provider: str
    display_name: str
    is_configured: bool = False
    is_tested: bool = False
    last_test_status: str = "NOT_TESTED"  # "SUCCESS", "FAILED", "NOT_TESTED"
    last_test_message: str | None = None
    default_model: str | None = None
    available_models: list[str] = Field(default_factory=list)
    key_reference_code: str | None = None
    has_encrypted_credential: bool = False


class TestConnectionRequest(BaseModel):
    provider: str  # "groq" or "gemini"
    api_key: str | None = Field(None, description="Masked/transient API key entered by user; optional if saved encrypted credential exists.")


class TestConnectionResponse(BaseModel):
    success: bool
    provider: str
    message: str
    error_category: str | None = None
    status_code: int | None = None
    duration_ms: float = 0.0
    correlation_id: str | None = None


class LoadModelsRequest(BaseModel):
    provider: str
    api_key: str | None = Field(None, description="Masked/transient API key entered by user; optional if saved encrypted credential exists.")


class LoadModelsResponse(BaseModel):
    success: bool
    provider: str
    message: str
    models: list[str] = Field(default_factory=list)
    error_category: str | None = None
    status_code: int | None = None
    duration_ms: float = 0.0
    correlation_id: str | None = None


class SaveConfigurationRequest(BaseModel):
    provider: str
    default_model: str
    key_reference_code: str = Field(..., description="Secure key reference code pointing to environment/vault.")
    api_key: str | None = Field(None, description="API key to encrypt and save. Optional if credential already saved.")
    discovered_models: list[str] = Field(default_factory=list)


class DiagnosticItem(BaseModel):
    provider: str
    configured: bool
    tested: bool
    auth_status: str  # "SUCCESS", "FAILED", "NOT_TESTED"
    discovery_status: str  # "DISCOVERED", "EMPTY", "NOT_ATTEMPTED"
    inference_status: str  # "VERIFIED", "UNTESTED"
    error_category: str | None = None
    last_action_timestamp: str = Field(default_factory=lambda: datetime.now(timezone.utc).isoformat())
    duration_ms: float = 0.0
    correlation_id: str | None = None


class ControlCenterState:
    """
    In-memory state container for AI Control Center operations.
    Stores metadata, model selections, and references only. Never stores raw API keys.
    Initializes configuration status from saved encrypted credential store.
    """

    def __init__(self):
        # Check if saved encrypted credentials exist
        groq_saved = credential_store.get_credential("groq")
        gemini_saved = credential_store.get_credential("gemini")

        self.providers: dict[str, ProviderStatusModel] = {
            "groq": ProviderStatusModel(
                provider="groq",
                display_name="Groq",
                is_configured=bool(groq_saved),
                is_tested=False,
                last_test_status="NOT_TESTED",
                default_model=groq_saved.get("default_model") if groq_saved else None,
                available_models=[],
                key_reference_code=groq_saved.get("key_reference_code", "GROQ_API_KEY") if groq_saved else "GROQ_API_KEY",
                has_encrypted_credential=bool(groq_saved)
            ),
            "gemini": ProviderStatusModel(
                provider="gemini",
                display_name="Google Gemini",
                is_configured=bool(gemini_saved),
                is_tested=False,
                last_test_status="NOT_TESTED",
                default_model=gemini_saved.get("default_model") if gemini_saved else None,
                available_models=[],
                key_reference_code=gemini_saved.get("key_reference_code", "GEMINI_API_KEY") if gemini_saved else "GEMINI_API_KEY",
                has_encrypted_credential=bool(gemini_saved)
            ),
        }
        self.diagnostics: dict[str, DiagnosticItem] = {
            "groq": DiagnosticItem(
                provider="groq",
                configured=bool(groq_saved),
                tested=False,
                auth_status="NOT_TESTED",
                discovery_status="NOT_ATTEMPTED",
                inference_status="UNTESTED"
            ),
            "gemini": DiagnosticItem(
                provider="gemini",
                configured=bool(gemini_saved),
                tested=False,
                auth_status="NOT_TESTED",
                discovery_status="NOT_ATTEMPTED",
                inference_status="UNTESTED"
            ),
        }

    def update_test_result(self, provider: str, success: bool, message: str, duration_ms: float, cid: str, err_cat: str | None):
        p = self.providers.get(provider)
        if p:
            p.is_tested = True
            p.last_test_status = "SUCCESS" if success else "FAILED"
            p.last_test_message = message

        d = self.diagnostics.get(provider)
        if d:
            d.tested = True
            d.auth_status = "SUCCESS" if success else "FAILED"
            d.error_category = err_cat
            d.duration_ms = duration_ms
            d.correlation_id = cid
            d.last_action_timestamp = datetime.now(timezone.utc).isoformat()

    def update_discovered_models(self, provider: str, success: bool, models: list[str], duration_ms: float, cid: str, err_cat: str | None):
        p = self.providers.get(provider)
        if p and success:
            p.available_models = models
            if models and (not p.default_model or p.default_model not in models):
                p.default_model = models[0]

        d = self.diagnostics.get(provider)
        if d:
            d.discovery_status = "DISCOVERED" if models else ("EMPTY" if success else "NOT_ATTEMPTED")
            d.error_category = err_cat
            d.duration_ms = duration_ms
            d.correlation_id = cid
            d.last_action_timestamp = datetime.now(timezone.utc).isoformat()

    def save_configuration(self, provider: str, default_model: str, key_ref: str, discovered_models: list[str]):
        p = self.providers.get(provider)
        if p:
            p.is_configured = True
            p.default_model = default_model
            p.key_reference_code = key_ref
            p.has_encrypted_credential = True
            if discovered_models:
                p.available_models = discovered_models

        d = self.diagnostics.get(provider)
        if d:
            d.configured = True
            d.last_action_timestamp = datetime.now(timezone.utc).isoformat()


# Global singleton instance for FastAPI session
control_center_state = ControlCenterState()
