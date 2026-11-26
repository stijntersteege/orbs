using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Orbs.Application.Common;
using Orbs.Domain.Common;

namespace Orbs.Api.Hosting;

/// <summary>Turns known exceptions into RFC 9457 problem responses with a fitting status code.</summary>
internal sealed class ProblemExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            DomainException => (StatusCodes.Status400BadRequest, "The request breaks a rule"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            ModelUnavailableException => (StatusCodes.Status409Conflict, "No model available"),
            ExternalServiceException => (StatusCodes.Status502BadGateway, "An external service failed"),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Malformed request"),
            _ => (0, string.Empty),
        };

        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = exception.Message },
        });
    }
}
