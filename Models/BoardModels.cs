namespace Bluba.Prediction.UI.Models;

public enum Severity { Low, Medium, High }

/// <summary>Una recomendación preventiva tal como se dibuja en "Cuidado sugerido".</summary>
public sealed record SuggestionCard(
    string Id,
    string Icon,
    string Short,
    string Title,
    string Detail,
    string FullStrategy);

/// <summary>Una fila del "Historial de desregulaciones".</summary>
public sealed record EpisodeRow(
    string Id,
    string When,
    string Intensity,
    Severity Severity,
    string Context);

/// <summary>Una fila del "Historial de Cuidados Aplicados".</summary>
public sealed record CareRow(
    string When,
    string Title,
    string Detail,
    string Result,
    Severity Severity);

/// <summary>Lectura de la aguja de la Billetera Sensorial.</summary>
public sealed record RiskGauge(
    int Score,
    string Level,
    int ConfidencePercent,
    double? DeltaPoints,
    IReadOnlyList<(string Label, int Score, string Level)> Trend,
    string? ComparisonNote = null);

/// <summary>Filtros de período que ofrecen ambos historiales.</summary>
public enum PeriodFilter { Last7Days, Today, Yesterday, LastMonth, All }

public static class PeriodFilterExtensions
{
    public static string Label(this PeriodFilter filter) => filter switch
    {
        PeriodFilter.Last7Days => "Últimos 7 días",
        PeriodFilter.Today => "Hoy",
        PeriodFilter.Yesterday => "Ayer",
        PeriodFilter.LastMonth => "Último mes",
        _ => "Todo el historial",
    };

    /// <summary>Rango [from, to] inclusivo que se envía a la API; null = sin límite.</summary>
    public static (DateOnly? From, DateOnly? To) Range(this PeriodFilter filter, DateOnly today) => filter switch
    {
        PeriodFilter.Last7Days => (today.AddDays(-6), today),
        PeriodFilter.Today => (today, today),
        PeriodFilter.Yesterday => (today.AddDays(-1), today.AddDays(-1)),
        PeriodFilter.LastMonth => (today.AddMonths(-1), today),
        _ => (null, null),
    };
}
