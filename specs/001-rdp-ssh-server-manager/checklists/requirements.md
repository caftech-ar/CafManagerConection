# Specification Quality Checklist: CafManagerConection (CMC)

**Purpose**: Validar que la especificación esté completa y sea de calidad suficiente antes
de pasar a la planificación.

**Created**: 2026-08-24

**Feature**: [spec.md](../spec.md)

## Content Quality

- [ ] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

### Revalidación del 2026-09-01, tras la enmienda 1.11.0 (puertos, ficha de proceso, color)

Resultado: **15/16**, sin cambios de estado. Se agregaron US11, 29 requisitos (FR-039a/b,
FR-087a-d, FR-100e-g, FR-101a-c, FR-150b-e, FR-164 a FR-166a), cuatro criterios (SC-023 a
SC-026), seis casos borde y tres suposiciones.

Dos cosas que se cuidaron a propósito durante la escritura:

- **SC-024 se reescribió** para no nombrar la directiva de configuración del servidor SSH. La
  primera redacción la nombraba, y eso habría hecho caer «Success criteria are
  technology-agnostic», que hasta ahora pasaba. El requisito que la nombra —FR-039a— sí puede,
  por el mismo criterio que FR-081 nombra `/proc/stat`: ahí la elección técnica *es* el
  requisito.
- **El ítem «No implementation details» sigue sin marcar**, como desde el 2026-08-24. Los
  requisitos nuevos nombran `sudo`, el pedido interactivo por teclado y los bloques de nginx, y
  es la misma desviación deliberada que ya está explicada abajo: sin nombrar la fuente, el
  requisito admitiría implementaciones incorrectas.

### Revalidación del 2026-08-24, tras la ampliación de alcance (constitución v1.2.0)

Resultado: **15/16**. Un ítem pasó de cumplido a incumplido.

**Regresión — "No implementation details"**: la ampliación introdujo requisitos que nombran
tecnología concreta:

- FR-081 y FR-082 nombran `/proc/stat`, `/proc/net/dev`, `MemTotal` y `MemAvailable`, y
  prohíben explícitamente interpretar la salida de `top` y `free`.
- FR-065 nombra el material Mica de Windows 11.
- FR-096 nombra la API de Docker.

**Se deja así deliberadamente, y conviene entender por qué**: en estos casos la elección
técnica *es* el requisito. "Mostrar el uso de CPU" sin especificar la fuente permitiría
interpretar la salida de `top`, cuyo formato cambia según distribución, versión e idioma, y
produciría lecturas incorrectas sin que nadie lo note. Lo mismo con `MemFree` frente a
`MemAvailable`: Linux usa la memoria libre como caché, así que la primera da un número
engañoso. Prohibir esas fuentes es una decisión de corrección funcional, no un detalle de
implementación filtrado.

El ítem queda sin marcar para que la desviación esté registrada, no para que se corrija.

### Validación original

Resultado: **todos los ítems pasan**. Se necesitaron dos iteraciones.

Correcciones aplicadas en la segunda iteración:

1. **Success criteria are technology-agnostic** — SC-011 nombraba el entorno de ejecución
   concreto que no hace falta instalar. Se reescribió como "sin requerir ninguna
   instalación previa", que expresa la misma promesa al usuario sin nombrar tecnología.
2. **Scope is clearly bounded** — la especificación enumeraba lo que el producto hace pero
   no lo que deliberadamente no hace. Se agregó la sección `Out of Scope`, que además ancla
   cada exclusión al Principio V de la constitución.

Observaciones que no bloquean:

- Los nombres de programas de terminal (`vim`, `nano`, `top`, `htop`, `less`, `tmux`) y los
  términos `UTF-8`, `ANSI` y `256 colores` aparecen en FR-026 a FR-029 y en SC-007/SC-008.
  No son detalles de implementación de CMC: son exactamente lo que el administrador espera
  poder usar y la única forma verificable de expresar ese requisito.
- No quedaron marcadores `[NEEDS CLARIFICATION]`. Los quince puntos que la descripción no
  definía se resolvieron con valores predeterminados documentados en la sección
  `Assumptions` de la especificación. Conviene revisarlos antes de implementar; los de mayor
  impacto son el anidamiento de carpetas sin límite de profundidad, la decisión de no
  restaurar sesiones al reabrir la aplicación y la ausencia de reintentos automáticos de
  conexión.
