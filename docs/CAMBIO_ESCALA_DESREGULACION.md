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

**Registro de revisiones**

- **2026-08-26 (v1):** escala de severidad, "Confianza", Fono SENDA (§2–§5).
- **2026-08-26 (v2):** una segunda revisión (code review) detectó 4 elementos que habían
  quedado sin revisar en v1: preguntas de check-in diario, lista de "formas de calmar",
  categorías de lugar/transición/desencadenante, y los colores/cortes del medidor de riesgo.
  Se agregó la sección **§8** con la justificación bibliográfica de cada uno (o la señal
  explícita de que no la tiene, en el caso del medidor).
- **2026-08-26 (v3):** se implementaron en código las 2 recomendaciones de §8 que seguían
  pendientes: la nota de evidencia débil para el chaleco de presión (§8.2) y la marca del
  `decision_threshold` real de la API sobre el medidor de riesgo (§8.4). Probado en navegador
  contra la API real.

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
| `Services/AdaptiveCatalog.cs` (check-in diario) | **Sin cambio de código** — revisado y justificado por primera vez en v2 | §8.1 |
| `DysregulationWizard.razor` (`Supports`, "formas de calmar") | Nota condicional agregada al elegir "Presión profunda / Chaleco" (evidencia débil/mixta) | §8.2 |
| `DysregulationWizard.razor` (`Locations`, `Transitions`, `Triggers`) | **Sin cambio de código** — justificado bajo el marco de Evaluación Funcional de Conducta (FBA) | §8.3 |
| `SensoryWalletCard.razor` + `Models/BoardModels.cs` + `Services/BoardMapper.cs` (medidor) | **Sin justificación clínica para los cuartiles de color** (se mantienen por legibilidad) — se agregó la marca del `decision_threshold` real de la API sobre el arco + nota aclaratoria | §8.4 |

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
| [9] | Owens, J. A., Spirito, A., & McGuinn, M. (2000). The Children's Sleep Habits Questionnaire (CSHQ): Psychometric properties of a survey instrument for school-aged children. *Sleep, 23*(8), 1043–1051. | Base de las subescalas de sueño (conciliación, despertares nocturnos) usadas como fundamento de las opciones de "Calidad del sueño" (§8.1) |
| [10] | Mazurek, M. O., & Sohl, K. (2016). Sleep and behavioral problems in children with autism spectrum disorder. *Journal of Autism and Developmental Disorders, 46*(6), 1906–1915. https://doi.org/10.1007/s10803-016-2723-7 | Asociación entre problemas de sueño y conducta en autismo (§8.1) |
| [11] | Goldman, S. E., McGrew, S., Johnson, K. P., Richdale, A. L., Clemons, T., & Malow, B. A. (2011). Sleep is associated with problem behaviors in children and adolescents with Autism Spectrum Disorders. *Research in Autism Spectrum Disorders, 5*(3), 1223–1229. https://doi.org/10.1016/j.rasd.2011.01.010 | Sustenta la pregunta "Estado al despertar" (irritabilidad matutina ligada al descanso) (§8.1) |
| [12] | Chaidez, V., Hansen, R. L., & Hertz-Picciotto, I. (2014). Gastrointestinal problems in children with autism, developmental delays or typical development. *Journal of Autism and Developmental Disorders, 44*(5), 1117–1127. https://doi.org/10.1007/s10803-013-1973-x | Prevalencia de problemas GI en autismo, base de la pregunta "Estado gastrointestinal" (§8.1) |
| [13] | Mazurek, M. O., Vasa, R. A., Kalb, L. G., Kanne, S. M., Rosenberg, D., Keefer, A., Murray, D. S., Freedman, B., & Lowery, L. A. (2013). Anxiety, sensory over-responsivity, and gastrointestinal problems in children with autism spectrum disorders. *Journal of Abnormal Child Psychology, 41*(1), 165–176. https://doi.org/10.1007/s10802-012-9668-x | Vincula síntomas GI con ansiedad/desregulación, no sólo con malestar físico (§8.1) |
| [14] | Case-Smith, J., Weaver, L. L., & Fristad, M. A. (2015). A systematic review of sensory processing interventions for children with autism spectrum disorders. *Autism, 19*(2), 133–148. https://doi.org/10.1177/1362361313517762 | Revisión sistemática de intervenciones sensoriales (base de "Audífonos" y estrategias sensoriales en general) (§8.2) |
| [15] | Watling, R., & Hauer, S. (2015). Effectiveness of Ayres Sensory Integration® and sensory-based interventions for people with autism spectrum disorder: A systematic review. *American Journal of Occupational Therapy, 69*(5), 6905180030p1–6905180030p12. https://doi.org/10.5014/ajot.2015.018051 | Segunda revisión sistemática independiente sobre intervenciones sensoriales (§8.2) |
| [16] | Steinbrenner, J. R., Hume, K., Odom, S. L., Morin, K. L., Nowell, S. W., Tomaszewski, B., Szendrey, S., McIntyre, N. S., Yücesoy-Özkan, S., & Savage, M. N. (2020). *Evidence-based practices for children, youth, and young adults with Autism.* National Clearinghouse on Autism Evidence and Practice Review Team, University of North Carolina at Chapel Hill. | Clasifica "Apoyo visual" y "Intervención basada en antecedentes" como prácticas basadas en evidencia — base de "Apoyo visual/Pictogramas" y "Espacio seguro/Calma" (§8.2) |
| [17] | Stephenson, J., & Carter, M. (2009). The use of weighted vests with children with autism spectrum disorders and other disabilities. *Journal of Autism and Developmental Disorders, 39*(1), 105–114. https://doi.org/10.1007/s10803-008-0605-3 | Revisión específica de chalecos de presión: concluye evidencia **débil/mixta** — motiva la recomendación de agregar una nota de cautela a esa opción (§8.2) |
| [18] | O'Neill, R. E., Horner, R. H., Albin, R. W., Sprague, J. R., Storey, K., & Newton, J. S. (1997). *Functional Assessment and Program Development for Problem Behavior: A Practical Handbook* (2nd ed.). Brooks/Cole. | Manual de referencia de Evaluación Funcional de Conducta (FBA); base de las categorías de lugar/transición/desencadenante (§8.3) |
| [19] | Iwata, B. A., Dorsey, M. F., Slifer, K. J., Bauman, K. E., & Richman, G. S. (1994). Toward a functional analysis of self-injury. *Journal of Applied Behavior Analysis, 27*(2), 197–209. (Reimpresión del original de 1982) | Artículo fundacional del análisis funcional de la conducta (§8.3) |
| [20] | Flannery, K. B., & Horner, R. H. (1994). The relationship between predictability and problem behavior for students with severe disabilities. *Journal of Behavioral Education, 4*(2), 157–176. | Sustenta por qué "transición previa" es un antecedente relevante a registrar (§8.3). **Verificar cita exacta** antes de usar en un informe formal |
| [21] | Youden, W. J. (1950). Index for rating diagnostic tests. *Cancer, 3*(1), 32–35. | Referencia clásica de optimización de un punto de corte a partir de datos, en vez de un valor arbitrario — precedente metodológico de `optimize_alert_threshold` (§8.4) |
| [22] | Vickers, A. J., & Elkin, E. B. (2006). Decision curve analysis: a novel method for evaluating prediction models. *Medical Decision Making, 26*(6), 565–574. https://doi.org/10.1177/0272989X06295361 | Metodología de selección de umbral bajo costos asimétricos — coincide con lo que ya hace `training.py: optimize_alert_threshold` (§8.4) |
| [23] | Fagerlin, A., Zikmund-Fisher, B. J., & Ubel, P. A. (2011). Helping patients decide: ten steps to better risk communication. *Journal of the National Cancer Institute, 103*(19), 1436–1443. https://doi.org/10.1093/jnci/djr318 | Buenas prácticas de comunicación de riesgo: anclar colores/cortes a algo interpretable, no a una división matemática arbitraria (§8.4) |

⚠️ **Pendiente de verificar:** las referencias [7] y [20] no se registraron con datos
bibliográficos 100% completos/contrastados en la revisión original. Antes de citarlas en
cualquier documento externo (paper, informe a un comité de ética, material de difusión), hay que
confirmar la cita exacta desde la fuente original o reemplazarlas por una equivalente verificada.
El resto de las referencias ([1]–[6], [8]–[19] salvo [20], y [21]–[23]) corresponden a trabajos
ampliamente citados y con datos bibliográficos consistentes, pero de todas formas se recomienda
verificarlas contra la fuente original antes de usarlas en una publicación o presentación a un
comité de ética.

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
- **(v2, implementado en v3)** El medidor de riesgo usa cuartiles de color (25/50/75) sin
  respaldo clínico. Se mapeó `decision_threshold` de la API al medidor y se dibuja como marca
  sobre el arco, con nota aclaratoria — ver §8.4. Los cuartiles de color siguen siendo una
  convención visual (se mantienen por legibilidad), ahora con esa salvedad explícita en pantalla.
- **(v2, implementado en v3)** "Presión profunda / Chaleco" en la lista de formas de calmar
  tiene evidencia débil/mixta en la literatura ([17]) — se agregó una nota condicional en el
  wizard cuando se selecciona esa opción. Ver §8.2.

---

## 8. Elementos revisados en la segunda pasada (v2)

Estos 4 puntos los marcó una revisión de código posterior a v1 como "quedó como estaba, sin
revisión". Se investigó cada uno; el resultado no es igual para los 4: los primeros tres sí
tienen respaldo bibliográfico razonable y no requieren cambio de código, mientras que el último
(el medidor) **no tiene** un corte clínico que lo justifique y sí requiere una decisión del
equipo.

### 8.1 Check-in diario (`Services/AdaptiveCatalog.cs`)

| Pregunta | Opciones actuales | Justificación | Cita |
| --- | --- | --- | --- |
| Calidad del sueño | Reparador / Interrumpido / Dificultad de conciliación | Los problemas de sueño (despertares nocturnos, latencia de conciliación) son uno de los correlatos conductuales mejor documentados en autismo; estas tres categorías reflejan subescalas estándar de instrumentos de sueño pediátrico | [9], [10] |
| Estado al despertar | Tranquilo / Irritable-Llorando | La irritabilidad matutina como consecuencia de mal descanso nocturno está documentada específicamente en autismo | [11] |
| Regulación durante el día | Excelente / Estable con apoyo / Desregulación frecuente | Es una versión de check-in de 1 pregunta del mismo continuo de reactividad que mide el EDI (ya citado en §2) — no es un instrumento nuevo, es una simplificación operacional del mismo constructo | [1], [2] |
| Estado gastrointestinal | Normal / Estreñimiento / Diarrea | Los síntomas gastrointestinales se asocian consistentemente con mayor ansiedad, irritabilidad y sobre-respuesta sensorial en niños autistas | [12], [13] |
| Adherencia a la medicación | Adherente / Parcial / No aplica | **No es una escala clínica**, es seguimiento operacional estándar de adherencia terapéutica (igual que "resultado de intervención" en §3). No se buscó ni se necesita una escala psicométrica para esto — sólo se deja explícito para que no se confunda con las demás preguntas | — |

**Conclusión:** no se requiere cambiar texto ni opciones; las 4 primeras preguntas ya eran
razonables y ahora quedan documentadas, y la de medicación queda explícitamente marcada como
"seguimiento operacional, no escala clínica" (mismo criterio aplicado a "Confianza" en §4).

### 8.2 Formas de calmar (lista `Supports` del wizard de desregulación)

| Opción | Justificación | Cita |
| --- | --- | --- |
| Audífonos (cancelación de ruido) | Estrategias de bloqueo/reducción auditiva están dentro de las intervenciones sensoriales con evidencia revisada sistemáticamente en autismo | [14], [15] |
| Apoyo visual / Pictogramas | Es una de las prácticas **con mayor nivel de evidencia** en autismo según la revisión sistemática más citada del campo (National Clearinghouse on Autism Evidence and Practice) | [16] |
| Espacio seguro / Calma | Corresponde a "Intervención basada en antecedentes" (Antecedent-Based Intervention), también clasificada como práctica basada en evidencia | [16] |
| Presión profunda / Chaleco | **Evidencia débil/mixta.** La revisión específica sobre chalecos de presión en autismo concluyó que la evidencia disponible era insuficiente para sustentar su uso como intervención basada en evidencia | [17] |

**Implementado (v3):** no se sacó el chaleco de presión (sigue siendo una estrategia usada en la
práctica real y reportada por familias). Se agregó una nota condicional en
`DysregulationWizard.razor`, Paso 4: al seleccionar "Presión profunda / Chaleco" aparece *"Evidencia
limitada/mixta en la literatura (Stephenson & Carter, 2009): úsalo sólo si ya fue acordado con el
equipo terapéutico"*, con la misma clase `wizard__notice` ya usada para el aviso de auto-ajuste
de nivel (§2), sin agregar componentes ni CSS nuevos.

### 8.3 Lugar, transición previa y desencadenante (`Locations`, `Transitions`, `Triggers`)

Estas tres listas no son una escala clínica: son categorías de **antecedentes**, el componente
central de la Evaluación Funcional de Conducta (*Functional Behavior Assessment*, FBA), el marco
más establecido en la literatura de análisis conductual aplicado para registrar dónde, cuándo y
qué precede a un episodio. "Lugar" y "transición previa" corresponden al concepto de *setting
events* / antecedentes inmediatos; "desencadenante" corresponde al estímulo discriminativo. Este
marco es exactamente lo que hace el wizard con sus 3 preguntas del Paso 2 y 3.

| Cita | Qué aporta |
| --- | --- |
| [18] | Manual de referencia del método FBA: cómo identificar antecedentes (lugar, rutina, transición) previo a un episodio |
| [19] | Artículo fundacional del análisis funcional como metodología para vincular antecedentes con la conducta problema |
| [20] | Evidencia específica de que la previsibilidad de las transiciones se relaciona con la frecuencia de conducta problema — sustenta por qué "transición previa" es una pregunta relevante y no arbitraria |

**Conclusión:** las categorías actuales (Escuela/Aula, Recreo, Furgón/Trayecto, Casa/Entrada,
Terapia; Salida del colegio, Cambio de actividad, Llegada a casa, Sin cambio evidente) son
consistentes con cómo la literatura de FBA agrupa antecedentes. No se requiere cambiarlas.

### 8.4 Colores y cortes del medidor de riesgo (Billetera Sensorial)

Este es el único de los 4 puntos donde la respuesta honesta es **"no hay literatura clínica que
justifique estos cortes específicos"**, y hay que decirlo así en vez de forzar una cita.

**Qué son hoy:** `SensoryWalletCard.razor` dibuja un arco de 4 cuartiles fijos (0-25 verde,
25-50 amarillo, 50-75 naranja, 75-100 rojo) sobre `risk_probability × 100`. Son cuartiles
matemáticos parejos (25 puntos cada uno) heredados de la maqueta original — **no** están
calibrados contra ningún desenlace real del modelo.

**El hallazgo importante:** la propia API **ya calcula** un punto de corte con fundamento
estadístico y no lo estamos usando. `app/prediction/training.py:101`
(`optimize_alert_threshold`) busca, sobre datos de calibración, el umbral que minimiza el costo
combinado de falsos negativos y falsos positivos (con costos configurables), y ese valor viaja
en la respuesta de `GET /cases/{id}/predictions/latest` como `decision_threshold` +
`alert_triggered` (`app/schemas/case.py:47-48`, ya modelado en el frontend en
`Models/ApiModels.cs: RiskPrediction.DecisionThreshold/AlertTriggered`). **El medidor nunca lee
ese campo**: `Models/BoardModels.cs: RiskGauge` no lo incluye y `Services/BoardMapper.ToGauge()`
no lo mapea, así que hoy se descarta.

Esto es exactamente el tipo de metodología (optimización de umbral sobre datos de validación,
en vez de un corte redondo arbitrario) que sí tiene respaldo metodológico sólido en la
literatura de modelos predictivos clínicos:

| Cita | Qué aporta |
| --- | --- |
| [21] | Referencia clásica del método de optimizar un punto de corte a partir de datos de validación (índice de Youden), en vez de fijar un valor arbitrario como 50 |
| [22] | Guía metodológica sobre selección de umbrales de decisión bajo costos asimétricos (exactamente lo que hace `optimize_alert_threshold`) |
| [23] | Buenas prácticas de comunicación de riesgo: recomienda anclar los cortes de color a algo interpretable (un umbral de decisión real), no a una división matemática sin significado clínico |

**Implementado (v3), opción 1 + una versión de la 2:**

1. `Models/BoardModels.cs: RiskGauge` ahora tiene un campo `DecisionThreshold` (0-1, nullable) y
   `Services/BoardMapper.ToGauge()` lo mapea desde `RiskPrediction.DecisionThreshold`.
   `SensoryWalletCard.razor` dibuja una línea corta cruzando el arco en el ángulo correspondiente
   (mismo cálculo de ángulo que ya usaba la aguja, radio 79–105 para cruzar el grosor del arco).
2. Debajo de la leyenda de colores se agregó, con la clase `.state-note` ya existente (sin CSS
   nuevo), el texto: *"La marca oscura del arco indica el umbral de alerta que el modelo calibró
   para este caso (X%); los colores de fondo son una referencia visual, no un corte clínico
   validado."* Sólo se muestra cuando la API entrega el dato (`DecisionThreshold` no nulo); en
   modo demostración (`DemoData.cs`) no se inventó un valor, así que ahí no aparece marca ni nota.
3. Se mantuvieron los 4 cuartiles de color por legibilidad (opción 3 del borrador), combinada con
   la marca real del umbral en vez de reemplazarlos — es la combinación menos invasiva con el
   diseño existente.

**Verificado en navegador** contra la API real (`PAC-000`): `decision_threshold = 15 %`, la
marca aparece correctamente sobre la franja verde, muy cerca de la aguja (Score 13). Los
episodios históricos con vocabulario antiguo (`"Intensidad: Severa"`, `"Intensidad: Moderada"`)
se siguen coloreando bien gracias a la compatibilidad agregada en `BoardMapper.SeverityOf()`
(§2).

---

## 9. Guion de demo (≈ 1:30 min)

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
