# Fase 00B — Propuesta para revisión visual

Fecha: 2026-11-10, America/Panama. **Pendiente de aprobación visual del propietario.** La autorización para actualizar DESIGN_SYSTEM.md no aprueba las pantallas ni habilita Fase 01.

## Alcance y decisiones

Se leyó íntegro FASE-00B-DIRECCION-VISUAL.md. La pantalla anterior de fundaciones no se tomó como referencia aprobada. Se sustituyó su presentación por un app shell con jerarquía financiera vacía y prototipos de teléfono/OTP.

- Petróleo y verde suave sobre fondo claro, sans serif y símbolo de líneas alineadas. Propuesta, no paleta definitiva.
- Encabezado compacto, navegación inferior móvil y lateral de escritorio. Saldo/cuentas primero; movimientos/compromisos después. Sin hero, gráficas ornamentales, gradientes ni sombras.
- Guiones y disponibilidad explícita, sin importes o personas ficticias. Acciones de negocio deshabilitadas; solo funciona la navegación entre vistas de diseño.
- Acceso con +507 sugerido, labels, ayuda, foco y error. Teléfono / Código SMS cambia únicamente la vista; ningún envío, verificación, sesión o éxito simulado. Entradas descartadas al cambiar de vista, sin persistencia.
- Salud técnica conservada en desarrollo. Cliente HTTP, cancelación, timeout, validación y endpoints existentes intactos. La compilación de presentación omite el panel.
- Scroll del área principal con espacio para navegación/safe-area. La primera inspección de capturas detectó que la barra fija tapaba parte del formulario; se corrigió y añadió una comprobación de campo enfocado por encima de la barra.

Componentes confinados a frontend/src/features/design-preview; no propagados a otros módulos. [Tokens y patrones](DESIGN_SYSTEM.md).

## Capturas comparables

Mismo contenido por tamaño: escritorio 1440×1000, móvil 390×844 y compacto 320×740 CSS px. Móvil usa Chromium con emulación iPhone y DPR 3, imágenes originales 1170×2532. No es un teléfono físico. El contenido inferior se consulta con scroll del área principal.

| Estado | Móvil | Escritorio | 320px |
|---|---|---|---|
| Resumen / app shell | [Ver](evidence/phase00b/shell-mobile.png) | [Ver](evidence/phase00b/shell-desktop.png) | [Ver](evidence/phase00b/shell-compact.png) |
| Acceso / teléfono | [Ver](evidence/phase00b/access-mobile.png) | [Ver](evidence/phase00b/access-desktop.png) | [Ver](evidence/phase00b/access-compact.png) |
| Código SMS sin envío | [Ver](evidence/phase00b/otp-mobile.png) | [Ver](evidence/phase00b/otp-desktop.png) | [Ver](evidence/phase00b/otp-compact.png) |
| Error de teléfono | [Ver](evidence/phase00b/phone-error-mobile.png) | [Ver](evidence/phase00b/phone-error-desktop.png) | [Ver](evidence/phase00b/phone-error-compact.png) |
| Foco de teclado | [Ver](evidence/phase00b/focus-mobile.png) | [Ver](evidence/phase00b/focus-desktop.png) | [Ver](evidence/phase00b/focus-compact.png) |

Se inspeccionaron las imágenes renderizadas de resumen móvil/escritorio, acceso móvil/escritorio/320px, OTP móvil y error en 320px. La aprobación estética corresponde al propietario.

## Pruebas ejecutadas

| Prueba | Resultado | Evidencia |
|---|---|---|
| Typecheck estricto + build frontend | PASS, 21 módulos | [Salida](evidence/phase00b/frontend-build.txt) |
| Tests frontend | PASS, **11/11**, 3 archivos | [Salida](evidence/phase00b/frontend-tests.txt); conserva 6 tests originales de salud y añade 5 de prototipo |
| E2E en tres tamaños | PASS, **9/9**, sin skips, fallos o flaky | [Reporte JSON](evidence/phase00b/e2e-results.json), generado con capturas finales |
| Navegación/layout | PASS | Inicio/Movimientos/Cuentas/Compromisos, aria-current, sin overflow horizontal, acciones financieras disabled |
| Prototipo sin operaciones reales | PASS | Validación teléfono/OTP, descarte de entradas, sin requests de escritura ni localStorage/sessionStorage; sin transición autenticada |
| Teclado/foco | PASS | Skip link, foco conservado, tabulación a campo; campo enfocado encima de navegación móvil |
| Contraste de pares medidos | PASS | E2E adjunta texto 10.69:1, ayuda 5.51:1, error 6.76:1, borde 3.18:1 y foco 5.71:1; anillo 3px |
| Salud desde presentación | PASS | GET /health 200 y GET /health/ready ready, vía Nginx, en E2E |
| Salud de desarrollo + reintento real | PASS | [Resultados](evidence/phase00b/development-health.json), [captura](evidence/phase00b/development-health.png); Vite → Nginx existente → API/PostgreSQL |
| Tests backend existentes | PASS, **10/10** | docker compose --profile tests run --rm backend-tests: Test Run Successful; compiló API/tests y usó PostgreSQL aislado |
| Skill refinada | PASS | quick_validate.py: Skill is valid!; [skills/list](evidence/phase00b/skills.json): frontend-design habilitada, scope repo, cero errores |
| Arquitectura y dependencias | PASS | git diff --quiet desde raíz sobre backend, infra, compose.yml, global.json, solución, package.json y lock: salida 0 |
| Compose y estado final | PASS | compose config --quiet; API/frontend/PostgreSQL healthy; solo 127.0.0.1:8080 publicado |

Componentes usan respuestas controladas; E2E y smoke de desarrollo consultaron el stack real. Tests backend usan una base temporal separada de la app. No se repitió ARM64: no cambió infraestructura ni se añadieron dependencias nativas.

## Procedimiento de mejora de frontend-design

1. Se leyó la skill existente y skill-creator/SKILL.md disponible en el entorno. La versión anterior era una lista genérica; frontmatter válido no demostraba calidad visual.
2. Se refinó la misma skill sin duplicar ni instalar otras: contrato activo, jerarquía financiera, tokens externos, veracidad del prototipo, formularios, navegación, contraste, inspección de capturas y aprobación antes de propagar componentes.
3. Se ejecutó quick_validate.py de skill-creator en un contenedor Python temporal, con repositorio de solo lectura. Resultado Skill is valid!; sin instalar Python en Windows.
4. node infra/verify-skills.cjs comprobó descubrimiento real por Codex: seis skills habilitadas, incluida frontend-design, cero errores. Sin threads ni turnos de otro agente.
5. Se aplicó la guía a la UI y se revisaron capturas. Se detectó y corrigió el solapamiento de navegación. Esta evidencia complementa al validador estructural, sin reemplazar aprobación visual.

## Reproducir

Con .env y stack local existentes, desde la raíz:

```powershell
docker compose up -d --build --no-deps --wait frontend
docker compose --profile tests run --rm backend-tests
cd frontend
npm run build
npm test
npm run test:e2e
```

Abrir http://127.0.0.1:8080 y «Vista de acceso». Teléfono / Código SMS permite revisar propuestas sin enviar nada.

En desarrollo, iniciar Vite con API_PROXY_TARGET=http://127.0.0.1:8080 para usar el Nginx existente sin alterar infraestructura. Abrir «Estado técnico · Desarrollo». Se probó con Chromium y reintento real: cuatro respuestas de salud/readiness fueron 200. El servidor Vite temporal se detuvo al terminar.

## Límites y parada

- Pendiente **aprobación visual explícita** del app shell y acceso/OTP antes de propagar componentes o avanzar a Fase 01.
- Pendientes dispositivo/teclado virtual físicos, lector de pantalla y auditoría completa. Los contrastes indicados no son una certificación WCAG integral.
- Sin autenticación, SMS, dashboard financiero real, endpoints o migraciones financieras. Ocho dígitos demuestra validación visual Panamá; no es política de identidad de Fase 01.
- Backend, persistencia, redes, Dockerfiles, paquetes y locks sin cambios. No se leyeron ni implementaron requisitos de Fase 01; contratos de entrada conservados.
- HEAD permanece 62296c5, igual que al inicio; no se hicieron commits, pushes ni despliegues públicos. Solo se reconstruyó frontend local.

Stack disponible en 127.0.0.1:8080; PostgreSQL temporal de tests detenido. Autorizar documentación durante esta tarea no habilita avanzar de fase.
