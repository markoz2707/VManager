using FluentAssertions;
using System.Net;
using System.Text.Json;

namespace HyperV.Tests.Helpers;

/// <summary>
/// Klasa do weryfikacji odpowiedzi API
/// </summary>
public static class ApiResponseVerifier
{
    /// <summary>
    /// Weryfikuje, czy odpowiedź HTTP ma oczekiwany kod statusu
    /// </summary>
    public static async Task<string> VerifySuccessResponse(HttpResponseMessage response, HttpStatusCode expectedStatusCode = HttpStatusCode.OK)
    {
        response.StatusCode.Should().Be(expectedStatusCode, 
            $"Expected status code {expectedStatusCode}, but got {response.StatusCode}");
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty("Response content should not be empty");
        
        return content;
    }

    /// <summary>
    /// Weryfikuje, czy odpowiedź HTTP ma kod błędu
    /// </summary>
    public static async Task<string> VerifyErrorResponse(HttpResponseMessage response, HttpStatusCode expectedStatusCode)
    {
        response.StatusCode.Should().Be(expectedStatusCode, 
            $"Expected error status code {expectedStatusCode}, but got {response.StatusCode}");
        
        var content = await response.Content.ReadAsStringAsync();
        return content;
    }

    /// <summary>
    /// Weryfikuje, czy odpowiedź zawiera prawidłowy JSON
    /// </summary>
    public static JsonElement VerifyJsonResponse(string content)
    {
        content.Should().NotBeNullOrEmpty("JSON content should not be empty");
        
        try
        {
            var jsonDocument = JsonDocument.Parse(content);
            return jsonDocument.RootElement;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Response content is not valid JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Weryfikuje, czy JSON zawiera oczekiwane pole
    /// </summary>
    public static void VerifyJsonProperty(JsonElement json, string propertyName, JsonValueKind expectedType)
    {
        json.TryGetProperty(propertyName, out var property).Should().BeTrue(
            $"JSON should contain property '{propertyName}'");
        
        property.ValueKind.Should().Be(expectedType, 
            $"Property '{propertyName}' should be of type {expectedType}");
    }

    /// <summary>
    /// Weryfikuje, czy JSON zawiera oczekiwane pole z określoną wartością
    /// </summary>
    public static void VerifyJsonPropertyValue<T>(JsonElement json, string propertyName, T expectedValue)
    {
        json.TryGetProperty(propertyName, out var property).Should().BeTrue(
            $"JSON should contain property '{propertyName}'");

        if (typeof(T) == typeof(string))
        {
            property.GetString().Should().Be(expectedValue as string, 
                $"Property '{propertyName}' should have value '{expectedValue}'");
        }
        else if (typeof(T) == typeof(int))
        {
            property.GetInt32().Should().Be((int)(object)expectedValue!,
                $"Property '{propertyName}' should have value '{expectedValue}'");
        }
        else if (typeof(T) == typeof(bool))
        {
            property.GetBoolean().Should().Be((bool)(object)expectedValue!,
                $"Property '{propertyName}' should have value '{expectedValue}'");
        }
        else
        {
            throw new ArgumentException($"Unsupported property type: {typeof(T)}");
        }
    }

    /// <summary>
    /// Weryfikuje odpowiedź dla endpointu health
    /// </summary>
    public static void VerifyHealthResponse(JsonElement json)
    {
        VerifyJsonProperty(json, "status", JsonValueKind.String);
        VerifyJsonProperty(json, "timestamp", JsonValueKind.String);
        VerifyJsonProperty(json, "version", JsonValueKind.String);
        VerifyJsonProperty(json, "services", JsonValueKind.Object);
        
        VerifyJsonPropertyValue(json, "status", "healthy");
    }

    /// <summary>
    /// Weryfikuje odpowiedź dla endpointu info
    /// </summary>
    public static void VerifyInfoResponse(JsonElement json)
    {
        VerifyJsonProperty(json, "name", JsonValueKind.String);
        VerifyJsonProperty(json, "version", JsonValueKind.String);
        VerifyJsonProperty(json, "description", JsonValueKind.String);
        VerifyJsonProperty(json, "endpoints", JsonValueKind.Object);
        VerifyJsonProperty(json, "capabilities", JsonValueKind.Array);
        
        VerifyJsonPropertyValue(json, "name", "HyperV Agent");
    }

    /// <summary>
    /// Weryfikuje odpowiedź dla sprawdzania obecności VM
    /// </summary>
    public static void VerifyVmPresentResponse(JsonElement json, bool expectedPresent)
    {
        VerifyJsonProperty(json, "present", JsonValueKind.True);
        VerifyJsonProperty(json, "hcs", JsonValueKind.True);
        VerifyJsonProperty(json, "wmi", JsonValueKind.True);
        
        VerifyJsonPropertyValue(json, "present", expectedPresent);
    }

    /// <summary>
    /// Weryfikuje odpowiedź z listą VM
    /// </summary>
    public static void VerifyVmListResponse(JsonElement json)
    {
        VerifyJsonProperty(json, "HCS", JsonValueKind.Object);
        VerifyJsonProperty(json, "WMI", JsonValueKind.Object);
        
        // Sprawdzenie struktury odpowiedzi HCS
        json.GetProperty("HCS").TryGetProperty("Count", out var hcsCount).Should().BeTrue();
        json.GetProperty("HCS").TryGetProperty("VMs", out var hcsVMs).Should().BeTrue();
        hcsVMs.ValueKind.Should().Be(JsonValueKind.Array);
        
        // Sprawdzenie struktury odpowiedzi WMI
        json.GetProperty("WMI").TryGetProperty("Count", out var wmiCount).Should().BeTrue();
        json.GetProperty("WMI").TryGetProperty("VMs", out var wmiVMs).Should().BeTrue();
        wmiVMs.ValueKind.Should().Be(JsonValueKind.Array);
    }

    /// <summary>
    /// Weryfikuje odpowiedź błędu z oczekiwanym komunikatem
    /// </summary>
    public static void VerifyErrorResponse(JsonElement json, string? expectedErrorMessage = null)
    {
        VerifyJsonProperty(json, "error", JsonValueKind.String);
        
        if (!string.IsNullOrEmpty(expectedErrorMessage))
        {
            var errorMessage = json.GetProperty("error").GetString();
            errorMessage.Should().Contain(expectedErrorMessage, 
                $"Error message should contain '{expectedErrorMessage}'");
        }
    }

    /// <summary>
    /// Weryfikuje, czy JSON zawiera tablicę z określoną minimalną liczbą elementów
    /// </summary>
    public static void VerifyJsonArrayMinCount(JsonElement json, string arrayPropertyName, int minCount)
    {
        VerifyJsonProperty(json, arrayPropertyName, JsonValueKind.Array);
        var array = json.GetProperty(arrayPropertyName);
        array.GetArrayLength().Should().BeGreaterOrEqualTo(minCount,
            $"Array '{arrayPropertyName}' should have at least {minCount} elements");
    }

    /// <summary>
    /// Weryfikuje, czy Content-Type jest application/json
    /// </summary>
    public static void VerifyContentTypeJson(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json",
            "Response should have Content-Type: application/json");
    }

    /// <summary>
    /// Weryfikuje odpowiedź dla listy zadań pamięci masowej
    /// </summary>
    public static void VerifyStorageJobsResponse(JsonElement json)
    {
        json.ValueKind.Should().Be(JsonValueKind.Array, "Storage jobs response should be an array");
        
        foreach (var job in json.EnumerateArray())
        {
            VerifyJsonProperty(job, "JobId", JsonValueKind.String);
            VerifyJsonProperty(job, "JobType", JsonValueKind.String);
            VerifyJsonProperty(job, "JobState", JsonValueKind.String);
            VerifyJsonProperty(job, "StartTime", JsonValueKind.String);
        }
    }

    /// <summary>
    /// Weryfikuje odpowiedź dla szczegółów zadania pamięci masowej
    /// </summary>
    public static void VerifyStorageJobResponse(JsonElement json)
    {
        VerifyJsonProperty(json, "JobId", JsonValueKind.String);
        VerifyJsonProperty(json, "JobType", JsonValueKind.String);
        VerifyJsonProperty(json, "JobState", JsonValueKind.String);
        VerifyJsonProperty(json, "StartTime", JsonValueKind.String);
        VerifyJsonProperty(json, "PercentComplete", JsonValueKind.Number);
    }
}