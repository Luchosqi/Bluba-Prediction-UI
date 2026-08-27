using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bluba.Prediction.UI.Models;

namespace Bluba.Prediction.UI.Services;

/// <summary>
/// Cliente tipado sobre BLUBA Predict API. Cada método devuelve null cuando la API
/// no responde o devuelve un error, para que la pantalla pueda caer a datos demo
/// en vez de romperse.
/// </summary>
public sealed class BlubaApiClient(HttpClient http, ILogger<BlubaApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public const string ContractVersion = "0.3.0";

    /// <summary>Detalle de la última llamada fallida; se lee justo después de la llamada.</summary>
    public string? LastError { get; private set; }
    public HealthStatus? LastHealth { get; private set; }

    /// <summary>Sondeo explícito de /health: es lo que decide si la pantalla entra en modo demo.</summary>
    public async Task<bool> IsOnlineAsync(CancellationToken ct = default)
    {
        LastHealth = null;
        HealthStatus? health;
        try
        {
            var response = await http.GetAsync("/health", ct);
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                health = await response.Content.ReadFromJsonAsync<HealthStatus>(JsonOptions, ct);
                LastHealth = health;
                LastError = "/health: base de datos no disponible";
                return false;
            }

            health = await ReadAsync<HealthStatus>(response, "/health", ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return Fail<bool>("/health", ex) ?? false;
        }

        LastHealth = health;
        if (health is null) return false;
        if (!string.Equals(health.ContractVersion, ContractVersion, StringComparison.Ordinal))
        {
            LastError = $"/health: contrato incompatible ({health.ContractVersion}; se esperaba {ContractVersion})";
            return false;
        }
        return string.Equals(health.Database, "ok", StringComparison.OrdinalIgnoreCase);
    }

    public Task<List<CaseSummary>?> GetCasesAsync(CancellationToken ct = default) =>
        GetAsync<List<CaseSummary>>("/cases", ct);

    public Task<CaseSummary?> GetCaseAsync(string caseId, CancellationToken ct = default) =>
        GetAsync<CaseSummary>($"/cases/{Uri.EscapeDataString(caseId)}", ct);

    public Task<RiskPrediction?> GetLatestPredictionAsync(string caseId, CancellationToken ct = default) =>
        GetAsync<RiskPrediction>($"/cases/{Uri.EscapeDataString(caseId)}/predictions/latest", ct);

    public Task<RiskPrediction?> PredictAsync(
        string caseId, DateOnly cutoff, CancellationToken ct = default) =>
        PostAsync<PredictRequest, RiskPrediction>(
            $"/cases/{Uri.EscapeDataString(caseId)}/predict",
            new PredictRequest(cutoff),
            ct);

    public Task<RiskChangeExplanation?> GetChangeExplanationAsync(string caseId, CancellationToken ct = default) =>
        GetAsync<RiskChangeExplanation>($"/cases/{Uri.EscapeDataString(caseId)}/predictions/latest/change-explanation", ct);

    public Task<AdaptiveQuestion?> GetAdaptiveQuestionAsync(string caseId, CancellationToken ct = default) =>
        GetAsync<AdaptiveQuestion>($"/cases/{Uri.EscapeDataString(caseId)}/adaptive-question", ct);

    public Task<AdaptiveAnswerResult?> SubmitAdaptiveAnswerAsync(
        string caseId, AdaptiveAnswerCreate payload, CancellationToken ct = default) =>
        PostAsync<AdaptiveAnswerCreate, AdaptiveAnswerResult>(
            $"/cases/{Uri.EscapeDataString(caseId)}/adaptive-responses", payload, ct);

    public Task<Strategies?> GetStrategiesAsync(string caseId, DateOnly cutoff, CancellationToken ct = default) =>
        GetAsync<Strategies>($"/cases/{Uri.EscapeDataString(caseId)}/strategies?cutoff={cutoff:yyyy-MM-dd}", ct);

    public Task<DysregulationHistory?> GetDysregulationsAsync(
        string caseId,
        int limit = 20,
        int offset = 0,
        DateOnly? from = null,
        DateOnly? to = null,
        string? intensity = null,
        CancellationToken ct = default)
    {
        var query = new List<string> { $"limit={limit}", $"offset={offset}" };
        if (from is { } f) query.Add($"from={f:yyyy-MM-dd}");
        if (to is { } t) query.Add($"to={t:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(intensity)) query.Add($"intensity={Uri.EscapeDataString(intensity)}");
        return GetAsync<DysregulationHistory>(
            $"/cases/{Uri.EscapeDataString(caseId)}/dysregulations?{string.Join('&', query)}", ct);
    }

    public Task<DysregulationCreated?> CreateDysregulationAsync(string caseId, DysregulationCreate payload, CancellationToken ct = default) =>
        PostAsync<DysregulationCreate, DysregulationCreated>($"/cases/{Uri.EscapeDataString(caseId)}/dysregulations", payload, ct);

    public Task<List<Intervention>?> GetInterventionsAsync(
        string caseId, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (from is { } f) query.Add($"from={f:yyyy-MM-dd}");
        if (to is { } t) query.Add($"to={t:yyyy-MM-dd}");
        var suffix = query.Count > 0 ? "?" + string.Join('&', query) : string.Empty;
        return GetAsync<List<Intervention>>($"/cases/{Uri.EscapeDataString(caseId)}/interventions{suffix}", ct);
    }

    public Task<Intervention?> CreateInterventionAsync(string caseId, InterventionCreate payload, CancellationToken ct = default) =>
        PostAsync<InterventionCreate, Intervention>($"/cases/{Uri.EscapeDataString(caseId)}/interventions", payload, ct);

    public async Task<Intervention?> UpdateInterventionOutcomeAsync(
        string caseId, int interventionId, InterventionOutcomeUpdate payload, CancellationToken ct = default)
    {
        var path = $"/cases/{Uri.EscapeDataString(caseId)}/interventions/{interventionId}/outcome";
        try
        {
            var response = await http.PatchAsJsonAsync(path, payload, JsonOptions, ct);
            return await ReadAsync<Intervention>(response, path, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return Fail<Intervention>(path, ex);
        }
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct)
    {
        try
        {
            var response = await http.GetAsync(path, ct);
            return await ReadAsync<T>(response, path, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return Fail<T>(path, ex);
        }
    }

    private async Task<TOut?> PostAsync<TIn, TOut>(string path, TIn payload, CancellationToken ct)
    {
        try
        {
            var response = await http.PostAsJsonAsync(path, payload, JsonOptions, ct);
            return await ReadAsync<TOut>(response, path, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return Fail<TOut>(path, ex);
        }
    }

    private async Task<T?> ReadAsync<T>(HttpResponseMessage response, string path, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var detail = response.StatusCode is HttpStatusCode.NotFound
                ? "sin datos en la API"
                : await SafeDetailAsync(response, ct);
            LastError = $"{(int)response.StatusCode} {path}: {detail}";
            logger.LogWarning("BLUBA API {Path} respondió {Status}: {Detail}", path, (int)response.StatusCode, detail);
            return default;
        }

        LastError = null;
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
    }

    private static async Task<string> SafeDetailAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return body.Length > 200 ? body[..200] : body;
        }
        catch
        {
            return response.ReasonPhrase ?? "error";
        }
    }

    private T? Fail<T>(string path, Exception ex)
    {
        LastError = $"{path}: {ex.Message}";
        logger.LogWarning(ex, "BLUBA API {Path} no disponible", path);
        return default;
    }
}
