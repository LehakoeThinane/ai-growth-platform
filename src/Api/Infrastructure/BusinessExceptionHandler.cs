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
            BusinessConflictException => new ProblemDetails { Status = 409, Title = "Record in use", Detail = exception.Message },
            DbUpdateConcurrencyException => new ProblemDetails { Status = 409, Title = "Record changed", Detail = "The record was changed or deleted by another request. Reload it before retrying." },
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } } =>
                new ProblemDetails { Status = 409, Title = "Related records changed", Detail = "Reload the organization and its products before retrying." },
            _ => null
        };
        if (problem is null) return false;
        context.Response.StatusCode = problem.Status!.Value;
        await context.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
