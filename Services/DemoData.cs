using Bluba.Prediction.UI.Models;

namespace Bluba.Prediction.UI.Services;

/// <summary>
/// Datos de respaldo idénticos a la maqueta React. Sólo se usan cuando la API
/// no responde, para que la demo nunca quede en blanco.
/// </summary>
public static class DemoData
{
    public static readonly RiskGauge Gauge = new(
        Score: 13,
        Level: "Bajo",
        ConfidencePercent: 62,
        DeltaPoints: -5,
        Trend: new[]
        {
            ("Pasado ayer", 22, "Nivel bajo"),
            ("Ayer", 18, "Nivel bajo"),
            ("Hoy", 13, "Nivel bajo"),
        });

    public static IReadOnlyList<SuggestionCard> Suggestions() =>
    [
        new("headphones", "headphones",
            "Audífonos en recreo (10:00 h)",
            "Estrategia en Recreo (10:00 h)",
            "Anticipar uso de audífonos de cancelación de ruido 5 min antes del timbre, debido a descanso nocturno interrumpido con 3 despertares.",
            "Audífonos de cancelación de ruido en recreo"),
        new("hydration", "glass-water",
            "Agua + Colación crujiente (12:30 h)",
            "Soporte GI y Sensorial (12:30 h)",
            "Ofrecer 150 ml de agua y colación de textura crujiente previa a terapia, ante registro de estreñimiento al despertar.",
            "Agua (150 ml) y colación de textura crujiente"),
        new("vest", "shirt",
            "Chaleco de presión (15:30 h)",
            "Transición al Transporte (15:30 h)",
            "Colocar chaleco de presión profunda (1.5 kg) por 15 min antes de la salida, estrategia con éxito previo el 12/06.",
            "Chaleco de presión profunda (1.5 kg)"),
    ];

    public static IReadOnlyList<EpisodeRow> Episodes() =>
    [
        new("demo-1", "24 ago · 19:20", "Nivel 2", Severity.Medium, "Regreso de una actividad familiar con ruido y muchas personas."),
        new("demo-2", "12 jun · 08:35", "Nivel 3", Severity.High, "Despertar temprano luego de una noche de sueño fragmentado."),
        new("demo-3", "28 may · 17:10", "Nivel 1", Severity.Low, "Cambio inesperado en la rutina de salida."),
        new("demo-4", "20 may · 16:40", "Nivel 2", Severity.Medium, "Aumento de ruido y movimiento durante una actividad grupal."),
    ];

    public static IReadOnlyList<CareRow> Care() =>
    [
        new("Hoy • 10:15 h", "Audífonos en recreo",
            "Aplicado 5 min antes del timbre tras noche de descanso nocturno interrumpido.",
            "Logró regularse rápido", Severity.Low),
        new("Ayer • 12:45 h", "Agua (150 ml) + Colación crujiente",
            "Soporte gastrointestinal y autorregulación propioceptiva previa a Terapia Ocupacional.",
            "Sin cambios visibles", Severity.Medium),
        new("24 de Ago • 15:35 h", "Chaleco de presión profunda (1.5 kg)",
            "Aplicado durante 15 min en la transición previa al transporte escolar.",
            "Logró regularse rápido", Severity.Low),
    ];
}
