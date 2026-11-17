# DRUMIX · Instrucciones para Andrés (pégale esto a tu IA: Cursor / ChatGPT)

> **Para la IA:** eres asistente en un proyecto Unity VR que **ya está terminado y funcionando**. El trabajo de Andrés es
> **solo PULIR** (ajustar valores, sonidos, visuales y posiciones). **No escribas ni edites código dentro de `Assets/DRUMIX/`.**
> Antes de proponer algo, lee todo este archivo. No inventes APIs (abajo están los nombres REALES). Responde en español.

## 1. Contexto
- Juego VR de batería **DRUMIX** para **Meta Quest 3S** (también se prueba en PC con teclado o Quest Link).
- **Unity 6000.5.5f1**, URP. Paquetes: URP 17.5 · uGUI 2.5 · Input System 1.19 · XR Interaction Toolkit 3.5.1 · XR Hands 1.8.1 · XR Management 4.7 · OpenXR 1.19 · Meta OpenXR 2.7.
  (Deducidas de las carpetas del Drive; si `Packages/manifest.json` del repo dice otra cosa, manda el repo.)
- Samuel entrega la escena **`Assets/DRUMIX/Scenes/DRUMIX_Bar.unity` terminada**. `DRUMIX > ARMAR TODO` ya hizo TODO:
  - batería fija con zonas de golpe y sus sonidos, baquetas en los controles;
  - **3 niveles** con secuencia y combos: **El Garaje** (76 bpm, básico), **El Bar Local** (88 bpm, rock), **El Gran Concierto** (104 bpm, corcheas + redobles);
  - guía visual (el aro del pad que toca se ilumina y se cierra), acierto/fallo, PERFECT/GOOD/MISS, % de 0 a 100, **gana con 50 %**, pasa al siguiente nivel;
  - público que aplaude/abuchea y lanza cosas al fallar, luces de escenario que reaccionan;
  - partículas (acierto, fallo, combo, lanzamiento, confeti al ganar), post-procesado suave, UI con transiciones;
  - conexión con lo del equipo: `DifficultyManager`, `AudioManager`, `SequenceManager.ValidarError()` (por reflexión, sin lógica duplicada).
- **Flujo:** Samuel sube la escena → Andrés hace Pull y pule encima → mientras tanto **Samuel no abre la escena**.

## 2. Carpetas: qué es de quién
| Ruta | Dueño | Regla |
|---|---|---|
| `Assets/DRUMIX/**` (Scripts, Editor, ThirdParty, Generated) | Samuel | **NO tocar código ni assets.** Si hace falta un cambio, pídeselo a Samuel. |
| **`Assets/DRUMIX_Config/`** | **Andrés** | **Aquí pules.** Niveles y config visual (ScriptableObjects). ARMAR TODO los crea solo si faltan y **nunca los pisa**. |
| `Assets/DRUMIX/Scenes/DRUMIX_Bar.unity` | por turnos | Andrés la abre, mueve cosas y agrega lo suyo (ver §5). |
| `Assets/Scripts/**`, `Assets/Editor/**`, `Assets/Prefabs/**`, `Assets/Sounds/**` | equipo | Lo de ustedes. |
| `Assets/Scripts/_Recuperados/` | provisional | Reemplazos que YA funcionan (ver §6). Opcional subir los originales. |
| `Assets/Scenes/1.NivelUno`, `2.NivelDos`, `3.NivelTres` | equipo | Intactas. |
| `Assets/DRUMIX_Proyecto/` | solo zip | No va al repo. |

## 3. Qué puedes pulir y DÓNDE (lista concreta)
### 3.1 Niveles: patrones, tempo, dificultad, ventanas → `Assets/DRUMIX_Config/Niveles/`
`Nivel1_ElGaraje.asset`, `Nivel2_ElBarLocal.asset`, `Nivel3_ElGranConcierto.asset`. Selecciónalo y edita en el Inspector:

| Campo | Qué hace | Por defecto (1 / 2 / 3) |
|---|---|---|
| `name`, `subtitle` | título y subtítulo en el menú | El Garaje · El Bar Local · El Gran Concierto |
| `pattern` | `Basico` (negras bombo/caja), `Rock` (+ redobles de toms), `Concierto` (corcheas + redobles cada 2 compases) | Basico / Rock / Concierto |
| `bpm` | tempo (50–180) | 76 / 88 / 104 |
| `bars` | duración en compases (2–64) | 8 / 12 / 16 |
| `seed` | variación del patrón (otro número = otra canción) | 11 / 1234 / 777 |
| `perfectWindow`, `goodWindow` | ventana de acierto en segundos (más grande = más fácil) | 0.12/0.28 · 0.10/0.22 · 0.07/0.13 |
| `passPercent` | % para ganar | 50 |
| `leadTime` | segundos que el aro avisa antes de la nota | 1.8 / 1.4 / 1.2 |
| `comboStep` | cada cuántos aciertos seguidos hay hito de combo | 10 |
| `lanes` | piezas que se usan (Kick, Snare, HiHat, Tom1, Tom2, FloorTom, Crash, Ride) | nivel 1: Kick, Snare, HiHat, Crash |
| `music`, `musicVolume` | pista de fondo opcional (loop mientras se juega) | vacío |

- Si pones valores locos se corrigen solos (`Sanitize`): la ventana nunca tapa la nota siguiente.
- **Nivel nuevo:** clic derecho en `Assets/DRUMIX_Config/Niveles` → `Create > DRUMIX > Nivel`, y vuelve a correr ARMAR TODO (lo agrega al final) **o** arrástralo a `DRUMIX_Juego > DrumixGame > Levels`.
- Mientras haya niveles, sus ventanas mandan sobre `DifficultyManager.MargenError`.

### 3.2 Visual → `Assets/DRUMIX_Config/Visual.asset`
Luz ambiente (cielo/horizonte/suelo), color de fondo, niebla (sí/no, color, densidad), `stageLightMultiplier`, post-procesado (`postProcessingPC`, `postProcessingQuest` **apagado por rendimiento**, bloom, saturación), partículas (activar cada efecto, colores PERFECT/GOOD/MISS/combo, `maxParticlesPerEffect`).
Estos valores los aplica **ARMAR TODO** (luz, niebla, post) o se leen al jugar (partículas, post en Quest).

### 3.3 Sonidos
- Golpes: `DRUMIX_ROOT/Kit (bateria fija)/…` → cada `DrumixPad` → campo `clip`. Hoy usan `sonidos bateria/` del equipo; bombo y hi-hat usan los incluidos.
- Clic de UI: `Sounds/POP`. Ganar/perder: `AudioManager` del equipo (si está en la escena).
- Público: `Publico` → `DrumixCrowdAudio` (`cooldown`).
- Para cambiar un sonido sin tocar nada: reemplaza el archivo en SU carpeta **conservando su `.meta`** (mismo nombre), o arrastra otro clip al campo.

### 3.4 Campos del Inspector en la escena (componentes de DRUMIX, solo valores)
| Objeto → componente | Campos útiles |
|---|---|
| `DRUMIX_Juego` → `DrumixGame` | `countdownSeconds`, `levelIndex` (nivel inicial), `levels` |
| `DRUMIX_Juego` → `DrumixTeamBridge` | `useTeamDifficulty`, `useTeamAudioManager`, `forwardMissesToTeamSequence`, `onSongStart/onPass/onFail` (nombres de métodos del equipo) |
| `DRUMIX_Juego` → `DrumixInput` | `keyboard`, `xrButtons` |
| cada pad → `DrumixPad` | `clip`, `minStickSpeed` (fuerza mínima), `minInterval`, colores `cueColor/hitColor/goodColor/missColor` |
| baquetas → `DrumixStick` | `hapticDuration` |
| `Publico` → `DrumixThrowDirector` | `throwOnFail`, `occasionalRandom`, `sideOffset`, `templates` (qué lanzan) |
| NPCs → `DrumixCrowdNPC` | `lookAtPlayer`, `canThrow` |
| `Luces` → `DrumixStageFX` | `perfectColor`, `goodColor`, `missColor` |
| `FX (partículas)` → `DrumixFX` | `config`, `celebrationPoint` (dónde sale el confeti) |
| `UI (world-space, VR + PC)` → `DrumixUI` | `metersPerPixel` (tamaño), `modo` (Auto/VR/PC) |

⚠️ Lo que cambies **en objetos generados** (dentro de `DRUMIX_ROOT`) se pierde si alguien vuelve a correr ARMAR TODO. Por eso: lo importante va en `Assets/DRUMIX_Config/` (sobrevive siempre) y anota en el commit qué valores cambiaste.

### 3.5 Posiciones
- Batería: mueve el objeto **`Kit (bateria fija)` entero**, nunca piezas sueltas. UI: mueve `UI (world-space, VR + PC)`. Público: mueve `Publico` o NPCs sueltos.
- Después corre **`DRUMIX > Revisar (autoprueba)`**: avisa si algo quedó superpuesto, parpadeando (z-fighting) o rosado.

## 4. ARMAR TODO ya no destruye lo tuyo
- Lo que **agregues dentro de `DRUMIX_ROOT`** (un objeto hijo nuevo, decoración, etc.) se guarda y vuelve al mismo padre al rearmar. Lo que está **fuera de `DRUMIX_ROOT`** nunca se toca.
- Lo que se rehace: los objetos generados y sus valores del Inspector (ver ⚠️ arriba). Los assets de `Assets/DRUMIX_Config/` no se pisan nunca.
- Aun así: **no hace falta correr ARMAR TODO**; la escena ya viene armada. Solo si Samuel lo pide.

## 5. Trabajar encima de DRUMIX_Bar
Estructura: `DRUMIX_ROOT` → `Bar (escenario)`, `Luces`, `DRUMIX_Juego (eventos + secuencia)`, `Kit (bateria fija)`, `XR Origin (DRUMIX)`, `Publico`, `UI (world-space, VR + PC)`, `FX (partículas)`, `Post-procesado (global)` + `EventSystem (DRUMIX)` aparte.
- Tus objetos: dentro de `DRUMIX_ROOT` (se conservan) o en un objeto raíz propio/Prefab propio.
- **No** agregues `Rigidbody` dinámico ni `XRGrabInteractable` a la batería (por eso antes se caía al centro).
- Quest 3S: no agregues luces con sombras ni luces en tiempo real extra; partículas pocas; texturas ≤ 2048.

## 6. Placeholders de `_Recuperados` (ya funcionan, subir originales es OPCIONAL)
- `TargetZoneTrigger.cs` (GUID `5165c13111c480041b9dcbafc6734a41`): al entrar `targetObjectName` suena `successSound` y cuenta acierto; con `wrongObjectIsMiss` cuenta fallo.
- `AutoInstructions.cs` (GUID `f75ea35e2c5490b449d9d2c3e89e9ea5`): muestra `instructionsUI` por `displayTime` s.
- `SequenceManager.cs`: `ValidarError()` registra un fallo; DRUMIX le avisa cada fallo real.
- `successSound_recuperado.wav` (GUID `af23204643ad277439305ad282ed0ab4`): campanita propia CC0.
- Si quieres subir los originales: **con Unity cerrado** borra el `.cs`/`.wav` **y** su `.meta` y pon el original **con su `.meta` original** (mismo GUID). Nunca dos clases con el mismo nombre.

## 7. Si alguna vez necesitas escuchar el juego desde un script tuyo (opcional)
Va en `Assets/Scripts/` (nunca en `Assets/DRUMIX/`). El bus es **`Drumix.DrumixEvents`** (`DrumGameEvents` ya NO existe).
```csharp
// Eventos: OnPadStruck(DrumixLane, float) · OnHit(DrumixHitInfo) · OnMiss(DrumixHitInfo) · OnCombo(int)
//          OnStateChanged(DrumixState) · OnScoreChanged(float %, int combo) · OnSongFinished(float %, bool gano)
// Tipos:   DrumixLane {Kick, Snare, HiHat, Tom1, Tom2, FloorTom, Crash, Ride} · DrumixJudgement {Perfect, Good, Miss}
//          DrumixState {Menu, Countdown, Playing, Results} · DrumixHitInfo {lane, judgement, offset, combo, stray}
using UnityEngine;
using Drumix;

public class MiEfectoAndres : MonoBehaviour
{
    public AudioSource sonidoGanar;

    void OnEnable()  { DrumixEvents.OnHit += Acierto; DrumixEvents.OnSongFinished += Fin; }
    void OnDisable() { DrumixEvents.OnHit -= Acierto; DrumixEvents.OnSongFinished -= Fin; }

    void Acierto(DrumixHitInfo info)
    {
        if (info.judgement == DrumixJudgement.Perfect) Debug.Log("PERFECT en " + info.lane + " · combo " + info.combo);
    }

    void Fin(float porcentaje, bool gano)
    {
        if (gano && sonidoGanar) sonidoGanar.Play();
    }
}
```
**Solo escuchar.** No llames `ReportHit/ReportMiss/ReportFinished` (DrumixGame ya es la secuencia; se contaría doble).

## 8. Build Settings (no reordenar)
0 = `Assets/Scenes/1.NivelUno` · 1 = `2.NivelDos` · 2 = `3.NivelTres` · **al final** `Assets/DRUMIX/Scenes/DRUMIX_Bar.unity`.

## 9. Reglas Git
- **Pull primero**, siempre, antes de abrir Unity. **Cierra Unity antes de Push.**
- **Commits pequeños** con mensaje claro ("pulo nivel 2: bpm 92, ventana 0.2") y avisar por WhatsApp.
- **Nunca** dos personas en la misma escena a la vez (mientras Andrés pule `DRUMIX_Bar`, Samuel no la abre).
- El paquete DRUMIX lo importó **una sola persona (Samuel)**. Nunca borres ni regeneres `.meta`; súbelos junto a su archivo.

## 10. Checklist antes de Push
- [ ] Console sin errores rojos.
- [ ] `DRUMIX > Revisar (autoprueba)` → "TODO BIEN" o solo avisos.
- [ ] Play: Enter empieza, `NIVEL ▶` cambia de nivel, A S D F G H J K tocan; los 3 niveles se pueden ganar (≥ 50 %) y perder.
- [ ] La batería no se mueve al golpear. Nada rosado ni parpadeando.
- [ ] (Quest) se ve fluido; si no, baja `maxParticlesPerEffect` o apaga niebla en `Visual.asset`.
- [ ] `git status`: **nada modificado dentro de `Assets/DRUMIX/`** (solo escena, `DRUMIX_Config` y lo tuyo).
- [ ] Cada asset nuevo con su `.meta`.

## 11. Qué reportarle a Samuel
- Valores que cambiaste (nivel, campo, antes → después) y objetos que moviste o agregaste.
- Cualquier error rojo: texto completo copiado + pasos para repetirlo.
- Si subiste los originales de `_Recuperados` (cuáles).
- Cómo corre en el Quest 3S (fluido / tirones, en qué momento).
- Versiones reales de `Packages/manifest.json` y `ProjectSettings/ProjectVersion.txt` si no coinciden con §1.
