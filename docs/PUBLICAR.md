# Cómo publicar una versión nueva

Guía para quien mantiene la app. Los técnicos solo necesitan descargar la última release.

## 1. Hacer el cambio

- Edita el código en `src/`.
- Sube el número de versión en **un solo sitio**: la línea `<Version>` de `src/HdmiMirror.csproj`. El pie del panel y el nombre del zip salen de ahí.
- Sube el cambio a `main` y espera a que **Compilar** salga en verde en la pestaña Actions antes de publicar.

## 2. Elegir el número

| Tipo de cambio | Ejemplo | Número |
|---|---|---|
| Arreglo de un fallo | El espejo sale en blanco en una mesa | 2.2.0 pasa a 2.2.1 |
| Función nueva | Recordar la posición del panel | 2.2.0 pasa a 2.3.0 |
| Cambio grande que rompe lo anterior | Rediseño del panel, configuración incompatible | 2.2.0 pasa a 3.0.0 |

## 3. Escribir las notas en `CHANGELOG.md`

Añade arriba una sección con este formato exacto. El título que va detrás de la fecha es el nombre de la release:

```markdown
## [2.2.1] - 2026-10-20 - Arreglo del espejo en blanco

### Corregido
- Qué fallaba y qué hace ahora, en una o dos frases.
```

## 4. Publicar

```bash
git add -A
git commit -m "v2.2.1: arreglo del espejo en blanco"
git tag -a v2.2.1 -m "v2.2.1"
git push --follow-tags
```

GitHub hace el resto solo:

- **Release** comprueba que la etiqueta coincide con `<Version>` del proyecto, compila el exe en Windows, construye `HdmiMirror-v2.2.1.zip` con el exe, el manual y el historial, y crea la release con las notas del CHANGELOG marcada como la última.

Revisa la pestaña **Actions**. Si sale en rojo, abre el error, corrígelo y publica con un número nuevo.

Sube las etiquetas de una en una: si se suben más de tres a la vez, GitHub no lanza las Actions.

## Compilar en local

En un Windows con el SDK de .NET 8: doble clic en `src/COMPILAR.bat`. Deja el exe en `src/Compilado/`.
