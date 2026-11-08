using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace FunEvents.Api.Security.PartnerApiKey;

public sealed class PartnerApiKeyEndpointFilter(IOptions<PartnerApiKeyOptions> options) : IEndpointFilter
{
    public const string ChannelIdItemKey = "FunEvents.ChannelId";

    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var suppliedKey = context.HttpContext.Request.Headers["X-Api-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(suppliedKey))
            return ValueTask.FromResult<object?>(Unauthorized());

        foreach (var channel in options.Value.Channels)
        {
            if (string.IsNullOrWhiteSpace(channel.Value))
                continue;

            var expectedBytes = Encoding.UTF8.GetBytes(channel.Value);
            var suppliedBytes = Encoding.UTF8.GetBytes(suppliedKey);
            if (!CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes))
                continue;

            context.HttpContext.Items[ChannelIdItemKey] = channel.Key;
            return next(context);
        }

        return ValueTask.FromResult<object?>(Unauthorized());
    }

    private static IResult Unauthorized() => Results.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Invalid API key",
        detail: "A valid X-Api-Key header is required.",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_api_key" });
}
