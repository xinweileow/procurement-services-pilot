from fastapi.testclient import TestClient

from app.main import app

client = TestClient(app)


def _valid_payload(**overrides):
    payload = {
        "request_id": "PR-2026-00001",
        "gl_account": "6000-1000",
        "amount": "1500.50",
        "tax_amount": "90.03",
        "wht_amount": "15.00",
        "currency": "MYR",
        "description": "PO settlement GL posting",
    }
    payload.update(overrides)
    return payload


def test_create_gl_posting_success():
    response = client.post("/mock/gl-posting", json=_valid_payload())
    assert response.status_code == 201
    body = response.json()
    assert body["status"] == "posted"
    assert body["posting_id"].startswith("GLP-")
    assert body["request_id"] == "PR-2026-00001"
    assert body["gl_account"] == "6000-1000"
    assert body["amount"] == "1500.50"
    assert "posted_on" in body


def test_create_gl_posting_missing_gl_account():
    response = client.post("/mock/gl-posting", json=_valid_payload(gl_account=""))
    assert response.status_code == 400


def test_create_gl_posting_missing_request_id():
    response = client.post("/mock/gl-posting", json=_valid_payload(request_id=""))
    assert response.status_code == 400


def test_create_gl_posting_invalid_amount():
    response = client.post("/mock/gl-posting", json=_valid_payload(amount="0"))
    assert response.status_code == 422


def test_create_gl_posting_negative_tax_amount():
    response = client.post("/mock/gl-posting", json=_valid_payload(tax_amount="-5"))
    assert response.status_code == 422


def test_get_gl_posting_roundtrip():
    create_resp = client.post("/mock/gl-posting", json=_valid_payload(request_id="PR-2026-00002"))
    posting_id = create_resp.json()["posting_id"]

    get_resp = client.get(f"/mock/gl-posting/{posting_id}")
    assert get_resp.status_code == 200
    assert get_resp.json()["posting_id"] == posting_id


def test_get_gl_posting_not_found():
    response = client.get("/mock/gl-posting/GLP-DOESNOTEXIST")
    assert response.status_code == 404


def test_list_gl_postings_filtered_by_request_id():
    request_id = "PR-2026-00099"
    client.post("/mock/gl-posting", json=_valid_payload(request_id=request_id, gl_account="7000-2000"))
    client.post("/mock/gl-posting", json=_valid_payload(request_id="PR-2026-00100"))

    response = client.get("/mock/gl-posting", params={"request_id": request_id})
    assert response.status_code == 200
    postings = response.json()
    assert len(postings) >= 1
    assert all(p["request_id"] == request_id for p in postings)
