namespace Bluba.Prediction.UI.Services;

/// <summary>
/// Opciones de respuesta de las preguntas adaptativas. Los códigos son exactamente los que
/// acepta <c>POST /cases/{id}/adaptive-responses</c>; cualquier otro valor devuelve 422.
/// </summary>
public static class AdaptiveCatalog
{
    public static readonly IReadOnlyDictionary<string, (string Title, (string Code, string Label)[] Options)> ByFeature =
        new Dictionary<string, (string, (string, string)[])>
        {
            ["sleep_quality"] = ("Calidad del sueño",
            [
                ("RESTFUL", "Reparador"),
                ("INTERRUPTED", "Interrumpido"),
                ("DIFFICULTY_FALLING_ASLEEP", "Dificultad de conciliación"),
            ]),
            ["wake_state"] = ("Estado al despertar",
            [
                ("CALM", "Tranquilo"),
                ("IRRITABLE", "Irritable / Llorando"),
            ]),
            ["regulation"] = ("Regulación durante el día",
            [
                ("EXCELLENT", "Excelente"),
                ("STABLE_WITH_SUPPORT", "Estable con apoyo"),
                ("FREQUENT_DYSREGULATION", "Desregulación frecuente"),
            ]),
            ["gastrointestinal"] = ("Estado gastrointestinal",
            [
                ("NORMAL", "Normal"),
                ("CONSTIPATION", "Estreñimiento"),
                ("DIARRHEA", "Diarrea"),
            ]),
            ["medication_adherence"] = ("Adherencia a la medicación",
            [
                ("ADHERENT", "Adherente"),
                ("PARTIAL", "Parcial"),
                ("NOT_APPLICABLE", "No aplica"),
            ]),
        };

    public static (string Title, (string Code, string Label)[] Options)? For(string? feature) =>
        feature is not null && ByFeature.TryGetValue(feature, out var entry) ? entry : null;
}
