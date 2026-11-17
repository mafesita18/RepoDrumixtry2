# Guía de integración (niveles, dificultad y audio)

Autor de estos sistemas: Guti. **No dupliquen estos scripts: edítenlos o llámenlos.**
Si cambian algo, avisen por el grupo con el nombre exacto del script y qué hace.

## 0. Antes de empezar
1. `git pull` y abrir el proyecto con Unity **6000.3.9f1**.
2. Escenas (File > Build Profiles), el orden NO se debe cambiar:

   | Índice | Escena | Dificultad que aplica |
   |---|---|---|
   | 0 | `1.NivelUno` | Fácil |
   | 1 | `2.NivelDos` | Medio |
   | 2 | `3.NivelTres` | Difícil |

   `2.NivelDos` y `3.NivelTres` son copias del Nivel 1: cada una tiene su propio `GameController`.
3. **Siempre empiecen a probar desde `1.NivelUno`.** El `AudioManager` solo existe ahí y luego persiste.
   Si dan Play desde el Nivel 2 o 3, `AudioManager.Instance` será `null` (no hay sonido).
4. Evitar que dos personas editen la **misma escena** a la vez (los conflictos de `.unity` son difíciles).
   Para cosas compartidas, usar prefabs.

## 1. Qué hay en cada escena
| Objeto | Componentes | Qué hace |
|---|---|---|
| `GameController` (cada nivel) | `LevelManager`, `DifficultyManager` | Pasar/reiniciar nivel y leer la dificultad del nivel actual |
| `AudioManager` (solo Nivel 1) | `AudioManager` | Ambiente en loop + SFX. Persiste entre escenas |

Prefab de respaldo: `Assets/Prefabs/LevelSystems.prefab` (LevelManager + DifficultyManager ya enlazados).

## 2. Cómo llamar a cada sistema desde su script

### LevelManager.cs
```csharp
[SerializeField] private LevelManager levelManager;   // arrastrar el GameController en el Inspector
// (o: levelManager = FindFirstObjectByType<LevelManager>();)

levelManager.CargarSiguienteNivel(); // al GANAR: 1 -> 2 -> 3. En el 3 no hace nada (solo log)
levelManager.ReiniciarNivel();       // al PERDER / botón "Reintentar"
```
Propiedad: `levelManager.IndiceNivelActual` (índice de escena actual).

### DifficultyManager.cs (mismo `GameController`)
```csharp
[SerializeField] private DifficultyManager dificultad;

float velocidad = dificultad.VelocidadElementos; // multiplicador (1 = base)
float margen    = dificultad.MargenError;        // segundos de tolerancia para un acierto
int   fallosMax = dificultad.FallosPermitidos;   // 0 = sin límite. ¡Quien cuente fallos lo compara!
float tiempo    = dificultad.TiempoLimite;       // 0 = sin límite

dificultad.IniciarTemporizador();   // llamar cuando EMPIEZA la secuencia (no arranca solo por defecto)
dificultad.DetenerTemporizador();   // llamar al ganar
dificultad.OnTiempoAgotado.AddListener(MiMetodo); // o conectarlo en el Inspector
```
- Si se agota el tiempo, por defecto llama a `ReiniciarNivel()` (se desactiva con `reiniciarNivelAlAgotarTiempo`).
- **Los números actuales son de propuesta** (Fácil/Medio/Difícil). Se editan en el Inspector del `DifficultyManager`
  y deben acordarlos Andrés, Samuel Z. y Guti. Si cambian, cambiarlos en las 3 escenas (o en el prefab y reaplicar).
- El `DifficultyManager` NO cuenta fallos: lo debe hacer el script de secuencia (Andrés) y comparar con `FallosPermitidos`.

### AudioManager.cs (Singleton)
```csharp
if (AudioManager.Instance != null)
{
    AudioManager.Instance.ReproducirSFXExito();       // acierto en la secuencia
    AudioManager.Instance.ReproducirSFXError();       // fallo
    AudioManager.Instance.ReproducirSFXGanarNivel();  // victoria
    AudioManager.Instance.ReproducirSFXPerderNivel(); // derrota
    AudioManager.Instance.ReproducirSFXClicBoton();   // botones de UI
}
AudioManager.Instance.SetVolumenMaestro(0.8f);   // 0 a 1 (también SetVolumenAmbiente / SetVolumenSFX)
```
Botones de UI (Mafe): en el `OnClick` del botón se puede arrastrar el objeto `AudioManager` y elegir
`ReproducirSFXClicBoton`. Como `AudioManager` no existe en el Nivel 2 y 3, para botones de esas escenas
es mejor llamar por código a `AudioManager.Instance` (como arriba).

## 3. Flujo recomendado (quién conecta qué)
| Evento del juego | Quién lo dispara | Qué debe llamar |
|---|---|---|
| Empieza la secuencia | Andrés | `dificultad.IniciarTemporizador()` |
| Golpe correcto | Andrés | `AudioManager.Instance.ReproducirSFXExito()` + avisar al público (Samuel Z.) |
| Golpe incorrecto | Andrés | `ReproducirSFXError()`, sumar fallo; si fallos ≥ `FallosPermitidos` (y ≠ 0) → derrota |
| Secuencia completa | Andrés | `DetenerTemporizador()`, `ReproducirSFXGanarNivel()`, pantalla "ganaste" (Mafe/Martín/Mari) y luego `CargarSiguienteNivel()` |
| Derrota (fallos o tiempo) | Andrés / `OnTiempoAgotado` | `ReproducirSFXPerderNivel()` y `ReiniciarNivel()` (o pantalla de derrota con botón) |
| Público reacciona | Samuel Z. | Escuchar los mismos eventos de acierto/fallo y `OnTiempoAgotado` |

Sugerencia: poner una pausa de 2-3 s (corrutina) antes de `CargarSiguienteNivel()` para que se oiga `SFX_GanarNivel`
y se vea la pantalla de victoria. Ojo: `DrumPad.cs` hoy solo reproduce su sonido; `BeatManager.cs` está vacío.
**Todavía nada llama a `CargarSiguienteNivel()`**: ese es el disparador pendiente de la secuencia.

## 4. Audios (`Assets/Sounds`)
`Ambiente_Concierto`, `SFX_ClicBoton`, `SFX_Exito`, `SFX_Error`, `SFX_GanarNivel`, `SFX_PerderNivel`
están asignados en el `AudioManager` del Nivel 1. Volúmenes por defecto: maestro 0.8, ambiente 0.4, SFX 0.7.
- Para cambiar un audio: reemplazar el archivo **con el mismo nombre** (se conserva su `.meta`) o arrastrar
  otro clip al Inspector del `AudioManager`.
- Evitar archivos pesados (WAV de ambiente de >100 MB bloquean el push a GitHub). Usar MP3/OGG o WAV comprimido.
- Los `.asd` son archivos temporales del editor de audio y están ignorados por git.

## 5. Herramienta de editor (menú `Drumixtry`)
`Assets/Editor/ConfigurarManagers.cs`
1. *Montar Nivel 1*: crea/configura `AudioManager` y los managers en la escena abierta.
2. *Montar LevelSystems en escena actual*: para escenas de nivel nuevas (añade `DifficultyManager` si falta).
3. *Asignar sonidos al AudioManager (por nombre)*: reasigna los 6 clips por nombre (abrir el Nivel 1).
