
# AI Growth Platform

A .NET 10 backend with two agent workflows: drafting business growth plans and reviewing support incidents. Both use one execution runner, validated tools, scoped memory, and a persistent audit trail.

## Current capabilities

- **Business catalogue:** create/list/read organizations and products, with input validation, pagination, and a database-enforced organization link. See [real data and model setup](docs/real-data-and-model-setup.md).
- **Growth:** read an organization and up to 100 products, consult built-in guidance, and optionally save a draft growth plan.
- **Support:** read an internal incident, consult an approved procedure, optionally mark a verified recovery resolved or escalate it, then re-read to verify the update.
- **Shared runner:** one tool decision per step, a configurable step limit and deadline, read-only runs by default, terminal failure states, and run/event retrieval.
- **Persistence:** organizations/products use the existing PostgreSQL Business module. Agent runs, internal tickets, plans, and selected memory use local JSON files for this first single-process implementation.
- **Model modes:** an explicitly labelled deterministic demo and an OpenAI Responses adapter for actual model-selected tool calls. Demo mode does not use an LLM or interpret arbitrary tasks; it executes the selected workflow against sample records.

## Run the local demo

Requires the .NET 10 SDK. No database or model credentials are needed for the agent demo endpoints.

```powershell
dotnet restore AiGrowthPlatform.slnx
dotnet build AiGrowthPlatform.slnx --no-restore
dotnet run --project src/Api --launch-profile http
```

The API runs at `http://localhost:5162`. OpenAPI is available at `/openapi/v1.json` in Development. `/health` reports process liveness, not database/provider readiness.

Demo organization: `11111111-1111-1111-1111-111111111111`.

Demo tickets: `INC-1042` has evidence of a recovered service; `INC-1043` requires escalation. Sample tickets are created once, and their updated states persist across restarts. The sample catalogue is separate from the existing PostgreSQL organization endpoint.

### Growth run

```powershell
$growth = @{
    workflow = 'growth'
    organizationId = '11111111-1111-1111-1111-111111111111'
    task = 'Review the product catalogue and save a draft growth plan.'
    allowWrites = $true
} | ConvertTo-Json
$run = Invoke-RestMethod 'http://localhost:5162/api/agent/runs' -Method Post -ContentType 'application/json' -Body $growth
$run | Select-Object id, mode, status, outcome, recommendation
Invoke-RestMethod "http://localhost:5162/api/agent/runs/$($run.id)/plan"
Invoke-RestMethod "http://localhost:5162/api/agent/runs/$($run.id)/events"
```

### Support run

```powershell
$support = @{
    workflow = 'support'
    organizationId = '11111111-1111-1111-1111-111111111111'
    ticketId = 'INC-1042'
    task = 'Resolve this incident if the approved procedure applies; otherwise escalate it.'
    allowWrites = $true
} | ConvertTo-Json
Invoke-RestMethod 'http://localhost:5162/api/agent/runs' -Method Post -ContentType 'application/json' -Body $support
```

Use `INC-1043` for escalation. Set `allowWrites` to `false` for a review without business-record changes. Read-only runs still write their audit trail and verified completion memory. Repeat a completed workflow to see `loadedMemory` from the previous run. A nonexistent ticket demonstrates a safely rejected run. HTTP 201 means a run record was created; inspect its `status` to determine whether the workflow completed.

## Live model and real business data

Set these environment variables in the process launching the API, or use an external secret store. Do not commit credentials.

| Setting | Purpose |
|---|---|
| `Agents__Mode=OpenAI` | Enable model-selected tool calls. Demo uses predefined workflow decisions. |
| `Agents__CatalogSource` | Auto (default), Demo, or Database. Auto selects Demo with the demo runner and Database with OpenAI. Explicit selection allows testing real records with the demo runner. |
| `Agents__Model` | A model available to your account supporting Responses function calling. No model ID is assumed. |
| `Agents__ApiKey` | OpenAI API credential. |
| `ConnectionStrings__BusinessDatabase` | PostgreSQL connection string for the existing Business schema. |
| `Agents__AccessKey` | Shared key sent as `X-Agent-Key`; required outside Development, at least 32 characters. |
| `Agents__DataDirectory` | Writable storage directory; defaults to `src/Api/App_Data/agents` when launched as above. Use a separate directory for live data. |
| `Agents__MaxSteps` | Maximum model decisions per run; default 12, range 1–30. |
| `Agents__TimeoutSeconds` | Whole-run deadline; default 120, range 1–300. Provider requests also have a 90-second timeout. |

Prepare PostgreSQL using all current EF migrations. If the EF CLI is installed:

```powershell
dotnet ef database update --project src/Modules/Business/Infrastructure --startup-project src/Api
```

Organization creation is at `POST /api/organizations`; product creation and listing are at `/api/organizations/{id}/products`. The agent reads those records when the Database catalogue is selected. Follow [real data and model setup](docs/real-data-and-model-setup.md) to test the full flow and configure local development model credentials.

Without an access key, all `/api` endpoints accept loopback requests only in Development. The shared key grants operator access to all business and agent records; this is not per-user or per-tenant authentication. Keep this version local/internal until platform-wide identity and authorization are added.

Live support currently uses the internal ticket store, not an external help desk. An authenticated operator can create a ticket through `POST /api/agent/tickets` with `id`, `organizationId`, `title`, `category`, and `serviceHealthy`. The health field is operator-supplied evidence, not an automatic health check. Only an open ticket with `category=service_recovered` and `serviceHealthy=true` can be resolved. All other open tickets must be escalated.

The OpenAI adapter uses strict function schemas, one function call per response, and local context replay with provider storage disabled. See the [official function-calling guide](https://developers.openai.com/api/docs/guides/function-calling). Live runs send the task, selected business records, prior outcome, and tool results to the model provider.

## Agent API

All routes below are under `/api/agent`.

| Method | Path | Purpose |
|---|---|---|
| GET | `/workflows` | Available workflows, tools, and active model mode. |
| GET | `/readiness` | Database/migration readiness and model configuration status without provider calls. |
| POST | `/runs` | Execute a workflow synchronously and return its persistent run record. |
| GET | `/runs/{id}` | Retrieve a run and its verified outcome. |
| GET | `/runs/{id}/events` | Retrieve ordered audit events. |
| GET | `/runs/{id}/plan` | Retrieve the growth draft saved by a run. |
| POST | `/tickets` | Create an internal support ticket. |
| GET | `/tickets/{organizationId}/{id}` | Retrieve an internal ticket. |

Run states: `running`, `completed`, `rejected`, `failed`, `step_limit`, `cancelled`, `timed_out`, and `interrupted`. The server sets `outcome` from verified tool/storage results. `recommendation` and saved plans are model-authored advice in OpenAI mode and require human review; structural checks do not prove every sentence is factual.

## Architecture and tests

See [the agent design](docs/agent-system-design.md) for component boundaries, controls, and remaining work.

```powershell
dotnet test --solution AiGrowthPlatform.slnx --configuration Release
```

Tests cover both workflows, read-only behavior, policy enforcement, missing evidence, invalid/unknown tools, step limits, cancellation, persistence, memory isolation, stale ticket updates, crash recovery, and the model HTTP contract without paid API calls. GitHub Actions builds and runs this suite on pushes and PRs to `Dev` and `Main`.

With the demo server running, `./scripts/Test-AgentApi.ps1` checks the HTTP endpoints end to end. It saves sample plans and resolves/escalates the two sample tickets.

`./scripts/Test-CatalogApi.ps1` tests product entry through to a growth plan with Demo mode and the Database catalogue. PostgreSQL integration tests use an isolated database when `AI_GROWTH_TEST_POSTGRES` is set; CI supplies that test server automatically. See [the setup guide](docs/real-data-and-model-setup.md#integration-tests).

## Current limits

This is the first working backend increment, with no frontend or external ticket/campaign integrations. The local store supports one API process and requires a persistent disk. Atomic file replacement protects individual records; action, audit, and memory files are not one transaction. A crash can leave a performed action without its final audit result. Startup marks unfinished runs interrupted; inspect ticket/plan records before retrying. POST requests have no cross-run idempotency key. Move these records to transactional storage and add idempotency before running multiple replicas or automated retries.

Knowledge consists of two built-in policies. Retrieval uses keyword matching. Persistent memory stores only the latest verified outcome per organization/workflow/ticket; there is no preferences editor or retention policy yet. Audit files contain task and business data and should be protected accordingly.

Live model execution and PostgreSQL integration require your configured services; offline demo and contract tests do not establish their availability.
