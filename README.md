# dmp.job.trxworker

Background worker of the DMP marketplace that turns confirmed crypto payments into bookkeeping entries.
It polls the `TransactionWorkerTask` table in the main DMP database (tasks are created by the web API and the
invoice worker), reads invoice state from Bitcart and, for each task, writes transactions, general ledger records
and account balance changes to the billing database. It then creates sales, queues seller emails in the `Mail`
table and returns stock for expired orders. Every step saves the task status, so a failed task continues from
the last completed step on the next poll.

## Tech stack

- .NET 10 (`net10.0`), C# with nullable reference types
- Generic Host (`Microsoft.Extensions.Hosting` 10.0) with a `BackgroundService`
- Entity Framework Core 10 + Npgsql (`Npgsql.EntityFrameworkCore.PostgreSQL` 10.0)
- `IHttpClientFactory` for the Bitcart API
- `Ulid` for transaction ids
- Central Package Management (`Directory.Packages.props`), `.slnx` solution

## Project structure

```
dmp.job.trxworker.slnx
├── Job.TrxWorker/          Host: Program.cs (DI, configuration) and TransactionWorkerService (10-second polling loop)
├── DMP.BL/                 Business logic
│   ├── Services/           TransactionService (payment/payout/incoming transfer/bonus processing), SettingsService
│   └── Models/             Bitcart DTOs, options classes, email models, well-known system account ids
├── DMP.DataAccess/         EF Core contexts and entities
│   ├── DmpDbContext.cs     Main DMP database (tasks, orders, sales, products, users, mails, payouts, ...)
│   └── BillingDbContext.cs Billing database (Transaction, GeneralLedger, Account)
└── DMP.Crosscutting/       Constants and small helpers (decimal truncation, wallet address masking)
```

### Task types

| Type | Steps |
|------|-------|
| `Payment` | Check the Bitcart invoice. If it expired, revert the order (return stock and product lines, queue product cache refresh). Otherwise: income transaction, move funds to sellers minus a 5% fee, create sales, queue "SellerNewSale" emails. |
| `Payout` | Check the seller balance, create a payout transaction (amount + network fee), queue a "SellerPayout" email, mark the payout complete. |
| `IncomingTransfer` | Record funds received through a Bitcart invoice on the system account. |
| `Bonus` | Move funds from the system account to a user (`FundsTransfer`). |

A task that fails more than 10 times is set to the `Error` status and no longer picked up.

## Configuration

Settings come from `appsettings.json`, `appsettings.{DOTNET_ENVIRONMENT}.json` and environment variables
(`Section__Key`). See [.env.example](.env.example).

| Setting / env var | Description |
|-------------------|-------------|
| `DOTNET_ENVIRONMENT` | `Production`, `Development` (docker dev) or `Local` (running from the IDE) |
| `ConnectionStrings__DmpConnection` | PostgreSQL connection string of the main DMP database |
| `ConnectionStrings__BillingDbConnection` | PostgreSQL connection string of the billing database |
| `BitcartOptions__ApiUrl` | Bitcart invoices endpoint, e.g. `http://bitcart-backend:8000/invoices` (no trailing slash) |
| `Minio__S3PublicEndpoint` | Public MinIO/S3 URL, used for the logo in emails |
| `DmpHosts__Client` | Client site URL |
| `DmpHosts__Seller` | Seller site URL, used for links in emails |
| `Logging__LogLevel__Default` | Log level (default `Information`) |

`appsettings.Development.json` and `appsettings.Local.json` contain non-secret defaults with `change-me` passwords.
Provide real credentials through environment variables.

## Getting started

### Prerequisites

- .NET SDK 10.0
- PostgreSQL with the DMP and billing databases (this service has no migrations; the schemas are managed elsewhere)
- A reachable Bitcart instance

### Run locally

```bash
export ConnectionStrings__DmpConnection="Host=localhost;Port=5442;Database=dmarketplace;Username=admin;Password=..."
export ConnectionStrings__BillingDbConnection="Host=localhost;Port=5452;Database=billing;Username=admin;Password=..."
DOTNET_ENVIRONMENT=Local dotnet run --project Job.TrxWorker
```

### Run with Docker

```bash
docker build -t dmp-job-trxworker .
docker run --env-file .env dmp-job-trxworker
```

In the full stack the service is started by the compose files in `dmp.docker` (`job.trxworker-dmp`).
It exposes no ports.

## Commands

| Command | Purpose |
|---------|---------|
| `dotnet build -c Release` | Build the solution |
| `dotnet format` | Apply formatting and code style from `.editorconfig` |
| `dotnet format --verify-no-changes` | Check formatting (CI-friendly) |

## Related repositories

- [dmp](https://github.com/denis-susha/dmp): umbrella repository with an overview of the whole system
- [dmp.api.web](https://github.com/denis-susha/dmp.api.web): web API; its admin endpoints create payout, incoming transfer and bonus tasks
- [dmp.job.invoiceworker](https://github.com/denis-susha/dmp.job.invoiceworker): tracks Bitcart invoices and creates payment tasks for this worker
- [dmp.job.server](https://github.com/denis-susha/dmp.job.server): sends the emails queued in `Mail` and processes the `JobProductCacheTask` rows queued by this worker
- [dmp.docker](https://github.com/denis-susha/dmp.docker): Docker Compose setup, including PostgreSQL and Bitcart
