param([string]$BaseUrl = 'http://localhost:5162', [string]$AccessKey = '')

$ErrorActionPreference = 'Stop'
$headers = @{}
if ($AccessKey) { $headers['X-Agent-Key'] = $AccessKey }
$organizationId = '11111111-1111-1111-1111-111111111111'

function Invoke-AgentRun($request) {
    Invoke-RestMethod "$BaseUrl/api/agent/runs" -Method Post -Headers $headers `
        -ContentType 'application/json' -Body ($request | ConvertTo-Json)
}

$workflows = Invoke-RestMethod "$BaseUrl/api/agent/workflows" -Headers $headers
if ($workflows.mode -ne 'Demo' -or $workflows.catalogSource -ne 'Demo') { throw 'This smoke test requires Demo mode and the Demo catalogue; it changes sample records only.' }

$growthRequest = @{
    workflow = 'growth'; organizationId = $organizationId
    task = 'Review the catalogue and save a draft growth plan'; allowWrites = $true
}
$growth = Invoke-AgentRun $growthRequest
if ($growth.status -ne 'completed') { throw "Growth failed: $($growth.outcome)" }
$plan = Invoke-RestMethod "$BaseUrl/api/agent/runs/$($growth.id)/plan" -Headers $headers
if ($plan.runId -ne $growth.id) { throw 'Plan was not persisted under the run ID.' }
$next = Invoke-AgentRun $growthRequest
if ($next.loadedMemory.lastRunId -ne $growth.id) { throw 'Previous verified outcome was not loaded.' }

foreach ($ticketId in @('INC-1042', 'INC-1043')) {
    $support = Invoke-AgentRun @{
        workflow = 'support'; organizationId = $organizationId; ticketId = $ticketId
        task = 'Resolve using the approved procedure or escalate'; allowWrites = $true
    }
    if ($support.status -ne 'completed') { throw "Support failed: $($support.outcome)" }
    $expected = if ($ticketId -eq 'INC-1042') { 'resolved' } else { 'escalated' }
    $ticket = Invoke-RestMethod "$BaseUrl/api/agent/tickets/$organizationId/$ticketId" -Headers $headers
    if ($ticket.status -ne $expected) { throw "Unexpected status for $ticketId" }
    Write-Output "$ticketId => $($ticket.status)"
}

$invalidWasRejected = $false
try {
    Invoke-AgentRun @{ workflow = 'wrong'; organizationId = $organizationId; task = 'Invalid request' } | Out-Null
} catch {
    if ([int]$_.Exception.Response.StatusCode -ne 400) { throw }
    $invalidWasRejected = $true
}
if (-not $invalidWasRejected) { throw 'Invalid workflow was not rejected with HTTP 400.' }

$missing = Invoke-AgentRun @{
    workflow = 'support'; organizationId = $organizationId; ticketId = 'INC-999999999999'
    task = 'Inspect a missing ticket'; allowWrites = $false
}
if ($missing.status -ne 'rejected') { throw 'Missing ticket incorrectly succeeded.' }
$events = Invoke-RestMethod "$BaseUrl/api/agent/runs/$($growth.id)/events" -Headers $headers
[pscustomobject]@{
    Growth = $growth.status; PlanSaved = ($null -ne $plan.id)
    MemoryReused = ($null -ne $next.loadedMemory); AuditEvents = $events.Count
    InvalidInputRejected = $invalidWasRejected; MissingTicket = $missing.status
}
