# KamCo Store API

A clean, production-style **REST API for an e-commerce store**, built with **ASP.NET Core 10 (C#)**, **Entity Framework Core** and **SQLite**. It ships with JWT authentication, role-based authorization (Admin / User), Swagger docs and seeded demo data — clone, run, done.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4) ![Database](https://img.shields.io/badge/DB-SQLite%20%2B%20EF%20Core-003B57) ![Auth](https://img.shields.io/badge/Auth-JWT%20%2B%20Roles-3DDC84) ![Tests](https://img.shields.io/badge/smoke%20tests-24%2F24%20pass-brightgreen)

## Features

- **Categories** — public reads, Admin-only writes (delete is blocked while products exist)
- **Products** — public listing with 🔍 search, category filter, sorting (`price_asc`, `price_desc`, `name`) and paging; Admin-only CRUD
- **Orders** — place orders with server-computed totals, automatic stock reservation, per-user order history, Admin order management with a lifecycle (`Pending → Paid → Shipped / Cancelled`)
- **Auth** — register / login with **JWT**, passwords hashed with **PBKDF2-SHA256 (100k iterations)** + per-user salt, `Admin` / `User` roles
- **Swagger UI** — interactive docs with a *Bearer token* box for testing protected endpoints
- **Clean error handling** — domain exceptions mapped to consistent `application/problem+json` responses
- **Seed data** — 3 categories, 6 products and 2 demo accounts created automatically on first run

## Quick start

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) only — SQLite is bundled, no database server needed.

```bash
git clone https://github.com/companysibex-png/KamCo.git
cd KamCo/KamCo.Api
dotnet run
```

Then open:

| URL | Description |
|---|---|
| `http://localhost:5xxx/swagger` | Interactive API docs |
| `http://localhost:5xxx/api/products` | Sample endpoint |

> The exact port is printed in the console when the app starts (see `Properties/launchSettings.json`).

### Demo accounts (seeded automatically)

| Role | Email | Password |
|---|---|---|
| Admin | `admin@kamco.com` | `Admin@123` |
| Customer | `sara@kamco.com` | `User@123` |

Anyone can also self-register via `POST /api/auth/register` (always gets the `User` role).

## API overview

| Method | Endpoint | Access | Description |
|---|---|---|---|
| POST | `/api/auth/register` | Public | Create account, returns JWT |
| POST | `/api/auth/login` | Public | Log in, returns JWT + role |
| GET | `/api/categories` | Public | All categories + product counts |
| POST/PUT/DELETE | `/api/categories…` | **Admin** | Manage categories |
| GET | `/api/products` | Public | Search / filter / sort / paging |
| GET | `/api/products/{id}` | Public | Single product |
| POST/PUT/DELETE | `/api/products…` | **Admin** | Manage products |
| POST | `/api/orders` | Logged-in | Place order (prices & stock validated server-side) |
| GET | `/api/orders/my` | Logged-in | Current user's orders |
| GET | `/api/orders/{id}` | Owner or Admin | Single order |
| GET | `/api/orders` | **Admin** | All orders |
| PATCH | `/api/orders/{id}/status` | **Admin** | Update order status |

Example — search cheap electronics:

```bash
curl "http://localhost:5xxx/api/products?search=charger&sortBy=price_asc&page=1&pageSize=5"
```

Example — place an order (with a token from login):

```bash
curl -X POST http://localhost:5xxx/api/orders \
  -H "Authorization: Bearer <YOUR_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{ "items": [ { "productId": 1, "quantity": 2 }, { "productId": 4, "quantity": 1 } ] }'
```

## Project structure

```
KamCo/
└── KamCo.Api/
    ├── Common/          # Domain exceptions + ProblemDetails handler
    ├── Controllers/     # Auth, Categories, Products, Orders
    ├── Data/            # EF Core DbContext + idempotent seeder
    ├── DTOs/            # Request/response contracts per feature
    ├── Models/          # User, Category, Product, Order, OrderItem
    ├── Services/        # Business logic (auth, token, catalog, orders)
    ├── Program.cs       # DI, JWT, Swagger, pipeline, seeding
    └── appsettings.json # Connection string + JWT config
```

**Layering:** Controller → Service → EF Core. Controllers stay thin; validation and business rules live in services; DTOs keep domain models out of the API surface.

## Configuration

`appsettings.json`:

```json
{
  "ConnectionStrings": { "Default": "Data Source=kamco.db" },
  "Jwt": {
    "Issuer": "KamCo.Api",
    "Audience": "KamCo.Client",
    "ExpiryMinutes": 60,
    "Key": "CHANGE-ME__this_is_a_dev_only_signing_key_replace_with_32+_chars_secret"
  }
}
```

> ⚠️ **Before deploying:** replace `Jwt:Key` with a strong secret (e.g. `openssl rand -base64 64`) and provide it via environment variables or a secrets manager — never commit production secrets.

## Notable design decisions

- **Order totals are never trusted from the client** — the server reads current prices and computes the total.
- **Stock is reserved atomically on order creation** and rejected with a clear 400 error when insufficient.
- **Deleting a product that appears in past orders deactivates it** instead of deleting, so order history stays intact (ON DELETE RESTRICT at the DB level too).
- **Deleting a non-empty category is rejected** with a 400 and a helpful message.
- **PBKDF2 + constant-time comparison** for password verification (OWASP-recommended approach).

## Testing

A full smoke-test suite (24 checks: catalog, auth, role enforcement, order rules, delete flows) is included:

```bash
# start the API, then:
bash ../scripts/smoke-test.sh   # prints PASS/FAIL per endpoint
```

## Roadmap ideas

- [ ] Refresh tokens & logout
- [ ] Pagination links in response headers
- [ ] Unit tests with xUnit + EF Core InMemory
- [ ] Dockerfile + docker-compose
- [ ] Product images upload

## License

MIT — see [LICENSE](LICENSE).
