import os
import logging
from typing import Optional
import httpx
from fastapi import APIRouter, Depends, HTTPException, Query, BackgroundTasks, status
from sqlalchemy.orm import Session
from sqlalchemy.exc import IntegrityError

from database import get_db, Client, ClientStatus
from schemas import (
    ClientCreate,
    ClientUpdate,
    ClientBlockRequest,
    ClientResponse,
    ClientIdentifyResponse,
)

logger = logging.getLogger("client-service")
router = APIRouter(prefix="/clients", tags=["Clients"])

# URL-addresses of other services
SERVICE_URLS = {
    "account": os.getenv("ACCOUNT_SERVICE_URL", "http://account-service:8001"),
    "transaction": os.getenv("TRANSACTION_SERVICE_URL", "http://transaction-service:8002"),
    "credit": os.getenv("CREDIT_SERVICE_URL", "http://credit-service:8003"),
    "deposit": os.getenv("DEPOSIT_SERVICE_URL", "http://deposit-service:8004"),
    "notification": os.getenv("NOTIFICATION_SERVICE_URL", "http://notification-service:8005"),
}


async def notify_external_service(service_name: str, endpoint: str, payload: dict):
    """Asynchronous notification of other services."""
    url = f"{SERVICE_URLS[service_name]}{endpoint}"
    try:
        async with httpx.AsyncClient(timeout=5.0) as client:
            response = await client.post(url, json=payload)
            response.raise_for_status()
            logger.info(f"Notification success: {service_name} ({url})")
    except httpx.HTTPError as exc:
        # TODO: add retry mechanism or send to broker
        logger.error(f"Error with: {service_name} ({url}): {exc}")


@router.post("/", response_model=ClientResponse, status_code=status.HTTP_201_CREATED)
def register_client(
    client_data: ClientCreate,
    background_tasks: BackgroundTasks,
    db: Session = Depends(get_db),
):
    """Registering a new user."""
    new_client = Client(**client_data.model_dump())
    db.add(new_client)
    try:
        db.commit()
        db.refresh(new_client)
    except IntegrityError:
        db.rollback()
        raise HTTPException(
            status_code=status.HTTP_409_CONFLICT,
            detail="Client with given info already exists.",
        )

    # Sending message to notification service
    background_tasks.add_task(
        notify_external_service,
        "notification",
        "/notifications/send",
        {
            "client_id": new_client.id,
            "email": new_client.email,
            "phone": new_client.phone,
            "event": "CLIENT_REGISTERED",
            "message": f"Welcome, {new_client.full_name}! Your account has been created successfully.",
        },
    )
    return new_client


@router.get("/identify", response_model=ClientIdentifyResponse)
def identify_client(
    client_id: Optional[int] = Query(None, description="Client ID"),
    tax_id: Optional[str] = Query(None, description="Tax number (РНОКПП (ІПН))"),
    phone: Optional[str] = Query(None, description="Phone number"),
    db: Session = Depends(get_db),
):
    """
    Identify client by ID, tax number or phone number.
    Used by other services to check for clients existence and status.
    """
    if not any([client_id, tax_id, phone]):
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Specify at least one: client_id, tax_id or phone.",
        )

    query = db.query(Client)
    if client_id:
        query = query.filter(Client.id == client_id)
    elif tax_id:
        query = query.filter(Client.tax_id == tax_id)
    elif phone:
        query = query.filter(Client.phone == phone)

    client = query.first()
    if not client:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Client not found.")

    return ClientIdentifyResponse(
        id=client.id,
        full_name=client.full_name,
        tax_id=client.tax_id,
        status=client.status,
        is_active=(client.status == ClientStatus.ACTIVE),
    )


@router.get("/{client_id}", response_model=ClientResponse)
def get_client_profile(client_id: int, db: Session = Depends(get_db)):
    """Getting full client info by ID."""
    client = db.query(Client).filter(Client.id == client_id).first()
    if not client:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Client not found.")
    return client


@router.patch("/{client_id}", response_model=ClientResponse)
def update_client_profile(
    client_id: int,
    update_data: ClientUpdate,
    background_tasks: BackgroundTasks,
    db: Session = Depends(get_db),
):
    """Updating client info."""
    client = db.query(Client).filter(Client.id == client_id).first()
    if not client:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Client not found.")

    if client.status == ClientStatus.BLOCKED:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Cannot access blocked account",
        )

    fields_to_update = update_data.model_dump(exclude_unset=True)
    for key, value in fields_to_update.items():
        setattr(client, key, value)

    try:
        db.commit()
        db.refresh(client)
    except IntegrityError:
        db.rollback()
        raise HTTPException(
            status_code=status.HTTP_409_CONFLICT,
            detail="Email or phone number already in use by other client.",
        )

    background_tasks.add_task(
        notify_external_service,
        "notification",
        "/notifications/send",
        {
            "client_id": client.id,
            "email": client.email,
            "phone": client.phone,
            "event": "CLIENT_UPDATED",
            "message": "Client info successfully updated.",
        },
    )
    return client


@router.post("/{client_id}/block", response_model=ClientResponse)
def block_client_profile(
    client_id: int,
    block_data: ClientBlockRequest,
    background_tasks: BackgroundTasks,
    db: Session = Depends(get_db),
):
    """
    Blocking client and notifying other services about it.
    """
    client = db.query(Client).filter(Client.id == client_id).first()
    if not client:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Client not found.")

    if client.status == ClientStatus.BLOCKED:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Account already blocked.",
        )

    client.status = ClientStatus.BLOCKED
    client.block_reason = block_data.reason
    db.commit()
    db.refresh(client)

    event_payload = {
        "client_id": client.id,
        "status": client.status.value,
        "reason": client.block_reason,
    }

    # Notify all other services
    background_tasks.add_task(notify_external_service, "account", "/internal/clients/blocked", event_payload)
    background_tasks.add_task(notify_external_service, "transaction", "/internal/clients/blocked", event_payload)
    background_tasks.add_task(notify_external_service, "credit", "/internal/clients/blocked", event_payload)
    background_tasks.add_task(notify_external_service, "deposit", "/internal/clients/blocked", event_payload)

    # Notifying the client themselves with notification-service
    background_tasks.add_task(
        notify_external_service,
        "notification",
        "/notifications/send",
        {
            "client_id": client.id,
            "email": client.email,
            "phone": client.phone,
            "event": "CLIENT_BLOCKED",
            "message": f"Your account is blocked. Reason: {client.block_reason}",
        },
    )

    return client