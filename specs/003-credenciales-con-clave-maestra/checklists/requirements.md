# Specification Quality Checklist: Credenciales cifradas con una clave maestra

**Propósito**: validar que la especificación esté completa antes de planificar
**Creado**: 2026-09-03
**Feature**: [spec.md](../spec.md)

## Calidad del contenido

- [x] Sin detalles de implementación (lenguajes, frameworks, APIs)
- [x] Centrada en el valor para el usuario y la necesidad del negocio
- [x] Escrita para alguien que no programa
- [x] Todas las secciones obligatorias completas

**Nota sobre el primer punto.** La spec nombra Argon2id, AES-256-GCM, DPAPI y los tamaños de
nonce, sal y clave. **Es deliberado y no es una violación**: no son elecciones de implementación
sino requisitos que la constitución 2.1.0 fija como no negociables en su Principio II. Un
requisito de seguridad que dice «cifrar de forma segura» no es verificable; uno que dice
AES-256-GCM con nonce nuevo por cifrado, sí. Lo que la spec **no** dice es en qué clase vive cada
cosa, cómo se llaman los métodos ni cómo queda el esquema: eso es del plan y del data-model.

## Completitud de los requisitos

- [x] No quedan marcadores [NEEDS CLARIFICATION]
- [x] Los requisitos son verificables y sin ambigüedad
- [x] Los criterios de éxito son medibles
- [x] Los criterios de éxito no traen detalles de implementación
- [x] Todos los escenarios de aceptación están definidos
- [x] Los casos límite están identificados
- [x] El alcance está acotado
- [x] Dependencias y supuestos identificados

## Preparación de la feature

- [x] Todo requisito funcional tiene criterio de aceptación claro
- [x] Los escenarios cubren los flujos principales
- [x] La feature cumple los criterios de éxito definidos
- [x] Ningún detalle de implementación se filtró a la especificación

## Puerta constitucional

- [x] Principio II (2.1.0): la spec **es** la aplicación del principio redefinido. FR-200 a FR-207
      recogen el modelo de dos claves; FR-216 a FR-218, las reglas de la clave maestra; FR-232 a
      FR-240, la excepción acotada de DPAPI con sus cinco reglas.
- [x] Principio IV: una dependencia nueva (Argon2id). La spec la nombra en Assumptions y deja su
      justificación para el plan, que es donde el principio la exige.
- [x] Principio V: es una redefinición del almacén, no una ampliación de alcance. No entra ningún
      dato nuevo del servidor ni ninguna función que no estuviera.
- [x] Principio VI: no instala servicios ni exige privilegios. DPAPI `CurrentUser` corre como el
      usuario.
- [ ] **Puerta de esquema** (Flujo de trabajo, punto 2): el vault exige cambios de esquema
      —envoltura de la clave, sal, parámetros, verificador, y las credenciales cifradas—. Requiere
      justificación escrita y confirmación explícita del usuario **antes** de escribir la
      migración. Queda abierta a propósito: es del plan, no de la spec.

## Lo que queda abierto

- **La puerta de esquema.** Es la única casilla sin marcar y es correcta que lo esté.
- **El valor por omisión del desbloqueo automático.** Resuelto como «se elige al crear el vault,
  apagado si no se elige» y anotado en Assumptions. Si el usuario lo quiere encendido de fábrica,
  cambia FR-235 y nada más.
- **US5, cambiar la clave maestra**, no estaba en el pedido. Se puede sacar sin tocar las otras
  cuatro historias.
