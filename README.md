# Online Store Platform

A microservices-based e-commerce platform demonstrating contract-first API design, 
synchronous service integration, event-driven architecture, 
and distributed transaction management.

## Architecture Overview

┌──────────────────────────────────────────────────────────────┐
│                  Storefront Web App (Port 5005)              │
│                ASP.NET Core MVC / Razor Pages                 │
└──────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              │               │               │
              ▼               ▼               ▼
       ┌────────────┐  ┌────────────┐  ┌──────────────┐
       │  Catalog   │  │   Order    │  │  Inventory   │
       │  Service   │  │  Service   │  │   Service    │
       │   (5001)   │  │   (5002)   │  │    (5003)    │
       ├────────────┤  ├────────────┤  ├──────────────┤
       │ GET /POST  │  │ GET /POST  │  │  GET /POST   │
       │ /products  │  │  /orders   │  │  /inventory  │
       │            │  │            │  │              │
       │   SQLite   │  │   SQLite   │  │    SQLite    │
       └────────────┘  └─────┬──────┘  └──────────────┘
                             │
                             │ Publishes
                             │ OrderPlaced
                             ▼
                       ┌─────────────┐
                       │  RabbitMQ   │
                       │    Broker   │
                       └──────┬──────┘
                              │
                 ┌────────────┴────────────┐
                 │                         │
                 ▼                         ▼
          ┌──────────────┐          ┌──────────────┐
          │  Inventory   │          │   Payment    │
          │   Consumer   │          │   Service    │
          │  (Reserves)  │          │    (5004)    │
          ├──────────────┤          ├──────────────┤
          │    SQLite    │          │ POST /refund │
          │              │          │              │
          │              │          │    SQLite    │
          └──────────────┘          └──────────────┘


            
## Services

| Service | Port | Database | Responsibility |
|---------|------|----------|-----------------|
| **Catalog** | 5001 | SQLite | Product CRUD, full catalog |
| **Order** | 5002 | SQLite | Order CRUD, orchestrates saga |
| **Inventory** | 5003 | SQLite | Stock management, reservations |
| **Payment** | 5004 | SQLite | Payment processing, refunds |
| **Storefront** | 5005 | N/A | Web UI, API consumer |

## Key Features

✅ **Contract-First API Design** — OpenAPI specs authored before implementation  
✅ **URI Versioning** — All APIs use `/v1/` path versioning  
✅ **Typed HTTP Clients** — Generated from contracts via NSwag  
✅ **Resilience** — Retry + exponential backoff + timeouts  
✅ **Correlation IDs** — End-to-end request tracing  
✅ **Event-Driven Integration** — RabbitMQ publish/subscribe with idempotent consumers  
✅ **Distributed Saga** — Order → Inventory → Payment with compensation on failure  
✅ **Problem Details** — RFC 7807 error responses  
✅ **Database Per Service** — EF Core with SQLite (no shared database)  

## Prerequisites

- .NET 8 SDK or later
- RabbitMQ (native installation or CloudAMQP account)
- SQLite (included with .NET)
- Visual Studio 2022 or VS Code

## Installation & Setup

### 1. Clone the Repository

```bash
git clone https://github.com/ErlNievera/OnlineStore.git
cd OnlineStore

2. Set Up RabbitMQ
Option A: Native Installation

bash
# Windows: Download from https://www.rabbitmq.com/download.html

# macOS:
brew install rabbitmq
brew services start rabbitmq

# Linux:
sudo apt-get install rabbitmq-server
sudo systemctl start rabbitmq-server
RabbitMQ will run on localhost:5672 (AMQP) and localhost:15672 (Management UI)

Option B: CloudAMQP (Free Tier)

Sign up at https://www.cloudamqp.com
Create a free instance
Copy the connection string and update appsettings.json in each service

3. Build the Solution
bash
dotnet build OnlineStore.sln

4. Run Database Migrations (per service)
Each service has its own SQLite database. Migrations are applied automatically on startup, or manually:

bash
# Catalog Service
cd src/CatalogService
dotnet ef database update

# Order Service
cd ../OrderService
dotnet ef database update

# Inventory Service
cd ../InventoryService
dotnet ef database update

# Payment Service
cd ../PaymentService
dotnet ef database update

5. Run All Services
Open 5 terminal windows and start each service:

bash
# Terminal 1 - Catalog Service
cd src/CatalogService
dotnet run --launch-profile https

# Terminal 2 - Order Service
cd src/OrderService
dotnet run --launch-profile https

# Terminal 3 - Inventory Service
cd src/InventoryService
dotnet run --launch-profile https

# Terminal 4 - Payment Service
cd src/PaymentService
dotnet run --launch-profile https

# Terminal 5 - Storefront Web App
cd web/Storefront
dotnet run --launch-profile https

6. Access the Application
Service	URL
Catalog Swagger	https://localhost:5001/swagger
Order Swagger	https://localhost:5002/swagger
Inventory Swagger	https://localhost:5003/swagger
Payment Swagger	https://localhost:5004/swagger
Storefront	https://localhost:5005
RabbitMQ Management	http://localhost:15672 (guest / guest)

API Usage Examples
Create a Product (Catalog)
bash
curl -X POST https://localhost:5001/catalog/v1/products \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: $(uuidgen)" \
  -d '{
    "name": "Laptop",
    "description": "High-performance laptop",
    "price": 999.99
  }'
Create Inventory
bash
curl -X POST https://localhost:5003/inventory/v1/inventory \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: $(uuidgen)" \
  -d '{
    "productId": 1,
    "quantityAvailable": 100
  }'
Place an Order (Triggers Saga)
bash
curl -X POST https://localhost:5002/orders/v1/orders \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: $(uuidgen)" \
  -d '{
    "productId": 1,
    "quantity": 5
  }'
This triggers the saga:

Validates product exists (calls Catalog synchronously)
Creates order in Pending status
Publishes OrderPlaced event to RabbitMQ
Inventory service consumes and reserves stock
Payment service consumes and charges payment
Order transitions to Confirmed (or Cancelled on failure)
Postman Collections
Import the Postman collections to test all APIs:

postman/Catalog.postman_collection.json
postman/Orders.postman_collection.json
postman/Inventory.postman_collection.json
postman/Payment.postman_collection.json

Project Structure
Code
OnlineStore/
├── README.md
├── CONTRIBUTIONS.md
├── .gitignore
├── OnlineStore.sln
├── contracts/
│   ├── catalog.yaml
│   ├── orders.yaml
│   ├── inventory.yaml
│   └── payment.yaml
├── src/
│   ├── CatalogService/
│   │   ├── CatalogService.csproj
│   │   ├── Program.cs
│   │   ├── Controllers/
│   │   ├── Models/
│   │   ├── Data/
│   │   └── appsettings.json
│   ├── OrderService/
│   ├── InventoryService/
│   ├── PaymentService/
│   └── Shared/
│       ├── Events/
│       ├── DTOs/
│       └── Handlers/
├── web/
│   └── Storefront/
│       ├── Storefront.csproj
│       ├── Program.cs
│       ├── Pages/
│       └── wwwroot/
└── postman/
    ├── Catalog.postman_collection.json
    ├── Orders.postman_collection.json
    ├── Inventory.postman_collection.json
    └── Payment.postman_collection.json

Key Design Patterns:

1. Synchronous Service Integration (Module 3)
Order Service calls Catalog Service via typed HttpClient
Retry policy: exponential backoff with jitter (3 retries)
Timeout: 5 seconds per attempt, 15 seconds total
Correlation ID attached to every call for tracing

2. Event-Driven Integration (Module 4)
Order Service publishes OrderPlaced events
Inventory and Payment services consume events asynchronously
Consumer is idempotent (same event processed only once)
Dead-letter queue captures poison messages

3. Distributed Saga (Module 5)
Happy Path: Order → Reserve Stock → Charge Payment → Confirm Order
Failure Path: Any step fails → Release Stock + Mark Order Cancelled + Refund Payment
Each step is a local database transaction within its service
Compensation is idempotent and can be safely retried

4. Data Consistency
Immediate Consistency: Catalog validation (sync call)
Eventual Consistency: Inventory and Payment (via saga)
Correlation ID enables audit trail of entire distributed transaction

Troubleshooting:

RabbitMQ Connection Fails
Check RabbitMQ is running: sudo systemctl status rabbitmq-server
Verify connection string in appsettings.json
Check firewall allows 5672 (AMQP) and 15672 (Management)

Database Locked / Migration Fails
Delete .db files in each service folder
Re-run migrations: dotnet ef database update

API Returns 404
Verify service is running on correct port (5001-5004)
Check Swagger UI shows the endpoint
Confirm correlation ID is in request header

Order Creation Fails
Ensure all services (Catalog, Inventory, Payment) are running
Check logs for cross-service call failures
Verify product exists in Catalog and inventory is created

License
This project is for educational purposes as part of a microservices course.
