# HdmiMirror

[![Compilar](https://github.com/Vilchaco/kerma-hdmi-mirror/actions/workflows/build.yml/badge.svg)](https://github.com/Vilchaco/kerma-hdmi-mirror/actions/workflows/build.yml)
[![Última versión](https://img.shields.io/badge/descargar-%C3%BAltima%20versi%C3%B3n-2ea44f)](https://github.com/Vilchaco/kerma-hdmi-mirror/releases/latest)

Espejo de pantalla para los prompters de las mesas de Kerma Games. Muestra la Dealer App invertida en el monitor del prompter para que se lea bien en el reflejo del cristal, y sustituye al montaje de OBS con monitores virtuales de RustDesk.

## Descargar

Descarga siempre la **[última versión](https://github.com/Vilchaco/kerma-hdmi-mirror/releases/latest)**. El zip está en el apartado **Assets** de la release y trae el programa ya compilado: no hace falta instalar .NET. Las notas de cada versión están en [CHANGELOG.md](CHANGELOG.md).

## Instalar o actualizar en una mesa

1. Copia el zip al PC de la mesa.
2. Clic derecho en el zip, **Propiedades**, marca **Desbloquear** y acepta.
3. Clic derecho en el zip y **Extraer todo**, por ejemplo en `C:\HdmiMirror`. Para actualizar, extrae encima de la carpeta anterior: la configuración de la mesa se conserva.
4. Doble clic en `HdmiMirror.exe`. Si Windows muestra "Windows protegió tu PC": **Más información** y **Ejecutar de todas formas**.

Si la Dealer App de esa mesa corre como administrador, HdmiMirror también tiene que hacerlo. Si no, Windows le bloquea el foco del escáner sin avisar.

## Qué resuelve

| Problema con OBS | En HdmiMirror |
|---|---|
| La proyección se come los clics | Los clics atraviesan el espejo y llegan a la Dealer App |
| El escáner de cartas pierde el foco | Devuelve el foco a la Dealer App en cuanto lo pierde |
| Por RustDesk se ve la imagen invertida | El espejo es invisible para las capturas: por remoto se ve la Dealer App normal |
| Hacen falta monitores virtuales | No hacen falta |

## Uso diario

| Atajo | Qué hace |
|---|---|
| `Ctrl+Alt+M` | Para el espejo y muestra el panel. Es la salida si el espejo tapa la pantalla. |
| `Ctrl+Alt+F` | Pausa o reanuda la devolución de foco, para usar otra app en ese PC. |
| `Ctrl+Alt+D` | Guarda un diagnóstico en el Escritorio. Adjúntalo al reportar un fallo. |

Con **Iniciar el espejo al abrir la app** marcado, basta con que el lanzador de la mesa abra `HdmiMirror.exe`: espera a la Dealer App e inicia el espejo solo.

El manual completo, con la configuración recomendada y los problemas frecuentes, va dentro del zip: `MANUAL.html`.

## Para quien mantiene la app

- Código en [`src/`](src): C# con WinForms y .NET 8.
- Cada cambio en `main` se compila solo en Windows ([Actions](https://github.com/Vilchaco/kerma-hdmi-mirror/actions)).
- Cómo publicar una versión nueva: [docs/PUBLICAR.md](docs/PUBLICAR.md).
