using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

const string defaultApiAddress = "http://localhost:5053";
const string defaultApiKey = "local-console-secret";
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .Build();
var apiAddress = Environment.GetEnvironmentVariable("FUN_EVENTS_API_BASE_URL")
    ?? configuration["FunEvents:ApiBaseUrl"]
    ?? defaultApiAddress;
var apiKey = Environment.GetEnvironmentVariable("FUN_EVENTS_API_KEY")
    ?? configuration["FunEvents:ApiKey"]
    ?? defaultApiKey;

if (!Uri.TryCreate(apiAddress, UriKind.Absolute, out var baseUri))
{
    Console.Error.WriteLine($"Invalid API base URL: {apiAddress}");
    return 1;
}

using var httpClient = new HttpClient { BaseAddress = baseUri };

Console.WriteLine("FunEvents reservation client");
Console.WriteLine($"API: {baseUri}");

while (true)
{
    Console.WriteLine();
    Console.WriteLine("1. Create reservation");
    Console.WriteLine("2. Read reservation");
    Console.WriteLine("3. Cancel reservation");
    Console.WriteLine("4. Pay reservation (simulated)");
    Console.WriteLine("0. Exit");
    Console.Write("Choose an action: ");

    switch (Console.ReadLine()?.Trim())
    {
        case "1":
            await CreateReservationAsync(httpClient, apiKey);
            break;
        case "2":
            await ReadReservationAsync(httpClient, apiKey);
            break;
        case "3":
            await CancelReservationAsync(httpClient, apiKey);
            break;
        case "4":
            await PayReservationAsync(httpClient, apiKey);
            break;
        case "0":
        case null:
            Console.WriteLine("Goodbye.");
            return 0;
        default:
            Console.WriteLine("Choose 1, 2, 3, 4, or 0.");
            break;
    }
}

static async Task CreateReservationAsync(HttpClient httpClient, string apiKey)
{
    var eventCode = ReadValue("Event code", "EVT-001");
    var sessionCode = ReadValue("Session code", "SES-001-A");
    var userCode = ReadValue("User code", "USR-042");
    var quantity = ReadQuantity();
    var idempotencyKey = Guid.NewGuid().ToString("N");

    Console.WriteLine($"Creating reservation (idempotency key: {idempotencyKey})...");
    await SendAsync(
        httpClient,
        apiKey,
        HttpMethod.Post,
        "/api/v1/reservations",
        "Reservation created successfully.",
        new { eventCode, sessionCode, userCode, quantity },
        idempotencyKey);
}

static async Task ReadReservationAsync(HttpClient httpClient, string apiKey)
{
    var reservationCode = ReadRequired("Reservation code");
    await SendAsync(
        httpClient,
        apiKey,
        HttpMethod.Get,
        $"/api/v1/reservations/{Uri.EscapeDataString(reservationCode)}",
        "Reservation details:");
}

static async Task CancelReservationAsync(HttpClient httpClient, string apiKey)
{
    var reservationCode = ReadRequired("Reservation code");
    await SendAsync(
        httpClient,
        apiKey,
        HttpMethod.Post,
        $"/api/v1/reservations/{Uri.EscapeDataString(reservationCode)}/cancel",
        "Cancellation result:");
}

static async Task PayReservationAsync(HttpClient httpClient, string apiKey)
{
    var reservationCode = ReadRequired("Reservation code");
    var suggestedKey = Guid.NewGuid().ToString("N");
    var idempotencyKey = ReadValue("Payment idempotency key (reuse to retry pending attempt)", suggestedKey);

    Console.WriteLine($"Starting simulated payment (idempotency key: {idempotencyKey})...");
    await SendAsync(
        httpClient,
        apiKey,
        HttpMethod.Post,
        $"/api/v1/reservations/{Uri.EscapeDataString(reservationCode)}/payments",
        "Payment result:",
        idempotencyKey: idempotencyKey,
        printSuccess: PrintPayment);
}

static async Task SendAsync(
    HttpClient httpClient,
    string apiKey,
    HttpMethod method,
    string path,
    string successMessage,
    object? body = null,
    string? idempotencyKey = null,
    Action<JsonElement>? printSuccess = null)
{
    using var request = new HttpRequestMessage(method, path);
    request.Headers.Add("X-Api-Key", apiKey);
    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

    if (idempotencyKey is not null)
        request.Headers.Add("Idempotency-Key", idempotencyKey);

    if (body is not null)
    {
        request.Content = new StringContent(
            JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Encoding.UTF8,
            "application/json");
    }

    try
    {
        using var response = await httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Request failed ({(int)response.StatusCode} {response.ReasonPhrase}).");
            PrintProblem(responseBody);
            return;
        }

        Console.WriteLine(successMessage);
        if (printSuccess is null)
            PrintReservation(responseBody);
        else
        {
            using var document = JsonDocument.Parse(responseBody);
            printSuccess(document.RootElement);
        }
    }
    catch (HttpRequestException exception)
    {
        Console.WriteLine("Could not reach the FunEvents API.");
        Console.WriteLine(exception.Message);
    }
    catch (JsonException exception)
    {
        Console.WriteLine("The API returned an invalid JSON response.");
        Console.WriteLine(exception.Message);
    }
}

static void PrintPayment(JsonElement payment)
{
    PrintProperty(payment, "paymentCode", "Payment code");
    PrintProperty(payment, "paymentStatus", "Payment status");
    PrintProperty(payment, "reservationCode", "Reservation code");
    PrintProperty(payment, "reservationStatus", "Reservation status");
    PrintProperty(payment, "providerReference", "Simulated provider reference");
}

static string ReadValue(string label, string defaultValue)
{
    Console.Write($"{label} [{defaultValue}]: ");
    var value = Console.ReadLine()?.Trim();
    return string.IsNullOrEmpty(value) ? defaultValue : value;
}

static string ReadRequired(string label)
{
    while (true)
    {
        Console.Write($"{label}: ");
        var value = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(value))
            return value;

        Console.WriteLine("A value is required.");
    }
}

static int ReadQuantity()
{
    while (true)
    {
        Console.Write("Quantity [1]: ");
        var value = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(value))
            return 1;

        if (int.TryParse(value, out var quantity) && quantity > 0)
            return quantity;

        Console.WriteLine("Enter a positive whole number.");
    }
}

static void PrintReservation(string responseBody)
{
    using var document = JsonDocument.Parse(responseBody);
    var reservation = document.RootElement;
    PrintProperty(reservation, "reservationCode", "Reservation code");
    PrintProperty(reservation, "status", "Status");
    PrintProperty(reservation, "eventCode", "Event");
    PrintProperty(reservation, "sessionCode", "Session");
    PrintProperty(reservation, "userCode", "User");
    PrintProperty(reservation, "quantity", "Quantity");
    PrintProperty(reservation, "createdAt", "Created at");
    PrintProperty(reservation, "expiresAt", "Expires at");
}

static void PrintProperty(JsonElement json, string propertyName, string label)
{
    if (json.TryGetProperty(propertyName, out var value))
        Console.WriteLine($"{label}: {value}");
}

static void PrintProblem(string responseBody)
{
    try
    {
        using var document = JsonDocument.Parse(responseBody);
        var problem = document.RootElement;
        if (problem.TryGetProperty("detail", out var detail))
            Console.WriteLine(detail.GetString());
        else
            Console.WriteLine(responseBody);

        if (problem.TryGetProperty("code", out var code))
            Console.WriteLine($"Error code: {code.GetString()}");
    }
    catch (JsonException)
    {
        Console.WriteLine(responseBody);
    }
}
