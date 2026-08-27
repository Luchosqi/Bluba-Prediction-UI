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
- **2026-08-26 (v4):** decisión de producto: el Paso 1 del wizard de desregulación había quedado
  con demasiados campos (duración, recuperación, checklist de conductas, aviso de auto-ajuste).
  Se revirtió a **sólo la escala de niveles** (mismo estilo de botón simple que el resto del
  wizard), sin recolectar las demás dimensiones del EOQ por ahora. También se sacó la marca del
  `decision_threshold` y su nota del medidor (quedó sólo el gráfico + Confianza + Faltan datos,
  igual que antes), y se quitaron las menciones a la ruta de este archivo dentro de textos
  visibles en la UI. Ver §2 y §8.4 actualizados, y el aviso para el backend al final de §2.
- **2026-08-26 (v5):** segunda decisión de producto, más profunda: el wizard de 5 pasos completo
  se colapsó a **una sola pantalla**: 4 botones de nivel (Leve / Moderada / Alta / Crisis · riesgo
  de seguridad) + un textarea de "Contexto en palabras". Se sacaron por completo los pasos de
  lugar/transición (§8.3), desencadenante y apoyo/efectividad (§8.2, incluida la nota del
  chaleco de presión) — esas categorías ya no existen en la UI. `Type`, `StrategyApplied` y
  `StrategyResult` ahora viajan a la API como **valores fijos**, no elegidos por la familia. Ver
  §2, §8.2 y §8.3 actualizados, y la sección de implicancias para el backend al final de §2.

---

## 1. Resumen de cambios por archivo (frontend)

| Archivo | Cambio | Por qué |
| --- | --- | --- |
| `Components/Pages/Prediction/DysregulationWizard.razor` | **(v5)** Ya no es un wizard de varios pasos: una sola pantalla con la escala **Leve/Moderada/Alta/Crisis · riesgo de seguridad** (botones) + un textarea de contexto libre. Escala "Leve(1-3)/Moderada(4-7)/Severa(8-10)" con rangos inventados → nombres justificados por literatura | §2 |
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
| `DysregulationWizard.razor` (`Supports`, "formas de calmar") | **(v5) Eliminado del formulario** junto con el resto de los pasos — la justificación de §8.2 (incluida la nota del chaleco) queda como referencia para si se reintroduce más adelante | §8.2 |
| `DysregulationWizard.razor` (`Locations`, `Transitions`, `Triggers`) | **(v5) Eliminado del formulario** — la justificación FBA de §8.3 queda como referencia para si se reintroduce más adelante | §8.3 |
| `SensoryWalletCard.razor` + `Models/BoardModels.cs` + `Services/BoardMapper.cs` (medidor) | **Sin justificación clínica para los cuartiles de color** (se mantienen por legibilidad). **(v4)** Se probó agregar la marca del `decision_threshold` real de la API + una nota, y se revirtió por decisión de diseño: el medidor vuelve a mostrar sólo el gráfico | §8.4 |

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
Antes era un asistente de 5 pasos (intensidad; lugar/transición; desencadenante; apoyo aplicado
y efectividad; comentario final) con rangos numéricos **inventados** en el primer paso ("Leve
1-3", "Moderada 4-7", "Severa 8-10"). **(v5)** Ahora es **una sola pantalla**:

1. **Intensidad observada**: 4 botones — Leve / Moderada / Alta / Crisis · riesgo de seguridad
   (ver tabla de la escala abajo). Mismo estilo `opt-chip` que el resto de la app.
2. **Contexto en palabras**: un textarea abierto ("¿Qué ocurrió antes? ¿Qué estímulos o
   situaciones pudieron afectar?"), sin categorías predefinidas.
3. Cancelar / Guardar registro.

Se sacaron por completo del formulario: la selección de lugar/transición previa, el
desencadenante primario, el apoyo aplicado y la efectividad — todo lo que en v1-v4 alimentaba
`type`, `strategy_applied` y `strategy_result` con valores elegidos por la familia. **(v4→v5)**
También se sacaron duración, recuperación y el checklist de conductas de riesgo (dimensiones del
**Emotional Outburst Questionnaire**, EOQ — referencia [6] en §6) que se habían probado en v2/v3.

**Qué envía hoy el frontend a `POST /cases/{id}/dysregulations`:**

| Campo del payload | Valor hoy | Antes (v1-v4) |
| --- | --- | --- |
| `intensity` | Uno de los 4 niveles justificados (elegido por la familia) | Igual, elegido por la familia |
| `type` | Siempre `"Desregulación Emocional"` (constante) | Derivado del desencadenante elegido (`Sobrecarga Sensorial`, `Transición de Actividad`, `Alimentación`, …) |
| `strategy_applied` | Siempre `"Acompañamiento sin apoyo específico"` (constante) | Elegido de una lista de apoyos (audífonos, chaleco, etc.) |
| `strategy_result` | Siempre `"Regulación Parcial"` (constante) | Elegido con una carita (logró regularse / parcial / no funcionó) |
| `suspected_trigger_text` | El texto libre de "Contexto en palabras", tal cual | Concatenación de lugar + transición + desencadenante + comentario |

**Por qué esto le importa al backend:** `suspected_trigger_text` sigue pasando por
`trigger_analyzer()` (`app/api/routes.py:190`), que extrae tags de texto libre con NLP — esa
parte sigue funcionando igual. Pero `app/services/strategy.py` también usa `event.tipo_evento`
para extraer tags (`_event_tags`, línea ~39): con `type` siempre igual a `"Desregulación
Emocional"`, esa señal deja de variar entre episodios. Antes el desencadenante elegido por la
familia aportaba una categoría explícita (auditiva, de rutina, alimentaria); ahora esa
categorización depende **enteramente** de lo que la familia escriba en el texto libre y de que
el NLP lo detecte. Vale la pena que el backend revise si esto degrada las recomendaciones de
"Cuidado sugerido" con el tiempo.

### La escala Nivel 0-4 (propuesta compuesta — **no es un instrumento validado por sí solo**)

| Nivel | Botón en la UI | Criterio observable | Valor enviado a la API (`intensity`) |
| --- | --- | --- | --- |
| 0 | *(no se ofrece en el formulario)* | Sin señales de desregulación | *(no aplica al registrar un episodio)* |
| 1 | **Leve** (Activación leve) | Reactividad y recuperación <5 min; se autorregula con apoyo mínimo | `Nivel 1 (Activación leve)` |
| 2 | **Moderada** (Desregulación moderada) | Reactividad y recuperación 5–15 min; requiere co-regulación activa | `Nivel 2 (Desregulación moderada)` |
| 3 | **Alta** (Desregulación alta) | Reactividad y/o recuperación 15–60 min; pérdida funcional significativa de la rutina | `Nivel 3 (Desregulación alta)` |
| 4 | **Crisis / riesgo de seguridad** | Duración o recuperación >60 min, o agresión física/autolesión | `Nivel 4 (Crisis / riesgo de seguridad)` |

El botón muestra la etiqueta corta (Leve/Moderada/Alta); el valor que viaja a la API mantiene el
nombre completo entre paréntesis, igual que antes, para no romper la compatibilidad con
`BoardMapper.IntensityLabel`/`SeverityOf` ni con el filtro de `PredictionBoard.razor`.

**Importante para el equipo:** esta tabla es una **combinación** informada por tres fuentes
(EOQ para las dimensiones del episodio, EDI para el concepto de reactividad como continuo, y el
precedente de 3 niveles discretos del CBCL-DP), tal como se concluyó en la revisión de
literatura previa. **No hay hoy un estudio que valide exactamente estos 5 niveles con estos
puntos de corte.** Es correcto decir "escala informada por literatura reciente sobre EOQ/EDI",
**no** es correcto decir "escala validada científicamente". Esa distinción debe mantenerse en
cualquier material de difusión o demo.

### Qué debe cambiar el backend (compañero)

El campo `intensity` sigue siendo un string libre (`Field(min_length=1)`, sin `Literal` ni
enum), así que **la API ya acepta los valores nuevos sin romperse**: hoy llega como
`"Nivel 1 (Activación leve)"` … `"Nivel 4 (Crisis / riesgo de seguridad)"` en vez de
`"Leve (1-3)"` … `"Severa (8-10)"`. Nada más cambia en el payload — el frontend **no** envía
duración, recuperación ni conductas (eso se descartó en v4, ver arriba).

> **⚠️ Aviso para el compañero de backend:** no sé cuál es la mejor forma de modelar esto del
> lado del backend (si conviene normalizar el nivel en una columna propia, dejarlo sólo como
> texto, o algo intermedio) — queda a tu criterio cómo trabajar ese flujo. Van 3 opciones que
> evalué, de menor a mayor esfuerzo, para que elijas la que tenga más sentido con el resto del
> modelo de datos:
>
> - **Opción A — no tocar el esquema.** Dejar `intensity` como está (string libre). Sólo
>   actualizar el *keyword matching* de `strategy.py` (punto 2 más abajo) para que reconozca
>   `"nivel 3"` / `"nivel 4"` en vez de `"severa"` / `"moderada"`. Es el cambio más chico, pero
>   cualquier filtro o reporte futuro por severidad sigue dependiendo de parsear el string.
> - **Opción B — normalizar en el backend, sin tocar el frontend.** Agregar una columna
>   `severity_level SMALLINT` a `dysregulation_events`, calculada en el propio backend al recibir
>   el POST (parseando el primer dígito de `intensity`, ej. con una regex `r"Nivel (\d)"`). El
>   frontend no cambia nada; el backend gana un campo confiable para filtrar/reportar.
> - **Opción C — contrato explícito.** Agregar `severity_level: int = Field(ge=1, le=4)` a
>   `DysregulationCreateIn` y pedirle al frontend que lo mande además del texto. Es la opción
>   más robusta a largo plazo (no depende de parsear strings), pero requiere un cambio (chico)
>   en `DysregulationWizard.SaveAsync()` y en `Models/ApiModels.cs: DysregulationCreate` para
>   agregar ese campo — avísame si prefieres esta opción y lo agrego.
>
> Si en el futuro se decide capturar duración/recuperación/conductas (dimensiones del EOQ, ver
> arriba), lo mismo aplica: mejor definir el contrato de esos campos en el backend primero, en
> vez de que el frontend improvise empaquetándolos como texto libre.

1. **Filtro por nivel en `GET /cases/{id}/dysregulations`** debería usar la nueva columna
   `severity_level` (`Query(None, ge=0, le=4)`) en vez del match exacto sobre `intensity`
   (`app/api/routes.py:264-265`). *Mientras no exista*, el filtro del frontend (`PredictionBoard.razor`)
   sólo hace match exacto contra el string nuevo (`"Nivel 1 (Activación leve)"`, etc.), así que
   **los episodios históricos con el vocabulario viejo no aparecerán al filtrar por nivel**
   (sí siguen apareciendo en "Todos" y en el historial general).

2. **`app/services/strategy.py`, diccionario `TAG_PATTERNS["HIGH_ALERT"]` (línea 24)** hace
   *keyword matching* sobre el texto de `intensidad` buscando las palabras `"severa"` y
   `"moderada"`. Con el vocabulario nuevo (`"Nivel 4 (Crisis...)"`) esas palabras ya no
   aparecen, así que el tag `HIGH_ALERT` **dejará de dispararse** para las estrategias
   recomendadas hasta que se actualice ese patrón (agregar `"nivel 3"`, `"nivel 4"`, `"crisis"`,
   `"riesgo de seguridad"`, o mejor: usar directamente `severity_level >= 3` una vez exista la
   columna).

3. Opcional pero recomendado: `app/synthetic/generator.py:101-102` genera los eventos
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
  antiguos (§2, punto 1) hasta que el backend decida cómo normalizar la severidad (ver el aviso
  para el backend en §2).
- El tag `HIGH_ALERT` de `strategy.py` deja de dispararse con el vocabulario nuevo (§2, punto 2)
  hasta que se actualicen sus patrones — esto puede degradar silenciosamente la calidad de las
  recomendaciones de "Cuidado sugerido" para casos de alta severidad.
- La escala Nivel 1-4 es una **propuesta compuesta**, no un instrumento publicado y validado en
  sí mismo. Cualquier comunicación externa debe dejarlo explícito (ver §2).
- La referencia [7] necesita completarse antes de usarse fuera de este documento interno.
- **(v2, implementado en v3, revertido en v4)** El medidor de riesgo usa cuartiles de color
  (25/50/75) sin respaldo clínico. Se probó mapear `decision_threshold` de la API y marcarlo
  sobre el arco (§8.4), pero se revirtió por decisión de diseño (mantener el medidor simple) —
  el `decision_threshold` calculado por la API sigue sin usarse en el frontend.
- **(v2, implementado en v3, revertido en v5)** "Presión profunda / Chaleco" en la lista de
  formas de calmar tenía evidencia débil/mixta en la literatura ([17]) y una nota condicional en
  el wizard — la lista completa de "apoyo usado" se eliminó del formulario en v5 (§8.2).
- **(v4, revertido en v5)** El wizard había dejado de recolectar duración/recuperación/conductas
  de riesgo (dimensiones del EOQ), pero seguía preguntando lugar/transición/desencadenante/apoyo/
  efectividad. **v5 saca todo eso también**: hoy el formulario sólo pide nivel + contexto libre.
  `type`, `strategy_applied` y `strategy_result` viajan a la API como constantes (ver tabla en
  §2) — si el equipo quiere retomar cualquiera de estas dimensiones (EOQ, antecedentes FBA,
  apoyo aplicado) más adelante, conviene definir primero el contrato en el backend (ver aviso en
  §2), en vez de que el frontend vuelva a improvisar campos.

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

**Implementado en v3, eliminado en v5.** Se llegó a agregar una nota condicional en
`DysregulationWizard.razor`, Paso 4: al seleccionar "Presión profunda / Chaleco" aparecía
*"Evidencia limitada/mixta en la literatura (Stephenson & Carter, 2009): úsalo sólo si ya fue
acordado con el equipo terapéutico"*. En v5 el formulario se colapsó a una sola pantalla (sólo
nivel + contexto libre) y **toda la selección de "apoyo usado" desapareció**, junto con esa nota.
La tabla de justificación queda como referencia: si el equipo reintroduce en el futuro un campo
de "apoyo aplicado" (aquí o en el registro de eventos, §3), esta evidencia sigue siendo válida
— en particular, mantener la advertencia sobre el chaleco de presión.

### 8.3 Lugar, transición previa y desencadenante (`Locations`, `Transitions`, `Triggers`)

Estas tres listas no son una escala clínica: son categorías de **antecedentes**, el componente
central de la Evaluación Funcional de Conducta (*Functional Behavior Assessment*, FBA), el marco
más establecido en la literatura de análisis conductual aplicado para registrar dónde, cuándo y
qué precede a un episodio. "Lugar" y "transición previa" corresponden al concepto de *setting
events* / antecedentes inmediatos; "desencadenante" corresponde al estímulo discriminativo. Este
marco es lo que hacían los Pasos 2 y 3 del wizard **hasta v4**.

| Cita | Qué aporta |
| --- | --- |
| [18] | Manual de referencia del método FBA: cómo identificar antecedentes (lugar, rutina, transición) previo a un episodio |
| [19] | Artículo fundacional del análisis funcional como metodología para vincular antecedentes con la conducta problema |
| [20] | Evidencia específica de que la previsibilidad de las transiciones se relaciona con la frecuencia de conducta problema — sustenta por qué "transición previa" es una pregunta relevante y no arbitraria |

**Implementado hasta v4, eliminado en v5.** Las categorías (Escuela/Aula, Recreo, Furgón/Trayecto,
Casa/Entrada, Terapia; Salida del colegio, Cambio de actividad, Llegada a casa, Sin cambio
evidente; y la lista de desencadenantes) eran consistentes con cómo la literatura de FBA agrupa
antecedentes, y no había ninguna razón bibliográfica para sacarlas — se sacaron en v5 por
decisión de producto (simplificar el formulario a una sola pantalla), no porque estuvieran mal
justificadas. Si se quiere recuperar esa estructura más adelante, esta sección sigue siendo el
respaldo. Mientras tanto, el "antecedente" del episodio sólo se captura como texto libre en
"Contexto en palabras", analizado por `trigger_analyzer()` en el backend (ver §2).

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

**Implementado en v3, revertido en v4.** Se llegó a implementar y verificar en navegador contra
la API real (`decision_threshold = 15 %` para `PAC-000`, marca visible sobre la franja verde,
cerca de la aguja): se agregó `RiskGauge.DecisionThreshold`, se mapeó en `BoardMapper.ToGauge()`
y se dibujó una línea sobre el arco + una nota debajo de la leyenda explicando qué significaba.

**Se revirtió todo eso en v4** por decisión de diseño: el medidor estaba dando "demasiada
justificación" en pantalla (la nota explicando colores + el umbral). Hoy `SensoryWalletCard.razor`
volvió a mostrar sólo el gráfico, "Confianza" y "Faltan datos" — sin la marca ni la nota.
`RiskGauge` ya no tiene el campo `DecisionThreshold`.

**El hallazgo sigue siendo válido y queda documentado para cuando se quiera retomar:** la API
calcula `decision_threshold` con un método estadístico razonable (`optimize_alert_threshold`,
citas [21]-[23] arriba) y hoy el frontend no lo usa para nada. Si en el futuro se quiere volver a
mostrarlo, la implementación de v3 (revertida) es el punto de partida — el código ya no está en
el repo, pero el enfoque (mapear el campo + dibujar una marca en el mismo ángulo que la aguja)
sigue siendo válido.

---

## 9. Guion de demo (≈ 1:30 min)

Pensado para que el compañero que presenta la demo lo siga paso a paso. Requiere la app corriendo
contra la API real (o en modo demo si `/health` no responde — igual funciona, cae a `DemoData.cs`
ya actualizado).

| Tiempo | Acción | Qué decir |
| --- | --- | --- |
| 0:00–0:15 | Abrir la Ficha del Consultante, pestaña **"Predicción (Nuevo)"** | "Esta es la Billetera Sensorial. Antes decía 'Confianza X%' como si fuera una certeza clínica; ahora aclara que es cobertura de datos del modelo, no un diagnóstico." — pasar el mouse sobre el estadístico para mostrar el tooltip nuevo |
| 0:15–0:35 | Click en **"Ingresar desregulación +"** | "Registrar una crisis ya no usa una escala de 'Leve/Moderada/Severa' con rangos numéricos inventados. Los mismos 4 nombres ahora están justificados en literatura reciente sobre desregulación en autismo (EOQ y EDI), uno de esos instrumentos ya validado con muestra chilena — y el formulario se simplificó a una sola pantalla." |
| 0:35–0:55 | Elegir, por ejemplo, **"Alta"**, escribir una frase corta en "Contexto en palabras" (ej. "Ruido fuerte en el recreo") | "Sólo pedimos el nivel y una descripción libre del contexto — nada de pasos ni campos de más" |
| 0:55–1:05 | **Guardar registro** | "Queda guardado en el historial con el nivel correcto" — mostrar que aparece en "Historial de desregulaciones" |
| 1:05–1:20 | Abrir el panel flotante **"Mamá Bluba"** (esquina inferior) → "Ver más líneas de ayuda" | "Sacamos el Fono SENDA: es la línea de drogas y alcohol, no corresponde a este público. Quedaron las líneas que sí aplican." |
| 1:20–1:30 | Cierre | "Todo esto está documentado con las citas exactas y los cambios pendientes de backend en `docs/CAMBIO_ESCALA_DESREGULACION.md`, en la rama `dev`." |
