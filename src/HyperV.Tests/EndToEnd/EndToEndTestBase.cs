using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace HyperV.Tests.EndToEnd;

/// <summary>
/// Klasa bazowa dla testów end-to-end
/// </summary>
public abstract class EndToEndTestBase : IDisposable
{
    protected readonly HttpClient Client;
    protected readonly string BaseUrl;
    
    protected EndToEndTestBase(string baseUrl = "http://127.0.0.1:8743")
    {
        BaseUrl = baseUrl;
        Client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
        
        // Czekaj, aż serwer będzie dostępny
        WaitForServerAvailability();
    }

    /// <summary>
    /// Czeka, aż serwer będzie dostępny
    /// </summary>
    private void WaitForServerAvailability(int timeoutSeconds = 30)
    {
        var timeout = DateTime.Now.AddSeconds(timeoutSeconds);
        
        while (DateTime.Now < timeout)
        {
            try
            {
                var response = Client.GetAsync("/api/v1/service/health").Result;
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Console.WriteLine($"Serwer API jest dostępny na {BaseUrl}");
                    return;
                }
            }
            catch
            {
                // Ignoruj błędy i spróbuj ponownie
            }
            
            Thread.Sleep(1000);
        }
        
        throw new InvalidOperationException($"Serwer API nie jest dostępny na {BaseUrl} w ciągu {timeoutSeconds} sekund");
    }

    /// <summary>
    /// Wysyła żądanie GET i zwraca odpowiedź
    /// </summary>
    protected async Task<HttpResponseMessage> GetAsync(string endpoint)
    {
        return await Client.GetAsync(endpoint);
    }

    /// <summary>
    /// Wysyła żądanie POST z JSON body i zwraca odpowiedź
    /// </summary>
    protected async Task<HttpResponseMessage> PostAsync<T>(string endpoint, T data)
    {
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await Client.PostAsync(endpoint, content);
    }

    /// <summary>
    /// Wysyła żądanie PUT z JSON body i zwraca odpowiedź
    /// </summary>
    protected async Task<HttpResponseMessage> PutAsync<T>(string endpoint, T data)
    {
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await Client.PutAsync(endpoint, content);
    }

    /// <summary>
    /// Wysyła żądanie DELETE i zwraca odpowiedź
    /// </summary>
    protected async Task<HttpResponseMessage> DeleteAsync(string endpoint)
    {
        return await Client.DeleteAsync(endpoint);
    }

    /// <summary>
    /// Weryfikuje, czy odpowiedź ma oczekiwany kod statusu
    /// </summary>
    protected static void AssertStatusCode(HttpResponseMessage response, HttpStatusCode expectedStatusCode)
    {
        response.StatusCode.Should().Be(expectedStatusCode, 
            $"Expected status code {expectedStatusCode}, but got {response.StatusCode}");
    }

    /// <summary>
    /// Pobiera i deserializuje JSON z odpowiedzi
    /// </summary>
    protected async Task<JsonDocument> GetJsonResponseAsync(HttpResponseMessage response)
    {
        AssertStatusCode(response, HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        return JsonDocument.Parse(content);
    }

    /// <summary>
    /// Sprawdza, czy serwer API jest dostępny
    /// </summary>
    protected async Task<bool> IsServerAvailableAsync()
    {
        try
        {
            var response = await GetAsync("/api/v1/service/health");
            return response.StatusCode == HttpStatusCode.OK;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Czeka określoną ilość czasu
    /// </summary>
    protected static async Task WaitAsync(int milliseconds)
    {
        await Task.Delay(milliseconds);
    }

    /// <summary>
    /// Weryfikuje, czy endpoint zwraca JSON
    /// </summary>
    protected async Task VerifyJsonEndpoint(string endpoint, HttpStatusCode expectedStatusCode = HttpStatusCode.OK)
    {
        var response = await GetAsync(endpoint);
        AssertStatusCode(response, expectedStatusCode);
        
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        
        // Sprawdź, czy to prawidłowy JSON
        var action = () => JsonDocument.Parse(content);
        action.Should().NotThrow("Response should contain valid JSON");
    }

    /// <summary>
    /// Czyści dane testowe (do implementacji w klasach dziedziczących)
    /// </summary>
    protected virtual async Task CleanupTestDataAsync()
    {
        // Implementacja w klasach dziedziczących
        await Task.CompletedTask;
    }

    public virtual void Dispose()
    {
        try
        {
            CleanupTestDataAsync().Wait(5000); // 5 sekund timeout na cleanup
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd podczas czyszczenia danych testowych: {ex.Message}");
        }
        
        Client?.Dispose();
    }
}