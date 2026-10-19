import unittest
from pydantic import ValidationError

from app.domain.contracts.provider import ILLMProvider
from app.domain.contracts.erp_api_client import IErpApiClient
from app.domain.models.completion import ProviderCompletionResponse, TokenUsage


class MockProvider:
    """Mock implementation of ILLMProvider for contract verification."""

    @property
    def provider_name(self) -> str:
        return "mock"

    async def generate_async(
        self,
        prompt: str,
        system_instruction: str | None = None,
        model: str | None = None,
        temperature: float = 0.2,
        max_tokens: int | None = None,
        **kwargs,
    ) -> ProviderCompletionResponse:
        return ProviderCompletionResponse(
            content="Mock response",
            model="mock-v1",
            provider="mock",
            usage=TokenUsage(prompt_tokens=10, completion_tokens=5, total_tokens=15),
        )


class MockErpApiClient:
    """Mock implementation of IErpApiClient for contract verification."""

    async def get_async(
        self,
        endpoint: str,
        bearer_token: str,
        params: dict | None = None,
    ) -> dict:
        return {"status": "SUCCESS"}

    async def post_async(
        self,
        endpoint: str,
        bearer_token: str,
        payload: dict,
    ) -> dict:
        return {"status": "SUCCESS"}


class TestContracts(unittest.TestCase):
    """Verifies that domain contracts, protocols, and Pydantic models behave as expected."""

    def test_provider_completion_response_validation(self):
        resp = ProviderCompletionResponse(
            content="Hello world",
            model="llama-3.3-70b",
            provider="groq",
            usage=TokenUsage(prompt_tokens=5, completion_tokens=10, total_tokens=15),
        )
        self.assertEqual(resp.content, "Hello world")
        self.assertEqual(resp.model, "llama-3.3-70b")
        self.assertEqual(resp.provider, "groq")
        self.assertEqual(resp.usage.total_tokens, 15)

    def test_provider_completion_response_immutable(self):
        resp = ProviderCompletionResponse(
            content="Hello world",
            model="llama-3.3-70b",
            provider="groq",
        )
        with self.assertRaises(ValidationError):
            resp.content = "New content"

    def test_illm_provider_protocol_conformance(self):
        provider = MockProvider()
        self.assertIsInstance(provider, ILLMProvider)

    def test_ierp_api_client_protocol_conformance(self):
        client = MockErpApiClient()
        self.assertIsInstance(client, IErpApiClient)


if __name__ == "__main__":
    unittest.main()
