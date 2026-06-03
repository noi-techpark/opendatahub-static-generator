# OpenDataHub Static Generator

A .NET 9 microservice pair that periodically fetches data from remote APIs and exposes the results as static JSON files over HTTP.

The idea is simple: instead of proxying live API calls to clients, you pre-fetch and cache the responses as flat files on a schedule. Clients always read fast, stable JSON from the static API — no matter what the upstream source looks like.

---

## Projects

### `opendatahub-static-api`

ASP.NET Core Minimal API that serves JSON files from the shared `data/` folder.

| Endpoint | Description |
|---|---|
| `GET /api?file=<name>` | Returns the content of `data/<name>.json` |
| `GET /api/list` | Lists all available JSON file names |

The `file` parameter accepts a name with or without the `.json` extension. Path traversal is blocked — only bare file names are accepted.

### `opendatahub-static-generator-worker`

.NET Worker Service that runs configured sync rules on a cron schedule using [Quartz.NET](https://www.quartz-scheduler.net/). Each rule fetches data from a remote API (handling pagination automatically) and writes the merged result as a JSON file into `data/`.

---

## How it fits together

```
┌─────────────────────────────────────┐
│   opendatahub-static-generator-worker│
│                                     │
│  every rule runs on its own cron    │
│  ──► fetches pages from remote API  │
│  ──► merges items into one array    │
│  ──► writes  data/<name>.json       │
└──────────────┬──────────────────────┘
               │ shared data/ folder
┌──────────────▼──────────────────────┐
│        opendatahub-static-api        │
│                                     │
│  GET /api?file=name                 │
│  ──► serves data/name.json          │
└─────────────────────────────────────┘
```

Both services point to the same `data/` directory at the repository root.

---

## Getting started

### Docker (recommended)

```bash
docker compose up --build
```

The API is available at `http://localhost:8080`. Both containers share a named Docker volume (`static-data`) mounted at `/data` inside each container — the worker writes there, the API reads from there.

To override secrets or cron settings without editing `appsettings.json`, use a `docker-compose.override.yml`:

```yaml
services:
  generator-worker:
    environment:
      Sync__Rules__0__Auth__Type: Basic
      Sync__Rules__0__Auth__Username: myuser
      Sync__Rules__0__Auth__Password: mypassword
```

### Local (without Docker)

#### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9)

#### Run the API

```bash
cd opendatahub-static-api
dotnet run
# Listening on http://localhost:5000
```

#### Run the worker

```bash
cd opendatahub-static-generator-worker
dotnet run
```

The worker fires each rule according to its cron expression. You can also trigger a rule immediately by adjusting the cron to run once at startup (e.g. set it to a time in the past — Quartz will fire it with misfire handling).

---

## Configuration

All sync rules live in `opendatahub-static-generator-worker/appsettings.json` under the `Sync` key.

```jsonc
{
  "Sync": {
    "OutputPath": "../data",   // path to the shared data folder
    "Rules": [ ... ]
  }
}
```

### Sync rule structure

```jsonc
{
  "Name": "my-rule",               // unique identifier, used in logs
  "OutputFileName": "my-data",     // written as data/my-data.json
  "BaseUrl": "https://api.example.com/v1/items",
  "QueryStrings": {                // static query parameters sent on every request
    "language": "en",
    "active": "true"
  },
  "Auth": { ... },                 // see Auth section
  "Paging": { ... },               // see Paging section
  "CronExpression": "0 0 2 * * ?" // Quartz 6-field cron (seconds-first)
}
```

### Authentication

Set `Auth.Type` to one of the three values below.

#### None

```jsonc
"Auth": { "Type": "None" }
```

#### Basic (username + password)

```jsonc
"Auth": {
  "Type": "Basic",
  "Username": "myuser",
  "Password": "mypassword"
}
```

#### Client Credentials / Service Account (OAuth2)

```jsonc
"Auth": {
  "Type": "ClientCredentials",
  "ClientId": "my-client-id",
  "ClientSecret": "my-client-secret",
  "TokenUrl": "https://auth.example.com/oauth/token",
  "Scope": "read"               // optional
}
```

Tokens are cached in memory and refreshed automatically 30 seconds before expiry.

### Paging

```jsonc
"Paging": {
  "Enabled": true,
  "PageQueryParam": "pagenumber",     // query param for the page index
  "PageSizeQueryParam": "pagesize",   // omit to not send a page-size param
  "PageSize": 100,                    // value sent for the page-size param
  "StartPage": 1,                     // first page index (use 0 for zero-based APIs)
  "DataPath": "Items",                // dot-path to the array inside the response
  "TotalPagesPath": "TotalPages",     // dot-path to the total page count  ← preferred
  "TotalCountPath": "TotalCount",     // alternative: dot-path to the total item count
  "HasMorePath": "meta.hasNextPage"   // alternative: dot-path to a boolean flag
}
```

The worker stops paging when one of these conditions is met (checked in order):

1. **`TotalPagesPath`** — stops when `currentPage >= TotalPages`. Use this when the API returns a page count (e.g. OpenDataHub).
2. **`TotalCountPath`** — stops once the accumulated item count reaches the total item count.
3. **`HasMorePath`** — stops when the flag is `false`.
4. **Implicit** — stops when a page returns fewer items than `PageSize`.

**`DataPath`** uses dot-notation to reach the array inside a wrapper object (e.g. `"data.results"`). Leave it empty when the response root is already a JSON array.

When `Paging.Enabled` is `false`, the raw response (or the node at `DataPath`) is written as-is.

### Cron expressions

Quartz uses a **6-field** format with seconds as the first field:

```
┌─────────── second  (0–59)
│ ┌───────── minute  (0–59)
│ │ ┌─────── hour    (0–23)
│ │ │ ┌───── day of month (1–31)
│ │ │ │ ┌─── month  (1–12)
│ │ │ │ │ ┌─ day of week (1–7 or SUN–SAT), ? to leave unspecified
│ │ │ │ │ │
0 0 2 * * ?   →  every day at 02:00
0 0 * * * ?   →  every hour
0 0/30 * * * ? → every 30 minutes
0 0 8 ? * MON →  every Monday at 08:00
```

---

## Full example — appsettings.json

```jsonc
{
  "Sync": {
    "OutputPath": "../data",
    "Rules": [
      {
        "Name": "accommodations",
        "OutputFileName": "accommodations",
        "BaseUrl": "https://api.example.com/v1/accommodations",
        "QueryStrings": { "language": "en" },
        "Auth": {
          "Type": "Basic",
          "Username": "user",
          "Password": "pass"
        },
        "Paging": {
          "Enabled": true,
          "PageQueryParam": "pagenumber",
          "PageSizeQueryParam": "pagesize",
          "PageSize": 100,
          "StartPage": 1,
          "DataPath": "Items",
          "TotalCountPath": "TotalCount"
        },
        "CronExpression": "0 0 2 * * ?"
      },
      {
        "Name": "events",
        "OutputFileName": "events",
        "BaseUrl": "https://api.example.com/v1/events",
        "QueryStrings": {},
        "Auth": {
          "Type": "ClientCredentials",
          "ClientId": "client-id",
          "ClientSecret": "client-secret",
          "TokenUrl": "https://auth.example.com/token"
        },
        "Paging": {
          "Enabled": true,
          "PageQueryParam": "page",
          "PageSizeQueryParam": "limit",
          "PageSize": 50,
          "StartPage": 0,
          "DataPath": "data",
          "HasMorePath": "meta.hasNextPage"
        },
        "CronExpression": "0 30 * * * ?"
      }
    ]
  }
}
```

After the worker runs, the API exposes the results:

```
GET http://localhost:5000/api?file=accommodations
GET http://localhost:5000/api?file=events
GET http://localhost:5000/api/list
```

---

## Secrets management

Do not commit credentials to source control. Use one of the standard .NET options:

- **User Secrets** (development): `dotnet user-secrets set "Sync:Rules:0:Auth:Password" "..."` 
- **Environment variables**: `Sync__Rules__0__Auth__Password=...`
- **A secrets manager** (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault) wired through the configuration pipeline.

---

## Project structure

```
opendatahub-static-generator/
├── opendatahub-static-generator.sln
├── data/                                         ← shared JSON output folder
├── opendatahub-static-api/
│   ├── opendatahub-static-api.csproj
│   ├── Program.cs
│   └── appsettings.json
└── opendatahub-static-generator-worker/
    ├── opendatahub-static-generator-worker.csproj
    ├── Program.cs
    ├── appsettings.json
    ├── Models/
    │   ├── AuthConfig.cs
    │   ├── PagingConfig.cs
    │   ├── SyncConfig.cs
    │   └── SyncRule.cs
    ├── Services/
    │   ├── ApiClientService.cs
    │   ├── SyncService.cs
    │   └── TokenCacheService.cs
    └── Jobs/
        └── SyncJob.cs
```
