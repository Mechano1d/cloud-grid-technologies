import os
import enum
from datetime import datetime, date
from sqlalchemy import create_engine, Column, Integer, String, Date, DateTime, Enum
from sqlalchemy.orm import declarative_base, sessionmaker

DATABASE_URL = os.getenv("DATABASE_URL", "sqlite:///./clients.db")

connect_args = {"check_same_thread": False} if DATABASE_URL.startswith("sqlite") else {}
engine = create_engine(DATABASE_URL, connect_args=connect_args)
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)

Base = declarative_base()


class ClientStatus(str, enum.Enum):
    ACTIVE = "active"
    BLOCKED = "blocked"


class Client(Base):
    __tablename__ = "clients"

    # general info
    id = Column(Integer, primary_key=True, index=True)
    full_name = Column(String(255), nullable=False)
    birth_date = Column(Date, nullable=False)
    address = Column(String(500), nullable=False)
    tax_id = Column(String(20), unique=True, index=True, nullable=False)  # РНОКПП (ІПН)
    passport_number = Column(String(50), unique=True, nullable=False)

    # contact info
    email = Column(String(255), unique=True, index=True, nullable=False)
    phone = Column(String(20), unique=True, index=True, nullable=False)

    # service info
    status = Column(Enum(ClientStatus), default=ClientStatus.ACTIVE, nullable=False)
    block_reason = Column(String(500), nullable=True)
    created_at = Column(DateTime, default=datetime.utcnow, nullable=False)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow, nullable=False)


def init_db():
    Base.metadata.create_all(bind=engine)


def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()