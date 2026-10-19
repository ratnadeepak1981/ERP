from typing import Any, Protocol, runtime_checkable

from app.domain.models.completion import ProviderCompletionResponse


@runtime_checkable
class ILLMProvider(Protocol):
    """
    Interface definition for an AI model provider adapter (e.g. Groq, Gemini).
    Follows Dependency Inversion: consumers depend on this contract, not vendor SDKs.
    """

    @property
    def provider_name(self) -> str:
        """Name of the provider (e.g., 'groq', 'gemini')."""
        ...

    async def generate_async(
        self,
        prompt: str,
        system_instruction: str | None = None,
        model: str | None = None,
        temperature: float = 0.2,
        max_tokens: int | None = None,
        **kwargs: Any,
    ) -> ProviderCompletionResponse:
        """Asynchronously generates completion text from the configured model."""
        ...
