## Qué cambia

<!-- Una o dos frases. Enlaza la historia: H-x.y o #issue. -->

## Cómo se probó

<!-- Pruebas automatizadas nuevas o modificadas, y pruebas manuales si aplica. -->

## Lista de verificación

- [ ] Las reglas de negocio están en `Tiie.Dominio` o `Tiie.Aplicacion`, no en el endpoint ni en React.
- [ ] Uso `TimeProvider` / `RelojAgenda`; nada de `DateTime.Now` ni `new Date()` para reglas.
- [ ] Los endpoints nuevos tienen política, verifican el módulo y están en la matriz de permisos.
- [ ] No hay datos personales sin enmascarar en logs, errores ni respuestas.
- [ ] Las pruebas fallan sin este cambio; las de reglas llevan el ID `RN-xx` en el nombre.
- [ ] La migración, si hay, es compatible con la versión anterior del código.
- [ ] Ejecuté `npm run api:tipos` si cambié un endpoint.
- [ ] Actualicé la documentación de `docs/` afectada (o no aplica).
- [ ] Funciona con teclado y en tamaño de celular (si toca la interfaz).

## Notas para quien revisa

<!-- Decisiones, riesgos, dudas o capturas de pantalla. -->
