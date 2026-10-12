import logging
import uuid
from contextvars import ContextVar
from typing import Callable

from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request
from starlette.responses import Response

# Context variable for storing the correlation ID throughout the async execution context
correlation_id_ctx: ContextVar[str] = ContextVar("correlation_id", default="")
tenant_id_ctx: ContextVar[str] = ContextVar("tenant_id", default="")

CORRELATION_HEADER = "X-Correlation-ID"


class StructuredLogRecord(logging.LogRecord):
    def getMessage(self) -> str:
        msg = super().getMessage()
        cid = correlation_id_ctx.get()
        tid = tenant_id_ctx.get()
        extra_parts = []
        if cid:
            extra_parts.append(f"corr_id={cid}")
        if tid:
            extra_parts.append(f"tenant_id={tid}")
        if extra_parts:
            return f"[{' '.join(extra_parts)}] {msg}"
        return msg


def configure_logging(level: str = "INFO") -> None:
    """Configures safe, structured standard library logging."""
    logging.setLogRecordFactory(StructuredLogRecord)
    logging.basicConfig(
        level=getattr(logging, level.upper(), logging.INFO),
        format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
        force=True,
    )


class CorrelationIdMiddleware(BaseHTTPMiddleware):
    """
    Middleware that extracts or generates a correlation ID, sets it in contextvars,
    and returns it in the X-Correlation-ID response header.
    """

    async def dispatch(self, request: Request, call_next: Callable) -> Response:
        # Check incoming header or generate new UUID4
        incoming_cid = request.headers.get(CORRELATION_HEADER)
        cid = incoming_cid.strip() if incoming_cid else str(uuid.uuid4())

        # Bind to contextvars
        correlation_token = correlation_id_ctx.set(cid)
        tenant_token = tenant_id_ctx.set("")

        try:
            response = await call_next(request)
            response.headers[CORRELATION_HEADER] = cid
            return response
        finally:
            correlation_id_ctx.reset(correlation_token)
            tenant_id_ctx.reset(tenant_token)
