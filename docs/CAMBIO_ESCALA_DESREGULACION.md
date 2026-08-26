# Cambio de contenido en formularios: desregulación, eventos, escala de severidad y confianza

**Alcance de este documento:** todos los cambios descritos ya están implementados en el
**frontend** (`Bluba-Prediction-UI`, rama `dev`). El **backend** (`Bluba-Prediction-API`)
**no fue tocado**: esta es la especificación para que se implementen ahí los cambios
correspondientes. Cada sección indica el endpoint real que consume el frontend hoy, qué
parámetros nuevos debería aceptar/exponer el backend, y la cita que sustenta cada decisión.

Motivación: el equipo detectó que varias afirmaciones de la app (escala "Leve/Moderada/Severa",
rangos numéricos 1-10, la palabra "Confianza" para una métrica del modelo, un fono de ayuda que
no corresponde al público de la app) no tenían respaldo bibliográfico o podían leerse como una
afirmación clínica más fuerte de lo que realmente son. Este cambio reemplaza ese contenido por
una versión trazable a literatura científica reciente, dejando explícito qué sí está validado y
qué es una propuesta piloto pendiente de validación propia.

---

## 1. Resumen de cambios por archivo (frontend)

| Archivo | Cambio | Por qué |
| --- | --- | --- |
| `Components/Pages/Prediction/DysregulationWizard.razor` | Paso 1 rediseñado: escala "Leve(1-3)/Moderada(4-7)/Severa(8-10)" → escala ordinal **Nivel 1-4** + duración + tiempo de recuperación + checklist de conductas de riesgo, con auto-ajuste a Nivel 4 si hay agresión física o autolesión | §2 |
| `Components/Pages/Prediction/InterventionWizard.razor` | Nota de trazabilidad: mismo criterio de resultado (regulación exitosa/parcial/sin efecto) que el registro de desregulaciones | §3 |
| `Components/Pages/Prediction/PredictionBoard.razor` | Filtro "Tipo" del historial de desregulaciones actualizado a Nivel 1-4 | §2 |
| `Services/BoardMapper.cs` | `SeverityOf()` reconoce el vocabulario nuevo y el histórico (`leve`/`moderada`/`nivel N`) para no des-colorear episodios antiguos | §2, §5 |
| `Services/DemoData.cs` | Episodios de ejemplo (modo demo sin API) actualizados a "Nivel 1"…"Nivel 3" | §2 |
| `Components/Pages/Prediction/AdaptiveQuestionDialog.razor` | "Confianza" → "Cobertura de datos" + aclaración explícita de que no es una escala clínica | §4 |
| `Components/Pages/Prediction/SensoryWalletCard.razor` | Tooltip de "Confianza" reforzado con la misma aclaración | §4 |
| `Components/Shared/MamaBlubaHelper.razor` | Se eliminó la línea **Fono SENDA (1412)** de los números de apoyo | §5 |
| `wwwroot/app.css` | Nueva clase `.opt-grid-2` (grilla 2×2 para los 4 niveles) | — |
| `README.md` | Actualizada la sección "Decisiones de mapeo" con el nuevo vocabulario | — |

---

## 2. Registro de desregulación (episodio / crisis)

### Endpoint que usa el frontend
- **Crear:** `POST /cases/{case_id}/dysregulations` — `app/api/routes.py:154`, valida contra
  `DysregulationCreateIn` (`app/schemas/case.py:159`).
- **Listar / filtrar:** `GET /cases/{case_id}/dysregulations` — `app/api/routes.py:243`, filtro
  `intensity` hace *match exacto* de string (`app/api/routes.py:264-265`).
- Tabla: `dysregulation_events`, columna `intensidad: String(100)` (`app/models/records.py:49`,
  migración `alembic/versions/0001_initial.py`).

### Qué cambió en el frontend
Antes el Paso 1 mostraba 3 botones ("Leve 1-3", "Moderada 4-7", "Severa 8-10") con rangos
numéricos **inventados**, sin ninguna fuente. Ahora:

1. **Escala ordinal Nivel 1-4** (ver tabla abajo), con nombre + descripción observable por nivel.
2. **Duración aproximada** del episodio: `<5 min`, `5–15 min`, `15–60 min`, `>60 min`.
3. **Tiempo de recuperación** (vuelta a la línea base): mismos 4 tramos.
4. **Checklist de conductas observadas**: llanto/vocalización, evitación/huida, agresión verbal,
   agresión física, autolesión.
5. Si se marca **agresión física** o **autolesión**, el nivel se ajusta automáticamente a
   **Nivel 4** (el usuario puede corregirlo manualmente si no corresponde).

Estas 4 dimensiones (intensidad, duración, recuperación, conductas/impacto) replican
directamente las que usa el **Emotional Outburst Questionnaire (EOQ)**, el instrumento con
evidencia más reciente y más cercana (incluye Chile) para caracterizar episodios de
desregulación — ver referencia [6] en §6.

### La escala Nivel 0-4 (propuesta compuesta — **no es un instrumento validado por sí solo**)

| Nivel | Nombre | Criterio observable | Valor enviado a la API (`intensity`) |
| --- | --- | --- | --- |
| 0 | Regulado | Sin señales de desregulación | *(no aplica al registrar un episodio)* |
| 1 | Activación leve | Reactividad y recuperación <5 min; se autorregula con apoyo mínimo | `Nivel 1 (Activación leve)` |
| 2 | Desregulación moderada | Reactividad y recuperación 5–15 min; requiere co-regulación activa | `Nivel 2 (Desregulación moderada)` |
| 3 | Desregulación alta | Reactividad y/o recuperación 15–60 min; pérdida funcional significativa de la rutina | `Nivel 3 (Desregulación alta)` |
| 4 | Crisis / riesgo de seguridad | Duración o recuperación >60 min, o agresión física/autolesión | `Nivel 4 (Crisis / riesgo de seguridad)` |

**Importante para el equipo:** esta tabla es una **combinación** informada por tres fuentes
(EOQ para las dimensiones del episodio, EDI para el concepto de reactividad como continuo, y el
precedente de 3 niveles discretos del CBCL-DP), tal como se concluyó en la revisión de
literatura previa. **No hay hoy un estudio que valide exactamente estos 5 niveles con estos
puntos de corte.** Es correcto decir "escala informada por literatura reciente sobre EOQ/EDI",
**no** es correcto decir "escala validada científicamente". Esa distinción debe mantenerse en
cualquier material de difusión o demo.

### Qué debe cambiar el backend (compañero)

El campo `intensity` sigue siendo un string libre (`Field(min_length=1)`, sin `Literal` ni
enum), así que **la API ya acepta los valores nuevos sin romperse**. Pero hay 3 cosas que sí
requieren trabajo de backend para que el cambio quede completo:

1. **Nueva columna normalizada para severidad**, en vez de depender de parsear el string libre:
   ```python
   # app/schemas/case.py — DysregulationCreateIn
   severity_level: int = Field(ge=0, le=4)          # Nivel 0-4, ver tabla arriba
   duration_bucket: str | None = None                # "<5min" | "5-15min" | "15-60min" | ">60min"
   recovery_bucket: str | None = None                # mismos valores que duration_bucket
   risk_behaviors: list[str] = Field(default_factory=list)  # subset de RiskBehaviors (ver wizard)
   ```
   Y en el modelo/migración: agregar `severity_level SMALLINT NOT NULL`, `duration_bucket
   VARCHAR(20)`, `recovery_bucket VARCHAR(20)`, y una tabla o columna JSON para
   `risk_behaviors` (columnas nullable para no romper filas existentes).

   **Mientras esta columna no exista**, el frontend empaqueta duración/recuperación/conductas
   como texto dentro de `suspected_trigger_text` (ej. `"Duración: 5–15 min. Recuperación: 5–15
   min. Conductas: Agresión física"`), junto con lugar/transición/desencadenante. Es un
   parche temporal: ese texto pasa además por `trigger_analyzer()` (`app/api/routes.py:190`),
   que no tiene patrones para estas frases, así que hoy no genera tags — no rompe nada, pero
   tampoco aporta.

2. **Filtro por nivel en `GET /cases/{id}/dysregulations`** debería usar la nueva columna
   `severity_level` (`Query(None, ge=0, le=4)`) en vez del match exacto sobre `intensity`
   (`app/api/routes.py:264-265`). *Mientras no exista*, el filtro del frontend (`PredictionBoard.razor`)
   sólo hace match exacto contra el string nuevo (`"Nivel 1 (Activación leve)"`, etc.), así que
   **los episodios históricos con el vocabulario viejo no aparecerán al filtrar por nivel**
   (sí siguen apareciendo en "Todos" y en el historial general).

3. **`app/services/strategy.py`, diccionario `TAG_PATTERNS["HIGH_ALERT"]` (línea 24)** hace
   *keyword matching* sobre el texto de `intensidad` buscando las palabras `"severa"` y
   `"moderada"`. Con el vocabulario nuevo (`"Nivel 4 (Crisis...)"`) esas palabras ya no
   aparecen, así que el tag `HIGH_ALERT` **dejará de dispararse** para las estrategias
   recomendadas hasta que se actualice ese patrón (agregar `"nivel 3"`, `"nivel 4"`, `"crisis"`,
   `"riesgo de seguridad"`, o mejor: usar directamente `severity_level >= 3` una vez exista la
   columna).

4. Opcional pero recomendado: `app/synthetic/generator.py:101-102` genera los eventos
   sintéticos con el vocabulario viejo (`"Severa (8-10)"`, etc.) para entrenar el modelo. Si se
   decide migrar el dataset sintético al vocabulario nuevo, hay que regenerar y reentrenar
   (`generate-synthetic` + `train-model`, ver `README.md` del backend).

---

## 3. Registro de eventos (aplicar una intervención/sugerencia)

### Endpoint que usa el frontend
- **Crear:** `POST /cases/{case_id}/interventions` (`app/api/routes.py`, `InterventionCreateIn`,
  `app/schemas/case.py:110`).
- **Registrar resultado:** `PATCH /cases/{case_id}/interventions/{id}/outcome`
  (`InterventionOutcomeIn`, `app/schemas/case.py:119`), con `outcome: Literal["REGULATED",
  "PARTIAL", "NO_CHANGE", "WORSENED", "UNKNOWN"]`.

### Qué cambió
Este formulario **no usa** la escala Leve/Moderada/Severa: ya usa una categorización de
resultado de 3 opciones (`REGULATED`/`PARTIAL`/`WORSENED`, mapeadas en la UI a "Logró
regularse", "Parcial", "No funcionó"), que es una categorización operacional estándar de
seguimiento de intervención, no una escala clínica de severidad. No requería reemplazo por
literatura; el único cambio fue agregar una nota en el paso 2 explicitando que usa **el mismo
criterio de resultado** que el registro de desregulaciones, para que ambos formularios queden
trazables entre sí (un episodio → la intervención aplicada → su resultado).

**No se requiere ningún cambio de backend para este punto.**

---

## 4. "Nivel de confianza" del modelo (Billetera Sensorial / diálogo adaptativo)

### Endpoint que usa el frontend
- `GET /cases/{id}/predictions/latest` (`RiskPrediction.confidence`, `app/schemas/case.py:39`).
- `GET /cases/{id}/adaptive-question` + `POST /cases/{id}/adaptive-responses`
  (`AdaptivePredictionOut.prediction_before/after.confidence`).

### Qué se revisó y qué cambió
Se revisó todo el código en busca de una escala de "confianza" autoreportada por la familia
(similar a las escalas clínicas de autoeficacia parental); **no existe tal pregunta en la app
hoy** — el único uso de la palabra "confianza" es el campo `confidence` que devuelve el modelo
de riesgo, una métrica estadística de **cobertura de datos del día**, no una escala clínica ni
un puntaje de certeza diagnóstica.

Como esa palabra puede leerse como una afirmación más fuerte de lo que es (ver motivación al
inicio del documento), se cambió el copy en dos lugares:

- `AdaptiveQuestionDialog.razor`: la etiqueta pasó de "Confianza" a **"Cobertura de datos"**, y
  se agregó la frase: *"Este porcentaje mide la cobertura de datos que el modelo tiene
  disponibles hoy para este caso; no es una escala clínica ni un diagnóstico de certeza."*
- `SensoryWalletCard.razor`: el tooltip del estadístico "Confianza" (que se mantiene con ese
  nombre en la tarjeta principal por espacio) se reforzó con la misma aclaración.

**No se requiere ningún cambio de backend para este punto** — es puramente de copy/UX. Si el
equipo quiere, a futuro, ofrecer una escala de autoeficacia/confianza parental real y validada,
eso sería un instrumento nuevo (ej. *Tolerance for Uncertainty*, *Parenting Sense of Competence
Scale*) y quedaría fuera del alcance de este cambio — no hay literatura de eso en la revisión
que motivó este documento.

---

## 5. Números de apoyo (panel "Mamá Bluba")

No consume ningún endpoint del backend: es una lista estática en
`Components/Shared/MamaBlubaHelper.razor`. Se eliminó la línea:

```
("1412", "1412", "Fono SENDA · Apoyo drogas/alcohol"),
```

Motivo: SENDA (Servicio Nacional para la Prevención y Rehabilitación del Consumo de Drogas y
Alcohol) es una línea de orientación en consumo de drogas y alcohol, sin relación con el
público de la app (familias de niños con neurodivergencias). Quedaron las líneas pertinentes:
Prevención Suicidio (`*4141`), SAMU (`131`), Salud Responde/MINSAL (`600 360 7777`), Fono
SernamEG (`1455`) y Fono Familia/Carabineros (`149`).

---

## 6. Referencias bibliográficas (las "leyes" que sustentan cada cambio)

Formato APA 7. Todas fueron recopiladas en la revisión previa del equipo; se listan aquí con el
lugar exacto donde se usan en el frontend.

| # | Referencia | Dónde se usa |
| --- | --- | --- |
| [1] | Mazefsky, C. A., Day, T. N., Siegel, M., White, S. W., Yu, L., Pilkonis, P. A., & Autism and Developmental Disabilities Inpatient Research Collaborative. (2018). Development of the Emotion Dysregulation Inventory: A PROMIS®ing method for creating sensitive and unbiased questionnaires for autism spectrum disorder. *Journal of Autism and Developmental Disorders, 48*(11), 3736–3746. https://doi.org/10.1007/s10803-016-2907-1 | Concepto base "desregulación como continuo" citado en el hint del Paso 1 de `DysregulationWizard.razor` ("EDI, Mazefsky et al., 2018") |
| [2] | Mazefsky, C. A., Yu, L., White, S. W., Siegel, M., & Pilkonis, P. A. (2018). The emotion dysregulation inventory: Psychometric properties and item response theory calibration in an autism spectrum disorder sample. *Autism Research, 11*(6), 928–941. https://doi.org/10.1002/aur.1947 | Fundamento de fondo (dimensiones Reactividad/Disforia, umbral clínico T≥60) para tratar la severidad como continuo en vez de 3 cajones arbitrarios. No se cita literalmente en la UI |
| [3] | Day, T. N., Mazefsky, C. A., Yu, L., Zeglen, K. N., Neece, C. L., & Pilkonis, P. A. (2024). The Emotion Dysregulation Inventory-Young Child: Psychometric properties and item response theory calibration in 2- to 5-year-olds. *Journal of the American Academy of Child & Adolescent Psychiatry, 63*(1), 52–64. https://doi.org/10.1016/j.jaac.2023.04.021 | Antecedente para trabajo futuro con población 2–5 años; no implementado aún en esta app |
| [4] | Kuo, C.-Y., Liu, C.-H., Huang, Y.-C., Liang, S. H.-Y., Lin, H.-Y., & Ni, H.-C. (2025). Psychometric properties of the Taiwan version of Emotion Dysregulation Inventory in Autism Spectrum Disorder. *Journal of the Formosan Medical Association, 124*(6), 523–528. https://doi.org/10.1016/j.jfma.2024.11.015 | Respalda la validez convergente EDI↔CBCL-DP usada como argumento para combinar ambos marcos en el diseño de la escala Nivel 0-4 (§2) |
| [5] | Treviño, M. S., & Gerstein, E. D. (2026). Evaluating emotion dysregulation in autism: Validation and application of the Emotion Dysregulation Inventory to identify subgroup profiles. *Journal of Autism and Developmental Disorders*. Advance online publication. https://doi.org/10.1007/s10803-026-07238-y | Respalda usar perfiles/niveles discretos en vez de sólo un puntaje continuo — justifica el enfoque de "Nivel 1-4" del Paso 1 |
| [6] | Teixeira, M. C. T. V., Lowenthal, R., Rattazzi, A., Cukier, S., Valdez, D., Garcia, R., Garrido Candela, G., Rosoli Murillo, A., Pereira da Silva Leite, F., Pinheiro, G., Woodcock, K., Chung, J. C. Y., Mevorach, C., Montiel-Nava, C., & Silvestre Paula, C. (2024). Understanding emotional outbursts: A cross-cultural study in Latin American children with autism spectrum disorder. *Brain Sciences, 14*(10), 1010. https://doi.org/10.3390/brainsci14101010 | **Fuente directa** de las dimensiones intensidad/duración/recuperación/conductas del Paso 1 (`DysregulationWizard.razor`); incluye muestra chilena (15,2%) |
| [7] | Estudio de *Scientific Reports* (2024) sobre comparación intra-sujeto de crisis más y menos severas en autismo (agresión, autolesión y activación fisiológica asociadas a mayor severidad) | Fundamenta la regla de auto-ajuste a Nivel 4 al marcar agresión física/autolesión en el checklist del Paso 1. **Pendiente:** completar autor(es)/DOI exactos — no se registró la cita completa en la revisión original; verificar antes de citarlo en un informe formal |
| [8] | Artículo (2026). Cognitive, Behavioural and Communication Correlates of Dysregulation in Australian Autistic Preschoolers. *Journal of Autism and Developmental Disorders*. https://doi.org/10.1007/s10803-026-07387-0 | Precedente de **tiers discretos** (CBCL-DP: típico / moderado / severo) usado como inspiración para tener niveles discretos en vez de un puntaje continuo — no se usa el CBCL-DP en sí, sólo el precedente metodológico |

⚠️ **Pendiente de verificar:** la referencia [7] no se registró con autor/DOI completos en la
consulta bibliográfica original. Antes de citarla en cualquier documento externo (paper, informe
a un comité de ética, material de difusión), hay que recuperar la cita completa desde la fuente
original o remplazarla por una equivalente verificada.

---

## 7. Riesgos y límites de este cambio (frontend-only)

- El **filtro por nivel** del historial de desregulaciones no es retrocompatible con datos
  antiguos (§2, punto 2) hasta que el backend agregue `severity_level`.
- El tag `HIGH_ALERT` de `strategy.py` deja de dispararse con el vocabulario nuevo (§2, punto 3)
  hasta que se actualicen sus patrones — esto puede degradar silenciosamente la calidad de las
  recomendaciones de "Cuidado sugerido" para casos de alta severidad.
- La escala Nivel 0-4 es una **propuesta compuesta**, no un instrumento publicado y validado en
  sí mismo. Cualquier comunicación externa debe dejarlo explícito (ver §2).
- La referencia [7] necesita completarse antes de usarse fuera de este documento interno.

---

## 8. Guion de demo (≈ 1:30 min)

Pensado para que el compañero que presenta la demo lo siga paso a paso. Requiere la app corriendo
contra la API real (o en modo demo si `/health` no responde — igual funciona, cae a `DemoData.cs`
ya actualizado).

| Tiempo | Acción | Qué decir |
| --- | --- | --- |
| 0:00–0:10 | Abrir la Ficha del Consultante, pestaña **"Predicción (Nuevo)"** | "Esta es la Billetera Sensorial. Antes decía 'Confianza X%' como si fuera una certeza clínica; ahora aclara que es cobertura de datos del modelo, no un diagnóstico." — pasar el mouse sobre el estadístico para mostrar el tooltip nuevo |
| 0:10–0:25 | Click en **"Ingresar desregulación +"** | "Registrar una crisis ya no usa una escala de 'Leve/Moderada/Severa' inventada. Ahora son 4 niveles con criterios observables, basados en el EOQ y el EDI — dos instrumentos con evidencia reciente, uno de ellos validado con muestra chilena." |
| 0:25–0:45 | Elegir **Nivel 2**, marcar duración **"5–15 min"** y recuperación **"5–15 min"**, luego marcar **"Agresión física"** en el checklist | "Si aparece agresión o autolesión, el sistema sube automáticamente a Nivel 4 — está empíricamente asociado a episodios más severos" — señalar el aviso amarillo que aparece explicando el ajuste |
| 0:45–0:55 | Avanzar rápido: Paso 2 (lugar/transición) → Paso 3 (desencadenante) | Sin detenerse, sólo clickear una opción de cada uno |
| 0:55–1:10 | Paso 4: elegir un apoyo y una efectividad → **Guardar registro** | "El registro queda guardado en el historial ya con el nivel correcto" — mostrar que aparece "Nivel 4" en "Historial de desregulaciones" |
| 1:10–1:20 | Abrir el panel flotante **"Mamá Bluba"** (esquina inferior) → "Ver más líneas de ayuda" | "Sacamos el Fono SENDA: es la línea de drogas y alcohol, no corresponde a este público. Quedaron las líneas que sí aplican." |
| 1:20–1:30 | Cierre | "Todo esto está documentado con las citas exactas y los cambios pendientes de backend en `docs/CAMBIO_ESCALA_DESREGULACION.md`, en la rama `dev`." |
