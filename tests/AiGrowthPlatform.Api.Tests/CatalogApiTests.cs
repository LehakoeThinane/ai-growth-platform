using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiGrowthPlatform.Agents;
using AiGrowthPlatform.Api.Agents;
using AiGrowthPlatform.Business.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace AiGrowthPlatform.Api.Tests;

public sealed class CatalogApiTests(PostgresFixture database) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    public async Task Entered_products_flow_through_the_database_into_a_saved_growth_plan()
    {
        await using var app = new ApiFactory(database.ConnectionString);
        using var client = app.CreateAuthorizedClient();
        var organizationResponse = await client.PostAsJsonAsync("/api/organizations", new { name = "Real catalogue test", websiteUrl = "https://example.com" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, organizationResponse.StatusCode);
        var organization = await organizationResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var id = organization.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(organizationResponse.Headers.Location, TestContext.Current.CancellationToken)).StatusCode);
        var route = $"/api/organizations/{id}/products";
        var productResponse = await client.PostAsJsonAsync(route, new { name = "Database workshop", description = "Created through the API", price = 1234.56m }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        var product = await productResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var productId = product.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(productResponse.Headers.Location, TestContext.Current.CancellationToken)).StatusCode);
        var list = await client.GetFromJsonAsync<JsonElement>(route, TestContext.Current.CancellationToken);
        Assert.Single(list.EnumerateArray());
        var runResponse = await client.PostAsJsonAsync("/api/agent/runs", new StartRunRequest
        {
            Workflow = "growth", OrganizationId = id, Task = "Draft a growth plan from my catalogue", AllowWrites = true
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, runResponse.StatusCode);
        var run = await runResponse.Content.ReadFromJsonAsync<AgentRun>(TestContext.Current.CancellationToken);
        Assert.Equal("completed", run!.Status);
        Assert.Equal("Demo", run.Mode);
        Assert.Equal("Database", run.CatalogSource);
        var observedProducts = run.Observations.Single(x => x.Tool == "list_products").Result;
        Assert.Equal(productId, observedProducts[0].GetProperty("id").GetGuid());
        var plan = await client.GetFromJsonAsync<GrowthPlan>($"/api/agent/runs/{run.Id}/plan", TestContext.Current.CancellationToken);
        Assert.Contains("Database workshop", plan!.Content);
        Assert.DoesNotContain("Starter consultation", plan.Content);
        var readiness = await client.GetFromJsonAsync<JsonElement>("/api/agent/readiness", TestContext.Current.CancellationToken);
        Assert.True(readiness.GetProperty("ready").GetBoolean());
        Assert.Equal("ready", readiness.GetProperty("database").GetString());
    }

    [PostgresFact]
    public async Task Missing_organizations_invalid_inputs_and_cross_organization_reads_are_rejected()
    {
        await using var app = new ApiFactory(database.ConnectionString);
        using var client = app.CreateAuthorizedClient();
        var response = await client.PostAsJsonAsync("/api/organizations", new { name = "Validation tests" }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();
        var route = $"/api/organizations/{id}/products";
        foreach (var body in new object[] { new { name = " ", price = 10m }, new { name = "Bad price", price = -1m }, new { name = "Rounding loss", price = 1.234m }, new { name = new string('x', 201), price = 10m } })
        {
            var invalid = await client.PostAsJsonAsync(route, body, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        var otherId = Guid.NewGuid();
        var missing = await client.PostAsJsonAsync($"/api/organizations/{otherId}/products", new { name = "Orphan" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(route + "?take=101", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(route + "?skip=-1", TestContext.Current.CancellationToken)).StatusCode);
        var valid = await client.PostAsJsonAsync(route, new { name = "Scoped product", price = 0 }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, valid.StatusCode);
        var productId = (await valid.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/organizations/{otherId}/products/{productId}", TestContext.Current.CancellationToken)).StatusCode);
        var invalidOrg = await client.PostAsJsonAsync("/api/organizations", new { name = "Bad URL", websiteUrl = "file:///tmp" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidOrg.StatusCode);
    }

    [PostgresFact]
    public async Task Organizations_and_products_can_be_edited_and_deleted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = new ApiFactory(database.ConnectionString);
        using var client = app.CreateAuthorizedClient();
        var created = await client.PostAsJsonAsync("/api/organizations", new { name = "Editable" }, ct);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();
        var orgRoute = $"/api/organizations/{id}";

        var renamed = await client.PutAsJsonAsync(orgRoute, new { name = "Renamed", websiteUrl = "https://example.org" }, ct);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var organization = await client.GetFromJsonAsync<JsonElement>(orgRoute, ct);
        Assert.Equal("Renamed", organization.GetProperty("name").GetString());
        Assert.Equal("https://example.org", organization.GetProperty("websiteUrl").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(orgRoute, new { name = " " }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/organizations/{Guid.NewGuid()}", new { name = "Missing" }, ct)).StatusCode);

        var productResponse = await client.PostAsJsonAsync($"{orgRoute}/products", new { name = "Original", description = "Before", price = 10m }, ct);
        var productId = (await productResponse.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();
        var productRoute = $"{orgRoute}/products/{productId}";
        var updated = await client.PutAsJsonAsync(productRoute, new { name = "Updated", price = 12.5m }, ct);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var product = await client.GetFromJsonAsync<JsonElement>(productRoute, ct);
        Assert.Equal("Updated", product.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, product.GetProperty("description").ValueKind);
        Assert.Equal(12.5m, product.GetProperty("price").GetDecimal());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(productRoute, new { name = "Bad", price = 1.234m }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/organizations/{Guid.NewGuid()}/products/{productId}", new { name = "Other org" }, ct)).StatusCode);

        var blocked = await client.DeleteAsync(orgRoute, ct);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/organizations/{Guid.NewGuid()}/products/{productId}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(productRoute, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(productRoute, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(productRoute, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(orgRoute, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(orgRoute, ct)).StatusCode);
    }

    [PostgresFact]
    public async Task Agent_and_business_endpoints_use_the_same_access_key()
    {
        await using var app = new ApiFactory(database.ConnectionString);
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        foreach (var route in new[] { "/api/organizations", "/api/agent/workflows", $"/api/organizations/{Guid.NewGuid()}/products" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(route, TestContext.Current.CancellationToken)).StatusCode);
        var unauthorizedCreate = await client.PostAsJsonAsync("/api/organizations", new { name = "Unauthorized" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedCreate.StatusCode);
    }

    [PostgresFact]
    public async Task Foreign_key_blocks_orphans_and_parent_deletion()
    {
        await using var db = database.CreateDb();
        Assert.False(db.Database.HasPendingModelChanges());
        db.Products.Add(new Product(Guid.NewGuid(), "Orphan"));
        var orphan = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(orphan.InnerException).SqlState);
        db.ChangeTracker.Clear();
        var organization = new Organization("FK test");
        db.Organizations.Add(organization);
        db.Products.Add(new Product(organization.Id, "Linked"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        db.Organizations.Remove(await db.Organizations.SingleAsync(x => x.Id == organization.Id, TestContext.Current.CancellationToken));
        var deletion = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(deletion.InnerException).SqlState);
    }

    [PostgresFact]
    public async Task Catalogue_is_ordered_limited_and_scoped()
    {
        await using var db = database.CreateDb();
        var organization = new Organization("Many products");
        var other = new Organization("Another organization");
        db.Organizations.AddRange(organization, other);
        db.Products.AddRange(Enumerable.Range(0, 105).Select(i => new Product(organization.Id, $"Product {i:D3}")));
        db.Products.Add(new Product(other.Id, "Do not include"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var products = await new BusinessCatalog(db).GetProductsAsync(organization.Id, TestContext.Current.CancellationToken);
        Assert.Equal(100, products.Count);
        Assert.Equal("Product 000", products[0].Name);
        Assert.Equal("Product 099", products[^1].Name);
    }

    private sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        private readonly string _data = Path.Combine(Path.GetTempPath(), "aigrowth-api-test-" + Guid.NewGuid().ToString("N"));
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:BusinessDatabase"] = connectionString,
                ["Agents:Mode"] = "Demo", ["Agents:CatalogSource"] = "Database",
                ["Agents:AccessKey"] = "integration-test-key", ["Agents:DataDirectory"] = _data
            }));
        }

        public HttpClient CreateAuthorizedClient()
        {
            var client = CreateClient(new() { BaseAddress = new Uri("https://localhost") });
            client.DefaultRequestHeaders.Add("X-Agent-Key", "integration-test-key");
            return client;
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            if (Directory.Exists(_data)) Directory.Delete(_data, true);
        }
    }
}
