# Historial de cambios

Todas las versiones de HdmiMirror, de la más reciente a la más antigua. El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y la numeración sigue el [versionado semántico](https://semver.org/lang/es/).

Las versiones anteriores a la 2.1.0 se entregaron sin número y su código no se conservó: sus entradas están reconstruidas para tener referencia, pero no tienen descarga.

## [2.2.0] - 2026-09-22 - El panel recuerda su posición

### Añadido
- El panel aparece donde se dejó la última vez, también cuando lo abre el lanzador de la mesa. Así no sale encima de la Dealer App. Se guarda al terminar de arrastrarlo.
- Recuerda si **Ajustes avanzados** estaba desplegado.

### Corregido
- Si la posición guardada ya no se ve, por ejemplo porque estaba en un monitor virtual que se quitó, el panel vuelve al centro en vez de abrirse fuera de pantalla.

### A tener en cuenta
- Primera versión que se publica en GitHub ya compilada. El manual explica cómo descargarla y actualizar.

## [2.1.0] - 2026-08-28 - Iniciar el espejo al abrir la app

### Cambiado
- La casilla de arranque pasa a ser **Iniciar el espejo al abrir la app**. El arranque de Windows lo gestiona el lanzador escalonado de cada mesa, no la app.
- El número de versión sale del proyecto y se muestra en el pie del panel.

### Corregido
- Al abrirse, la app borra su registro antiguo de autoarranque (la tarea programada o la clave Run llamadas "HdmiMirror") para no arrancar dos veces junto al lanzador.

## [2.0.0] - 2026-08-28 - Rediseño del panel

### Cambiado
- Panel reorganizado en tres zonas. Arriba, una tarjeta de estado con color, el botón Iniciar/Parar y chips de salud (captura, foco, autoarranque). En el centro, la zona **Mesa**. Abajo, **Ajustes avanzados**, plegados.
- Un único desplegable **Dealer app** alimenta a la vez la captura y el foco. Antes había que elegir la misma ventana dos veces.
- Los modos de clic pasan a llamarse **El supervisor mira: el cristal / el panel físico**.

### Corregido
- El vigilante de foco se vuelve a activar solo si la dealer app se cierra y se reabre.

### A tener en cuenta
- La configuración anterior (`hdmimirror.config.json`) se migra sola.

## [1.3.0] - 2026-08-26 - Autoarranque con Windows

### Añadido
- Casilla para arrancar con Windows e iniciar el espejo solo. Retirada en la 2.1.0 en favor del lanzador de cada mesa.

## [1.2.0] - 2026-08-26 - Rendimiento y estabilidad

### Cambiado
- El vigilante de foco funciona por eventos y no consume nada en reposo. Tras varios fallos seguidos reintenta cada 10 segundos en vez de insistir sin parar.
- FPS por defecto: 20. El cursor y el hook de ratón consumen menos.

### Corregido
- Pantalla en blanco al capturar la ventana de la Dealer App: la app lo detecta y cambia sola a capturar el rectángulo de pantalla. Si la dealer app se reinicia, el espejo la vuelve a encontrar por su título. Los errores se escriben en pantalla en vez de dejarla en blanco.
- Mesa de Ruleta: el vigilante reconoce todas las ventanas de la dealer app (número ganador, rueda...) y ya no pelea contra ellas ni contra el propio panel, que dejaba el PC casi sin responder.

## [1.1.0] - 2026-08-06 - Clics recolocados y ajustes que se guardan

### Añadido
- La configuración se guarda sola en `hdmimirror.config.json`, junto al ejecutable.
- Icono propio.

### Corregido
- Clics recolocados: ya no se pierden clics y se puede mantener un botón pulsado.

## [1.0.0] - 2026-08-05 - Primera versión

### Añadido
- Espejo de una ventana o un monitor sobre la salida del prompter, sin OBS.
- Los clics atraviesan el espejo y llegan a la dealer app.
- El espejo es invisible para RustDesk y OBS: por remoto se ve la dealer app normal, sin monitores virtuales.
- Vigilante de foco para que el escáner de cartas siempre escriba en la dealer app.
- Diagnóstico con Ctrl+Alt+D y atajos globales.
