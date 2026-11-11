---
name: quality-review
description: Validar una entrega de AlineaApp contra su contrato y producir evidencias reproducibles y pendientes precisos.
---

Lee [contrato compartible](../../../docs/contracts/FASE-00-FUNDACIONES-ALINEAAPP.public.md) y [verificación](../../../docs/VERIFICATION.md).

- [ ] Revisar alcance y diff existente antes de ejecutar; pruebas según riesgo.
- [ ] Ejecutar build API, tests PostgreSQL, typecheck/build frontend, tests UI y Compose config.
- [ ] Con Docker disponible, comprobar stack, claves persistidas, reinicio, fallos y recuperación.
- [ ] Separar manifest ARM64, build ARM64 y ejecución en hardware real. Nunca equipararlos.
- [ ] Buscar secretos y datos personales sin imprimirlos; distinguir contrato original de archivos implementados.
- [ ] Entregar tabla prueba/resultado/evidencia, versiones, comandos y pendientes. No afirmar pasos no realizados.

No commits, pushes, despliegues ni cambios de infraestructura externa sin autorización.
