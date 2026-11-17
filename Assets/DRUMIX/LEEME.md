# DRUMIX · 1 click

## Pasos (5 min)
1. **Pull** del repo y avisa por WhatsApp: "voy a meter DRUMIX, nadie toque la escena DRUMIX_Bar".
2. Si tienes cosas viejas nuestras (carpeta `Assets/BateriaVR`, `AudienceMember.cs`, `DrumGameEvents.cs`...):
   - Si Unity abre **sin errores rojos** → después del paso 3 usa **DRUMIX > Limpiar paquete viejo**.
   - Si hay **errores rojos de duplicados** → cierra Unity y borra a mano `Assets/BateriaVR` (y cualquier `AudienceMember.cs` / `AudienceReactionManager.cs` / `DrumGameEvents.cs` que NO esté en `Assets/Scripts/`). Lo del equipo en `Assets/Scripts/` no se toca.
3. Copia la carpeta **DRUMIX** del zip dentro de `Assets/` (queda `Assets/DRUMIX/`). Espera a que compile.
4. Menú **DRUMIX > ARMAR TODO (1 click)**. Te pide guardar tu escena si hay cambios → Sí.
5. **Play**. PC: **Enter** = aceptar/jugar, **A S D F G H J K** = piezas (bombo, caja, hi-hat, tom1, tom2, tom piso, crash, ride), **Esc/P** = pausa (Esc en submenús = volver). Quest: baquetas + **A/X** = bombo (en menús = aceptar), gatillo = botones, **Menú** (control izq.) = pausa.
6. (Opcional) **DRUMIX > Revisar (autoprueba)** → debe decir TODO BIEN o solo avisos.
7. Cierra Unity → commit pequeño ("DRUMIX one-click") → **Push** → avisa por WhatsApp.

¿Sale algo rojo en la Console? Cópialo y pégalo en el chat.

## Reglas del equipo
- **Pull primero**, siempre.
- **Nadie trabaja a la vez en la misma escena.** DRUMIX solo usa su escena `Assets/DRUMIX/Scenes/DRUMIX_Bar.unity`; no toca las suyas.
- **Cierra Unity antes de Push.**
- **Commits pequeños** y avisar por WhatsApp.
- Solo una persona importa DRUMIX la primera vez (así los `.meta` no chocan); los demás hacen Pull.

## Qué hace el botón
Crea/abre `DRUMIX_Bar.unity` y arma todo bajo **DRUMIX_ROOT** (si lo corres otra vez lo rehace sin duplicados y conserva lo que agregaste a mano): batería fija, baquetas en los controles, público que reacciona y a veces tira cosas, UI completa (menú, Dante, niveles, HUD, pausa, ajustes, calibración, resultados), luces, sonido y bar.
Usa primero los modelos/sonidos **de su proyecto** (busca por nombre: drum-kit, kick/bombo, snare/caja/redoblante, hihat, tom, platillo/crash/ride, palitotambor/baqueta, character/npc, escenario...), luego los **incluidos** (`ThirdParty/`, ver `ASSETS.md`) y si no hay nada, primitivas. La Console muestra qué usó.

## Niveles, pulido visual y no destructivo
- **3 niveles** (El Garaje · El Bar Local · El Gran Concierto) en `Assets/DRUMIX_Config/Niveles/` (fuera del paquete): bpm, patrón, ventanas, % para pasar, piezas, música. ARMAR TODO los crea **solo si faltan**; nunca los pisa. En el menú: **NIVELES** (tarjetas con mejor % y estrellas; el siguiente se abre al pasar el anterior); al ganar pasa solo al siguiente.
- **Visual** en `Assets/DRUMIX_Config/Visual.asset`: ambiente, niebla, fondo, intensidad de luces, bloom/color (PC sí, Quest apagado por defecto), partículas y sus colores.
- **ARMAR TODO no destructivo**: lo que agregues a mano dentro de `DRUMIX_ROOT` se guarda y vuelve al mismo padre; lo de fuera nunca se toca. Lo que se rehace son los objetos generados (y sus valores del Inspector).

## Con SU proyecto (lo que ya está calibrado)
- **Batería**: usa `drum-kit/source/Drum Set_sketchfab.fbx` a tamaño real (la copia de `ESCENARIO 3D/source` se ignora). Como sus piezas se llaman `Cylinder.xxx`, las zonas de golpe son **las mismas que el equipo calibró en 1.NivelUno** (Tambor1-4, Platillo1-2) con **sus sonidos** (`sonidos bateria/tambor1-4`, `Platillo-1`, `platillo2`). Bombo y hi-hat no tenían sensor: DRUMIX les pone zona y sonido incluido.
  - Caja = Tambor2, Tom 1 = Tambor3, Tom 2 = Tambor4, Tom de piso = Tambor1, Crash = Platillo2, Ride = Platillo1.
  - La batería es la del equipo, pero DRUMIX **no** usa los "Cube Interactable" (tienen Rigidbody + Grab de XRI: por eso las piezas se movían/caían al centro).
- **Baqueta**: `palitotambor.fbx` (su Camera y Light importadas se apagan solas).
- **Escenario**: `ESCENARIO 3D/.../stage.obj` girado 180° como en sus niveles, con la tarima al nivel del piso. Lo demás de `3d/` (cocina, cereal, frutas) no se usa como escenario.
- **Lanzables**: el tomate pasa a ser la **manzana** del equipo (`apple/`). Clic de UI: `Sounds/POP`.
- **DifficultyManager** (si está en la escena): su `MargenError` es la ventana de acierto y `VelocidadElementos` multiplica el tempo. **AudioManager** (si está): al terminar suena `ReproducirSFXGanarNivel` / `ReproducirSFXPerderNivel`. Se apaga en `DRUMIX_Juego` → `DrumixTeamBridge`.
- **Limpiar paquete viejo** nunca toca `Assets/Scripts/`, `Assets/Scripts/Editor/` ni `Assets/Editor/` (ahí está `ConfigurarManagers.cs`).

## Nada superpuesto (geometría, z-fighting, materiales)
- **Escenario del equipo**: su tarima queda exactamente en y = 0 (jugador y batería pisan ahí). No se crea la tarima DRUMIX ni el bar incluido, y el piso DRUMIX baja 2 cm (solo se ve fuera del escenario): no hay dos superficies a la misma altura.
- **Batería**: se apoya sola 6 mm sobre la tarima (ni enterrada ni parpadeando). La copia del kit que viene dentro de `ESCENARIO 3D/source` nunca se usa.
- **Público**: personas de **1.60–1.85 m** (se miden con la malla real y se reescalan solas, también al dar Play), pies en el piso, brazos abajo (sin pose en T), sin Animator (se mueven por código en un hijo `Visual`). Delante del borde del escenario, a ≥ 0.9 m entre personas, sin tocar batería, muebles, panel de UI ni al jugador. Si alguno choca, ARMAR TODO lo corre de a 10 cm; si no cabe, lo omite y lo dice en el resumen.
- **Muebles** (bar incluido): lo mismo; los parlantes no atraviesan la tarima y los monitores quedan enteros encima.
- **Materiales**: los generados con los mismos parámetros son UN solo asset compartido (`Assets/DRUMIX/Generated/Materials`). Si un renderer DRUMIX tiene más materiales que submeshes se recorta (si no, Unity dibuja la malla otra vez encima). Mallas duplicadas en el mismo lugar se vacían. Todo URP Lit/Unlit; nada rosado. En Play los colores cambian con MaterialPropertyBlock (no se crean materiales).
- **DRUMIX > Revisar** comprueba todo esto. **DRUMIX > Revisar materiales del proyecto (solo lectura)** lista prefabs/modelos del equipo con materiales sobrantes o rotos, sin tocarlos.

## Tamaños y vista (fijados por código)
- **Batería**: tarola a ~0.65 m del piso, ancho máximo 1.8 m, tarola ~0.42 m delante del jugador.
- **Cámara PC**: sentado, ojos a 1.15 m sobre la tarima, 0.45 m detrás, mirando 15° hacia abajo. Con visor manda el visor.
- **Pads**: PERFECT dorado, GOOD azul, MISS rojo (color puro 0.15 s y luego se desvanece). Partículas salen en el pad.
- **UI (estilo de BateriaVR, rehecha para DRUMIX)**: marco morado "grabado", un solo canvas (`DrumixUI`), todo por código:
  - **Menú**: JUGAR · NIVELES · CALIBRAR BATERÍA · AJUSTES · SALIR, con el nivel elegido y sus reglas (ventanas PERFECT/GOOD y % para pasar).
  - **Dante** (líder de la banda) da la bienvenida la primera vez (texto tipo máquina de escribir, SIGUIENTE / SALTAR).
  - **Niveles**: una tarjeta por nivel de `DRUMIX_Config/Niveles` (bpm, descripción, mejor %, estrellas, BLOQUEADO hasta pasar el anterior). Con más de 3, hay páginas.
  - **HUD**: barra arriba (precisión %, PUNTOS, COMBO, MULTI x1–x4, progreso, tiempo, botón PAUSA), "SIGUIENTE: pieza (tecla)", PERFECT/GOOD/MISS chico arriba al centro (se desvanece en 0.5 s), cuenta 3-2-1 al centro sin tapar nada, y en PC el **carril de notas** de 8 columnas A–K (la nota baja hasta la línea dorada = golpear; la columna destella al tocar). En VR la guía es el aro que se cierra sobre cada pad.
  - **Pausa**: CONTINUAR · REINICIAR · AJUSTES · MENÚ (la canción y las notas se detienen).
  - **Ajustes**: volumen general / música / efectos, latencia (ms), vibración, luces suaves, objetos del público, borrar progreso. Se guardan en el equipo (PlayerPrefs).
  - **Calibración**: mover la batería (izq/der, acercar/alejar, subir/bajar, giro ±5°), guardar o cancelar.
  - **Resultados**: APROBADO / REINTENTAR, %, estrellas, rango S–D, puntos, combo, PERFECT/GOOD/MISS, frase de Dante, SIGUIENTE NIVEL / OTRA VEZ / NIVELES / MENÚ.
  - Las posiciones del HUD están en `DrumixUILayout` y una prueba comprueba que nada se encima.
- **Luz**: direccional 1.2, ambiente en degradado. `Visual.asset` viejo se actualiza UNA vez a estos valores (luego respeta lo que cambies).

## Tips
- UI: en PC sin visor sale **a pantalla completa** sola; con visor/Quest es un panel 3D delante de ti.
- Si el proyecto tiene `ESCENARIO 3D`, lo usa como bar. ¿No cuadra? **DRUMIX > Opciones > Usar escenario 3D del proyecto** (desmarcar) y ARMAR TODO.
- `LevelSystems` del equipo no se duplica; si lo quieres en la escena: **DRUMIX > Opciones > Incluir LevelSystems del equipo**.
- ¿Batería incluida (Grab3D) y tocas de pie? `DRUMIX_ROOT/Kit (bateria fija)` → `DrumixKitLock` → `heightOffset` = 0.25.
- ¿Quieres más público? Selecciona cualquier modelo → **DRUMIX > Convertir selección en público** → Ctrl+D las veces que quieras.
- Scripts del equipo (`DrumPad`, `AudioManager`, `LevelManager`...) se respetan: DRUMIX está en el namespace `Drumix` y nunca los reemplaza. Si una pieza ya tiene el `DrumPad` del equipo, DRUMIX no suena doble.
- Para compilar al Quest: agrega `DRUMIX_Bar` **al final** de Build Settings (no lo hago solo para no tocar ProjectSettings del equipo). Sus niveles deben seguir en 0, 1 y 2 porque `LevelManager`/`DifficultyManager` usan el buildIndex.
- La batería del equipo es grande (para tocar de pie): la caja queda a ~1 m de alto y ~0.4 m delante.

## Actualizar DRUMIX (importante)
`Assets/DRUMIX` trae **sus `.meta` con GUIDs fijos** (siempre los mismos). Para actualizar: con Unity cerrado borra `Assets/DRUMIX` **y** `Assets/DRUMIX.meta`, pega los del zip nuevo (carpeta + `.meta`), abre Unity y corre **DRUMIX > ARMAR TODO**. Si alguna vez se pegó la carpeta SIN `.meta`, los assets de `Assets/DRUMIX_Config` pueden quedar "rotos" (script perdido): ARMAR TODO los detecta, los rehace con valores de fábrica y lo dice en el resumen.
