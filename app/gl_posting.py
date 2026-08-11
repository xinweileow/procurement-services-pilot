"""GL Posting Placeholder Integration.

This module provides a PRELIMINARY / MOCK service for posting procurement
financial events (e.g. Payment, Tax, WHT) to the General Ledger (GL).

It is explicitly a stop-gap pending the final finance system integration
(see Business KB: "Financial Close & Controls -> GL Posting (Tax / WHT)").

No autonomous approval, policy decision, external-connector authority or
production mutation is performed here - this only simulates a GL posting
and keeps an in-memory, append-only log of postings for traceability.
"""
from __future__ import annotations

import uuid
from datetime import datetime, timezone
from decimal import Decimal
from typing import Dict, List, Optional

from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field

router = APIRouter(prefix="/mock", tags=["gl-posting"])


class GLPostingRequest(BaseModel):
    request_id: str = Field(..., description="Procurement request this posting relates to")
    gl_account: str = Field(..., description="Target GL account / charge account code")
    amount: Decimal = Field(..., description="Principal amount to post")
    tax_amount: Optional[Decimal] = Field(default=None, description="Tax amount, if applicable")
    wht_amount: Optional[Decimal] = Field(default=None, description="Withholding tax amount, if applicable")
    currency: str = Field(default="MYR", description="ISO currency code")
    description: Optional[str] = Field(default=None, description="Free-text posting narrative")


class GLPostingResponse(BaseModel):
    posting_id: str
    request_id: str
    gl_account: str
    amount: Decimal
    tax_amount: Optional[Decimal]
    wht_amount: Optional[Decimal]
    currency: str
    status: str
    posted_on: str


# In-memory, append-only store of postings (mock persistence only).
_GL_POSTINGS: Dict[str, GLPostingResponse] = {}


def _validate(payload: GLPostingRequest) -> None:
    if not payload.gl_account or not payload.gl_account.strip():
        raise HTTPException(status_code=400, detail="gl_account is required")
    if not payload.request_id or not payload.request_id.strip():
        raise HTTPException(status_code=400, detail="request_id is required")
    if payload.amount is None or payload.amount <= 0:
        raise HTTPException(status_code=422, detail="amount must be greater than zero")
    if payload.tax_amount is not None and payload.tax_amount < 0:
        raise HTTPException(status_code=422, detail="tax_amount cannot be negative")
    if payload.wht_amount is not None and payload.wht_amount < 0:
        raise HTTPException(status_code=422, detail="wht_amount cannot be negative")


def post_to_gl(payload: GLPostingRequest) -> GLPostingResponse:
    """Simulate posting to the GL. Preliminary placeholder only."""
    _validate(payload)

    posting = GLPostingResponse(
        posting_id=f"GLP-{uuid.uuid4().hex[:12].upper()}",
        request_id=payload.request_id,
        gl_account=payload.gl_account,
        amount=payload.amount,
        tax_amount=payload.tax_amount,
        wht_amount=payload.wht_amount,
        currency=payload.currency,
        status="posted",
        posted_on=datetime.now(timezone.utc).isoformat(),
    )
    _GL_POSTINGS[posting.posting_id] = posting
    return posting


@router.post("/gl-posting", response_model=GLPostingResponse, status_code=201)
def create_gl_posting(payload: GLPostingRequest) -> GLPostingResponse:
    return post_to_gl(payload)


@router.get("/gl-posting/{posting_id}", response_model=GLPostingResponse)
def get_gl_posting(posting_id: str) -> GLPostingResponse:
    posting = _GL_POSTINGS.get(posting_id)
    if posting is None:
        raise HTTPException(status_code=404, detail="GL posting not found")
    return posting


@router.get("/gl-posting", response_model=List[GLPostingResponse])
def list_gl_postings(request_id: Optional[str] = None) -> List[GLPostingResponse]:
    postings = list(_GL_POSTINGS.values())
    if request_id:
        postings = [p for p in postings if p.request_id == request_id]
    return postings
