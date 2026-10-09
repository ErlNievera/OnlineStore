# Online Store Platform

A microservices-based e-commerce platform built with ASP.NET Core, C#, Entity Framework Core, SQLite, and RabbitMQ. This project demonstrates contract-first API design, synchronous service integration, event-driven communication, and distributed transaction management using the Saga pattern.

## Architecture Overview

The platform consists of five applications:

- **CatalogService** — manages product information.
- **InventoryService** — manages stock and inventory reservations.
- **OrderService** — manages orders and coordinates the order Saga.
- **PaymentService** — simulates payment processing and records payment results.
- **Storefront** — provides the customer-facing web application.

Each backend service owns its own database. Services communicate using HTTP APIs and RabbitMQ events.

### Architecture Diagram

```text
                    Storefront
                  ASP.NET Core
                  localhost:5272
                        |
             +----------+----------+
             |          |           |
             v          v           v
         Catalog      Orders     Inventory
          HTTP         HTTP        HTTP
         :5089        :5202       :5214
             |          |           |
          SQLite     SQLite      SQLite
                        |
                        v
                     RabbitMQ
                 localhost:5672
                        |
              OrderPlaced event
                        |
                        v
                Inventory Consumer
                        |
                 Reserve inventory
                        |
                        v
                Inventory result event
                        |
                        v
                Order Saga Consumer
                        |
                        v
                  PaymentService
                  localhost:5120
                        |
                      SQLite
```

The OrderService consumes inventory result events to continue the Saga. The successful path completes the order after payment succeeds. The failure path cancels the order and releases reserved stock when payment fails.

## Services and Ports

| Service | HTTP URL | Database | Responsibility |
|---|---|---|---|
| CatalogService | `http://localhost:5089` | SQLite | Product management and CRUD |
| OrderService | `http://localhost:5202` | SQLite | Order management and Saga orchestration |
| InventoryService | `http://localhost:5214` | SQLite | Inventory management and stock reservation |
| PaymentService | `http://localhost:5120` | SQLite | Payment simulation and payment records |
| Storefront | `http://localhost:5272` | N/A | Customer-facing web interface |
| RabbitMQ Management | `http://localhost:15672` | N/A | Message broker monitoring |

Open Swagger UI by visiting `/swagger` on each backend service.

> Ports above are the HTTP ports used by the current development setup. If your launch profiles change, update this table to match them.

## Key Features

- **Contract-first API design:** OpenAPI specifications define API contracts.
- **Versioned APIs:** Versioned URI paths such as `/catalog/v1/products` and `/orders/v1/orders`.
- **Typed HTTP clients:** NSwag-generated clients and service adapters for API communication.
- **Resilience:** Configured HTTP retries and timeouts.
- **Correlation IDs:** Request tracing across service calls and events.
- **Event-driven communication:** RabbitMQ events connect OrderService and InventoryService.
- **Saga orchestration:** Tracks order processing and coordinates compensation on failure.
- **Compensation:** Cancels failed orders and releases reserved stock.
- **Idempotent event handling:** Processed-event tracking helps prevent duplicate event processing.
- **Dead-letter queues:** Failed messages can be routed for investigation.
- **Problem Details:** Standardized API error responses where implemented.
- **Database per service:** Each backend service owns its own SQLite database.
- **Customer checkout:** Storefront communicates with backend APIs to create orders.

## Technology Stack

- C# and ASP.NET Core
- .NET 9 for the current service projects
- Entity Framework Core
- SQLite
- RabbitMQ
- OpenAPI and Swagger UI
- NSwag-generated API clients
- ASP.NET Core Razor Pages
- Postman

## Prerequisites

Install the following before running the project:

- .NET 9 SDK
- Visual Studio 2022 or VS Code
- RabbitMQ with its management plugin enabled
- Git
- Postman (recommended for API testing)

The current projects target .NET 9, so install the matching SDK rather than relying only on the minimum framework version from an older draft.

## Installation and Setup

### 1. Clone the repository

```bash
git clone https://github.com/ErlNievera/OnlineStore.git
cd OnlineStore
```

### 2. Start RabbitMQ

Install RabbitMQ and make sure the service is running.

The default local development configuration uses:

- AMQP: `localhost:5672`
- Management UI: `http://localhost:15672`
- Development username: `guest`
- Development password: `guest`

The default `guest` account is intended for local development. Do not use these credentials for a public or production deployment.

Open the management UI to verify that RabbitMQ is available.

### 3. Restore and build the solution

From the repository root:

```bash
dotnet restore
dotnet build
```

If your solution file is named `OnlineStore.sln`, you can also run:

```bash
dotnet build OnlineStore.sln
```

### 4. Configure service connections

Check each service's `appsettings.json` and development settings.

Make sure that:

- Each service uses its own SQLite connection string.
- OrderService points to the correct CatalogService, InventoryService, and PaymentService URLs.
- Storefront points to the correct OrderService and CatalogService URLs.
- RabbitMQ connection settings match the running broker.

For the local HTTP configuration, use the ports listed in the Services and Ports table.

### 5. Apply database migrations

Each service maintains its own database and migrations.

From the repository root, run the appropriate command for each project. For example:

```bash
dotnet ef database update \
  --project src/CatalogService
```

```bash
dotnet ef database update \
  --project src/OrderService
```

```bash
dotnet ef database update \
  --project src/InventoryService
```

```bash
dotnet ef database update \
  --project src/PaymentService
```

If the Entity Framework CLI is not installed, install the `dotnet-ef` tool version compatible with the project's EF Core version.

Verify each project's actual location and connection string before running the commands. If migrations are already applied automatically during startup, avoid applying them unnecessarily.

**Important:** Do not delete SQLite database files as a routine troubleshooting step. Doing so can erase products, orders, inventory, payment records, and Saga state.

### 6. Run the applications

Start the applications using Visual Studio's configured launch profiles or separate terminals.

The HTTP launch commands follow this pattern:

```bash
dotnet run --project src/CatalogService
dotnet run --project src/InventoryService
dotnet run --project src/OrderService
dotnet run --project src/PaymentService
dotnet run --project src/Storefront
```

If your local folder names differ, use the actual `.csproj` paths from Solution Explorer.

Start RabbitMQ before testing event-driven flows. Keep all required applications running during end-to-end testing.

### 7. Open the applications

| Application | URL |
|---|---|
| CatalogService Swagger | `http://localhost:5089/swagger` |
| OrderService Swagger | `http://localhost:5202/swagger` |
| InventoryService Swagger | `http://localhost:5214/swagger` |
| PaymentService Swagger | `http://localhost:5120/swagger` |
| Storefront | `http://localhost:5272` |
| RabbitMQ Management | `http://localhost:15672` |

## Order Processing and Saga

The Saga coordinates the order, inventory reservation, and payment steps.

### Successful Flow

1. A customer submits an order through Storefront.
2. OrderService validates the product through CatalogService.
3. OrderService checks inventory availability.
4. OrderService saves the order and Saga state.
5. OrderService publishes an `OrderPlaced` event to RabbitMQ.
6. InventoryService consumes the event and attempts to reserve stock.
7. InventoryService publishes an inventory success event.
8. OrderService processes the inventory result and requests payment.
9. PaymentService records the simulated payment result.
10. If payment succeeds, OrderService marks the Saga and order as `Completed`.

### Failure and Compensation Flow

1. The customer submits an order.
2. InventoryService successfully reserves stock.
3. OrderService requests payment.
4. PaymentService reports a failed payment or payment processing cannot be completed.
5. OrderService initiates compensation by releasing reserved stock.
6. The order becomes `Cancelled`.
7. The Saga state becomes `Failed`.

The project has been tested with both a successful order and a simulated payment failure. In the failure test, the order was cancelled, the Saga state was `Failed`, and inventory quantities returned to their pre-test values.

> PaymentService currently simulates payment outcomes for educational testing. It is not a production payment gateway.

## API Testing

Use Swagger UI and Postman to test each service.

Recommended test cases:

- Create, retrieve, update, and delete products.
- Create and retrieve inventory records.
- Verify stock availability and reservation behavior.
- Create orders and retrieve orders by ID.
- Verify order quantity and total amount.
- Confirm successful payment processing.
- Simulate payment failure and verify compensation.
- Test invalid requests and missing resource IDs.
- Verify duplicate-event handling.
- Verify retry, timeout, and dead-letter behavior.

Check the actual endpoint paths and request schemas in each service's Swagger UI before sending requests.

## Error Handling and Resilience

The project includes typed HTTP clients, correlation ID propagation, and configured retry and timeout policies for synchronous service calls.

Test service failures to verify the expected behavior. In particular, payment operations require idempotency before retries can safely be used without the risk of duplicate payments.

RabbitMQ publisher confirms, consumer acknowledgements, idempotency, and dead-letter routing should be verified through integration tests. These mechanisms do not by themselves guarantee that database updates and message publishing happen atomically.

## Project Structure

```text
OnlineStore/
├── README.md
├── CONTRIBUTIONS.md
├── OnlineStore.sln
├── contracts/
│   ├── catalog.yaml
│   └── orders.yaml
├── src/
│   ├── CatalogService/
│   ├── OrderService/
│   ├── InventoryService/
│   ├── PaymentService/
│   ├── Storefront/
│   └── Shared/
│       ├── Events/
│       ├── DTOs/
│       └── Handlers/
└── postman/
```

The tree is a simplified overview. Update it if additional contract files, test projects, generated clients, or Postman collections are added.

## Known Limitations and Future Improvements

- Add an outbox pattern to improve reliability between database commits and event publishing.
- Add payment idempotency keyed by order ID.
- Add recovery for interrupted Saga steps and compensation failures.
- Verify dead-letter queue policies and provide a documented retry or replay procedure.
- Expand automated integration testing for service outages and duplicate messages.
- Complete and validate OpenAPI contracts for every service.
- Add production-grade authentication, secrets management, monitoring, and deployment configuration if required.

## Team Contributions

See [CONTRIBUTIONS.md](CONTRIBUTIONS.md) for the responsibilities and work assigned to each team member.

## License

This project is intended for educational purposes as part of a microservices course.
