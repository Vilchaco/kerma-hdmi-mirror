# Marca

HdmiMirror sigue el **Kerma Games Brandbook 2026** (págs. 21 y 26). El PDF no está en este repositorio porque es público; lo tiene Fran.

Los colores se definen en un solo sitio, [`src/Marca.cs`](../src/Marca.cs). El resto de la app solo usa esos nombres. Son los mismos valores que el bloque `BRANDBOOK` de Tech Inventory, así que las apps de Kerma se ven como una sola familia.

## Colores

| Token | Valor | Nombre en el brandbook | Uso |
|---|---|---|---|
| `Fondo` | `#0A0712` | Kerma Black Magic | Fondo de la ventana y del manual |
| `Superficie` | `#110C1F` | Kerma Jambolan | Tarjeta de estado, desplegables, casillas |
| `Superficie2` | `#161029` | Secundario | Botones neutros, chips, opción seleccionada |
| `Borde` | `#353041` | Secundario | Bordes de todos los controles |
| `Texto` | `#FFFFFF` | Blanco | Texto principal |
| `Acento` | `#EA7F2B` | Kerma Honeybell | Solo el botón **Iniciar espejo** |
| `Mut` / `Mut2` | derivados de `#CFCED2` | Gris | Etiquetas y textos secundarios |

Colores de estado (no son de marca, solo en puntos pequeños): correcto `#5FB878`, problema `#E05555`, aviso `#F4C95D`. Los avisos van en amarillo para no confundirse con el naranja.

## Letra

- **Sora** es la letra de toda la app. **Orbitron** solo va en la marca, arriba del panel.
- Las dos viajan dentro del exe (`src/Marca/`, licencia OFL en `src/Marca/OFL.txt`) porque los PCs de las mesas no las tienen instaladas. Si no cargan, la app usa Segoe UI.

## Reglas

- Un solo elemento naranja por pantalla: **Iniciar espejo**. "Parar", la opción seleccionada y las casillas son neutras.
- Sin rellenos verdes ni textos rojos: el estado se lee en un punto de color.
- Sin degradados. El brandbook tiene degradados de naranja a amarillo, pero no se usan.
- Los textos no se parten en dos líneas ni se cortan. Si un cambio lo provoca, ajusta la retícula del principio de `PanelControl.cs` (`Ancho`, `ColumnaEtiqueta`).

## Comprobar un cambio visual

En la pestaña **Actions**, lanza **Capturas**. Abre el panel en Windows, lo pone en cinco estados (parado, ajustes avanzados, aviso, en marcha y sin imagen) y sube una imagen de cada uno en el artefacto `capturas`.
