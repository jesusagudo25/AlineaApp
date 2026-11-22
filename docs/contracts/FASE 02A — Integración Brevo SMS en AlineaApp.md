**FASE 02A — Integración Brevo SMS en AlineaApp**

**Contexto actual**
- Fase 01: autenticación OTP implementada y probada localmente.
- API Key `AlineaApp-OTP` creada, con permisos SMS Read/write.
- Sender ID `AlineaApp` solicitado para Panamá, pendiente de aprobación.
- Créditos SMS disponibles: 0.
- No se autorizan envíos reales todavía.

**Objetivo**

Integrar Brevo como proveedor SMS utilizando la autenticación existente, sin reconstruirla.

**Implementación**

1. Inspecciona únicamente los archivos de autenticación, proveedores SMS, configuración y pruebas relacionadas.
2. Implementa el adaptador Brevo utilizando `HttpClientFactory` y la API oficial de SMS transaccionales.
3. Reutiliza la abstracción `ISmsSender` existente.
4. Configura API Key, Sender ID y selección de proveedor mediante variables de entorno o secretos externos a Git.
5. Conserva las políticas de OTP, CSRF, rate limiting, sesiones y autorización existentes.
6. Maneja respuestas de Brevo, errores HTTP, falta de créditos y timeouts sin filtrar información sensible.
7. Ante un envío fallido, no debe habilitarse la verificación del OTP correspondiente.
8. Mantén el proveedor local exclusivamente para Development/Testing.
9. Implementa pruebas específicas con HTTP simulado. No hagas llamadas reales a Brevo.
10. Documenta las variables necesarias para activar el proveedor cuando se apruebe el Sender ID.

**Restricciones**

- No modificar diseño ni módulos financieros.
- No modificar Raspberry Pi ni Cloudflare.
- No generar commits, pushes o despliegues.
- No ejecutar suites completas de pruebas innecesariamente.
- No solicitar ni mostrar credenciales en la conversación.
- No realizar SMS reales.

**Entrega esperada**

Resumen breve de cambios, pruebas ejecutadas y pendientes para activar Brevo.

Al terminar, detente y espera la siguiente fase.