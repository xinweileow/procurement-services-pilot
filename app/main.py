from fastapi import FastAPI

from app.gl_posting import router as gl_posting_router

app = FastAPI(title="Procurement System")

app.include_router(gl_posting_router)


@app.get("/health")
def health() -> dict:
    return {"status": "ok"}
