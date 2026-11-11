---
name: react-typescript
description: Implementar componentes, estado y consumo HTTP en el frontend React y TypeScript de AlineaApp.
---

Lee [arquitectura](../../../docs/ARCHITECTURE.md). Trabaja por funcionalidad en `frontend/src/features` y usa `src/lib/http.ts` para HTTP.

- [ ] TS estricto, unknown para respuestas externas y validación antes de usar datos.
- [ ] Cancelar solicitudes al desmontar; evitar respuestas tardías y dobles consultas por reintentos.
- [ ] API base pública sin secretos; mismo origen mediante proxy por defecto.
- [ ] Manejar timeout, HTTP no exitoso, JSON inválido y vacío con textos útiles.
- [ ] Ejecutar `npm run build` y `npm test` desde frontend. E2E requiere stack real.

No añadir stores, router o librerías UI hasta que una necesidad concreta lo justifique.
