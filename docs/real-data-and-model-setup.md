# Use your own products and a live model

The API now accepts organizations and products and lets the growth workflow use those exact database records. The catalogue source and model mode are independent: start with database records and the deterministic demo runner, then enable model-selected tool calls.

## Prepare the database

The configured `BusinessDatabase` connection must point to PostgreSQL. `appsettings.json` only holds a placeholder password; keep your real connection string in local user secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:BusinessDatabase" "Host=localhost;Port=5432;Database=aigrowthplatform;Username=postgres;Password=<your-password>" --project src/Api
```

Apply all current EF migrations:

```powershell
dotnet ef database update --project src/Modules/Business/Infrastructure --startup-project src/Api
```

`LinkProductsToOrganizations` adds an index and a foreign key. It stops if existing products refer to missing organizations; correct those records before retrying. It does not delete data automatically. Organization deletion is restricted while products reference it.

## Try the database catalogue without model charges

Stop the server with Ctrl+C, then restart:

```powershell
dotnet run --project src/Api --launch-profile http -- --Agents:Mode Demo --Agents:CatalogSource Database
```

Check `http://localhost:5162/api/agent/readiness`. It should report `ready: true`, `catalogSource: Database`, and `database: ready`. Pending migrations or an unreachable database produce HTTP 503. The check does not contact a model provider.

In another terminal:

```powershell
.\scripts\Test-CatalogApi.ps1
```

This creates a sample organization and product in PostgreSQL, runs the growth workflow, and verifies that its saved plan refers to the entered product. The sample business records remain in your database. The script requires the Demo runner and Database catalogue so it never incurs model charges.

## Enter business records

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/organizations` | Create using `name` and optional `websiteUrl`. |
| GET | `/api/organizations?skip=0&take=50` | List organizations. |
| GET | `/api/organizations/{id}` | Read an organization. |
| POST | `/api/organizations/{id}/products` | Create using `name`, optional `description`, and optional `price`. |
| GET | `/api/organizations/{id}/products?skip=0&take=50` | List that organization's products. |
| GET | `/api/organizations/{id}/products/{productId}` | Read a product scoped to its organization. |

Lists support `skip >= 0` and `take` from 1 to 100. Names are required and limited to 200 characters; descriptions to 2000. Website URLs must be absolute HTTP/HTTPS URLs. Prices must be non-negative, have at most two decimal places, and fit within 16 whole-number digits. Omit price when unknown. There is no currency field yet; keep amounts in a consistent currency for your catalogue. Product update and delete endpoints are not included.

When `Agents:AccessKey` is configured, include `X-Agent-Key` on both business and agent requests. Without it, `/api` routes accept loopback requests only in Development. The shared key grants operator access to all records; per-user organization authorization remains future work.

After entering products, send a growth request to `/api/agent/runs` using your organization's ID and `allowWrites=true` to save a draft. The run's `catalogSource` identifies where its data came from. `mode=Demo` still means predefined decisions, even when using real database records.

## Configure model-selected tool calls

Use a model ID available to your OpenAI API account that supports Responses function calling. No model ID or API credential is assumed.

```powershell
.\scripts\Set-LiveModel.ps1 -Model 'YOUR_AVAILABLE_MODEL_ID'
```

The helper prompts for a hidden API key and sends it to .NET user secrets through stdin. It stores only the API key and model outside the repository; it does not enable live mode or contact the provider. User secrets are a development configuration store, not an encrypted production vault. Keep credentials out of chat and version control.

Restart in live mode:

```powershell
dotnet run --project src/Api --launch-profile http -- --Agents:Mode OpenAI --Agents:CatalogSource Database
```

Use `--Agents:CatalogSource Demo` instead to try the model against sample records. These are real model calls and can incur provider charges. Start with `allowWrites=false` to review data without saving a business plan or changing a ticket. Audit events and verified outcome memory still persist.

`/api/agent/readiness` reports `configured_not_contacted` for a configured live model. Only an actual workflow run verifies credentials and model access. Provider errors terminate the run with an explicit failure rather than falling back to Demo.

The same settings can be supplied as environment variables: `Agents__Mode`, `Agents__CatalogSource`, `Agents__Model`, `Agents__ApiKey`, and `Agents__AccessKey`. Command-line settings take precedence; existing environment variables can override user secrets. Use a separate `Agents__DataDirectory` for live runs.

## Integration tests

Set `AI_GROWTH_TEST_POSTGRES` to a connection string for a test-server account permitted to create databases, then run:

```powershell
dotnet test --solution AiGrowthPlatform.slnx --configuration Release
```

The fixture creates a uniquely named `aigrowth_test_*` database, applies migrations, tests HTTP operations and the database-backed growth workflow, then drops only that temporary database. It does not add test rows to the configured application's database. Without this environment variable, PostgreSQL tests are explicitly skipped. CI supplies a PostgreSQL service and runs them automatically.
