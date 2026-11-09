using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FunEvents.Api.OpenApi;

public sealed class PartnerApiKeyOpenApiTransformer : IOpenApiDocumentTransformer
{
    private const string ReservationPathPrefix = "/api/v1/reservations";
    private const string SecuritySchemeName = "PartnerApiKey";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SecuritySchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            Name = "X-Api-Key",
            In = ParameterLocation.Header,
            Description = "API key identifying the partner sales channel."
        };

        foreach (var path in document.Paths.Where(
                     path => path.Key.StartsWith(ReservationPathPrefix, StringComparison.Ordinal)))
        {
            foreach (var operation in path.Value.Operations.Values)
            {
                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(SecuritySchemeName, document)] = []
                });
            }
        }

        return Task.CompletedTask;
    }
}
