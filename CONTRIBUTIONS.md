# Team Contributions

This document maps each team member to the components and responsibilities they own.

## Member 1: ErlNievera — Backend / Integration Lead

Responsible for the complete backend implementation and integration between services.

**Components:**
- CatalogService
- OrderService
- InventoryService
- PaymentService

**Responsibilities:**
- REST APIs with full CRUD
- RabbitMQ publisher/consumer integration
- OrderPlaced event contract
- Saga orchestration
- Correlation ID propagation
- Idempotency implementation
- Retry and timeout handling
- Compensation logic
- Dead-letter queue configuration
- Backend database integration (EF Core, migrations)
- Service-to-service integration
- Final backend integration testing

## Member 2: Klarence Villar — Storefront / UI Developer

Responsible for the user-facing web application.

**Components:**
- Storefront Web Application

**Responsibilities:**
- ASP.NET Core MVC / Razor Pages setup
- Product catalog UI
- Product details page
- Inventory display
- Order creation UI
- Order status/result pages
- Backend API client integration
- Form validation
- UI error handling
- Navigation and basic styling

## Member 3: Stefan Pimintel — Testing / QA / Documentation

Responsible for testing, quality assurance, and project documentation.

**Components:**
- Postman Collections
- Test Documentation
- README & Architecture

**Responsibilities:**
- Postman collection creation
- Happy-path API testing
- 400/404 negative testing
- Payment failure testing
- Compensation testing
- Inventory failure testing
- RabbitMQ duplicate-event testing
- Idempotency testing
- Retry/transient-failure testing
- Dead-letter queue testing
- Final integration testing
- README documentation
- Architecture diagram
- Test documentation

## Commit History

(To be updated as work progresses)
