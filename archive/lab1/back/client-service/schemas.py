from datetime import date, datetime
from typing import Optional
from pydantic import BaseModel, EmailStr, Field, ConfigDict
from database import ClientStatus


class ClientCreate(BaseModel):
    full_name: str = Field(..., min_length=3, max_length=255, examples=["Шевченко Тарас Григорович"])
    birth_date: date = Field(..., examples=["1995-03-09"])
    address: str = Field(..., min_length=5, max_length=500, examples=["м. Київ, вул. Хрещатик, 1"])
    tax_id: str = Field(..., min_length=8, max_length=20, examples=["1234567890"])
    passport_number: str = Field(..., min_length=6, max_length=50, examples=["001234567"])
    email: EmailStr = Field(..., examples=["taras@example.com"])
    phone: str = Field(..., pattern=r"^\+?[0-9]{10,15}$", examples=["+380501234567"])


class ClientUpdate(BaseModel):
    full_name: Optional[str] = Field(None, min_length=3, max_length=255)
    address: Optional[str] = Field(None, min_length=5, max_length=500)
    email: Optional[EmailStr] = None
    phone: Optional[str] = Field(None, pattern=r"^\+?[0-9]{10,15}$")


class ClientBlockRequest(BaseModel):
    reason: str = Field(..., min_length=3, max_length=500, examples=["Підозріла активність"])


class ClientResponse(BaseModel):
    id: int
    full_name: str
    birth_date: date
    address: str
    tax_id: str
    passport_number: str
    email: EmailStr
    phone: str
    status: ClientStatus
    block_reason: Optional[str] = None
    created_at: datetime
    updated_at: datetime

    model_config = ConfigDict(from_attributes=True)


class ClientIdentifyResponse(BaseModel):
    """Спрощена схема для швидкої ідентифікації іншими мікросервісами."""
    id: int
    full_name: str
    tax_id: str
    status: ClientStatus
    is_active: bool