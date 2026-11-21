import logging
from contextlib import asynccontextmanager
from fastapi import FastAPI
from database import init_db
from routes import router as client_router

logging.basicConfig(level=logging.INFO)


@asynccontextmanager
async def lifespan(app: FastAPI):
    # initializing database tables
    init_db()
    yield


app = FastAPI(
    title="Bank Client Service",
    description="Client information management service",
    version="1.0.0",
    lifespan=lifespan,
)

app.include_router(client_router)


@app.get("/health", tags=["System"])
def health_check():
    return {"status": "ok", "service": "client-service"}