# DRUMIX · Proyecto COMPLETO (equipo + DRUMIX)

Es **su proyecto** (escenas, drum-kit, palitotambor, sonidos, ESCENARIO 3D, 3d, apple, Scripts, Editor, Prefabs, Samples, VRTemplateAssets, XR, XRI, Settings, TextMesh Pro…) **+ `Assets/DRUMIX`** + ProjectSettings/Packages para que abra directo. Sin `Library/` (Unity la crea).

**Para Andrés:** lee `INSTRUCCIONES_ANDRES_IA.md` (está en la raíz). Su trabajo es solo pulir: niveles y visual en `Assets/DRUMIX_Config/`.

## Abrir (versión COMPLETA)
1. Descomprime en una carpeta **corta** (ej. `C:\U\DRUMIX`). Rutas largas en Windows dan errores.
2. Unity Hub → **Add project from disk** → esa carpeta → **Unity 6000.5.5f1**. La primera vez tarda (importa y baja paquetes).
3. Solo se configura y arma `Assets/DRUMIX/Scenes/DRUMIX_Bar.unity` (queda **al final** de Build Settings y se abre). Si pide reiniciar por el Input System → **Reiniciar**.
4. **Play**: Enter = empezar · A S D F G H J K = piezas. Con Quest Link: visor puesto y Play.
5. (Opcional) **DRUMIX > Revisar (autoprueba)** → debe decir TODO BIEN o solo avisos.

## Abrir (versión LIVIANA, DRUMIX_Proyecto_Liviano.zip)
Trae todo MENOS `Assets/3d`, `Assets/VRTemplateAssets` y `Assets/Samples` (~230 MB, ya están en su Drive). Sus `.meta` raíz sí vienen.
1. Descomprime.
2. **Antes de abrir Unity**, copia dentro de `Assets/` esas 3 carpetas (de `3d.zip`, `VRTemplateAssets.zip`, `Samples.zip` del Drive).
3. **Borra** `Assets/3d/bowlcreal/source/cereallll.blend` y su `.meta` (error rojo si el PC no tiene Blender; nadie lo usa).
4. Sigue los pasos 2–5 de arriba.

## ¿Algo raro?
- Menú **DRUMIX > ARMAR TODO** lo vuelve a armar (no duplica nada, no toca sus escenas). **No destructivo:** lo que agregaste dentro de `DRUMIX_ROOT` vuelve a su lugar; lo de fuera no se toca; `Assets/DRUMIX_Config/` (niveles y visual) nunca se pisa.
- **DRUMIX > Proyecto > Repetir configuración inicial** repite el arranque automático.
- Error rojo en la Console → cópialo y pégalo en el chat.

## Qué NO cambió
- Sus escenas (`1.NivelUno`, `2.NivelDos`, `3.NivelTres`, `BackUpNiveles`, `SampleScene`, `BasicScene`) están **intactas** (mismo archivo, mismo GUID).
- Build Settings: 0 = 1.NivelUno, 1 = 2.NivelDos, 2 = 3.NivelTres (su `LevelManager`/`DifficultyManager` usan el buildIndex). DRUMIX_Bar va después.
- URP: **sus** `Performance URP Config` (Low/Medium/Android) y `Quality URP Config` (High/PC) + su `UniversalRenderPipelineGlobalSettings`. XR: **sus** `Assets/XR` y `Assets/XRI`.

## Paquetes (Packages/manifest.json)
Unity 6000.5.5f1 · URP 17.5 · uGUI 2.5 · Input System 1.19 · XRI 3.5.1 · XR Hands 1.8.1 · XR Management 4.7 · OpenXR 1.19 · Meta OpenXR 2.7.
Su ProjectSettings/Packages originales no venían en el Drive: si el repo tiene otras versiones, se igualan.

## Arreglos aplicados
- **Faltantes del equipo** → `Assets/Scripts/_Recuperados/` con el MISMO GUID que usan las escenas (nada configurado se pierde):
  - `TargetZoneTrigger` (SensorBowl), `AutoInstructions` (GestorInstrucciones), `SequenceManager.ValidarError()`.
  - Ya **funcionan de verdad**: `TargetZoneTrigger` suena y cuenta acierto/fallo, `AutoInstructions` muestra las instrucciones, `SequenceManager.ValidarError()` registra el fallo (DRUMIX le avisa cada fallo real, sin contar doble).
  - `successSound_recuperado.wav`: campanita sintetizada propia (**CC0**) con el GUID que esperan las escenas.
  - Subir los originales es **opcional**: borra el archivo + `.meta` de `_Recuperados` y pon el original con SU `.meta`.
- **Juego completo en ARMAR TODO**: 3 niveles (El Garaje 76 bpm básico · El Bar Local 88 bpm rock · El Gran Concierto 104 bpm corcheas) con guía visual en los pads, PERFECT/GOOD/MISS, combos, % 0–100, gana con 50 % y pasa al siguiente. Editables en `Assets/DRUMIX_Config/Niveles/*.asset`.
- **Pulido visual (Quest 3S)**: ambiente degradado morado, niebla barata, 1 direccional + 4 puntuales sin sombras, fondo sólido, bloom/color suave (en PC; en Quest apagado salvo que lo actives), partículas aditivas de acierto/fallo/combo/lanzamiento/confeti, pads que destellan dorado/celeste/rojo, UI con fundidos. Todo en `Assets/DRUMIX_Config/Visual.asset`.
- **API obsoleta** en `DifficultyManager.cs` y `Editor/ConfigurarManagers.cs` → versión nueva (mismo comportamiento, 0 warnings).
- Quitados `cereallll.blend` (error sin Blender) y 5 texturas `Zyklus1_*.JPEG` duplicadas sueltas en `Assets/`.
- **Nada superpuesto** en la escena DRUMIX: su escenario queda con la tarima en y = 0 (sin tarima/piso DRUMIX a la misma altura), batería apoyada, público delante del escenario y separado, materiales generados sin duplicar, sin ranuras de material de más, todo URP.
- Revisado: 0 GUIDs duplicados, 0 `.meta` huérfanos, 0 errores y 0 warnings de compilación, ProjectSettings apuntando a archivos que existen.

## Avisos que quedan (no son errores)
- `VRTemplateAssets/Tutorial` necesita el paquete *Tutorial Framework*; sin él son assets sin script (no da errores de compilación).
- `palitotambor.fbx` trae Camera y Light dentro (en sus escenas = cámara extra por baqueta; DRUMIX las apaga en su escena).
- Sus tambores en NivelUno son "Cube Interactable" con Rigidbody + XR Grab (por eso se movían). DRUMIX usa sus posiciones y sonidos con zonas fijas.
- El objeto "Grid" de sus escenas tiene 2 materiales en un plano (grilla del template VR, a propósito). No se tocó.
