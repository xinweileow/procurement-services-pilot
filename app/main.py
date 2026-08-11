from fastapi import FastAPI

app = FastAPI(title="Procurement System")


@app.get("/health")
def health() -> dict:
    return {"status": "ok"}
