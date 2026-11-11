---
name: database-finance
description: Revisar modelos, migraciones y persistencia PostgreSQL de AlineaApp y preservar invariantes del futuro dominio financiero.
---

Lee [reglas](../../../docs/DOMAIN_RULES.md), [ADR](../../../docs/DECISIONS/0001-fundaciones.md) y [backup](../../../docs/BACKUP_RESTORE.md).

- [ ] Fase 00 solo CONFIG_GENERAL, clave única, defaults USD y YES sin secretos.
- [ ] Generar migración EF y snapshot, inspeccionar SQL y probar en PostgreSQL real.
- [ ] Inicialización idempotente preserva valores existentes; nunca resetear ni borrar volumen para arreglar un test.
- [ ] Futuro dinero decimal/numeric(18,2), eventos UTC y calendario explícito; no construir finanzas sin contrato de fase.
- [ ] Al agregar entidades privadas diseñar propietario y filtro de identidad desde servidor, con tests cruzados.
- [ ] Respaldar y probar restauración aislada antes de una migración de riesgo. No ejecutar cambios destructivos sin autorización.
