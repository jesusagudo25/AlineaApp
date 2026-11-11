---
name: frontend-design
description: Diseñar y revisar pantallas de AlineaApp, su app shell móvil, formularios y estados visuales; producir evidencia responsive antes de aprobar una dirección de interfaz.
---

Lee el contrato de la fase solicitada. Para dirección visual usa [Fase 00B](../../../docs/contracts/FASE-00B-DIRECCION-VISUAL.md) y los [tokens y patrones](../../../docs/DESIGN_SYSTEM.md). No leer ni implementar una fase futura para completar un prototipo.

## Diseñar una herramienta financiera

Determina qué tarea resuelve la pantalla, qué información tiene autoridad y qué acciones existen realmente. Propón jerarquía y estados antes de decorar. La pantalla inicial debe permitir entender saldo, actividad y próximos compromisos sin una sección hero ni texto promocional.

La dirección propuesta es cálida y contemporánea: petróleo, verde suave, superficies claras, tipografía sans serif y números tabulares. Los valores concretos viven en DESIGN_SYSTEM; no declarar definitiva una paleta no aprobada. La identidad nace de proporciones, alineación, navegación y estados consistentes, no de copiar un dashboard de tarjetas.

- Usa superficies agrupadas cuando tengan una función: saldo, listado, formulario. Evita repetir tarjetas vacías, gradientes, sombras pesadas, gráficos ornamentales y métricas inventadas.
- En móvil prioriza lectura y una acción principal. Navegación inferior con etiquetas; en escritorio puede convertirse en lateral sin duplicar controles accesibles.
- En formularios usa labels persistentes, ayudas próximas, errores asociados y teclado apropiado. El placeholder no reemplaza al label. No transmitir error o éxito solo por color.

## Veracidad del prototipo

- Identifica de forma visible las vistas de diseño y los datos de demostración, si el contrato los permite. Prefiere guiones y estados vacíos a inventar importes, nombres, cuentas o actividad.
- La navegación entre vistas del prototipo puede funcionar. Las acciones de negocio inexistentes deben estar deshabilitadas con explicación próxima; nunca confirmar un guardado o autenticación que no ocurrió.
- El acceso/OTP de Fase 00B es únicamente visual: sin SMS, endpoints de identidad, sesión, almacenamiento de teléfono/código ni transición a usuario autenticado.
- Mantén verificaciones técnicas fuera de la jerarquía de producto; usa un panel de desarrollo cuando corresponda. No alterar backend, persistencia o infraestructura para conseguir una captura.

## Revisar el resultado renderizado

- [ ] Comprobar 320px, un teléfono actual y escritorio: sin scroll horizontal, texto cortado, targets menores de 44px o navegación tapando contenido.
- [ ] Recorrer por teclado, probar focus visible, errores, disabled, carga, vacío y recuperación donde apliquen. El foco no debe quedar en un elemento desmontado al cambiar de vista.
- [ ] Medir contraste de texto normal >= 4.5:1 y componentes/foco >= 3:1; no asumir que verde suave equivale a legible.
- [ ] Ejecutar build/typecheck, tests pertinentes y E2E; distinguir respuestas controladas de consultas al stack real.
- [ ] Capturar los mismos estados en móvil y escritorio, inspeccionar las imágenes y corregir problemas observados. Un build correcto o un validador de SKILL.md no aprueban el diseño.
- [ ] Documentar decisiones, comandos, evidencias y límites. Pedir aprobación visual explícita antes de propagar esta dirección a otros módulos o avanzar de fase.

No añadir dependencias de UI ni recursos remotos sin necesidad concreta. Mantener las propuestas de componentes dentro del prototipo hasta su aprobación.
