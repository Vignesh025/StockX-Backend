<p align="center">
  <h1 align="center">📈 StockX Backend</h1>
  <p align="center">
    A production-ready RESTful API for a stock-trading simulation platform.<br/>
    Built with <strong>.NET 10</strong>, <strong>Entity Framework Core</strong>, <strong>PostgreSQL</strong>, and a Clean Architecture design.
  </p>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 10" />
  <img src="https://img.shields.io/badge/PostgreSQL-15+-4169E1?logo=postgresql&logoColor=white" alt="PostgreSQL" />
  <img src="https://img.shields.io/badge/Docker-Ready-2496ED?logo=docker&logoColor=white" alt="Docker" />
  <img src="https://img.shields.io/badge/Stripe-Payments-635bff?logo=stripe&logoColor=white" alt="Stripe" />
  <img src="https://img.shields.io/badge/Alpaca-Market_Data-FFCC00?logo=alpaca&logoColor=black" alt="Alpaca" />
</p>

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Configuration](#configuration)
  - [Running Locally](#running-locally)
  - [Running with Docker](#running-with-docker)
- [API Reference](#api-reference)
  - [Authentication](#authentication-endpoints)
  - [Stocks](#stock-endpoints)
  - [Trading](#trading-endpoints)
  - [Portfolio](#portfolio-endpoints)
  - [Wallet](#wallet-endpoints)
  - [Transactions](#transaction-endpoints)
  - [Payments](#payment-endpoints)
  - [Admin](#admin-endpoints)
- [Database Schema](#database-schema)
- [Testing](#testing)
- [Deployment](#deployment)
- [Environment Variables](#environment-variables)

---

## Overview

**StockX** is a full-stack stock-trading simulation platform that lets users:

- **Register & authenticate** with JWT-based security
- **Search** thousands of real US-listed equities (powered by Alpaca Markets)
- **Buy & sell** stocks using a virtual wallet
- **Track** portfolio performance with real-time P&L calculations
- **Deposit** funds via Stripe Checkout integration
- **View** complete transaction history with filtering

An **Admin** panel provides user management with paginated search and drill-down detail views.

---

## Architecture

The solution follows **Clean Architecture** principles, organized into four distinct layers:

```
┌──────────────────────────────────────────────────────────────┐
│                        StockX.API                            │
│        Controllers · Middlewares · Filters · Program.cs      │
├──────────────────────────────────────────────────────────────┤
│                      StockX.Services                         │
│   AuthService · TradingService · WalletService · StockService│
│   PaymentService · AdminService · TokenService               │
├──────────────────────────────────────────────────────────────┤
│                    StockX.Infrastructure                      │
│    EF Core DbContext · Repositories · UnitOfWork             │
│    Alpaca API Client · Stripe API Client · MemoryCache       │
├──────────────────────────────────────────────────────────────┤
│                        StockX.Core                           │
│      Entities · DTOs · Enums · Interfaces (Contracts)        │
└──────────────────────────────────────────────────────────────┘
```

| Layer | Responsibility |
|---|---|
| **StockX.Core** | Domain entities, DTOs, enums, and interface contracts. Zero external dependencies. |
| **StockX.Infrastructure** | Data access (EF Core + PostgreSQL), external API integrations (Alpaca, Stripe), caching, and the Unit of Work pattern. |
| **StockX.Services** | Business logic and orchestration. Implements service interfaces defined in Core. |
| **StockX.API** | ASP.NET Core web host — controllers, middleware pipeline, JWT authentication, CORS, and Swagger. |

**Dependency Rule:** Each layer depends only on the layers below it. Core has no outward dependencies.

---

## Tech Stack

| Category | Technology |
|---|---|
| **Runtime** | .NET 10 / ASP.NET Core 10 |
| **ORM** | Entity Framework Core 10 (Code-First) |
| **Database** | PostgreSQL (Npgsql provider) |
| **Authentication** | JWT Bearer Tokens (`Microsoft.AspNetCore.Authentication.JwtBearer`) |
| **Market Data** | [Alpaca Markets API](https://alpaca.markets/) — real-time quotes & asset metadata |
| **Payments** | [Stripe Checkout](https://stripe.com/docs/payments/checkout) — wallet deposits via webhook |
| **Caching** | In-memory cache (`IMemoryCache`) for stock quotes |
| **API Docs** | Swagger / Swashbuckle (Development only) |
| **Containerization** | Docker (multi-stage build) |
| **Testing** | xUnit, Moq, FluentAssertions, `WebApplicationFactory` (integration), EF Core InMemory |

---

## Project Structure

```
StockX-Backend/
├── StockX.API/                          # Web API host
│   ├── Controllers/
│   │   ├── AdminController.cs           # Admin user management  [Authorize(Roles="Admin")]
│   │   ├── AuthController.cs            # Register, Login, Me
│   │   ├── PaymentController.cs         # Stripe webhook handler
│   │   ├── PortfolioController.cs       # Portfolio summary       [Authorize]
│   │   ├── StockController.cs           # Search, Top, Details
│   │   ├── TradingController.cs         # Buy & Sell stocks       [Authorize]
│   │   ├── TransactionController.cs     # Transaction history     [Authorize]
│   │   └── WalletController.cs          # Balance & Deposit       [Authorize]
│   ├── Filters/
│   │   ├── AuthorizeFilter.cs
│   │   └── RoleAuthorizationFilter.cs
│   ├── Middlewares/
│   │   ├── ErrorHandlingMiddleware.cs
│   │   ├── ExceptionHandlingMiddleware.cs
│   │   └── JwtMiddleware.cs
│   ├── Program.cs                       # Application entry point
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── appsettings.Production.json
│   └── appsettings.Testing.json
│
├── StockX.Core/                         # Domain layer (no dependencies)
│   ├── DTOs/                            # Data transfer objects
│   │   ├── Admin/
│   │   ├── Auth/
│   │   ├── Common/
│   │   ├── Payment/
│   │   ├── Portfolio/
│   │   ├── Stock/
│   │   ├── Trading/
│   │   └── Wallet/
│   ├── Entities/
│   │   ├── PaymentIntent.cs
│   │   ├── Stock.cs
│   │   ├── Transaction.cs
│   │   ├── User.cs
│   │   └── UserStockHolding.cs
│   ├── Enums/
│   │   ├── PaymentIntentStatus.cs       # Pending · Completed · Failed
│   │   ├── TransactionStatus.cs         # Pending · Completed · Failed
│   │   ├── TransactionType.cs           # Deposit · StockBuy · StockSell
│   │   └── UserRole.cs                  # Admin · NormalUser
│   └── Interfaces/                      # Service & repository contracts
│       ├── Persistence/
│       ├── Repositories/
│       └── Services/
│
├── StockX.Infrastructure/               # Data & external integrations
│   ├── Caching/
│   │   ├── ICacheService.cs
│   │   └── MemoryCacheService.cs
│   ├── External/
│   │   ├── AlpacaApi/                   # Market data client
│   │   └── StripeApi/                   # Payment processing client
│   ├── Persistence/
│   │   ├── Context/
│   │   │   └── ApplicationDbContext.cs  # EF Core DbContext + Fluent API config
│   │   ├── Migrations/
│   │   ├── Repositories/               # Generic + specialized repositories
│   │   ├── Seeding/
│   │   │   └── StockSeeder.cs          # Auto-seeds stocks from Alpaca on startup
│   │   └── UnitOfWork/
│   └── DependencyInjection.cs
│
├── StockX.Services/                     # Business logic layer
│   ├── Admin/   → AdminService
│   ├── Auth/    → AuthService
│   ├── Payment/ → PaymentService
│   ├── Stock/   → StockService
│   ├── Trading/ → TradingService
│   ├── Wallet/  → WalletService
│   └── DependencyInjection.cs
│
├── StockX.Tests/                        # Test suite
│   ├── IntegrationTests/                # End-to-end API tests (WebApplicationFactory)
│   │   ├── CustomWebApplicationFactory.cs
│   │   ├── AdminControllerTests.cs
│   │   ├── AuthControllerTests.cs
│   │   ├── PaymentControllerTests.cs
│   │   ├── PortfolioControllerTests.cs
│   │   ├── StockControllerTests.cs
│   │   ├── TradingControllerTests.cs
│   │   └── WalletControllerTests.cs
│   └── UnitTests/
│       ├── Repositories/               # Repository unit tests (EF InMemory)
│       └── Services/                   # Service unit tests (Moq)
│
├── Dockerfile                           # Multi-stage production build
├── .dockerignore
├── .gitignore
└── StockX.slnx                          # Solution file
```

---

## Getting Started

### Prerequisites

| Tool | Version |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ |
| [PostgreSQL](https://www.postgresql.org/download/) | 15+ |
| [Docker](https://docs.docker.com/get-docker/) | *(optional)* |
| [Stripe CLI](https://stripe.com/docs/stripe-cli) | *(optional — for local webhook testing)* |

### Configuration

All application settings live in `StockX.API/appsettings.{Environment}.json`. For **local development**, update `appsettings.Development.json`:

```jsonc
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=StockX;Username=postgres;Password=<your-password>"
  },
  "Alpaca": {
    "ApiKey": "<your-alpaca-api-key>",
    "SecretKey": "<your-alpaca-secret-key>",
    "BaseUrl": "https://paper-api.alpaca.markets",
    "DataUrl": "https://data.alpaca.markets"
  },
  "Stripe": {
    "SecretKey": "sk_test_...",
    "PublishableKey": "pk_test_...",
    "SuccessUrl": "http://localhost:3000/wallet/deposit/success",
    "CancelUrl": "http://localhost:3000/wallet/deposit/cancel",
    "WebhookSecret": "whsec_..."
  },
  "Jwt": {
    "Secret": "<a-strong-secret-key>",
    "ExpirationHours": 24
  },
  "AllowedOrigins": [
    "http://localhost:3000"
  ]
}
```

> **⚠️ Never commit real API keys.** Use environment variables or a secrets manager in production.

### Running Locally

```bash
# 1. Clone the repository
git clone https://github.com/<your-username>/StockX-Backend.git
cd StockX-Backend

# 2. Restore dependencies
dotnet restore

# 3. Apply EF Core migrations
dotnet ef database update --project StockX.Infrastructure --startup-project StockX.API

# 4. Run the API
dotnet run --project StockX.API
```

The API will start at `https://localhost:5001` (or `http://localhost:5000`).  
Swagger UI is available at `https://localhost:5001/swagger` in Development mode.

> **Stock Seeding:** On first startup, the app automatically seeds ~10,000+ US equities from the Alpaca API in the background. This is non-blocking — the API is ready immediately.

### Running with Docker

```bash
# Build the image
docker build -t stockx-backend .

# Run with environment variables
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=host.docker.internal;Port=5432;Database=StockX;Username=postgres;Password=<pw>" \
  -e Jwt__Secret="<your-jwt-secret>" \
  -e Alpaca__ApiKey="<key>" \
  -e Alpaca__SecretKey="<secret>" \
  -e Stripe__SecretKey="sk_test_..." \
  -e Stripe__WebhookSecret="whsec_..." \
  stockx-backend
```

The container exposes port **8080** and runs in `Production` mode.

---

## API Reference

Base URL: `/api`

### Authentication Endpoints

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/auth/register` | ❌ | Create a new user account |
| `POST` | `/api/auth/login` | ❌ | Authenticate and receive a JWT |
| `GET` | `/api/auth/me` | 🔒 | Get current user profile + wallet balance |

<details>
<summary><strong>Request / Response Examples</strong></summary>

**Register**
```json
// POST /api/auth/register
{
  "name": "John Doe",
  "email": "john@example.com",
  "password": "SecurePass123!"
}
// → 200 { "userId": "...", "message": "Registration successful." }
```

**Login**
```json
// POST /api/auth/login
{
  "email": "john@example.com",
  "password": "SecurePass123!"
}
// → 200 { "token": "eyJhbG...", "user": { "id": "...", "name": "...", "email": "...", "role": "NormalUser" } }
```

</details>

---

### Stock Endpoints

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/api/stock/search?query=AAPL&limit=10` | ❌ | Search stocks by symbol or name |
| `GET` | `/api/stock/top?limit=15` | ❌ | Get top stocks by market value |
| `GET` | `/api/stock/{symbol}` | ❌ | Get stock details with current price |

---

### Trading Endpoints

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/trading/buy` | 🔒 | Buy shares of a stock |
| `POST` | `/api/trading/sell` | 🔒 | Sell shares of a stock |

<details>
<summary><strong>Request / Response Examples</strong></summary>

**Buy Stock**
```json
// POST /api/trading/buy
{
  "symbol": "AAPL",
  "quantity": 5
}
// → 200
{
  "success": true,
  "transaction": {
    "transactionId": "...",
    "type": "STOCK_BUY",
    "amount": 875.50,
    "stockSymbol": "AAPL",
    "quantity": 5,
    "pricePerShare": 175.10,
    "timestamp": "2026-04-28T12:00:00Z",
    "status": "Completed"
  },
  "newBalance": 4124.50,
  "message": "Successfully purchased 5 shares of AAPL."
}
```

</details>

---

### Portfolio Endpoints

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/api/portfolio` | 🔒 | Get portfolio summary with holdings and P&L |

<details>
<summary><strong>Response Example</strong></summary>

```json
{
  "holdings": [
    {
      "symbol": "AAPL",
      "name": "Apple Inc.",
      "quantity": 10,
      "averageCostBasis": 170.00,
      "currentPrice": 175.10,
      "currentValue": 1751.00,
      "profitLoss": 51.00,
      "profitLossPercent": 3.0
    }
  ],
  "totalValue": 1751.00,
  "totalCost": 1700.00,
  "totalProfitLoss": 51.00
}
```

</details>

---

### Wallet Endpoints

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/api/wallet/balance` | 🔒 | Get current wallet balance |
| `POST` | `/api/wallet/deposit/initiate` | 🔒 | Initiate a Stripe Checkout deposit |

---

### Transaction Endpoints

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/api/transactions?type=all&limit=50&offset=0` | 🔒 | Get transaction history with type filter |

Supported `type` values: `all`, `deposit`, `trade`, `stock_buy`, `stock_sell`

---

### Payment Endpoints

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/payment/webhook/stripe` | ❌* | Stripe webhook receiver |

> \* Secured by Stripe HMAC-SHA256 signature verification, not JWT.

---

### Admin Endpoints

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/api/admin/users?page=1&limit=20&search=` | 👑 Admin | List all users (paginated) |
| `GET` | `/api/admin/users/{userId}` | 👑 Admin | Get detailed user profile |

---

## Database Schema

The application uses **PostgreSQL** with EF Core Code-First migrations. Below is the entity relationship model:

```
┌──────────────────────┐       ┌──────────────────────────────┐
│        Users         │       │           Stocks             │
├──────────────────────┤       ├──────────────────────────────┤
│ UserId (PK, GUID)    │       │ Symbol (PK, VARCHAR)         │
│ Name                 │       │ Name                         │
│ Email (UNIQUE)       │       │ Exchange                     │
│ PasswordHash         │       │ AssetType                    │
│ Role (enum)          │       │ LastMetadataUpdate            │
│ IsActive             │       └────────┬─────────────────────┘
│ CreatedAt            │                │
│ UpdatedAt            │                │
└──┬───────┬───────────┘                │
   │       │                            │
   │       │    ┌───────────────────────────────────────┐
   │       │    │       UserStockHoldings               │
   │       │    ├───────────────────────────────────────┤
   │       └────│ UserId (FK → Users)                   │
   │            │ StockSymbol (FK → Stocks)             │
   │            │ TotalQuantity                         │
   │            │ AverageCostBasis                      │
   │            │ LastUpdated                           │
   │            └───────────────────────────────────────┘
   │
   │       ┌───────────────────────────────────────┐
   │       │          Transactions                  │
   │       ├───────────────────────────────────────┤
   ├───────│ TransactionId (PK, GUID)              │
   │       │ UserId (FK → Users)                   │
   │       │ Type (Deposit/StockBuy/StockSell)     │
   │       │ Amount                                │
   │       │ StockSymbol (FK → Stocks, nullable)   │
   │       │ Quantity                              │
   │       │ PricePerShare                         │
   │       │ Status (Pending/Completed/Failed)     │
   │       │ Timestamp                             │
   │       │ Notes                                 │
   │       └───────────────────────────────────────┘
   │
   │       ┌───────────────────────────────────────┐
   │       │         PaymentIntents                 │
   │       ├───────────────────────────────────────┤
   └───────│ IntentId (PK, VARCHAR)                │
           │ UserId (FK → Users)                   │
           │ Amount                                │
           │ Currency (default: "USD")             │
           │ Status (Pending/Completed/Failed)     │
           │ TransactionId (FK → Transactions)     │
           │ CreatedAt                             │
           │ CompletedAt                           │
           └───────────────────────────────────────┘
```

---

## Testing

The project includes both **unit tests** and **integration tests** for comprehensive coverage.

### Test Categories

| Category | Framework | Scope | Count |
|---|---|---|---|
| **Unit Tests — Services** | xUnit + Moq | Business logic (AuthService, TradingService, WalletService, StockService, PaymentService, AdminService) | 6 test classes |
| **Unit Tests — Repositories** | xUnit + EF InMemory | Data access (Repository, UserRepository, StockRepository, HoldingRepository, TransactionRepository) | 5 test classes |
| **Integration Tests** | xUnit + WebApplicationFactory | End-to-end API (Auth, Stock, Trading, Portfolio, Wallet, Transaction, Payment, Admin) | 8 test classes |

### Running Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity normal

# Run with code coverage (requires coverlet)
dotnet test --collect:"XPlat Code Coverage"
```

A coverage report script is available in the root:

```powershell
# Generate HTML coverage report
.\coverage.ps1
```

---

## Deployment

The backend is deployment-ready for container platforms like **Render**, **Railway**, or **AWS ECS**.

### Docker Production Build

The provided `Dockerfile` uses a **multi-stage build**:

1. **Build stage** — Restores, compiles, and publishes the app using the .NET SDK
2. **Runtime stage** — Runs the published output on the lightweight ASP.NET runtime image

```dockerfile
# Key settings in the container:
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
```

### Production Checklist

- [ ] Set `ConnectionStrings__DefaultConnection` to your production PostgreSQL instance
- [ ] Set `Jwt__Secret` to a strong, unique secret (min 32 chars)
- [ ] Set `ALLOWED_ORIGINS` or `AllowedOrigins` to your frontend domain(s)
- [ ] Configure `Stripe__SecretKey` and `Stripe__WebhookSecret` with live keys
- [ ] Configure `Alpaca__ApiKey` and `Alpaca__SecretKey`
- [ ] Ensure `ForwardedHeaders:Enabled` is `true` when behind a reverse proxy
- [ ] Ensure `HttpsRedirection:Enabled` is `false` when TLS is terminated at the proxy

---

## Environment Variables

All config values in `appsettings.json` can be overridden via environment variables using the `__` (double underscore) separator.

| Variable | Required | Description |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | ✅ | PostgreSQL connection string |
| `Jwt__Secret` | ✅ | HMAC key for signing JWTs |
| `Jwt__ExpirationHours` | ❌ | Token lifetime (default: `24`) |
| `Alpaca__ApiKey` | ✅ | Alpaca Markets API key |
| `Alpaca__SecretKey` | ✅ | Alpaca Markets secret key |
| `Alpaca__BaseUrl` | ❌ | Alpaca base URL (default: paper trading) |
| `Alpaca__DataUrl` | ❌ | Alpaca data URL |
| `Stripe__SecretKey` | ✅ | Stripe secret key |
| `Stripe__PublishableKey` | ❌ | Stripe publishable key (used by frontend) |
| `Stripe__WebhookSecret` | ✅ | Stripe webhook signing secret |
| `Stripe__SuccessUrl` | ❌ | Redirect URL after successful payment |
| `Stripe__CancelUrl` | ❌ | Redirect URL after cancelled payment |
| `ALLOWED_ORIGINS` | ❌ | Comma-separated frontend origins for CORS |
| `ForwardedHeaders__Enabled` | ❌ | Enable X-Forwarded headers (default: `true`) |
| `HttpsRedirection__Enabled` | ❌ | Enable HTTPS redirect (default: `false` in prod) |

---

<p align="center">
  Built with ❤️ using .NET 10 & Clean Architecture
</p>
