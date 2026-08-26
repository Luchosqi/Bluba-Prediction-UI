using System.Globalization;
using Bluba.Prediction.UI.Models;

namespace Bluba.Prediction.UI.Services;

/// <summary>Traduce las respuestas de la API al vocabulario que muestra la pantalla.</summary>
public static class BoardMapper
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CL");

    /// <summary>
    /// El medidor muestra riesgo (0 = sin riesgo). La API entrega risk_probability
    /// y, de forma complementaria, wallet_score = 100 * (1 - risk_probability).
    /// </summary>
    public static RiskGauge ToGauge(RiskPrediction prediction, RiskChangeExplanation? change)
    {
        var score = (int)Math.Round(prediction.RiskProbability * 100);
        var trend = new List<(string, int, string)>();
        if (change?.PreviousPrediction is { } previous)
        {
            var previousScore = (int)Math.Round(previous.RiskProbability * 100);
            trend.Add(("Anterior", previousScore, LevelOf(previousScore)));
        }
        trend.Add(("Hoy", score, LevelOf(score)));

        // La API rellena risk_change incluso cuando declara que la comparación no aplica
        // (sin predicción previa devuelve delta 0). Mostrar ese 0 se leería como
        // "sin cambios desde ayer", que no es lo mismo que "no hay con qué comparar".
        var comparable = change is { ComparisonSupported: true };

        return new RiskGauge(
            Score: score,
            Level: RiskLevelLabel(prediction.RiskLevel),
            ConfidencePercent: (int)Math.Round(prediction.Confidence * 100),
            DeltaPoints: comparable ? change!.RiskChange.DeltaProbabilityPoints : null,
            Trend: trend,
            ComparisonNote: comparable ? null : ComparisonNoteFor(change?.Reason));
    }

    private static string? ComparisonNoteFor(string? reason) => reason switch
    {
        "NO_PREVIOUS_PREDICTION" => "Aún no hay una predicción anterior con la cual comparar.",
        "MODEL_VERSION_CHANGED" => "La versión del modelo cambió entre ambas predicciones, así que no son comparables.",
        null => "La API no entregó una comparación para este caso.",
        _ => $"La comparación no está disponible ({reason}).",
    };

    public static string RiskLevelLabel(string apiLevel) => apiLevel switch
    {
        "LOW" => "Bajo",
        "MODERATE" => "Moderado",
        "HIGH" => "Alto",
        _ => apiLevel,
    };

    public static string LevelOf(int score) => score switch
    {
        < 25 => "Nivel bajo",
        < 50 => "Nivel moderado",
        < 75 => "Nivel alto",
        _ => "Nivel muy alto",
    };

    public static IReadOnlyList<SuggestionCard> ToSuggestions(Strategies strategies)
    {
        var cards = new List<SuggestionCard>();
        var index = 0;
        foreach (var item in strategies.Items.Take(3))
        {
            var signals = item.MatchedSignals.Count > 0
                ? string.Join(", ", item.MatchedSignals)
                : "el estado registrado de las últimas 24 horas";
            cards.Add(new SuggestionCard(
                Id: $"strategy-{index++}",
                Icon: IconFor(item.Strategy),
                Short: Shorten(item.Strategy, 60),
                Title: Shorten(item.Strategy, 90),
                Detail: $"Sugerida por {signals}. Resultado previo registrado: {item.PreviousOutcome.ToLower(Es)}.",
                FullStrategy: item.Strategy));
        }
        return cards;
    }

    /// <summary>Elige el pictograma según las palabras clave de la estrategia.</summary>
    public static string IconFor(string strategy)
    {
        var text = strategy.ToLowerInvariant();
        if (text.Contains("audífono") || text.Contains("audifono") || text.Contains("ruido") || text.Contains("auditiv"))
            return "headphones";
        if (text.Contains("agua") || text.Contains("colación") || text.Contains("colacion") || text.Contains("hidrat") || text.Contains("aliment"))
            return "glass-water";
        if (text.Contains("chaleco") || text.Contains("presión") || text.Contains("presion") || text.Contains("peso"))
            return "shirt";
        return "sparkles";
    }

    public static IReadOnlyList<EpisodeRow> ToEpisodes(DysregulationHistory history) =>
        history.Items.Select(item => new EpisodeRow(
            Id: item.EventId,
            When: FormatWhen(item.OccurredAt),
            Intensity: IntensityLabel(item.Intensity),
            Severity: SeverityOf(item.Intensity),
            Context: ContextOf(item))).ToList();

    private static string ContextOf(DysregulationHistoryItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.SuspectedTrigger.RawText))
            return item.SuspectedTrigger.RawText!;
        if (item.SuspectedTrigger.Tags is { Count: > 0 })
            return $"{item.Type} · {string.Join(", ", item.SuspectedTrigger.Tags!)}";
        return item.Type;
    }

    public static IReadOnlyList<CareRow> ToCare(IEnumerable<Intervention> interventions) =>
        interventions.Select(item => new CareRow(
            When: FormatWhen(item.OccurredAt),
            Title: item.InterventionType,
            Detail: item.Description,
            Result: OutcomeLabel(item.Outcome),
            Severity: OutcomeSeverity(item.Outcome))).ToList();

    public static string OutcomeLabel(string? outcome) => outcome switch
    {
        "REGULATED" => "Logró regularse rápido",
        "PARTIAL" => "Parcial / requirió tiempo",
        "NO_CHANGE" => "Sin cambios visibles",
        "WORSENED" => "Desfavorable",
        "UNKNOWN" => "Resultado desconocido",
        _ => "Sin evaluar",
    };

    public static Severity OutcomeSeverity(string? outcome) => outcome switch
    {
        "REGULATED" => Severity.Low,
        "PARTIAL" or "NO_CHANGE" or "UNKNOWN" or null => Severity.Medium,
        _ => Severity.High,
    };

    /// <summary>La API guarda intensidades como "Moderada (4-7)"; la tarjeta muestra sólo la palabra.</summary>
    public static string IntensityLabel(string apiIntensity)
    {
        var cut = apiIntensity.IndexOf('(');
        return (cut > 0 ? apiIntensity[..cut] : apiIntensity).Trim();
    }

    public static Severity SeverityOf(string apiIntensity)
    {
        var text = apiIntensity.ToLowerInvariant();
        if (text.StartsWith("leve")) return Severity.Low;
        if (text.StartsWith("moderada")) return Severity.Medium;
        return Severity.High;
    }

    /// <summary>
    /// La API escribe sus marcas de tiempo con _naive_utc, es decir UTC sin sufijo de zona,
    /// así que System.Text.Json las entrega como Unspecified. Se interpretan como UTC y se
    /// convierten a hora local; mostrarlas crudas las corría por el desfase horario.
    /// </summary>
    public static string FormatWhen(DateTime value)
    {
        var local = value.Kind switch
        {
            DateTimeKind.Local => value,
            DateTimeKind.Utc => value.ToLocalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime(),
        };
        var today = DateTime.Now.Date;
        if (local.Date == today) return $"Hoy · {local:HH:mm} h";
        if (local.Date == today.AddDays(-1)) return $"Ayer · {local:HH:mm} h";
        return $"{local.ToString("dd MMM", Es)} · {local:HH:mm} h";
    }

    private static string Shorten(string value, int max) =>
        value.Length <= max ? value : value[..max].TrimEnd(' ', ',', '.', ';') + "…";
}
