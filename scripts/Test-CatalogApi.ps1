param([string]$BaseUrl = 'http://localhost:5162', [string]$AccessKey = '')

$ErrorActionPreference = 'Stop'
$headers = @{}
if ($AccessKey) { $headers['X-Agent-Key'] = $AccessKey }
$workflow = Invoke-RestMethod "$BaseUrl/api/agent/workflows" -Headers $headers
if ($workflow.mode -ne 'Demo' -or $workflow.catalogSource -ne 'Database') {
    throw 'Run with Agents:Mode=Demo and Agents:CatalogSource=Database. This test creates sample business records without model charges.'
}
$readiness = Invoke-RestMethod "$BaseUrl/api/agent/readiness" -Headers $headers
if (-not $readiness.ready) { throw 'Apply the Business database migrations before running this test.' }

$organization = Invoke-RestMethod "$BaseUrl/api/organizations" -Headers $headers -Method Post `
    -ContentType 'application/json' -Body (@{
        name = "Catalogue check $(Get-Date -Format 'yyyyMMdd-HHmmss')"
        websiteUrl = 'https://example.com'
    } | ConvertTo-Json)
$product = Invoke-RestMethod "$BaseUrl/api/organizations/$($organization.id)/products" -Headers $headers -Method Post `
    -ContentType 'application/json' -Body (@{
        name = 'Business growth workshop'; description = 'A workshop entered through the product API'; price = 1500
    } | ConvertTo-Json)
$run = Invoke-RestMethod "$BaseUrl/api/agent/runs" -Headers $headers -Method Post `
    -ContentType 'application/json' -Body (@{
        workflow = 'growth'; organizationId = $organization.id
        task = 'Review my actual products and save a draft growth plan.'; allowWrites = $true
    } | ConvertTo-Json)
if ($run.status -ne 'completed') { throw "Run failed: $($run.outcome)" }
$observedProducts = ($run.observations | Where-Object tool -eq 'list_products').result
if ($product.id -notin $observedProducts.id) { throw 'The agent did not read the product created through the API.' }
$plan = Invoke-RestMethod "$BaseUrl/api/agent/runs/$($run.id)/plan" -Headers $headers
if ($plan.content -notmatch [regex]::Escape($product.name)) { throw 'Saved demo plan does not reference the entered product.' }
[pscustomobject]@{
    OrganizationId = $organization.id; ProductId = $product.id; RunId = $run.id
    ModelMode = $run.mode; CatalogSource = $run.catalogSource
    Status = $run.status; EnteredProductUsed = $true; PlanSaved = ($null -ne $plan.id)
}
