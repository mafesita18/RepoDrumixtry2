# Assets incluidos (todos gratis, CC0 = sin atribución obligatoria)

El builder usa primero los del proyecto del equipo, luego estos, luego primitivas.

| Carpeta | Qué | Autor / fuente | Licencia |
|---|---|---|---|
| `ThirdParty/DrumKit` | Batería completa texturizada (bombo, caja, 3 toms, hi-hat, crash, ride) | Grab3D — https://grab3d.com/models/music/drum-kit/ | CC0 1.0 |
| `ThirdParty/Drums` | Samples kick, hi-hat, tom1, tom2, tom piso, crash, ride (WAV) | SFXMint — https://sfxmint.com/sets/acoustic-drum-kit | CC0 1.0 |
| `ThirdParty/Drums/Snare.wav` | Caja | Spring Spring — https://opengameart.org/content/an-interesting-snare-drum-sound | CC0 1.0 |
| `ThirdParty/Crowd` | Aplauso, abucheo, murmullo (OGG) | SoundBiterSFX / NeoSpica (Freesound), StarNinjas (OpenGameArt) | CC0 1.0 |
| `ThirdParty/Characters` | Personaje riggeado + 4 skins + animación idle | Kenney — Animated Characters 2 (kenney.nl) | CC0 1.0 |
| `ThirdParty/Food` | Zanahoria y tomate para lanzar | Kenney — Food Kit (kenney.nl) | CC0 1.0 |
| `ThirdParty/Bar` | Barra, estantes de licor y vasos, letrero luminoso, taburetes, mesas, botella de cerveza, vaso pinta, parlantes PA, monitores, amplificador, micrófono, barras de luces, tiras de neón | 3DAssets.dev (@3dassets) — URLs en `ThirdParty/Bar/License.txt` | CC0 1.0 |
| `ThirdParty/UI` | Sonidos hover/click | Kenney — Interface Sounds (kenney.nl) | CC0 1.0 |

Notas honestas:
- Los modelos de `Bar/` venían en GLB; se convirtieron a OBJ+MTL (geometría + colores, sin texturas) para que Unity los importe sin plugins. Su autor reporta que fueron hechos con ayuda de IA. Botella y pinta recoloreadas a ámbar.
- La batería Grab3D es procedural (Blender por scripts; sin geometría generada por IA). Es UNA sola malla: las zonas de golpe son triggers invisibles medidos sobre cada pieza (con aro que brilla).
- Créditos voluntarios (CC0 no los exige): Grab3D, SFXMint, Spring Spring, SoundBiterSFX, NeoSpica, StarNinjas, Kenney, 3DAssets.dev.
