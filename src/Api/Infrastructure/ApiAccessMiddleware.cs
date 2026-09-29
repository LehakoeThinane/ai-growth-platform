using System.Net;
using System.Security.Cryptography;
using System.Text;
using AiGrowthPlatform.Agents;
using Microsoft.Extensions.Options;

namespace AiGrowthPlatform.Api.Infrastructure;

// One operator key protects both business records and agent actions. Per-user tenancy comes later.
public sealed class ApiAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IOptions<AgentOptions> options, IHostEnvironment environment)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            var expected = options.Value.AccessKey;
            var supplied = context.Request.Headers["X-Agent-Key"].ToString();
            var allowed = expected.Length > 0
                ? CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(supplied)),
                    SHA256.HashData(Encoding.UTF8.GetBytes(expected)))
                : environment.IsDevelopment() && context.Connection.RemoteIpAddress is { } ip && IPAddress.IsLoopback(ip);
            if (!allowed)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }
        await next(context);
    }
}
