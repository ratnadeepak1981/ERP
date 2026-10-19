from typing import Any
from pydantic import BaseModel, ConfigDict, Field


class TokenUsage(BaseModel):
    """Model tracking token consumption for an AI invocation."""

    model_config = ConfigDict(frozen=True)

    prompt_tokens: int = Field(default=0, ge=0)
    completion_tokens: int = Field(default=0, ge=0)
    total_tokens: int = Field(default=0, ge=0)


class ProviderCompletionResponse(BaseModel):
    """Standardized response from an AI provider completion."""

    model_config = ConfigDict(frozen=True)

    content: str
    model: str
    provider: str
    finish_reason: str = "stop"
    usage: TokenUsage = Field(default_factory=TokenUsage)
    raw_response: dict[str, Any] = Field(default_factory=dict, repr=False)
