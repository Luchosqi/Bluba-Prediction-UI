# Bluba Prediction UI

Frontend en **Blazor Server (.NET 8)** de la Ficha del Consultante. Reemplaza la maqueta
React de `../Replicar pantalla y agregar sección` y consume la
[BLUBA Predict API](../Bluba-Prediction-API).

## Arranque

```bash
dotnet run
```

Abrir <http://localhost:5163>.

La dirección de la API se configura en `appsettings.json`:

```json
"BlubaApi": { "BaseAddress": "http://localhost:8000", "TimeoutSeconds": 10 }
```

Las llamadas salen desde el servidor, no desde el navegador, así que **no se requiere CORS**
en la API.

## Qué necesita el backend para que se vea con datos reales

El frontend no trae datos: todo sale de la API. En una máquina con el backend recién clonado
hay que dejarlo en este estado (los pasos son los del README del backend):

```bash
docker compose up -d
alembic upgrade head
python -m app.cli import-data --dir ./data      # carga los CSV
python -m app.cli generate-synthetic --cases 60 --days 60 --seed 42
python -m app.cli train-model --source SYNTHETIC --seed 42   # crea artifacts/model.joblib
uvicorn app.main:app --reload
```

`artifacts/` está en el `.gitignore` del backend, así que **el modelo entrenado no viaja por
git**: cada quien tiene que correr `train-model` en su máquina.

Además, la Billetera Sensorial necesita que exista al menos una predicción para el caso. Sin
ella `GET /predictions/latest` responde 404 y la tarjeta cae a datos de ejemplo. Para generarla:

```bash
curl -X POST http://localhost:8000/cases/PAC-001/predict \
  -H 'Content-Type: application/json' -d '{"cutoff":"2026-08-26"}'
```

Con la API arriba pero sin modelo ni predicciones, la pantalla queda a medias: los historiales
y «Cuidado sugerido» sí muestran datos reales (no dependen del modelo), mientras que el medidor
usa el respaldo de ejemplo y el aviso «Faltan datos» no aparece.

## Modo demostración

Si `/health` no responde, la pestaña Predicción muestra un aviso y cae a los datos de ejemplo
de la maqueta React (`Services/DemoData.cs`), en vez de quedar en blanco. Cada sección hace lo
mismo de forma independiente: si un endpoint concreto falla o devuelve 404, esa tarjeta usa su
respaldo y el resto sigue con datos reales.

## Endpoints consumidos

| Sección de la pantalla | Endpoint |
| --- | --- |
| Selector de consultante y cabecera | `GET /cases` |
| Billetera Sensorial (aguja y confianza) | `GET /cases/{id}/predictions/latest` |
| «Vs. ayer» y diálogo Comparativa diaria | `GET /cases/{id}/predictions/latest/change-explanation` |
| Aviso «Faltan datos» junto a Confianza | `GET /cases/{id}/adaptive-question` |
| Diálogo «Completar datos del día» | `POST /cases/{id}/adaptive-responses` |
| Cuidado sugerido | `GET /cases/{id}/strategies?cutoff=` |
| Historial de desregulaciones (filtros + paginación) | `GET /cases/{id}/dysregulations` |
| Asistente «Ingresar desregulación +» | `POST /cases/{id}/dysregulations` |
| Historial de Cuidados Aplicados | `GET /cases/{id}/interventions` |
| Asistente «Registrar evento +» | `POST /cases/{id}/interventions` + `PATCH …/{id}/outcome` |
| Aviso de modo demostración | `GET /health` |

## Decisiones de mapeo

- **La aguja marca riesgo, no billetera.** La maqueta muestra 13 sobre una escala donde el
  verde está a la izquierda, así que el medidor usa `risk_probability × 100`. El campo
  `wallet_score` de la API es su complemento (`100 × (1 − risk_probability)`).
- **Los cuartiles del arco** (0-25 bajo, 25-50 moderado, 50-75 alto, 75-100 muy alto) se
  conservan tal cual venían de la maqueta; sólo el ángulo de la aguja se calcula.
- **Vocabulario de intensidad.** Los asistentes envían los valores que ya usa el dataset
  (`Leve (1-3)`, `Moderada (4-7)`, `Severa (8-10)`) para que los filtros por intensidad
  coincidan con los registros históricos.
- **Las marcas de tiempo de la API se interpretan como UTC.** La API las escribe con
  `_naive_utc()` y las devuelve sin sufijo de zona, así que el frontend las convierte a hora
  local antes de mostrarlas. Ver la advertencia de abajo.
- **`comparison_supported` se respeta.** Cuando la API declara que la comparación no aplica
  rellena igual `risk_change` con delta 0; mostrar ese 0 se leería como "sin cambios desde
  ayer". En ese caso «Vs. ayer» muestra `—` y el diálogo explica el motivo.
- **Las preguntas adaptativas se encadenan.** Tras cada respuesta la API recalcula qué dato
  falta, así que el diálogo vuelve a pedir `adaptive-question` y sigue hasta que
  `needs_more_information` es `false`. Ver la nota sobre la confianza más abajo.
- **El resultado de una intervención va en dos llamadas** porque la API lo modela como
  inmutable: primero `POST` y luego `PATCH …/outcome`.
- **Nombre del consultante.** La API entrega casos anonimizados (`PAC-001`), así que
  «Amelia Soto Neira» se mantiene como etiqueta de la maqueta y los datos clínicos reales
  (rango de edad, perfil sensorial) vienen de `GET /cases`.

## Estructura

```
Models/ApiModels.cs      DTOs del contrato openapi (snake_case)
Models/BoardModels.cs    Modelos de la vista (tarjetas, filas, medidor)
Services/BlubaApiClient  Cliente tipado; devuelve null ante error en vez de lanzar
Services/BoardMapper     Traducción API → vocabulario de la pantalla
Services/DemoData        Respaldo de la maqueta React
Components/Pages/Ficha   Página con barra lateral, cabecera y pestañas
Components/Pages/Prediction/
  PredictionBoard        Orquesta las cuatro tarjetas y sus filtros
  SensoryWalletCard      Medidor SVG + Comparativa diaria
  DysregulationWizard    Asistente de 5 pasos
  InterventionWizard     Asistente de 2 pasos
```

## Advertencia: zonas horarias inconsistentes en el backend

La columna `fecha_hora` mezcla dos convenciones y ninguna lleva sufijo de zona:

| Origen | Ejemplo almacenado | Convención |
| --- | --- | --- |
| `python -m app.cli import-data` (CSV) | `2026-07-03T16:30` | hora de pared local, sin convertir |
| `POST /cases/{id}/dysregulations` | `2026-08-26T13:55` | UTC, vía `_naive_utc()` |

Ningún cliente puede mostrar ambas correctamente. Este frontend asume **UTC**, que es la
intención declarada de la API: los registros creados desde la app se ven a la hora correcta y
las 7 filas importadas del CSV aparecen corridas por el desfase horario (−4 h en Chile).

Para invertir el criterio (CSV correcto, registros nuevos corridos), en
`Services/BoardMapper.FormatWhen` basta con devolver `value` tal cual en el caso
`DateTimeKind.Unspecified`.

La solución de fondo es del backend: importar el CSV convirtiendo a UTC, o exponer
`datetime` con zona horaria en las respuestas.

## Nota: la confianza baja con la primera respuesta

`POST /adaptive-responses` devuelve `prediction_before` y `prediction_after`, y la UI muestra
ese delta real en vez de prometer que cada respuesta sube la confianza. Medido sobre PAC-001:

| Respuesta | Confianza |
| --- | --- |
| (inicial) | 81 % |
| 1ª · calidad del sueño | **75 %** ↓ |
| 2ª · medicación | 76 % |
| 3ª · estado al despertar | 80 % |
| 4ª · gastrointestinal | 82 % |
| 5ª · regulación | **85 %** ↑ |

La primera respuesta crea el registro diario de hoy con un solo campo de cinco completo, y esa
cobertura parcial pesa más que el dato nuevo. Sólo al completar la tanda se supera el punto de
partida. Por eso el diálogo encadena las preguntas y, cuando la primera respuesta baja la
confianza, explica el motivo en lugar de dejar al usuario con un número peor sin contexto.
