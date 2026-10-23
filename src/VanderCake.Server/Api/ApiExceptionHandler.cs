using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VanderCake.Application.Abstractions;
using VanderCake.Domain.Common;

namespace VanderCake.Server.Api;

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception error, CancellationToken ct)
    {
        var (status, title, detail) = error switch
        {
            DomainRuleException => (400, "Validation failed", error.Message),
            BadHttpRequestException => (400, "Invalid request", "The request body or parameters are invalid."),
            JsonException => (400, "Invalid JSON", "The request body is invalid."),
            ApplicationFailure { Kind: FailureKind.NotFound } => (404, "Not found", error.Message),
            ApplicationFailure { Kind: FailureKind.Forbidden } => (403, "Forbidden", error.Message),
            ApplicationFailure { Kind: FailureKind.Conflict } => (409, "Conflict", error.Message),
            DbUpdateConcurrencyException => (409, "Conflict", "Data changed; reload before saving."),
            SqliteException { SqliteErrorCode: 5 or 6 } => (409, "Database busy", "Another write is in progress; reload before retrying."),
            DbUpdateException { InnerException: SqliteException { SqliteErrorCode: 5 or 6 } }
                => (409, "Database busy", "Another write is in progress; reload before retrying."),
            DbUpdateException { InnerException: SqliteException { SqliteErrorCode: 19 } }
                => (409, "Data conflict", "The operation conflicts with existing data or a database constraint."),
            _ => (500, "Unexpected error", "The request could not be completed.")
        };
        if (status == 500) logger.LogError(error, "Unhandled request failure {TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = status;
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new() { Status = status, Title = title, Detail = detail }
        });
        return true;
    }
}
