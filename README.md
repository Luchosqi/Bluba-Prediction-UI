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
"BlubaApi": { "BaseAddress": "http://localhost:8000", "TimeoutSeconds": 10, "TimeZone": "America/Santiago" }
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
- **Vocabulario de intensidad.** El asistente de desregulación envía `Nivel 1 (Activación
  leve)` … `Nivel 4 (Crisis / riesgo de seguridad)`, una escala ordinal 0-4 basada en EOQ/EDI
  (ver `docs/CAMBIO_ESCALA_DESREGULACION.md`) que reemplaza la escala previa
  `Leve/Moderada/Severa (1-10)`, sin respaldo bibliográfico. `BoardMapper.SeverityOf` reconoce
  ambos formatos para no romper el coloreado de episodios históricos; el API también expone
  `severity_level` normalizado y acepta el filtro por nivel para datos nuevos e históricos.
- **Las marcas de tiempo usan UTC en el contrato.** El API almacena UTC, devuelve `Z` y
  aplica los filtros de fecha en `America/Santiago` (configurable). El cliente convierte el
  timestamp a la zona de la pantalla antes de mostrarlo.
- **`comparison_supported` se respeta.** Cuando la API declara que la comparación no aplica
  rellena igual `risk_change` con delta 0; mostrar ese 0 se leería como "sin cambios desde
  ayer". En ese caso «Vs. ayer» muestra `—` y el diálogo explica el motivo.
- **Las preguntas adaptativas se encadenan.** Tras cada respuesta la API recalcula qué dato
  falta, así que el diálogo vuelve a pedir `adaptive-question` y sigue hasta que
  `needs_more_information` es `false`. Ver la nota sobre la confianza más abajo.
- **El resultado de una intervención va en dos llamadas** porque la API lo modela como
  inmutable: primero `POST` y luego `PATCH …/outcome`; los identificadores se conservan
  para reintentar sólo la operación que falló.
- **El ciclo de episodio recalcula el riesgo.** Tras crear una desregulación, la UI llama a
  `POST /cases/{id}/predict` con el día local y recarga medidor, estrategias, historial y
  pregunta adaptativa. Si falta el modelo, el episodio permanece guardado y se informa como
  predicción pendiente.
- **Contrato común:** la UI exige `contract_version = 0.3.0`, igual que `openapi.yaml` del API.
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
  DysregulationWizard    Registro de episodio + reintento idempotente
  InterventionWizard     Asistente de 2 pasos
```

## Política de zonas horarias

La columna `fecha_hora` se almacena como UTC sin zona en PostgreSQL por compatibilidad, pero el
contrato HTTP devuelve timestamps conscientes de zona:

| Origen | Ejemplo almacenado | Convención |
| --- | --- | --- |
| `python -m app.cli import-data` (CSV) | `2026-07-03 16:30` | se interpreta como hora Chile y se convierte a UTC |
| `POST /cases/{id}/dysregulations` | `2026-08-26T13:55-04:00` | se convierte a UTC |

El frontend muestra ambas fuentes en hora local y calcula "Hoy"/"Ayer" con la zona anunciada
por `/health`. Los rangos de fecha se envían como fechas de calendario, no como intervalos UTC
construidos por el cliente.

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
