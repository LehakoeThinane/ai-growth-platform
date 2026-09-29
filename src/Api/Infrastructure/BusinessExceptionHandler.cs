using AiGrowthPlatform.Business.Application;
using AiGrowthPlatform.Business.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AiGrowthPlatform.Api.Infrastructure;

public sealed class BusinessExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            BusinessValidationException => new ProblemDetails { Status = 400, Title = "Invalid business data", Detail = exception.Message },
            BusinessNotFoundException => new ProblemDetails { Status = 404, Title = "Record not found", Detail = exception.Message },
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } } =>
                new ProblemDetails { Status = 409, Title = "Related organization changed", Detail = "Reload the organization before retrying." },
            _ => null
        };
        if (problem is null) return false;
        context.Response.StatusCode = problem.Status!.Value;
        await context.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
