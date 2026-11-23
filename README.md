# Purr-tales

Plataformero 2D de precisión en Unity, inspirado en Celeste, Super Meat Boy y Portal.
Integradora — Ingeniería en Desarrollo y Gestión de Software, UTT.

## Abrir el proyecto
1. Unity Hub → **Add → Add project from disk** → selecciona esta carpeta.
2. Versión de Unity: **6000.0.83f1** (cualquier Unity 6000.0.x debería funcionar).
3. La **primera vez** que se abre, el proyecto genera solo la escena del juego
   (`Assets/_Juego/Scenes/PurrTales_Prueba.unity`) y la deja abierta. Solo tienes que darle **▶ Play** y hacer clic en la ventana *Game*.

De ahí en adelante la escena es un asset normal: edítala, duplícala o úsala de base para los niveles.

## Controles
| Acción | Teclado / Mouse | Gamepad |
|---|---|---|
| Mover | A/D o ←/→ | Stick izq. |
| Saltar (mantener = más alto) | Espacio | A / Sur |
| Dash en 8 direcciones | Shift (+ dirección) | RB |
| Portal azul / naranja | Clic izq. / Clic der. | LT / RT (apuntar con stick der.) |

## Estructura
```
Assets/
  _Juego/
    Scripts/
      Player/    PlayerInputReader, PlayerMovement, PlayerDash, PlayerDeathHandler
      Portals/   Portal, PortalManager, PortalGun, PortalTraveler
      Systems/   CheckpointManager, Checkpoint, CameraFollow, GameHUD, LevelGoal, GameLayers
    Editor/      PurrTalesSceneBuilder (genera la escena), PurrTalesAutoSetup (primer arranque)
    Prefabs/     PortalAzul, PortalNaranja            (se generan al abrir)
    Scenes/      PurrTales_Prueba                     (se genera al abrir)
    Generated/   sprites placeholder, materiales, material físico sin fricción
  InputSystem_Actions.inputactions   acciones del Input System
  Settings/                          URP (Renderer 2D)
  TextMesh Pro/                      para la UI futura
```

## Capas
| Layer | Uso |
|---|---|
| `Ground` | Suelo. Al tocarlo se recarga el dash. |
| `Wall` | Paredes con wall slide / wall jump. |
| `PortalSurface` | Superficies claras donde se pueden abrir portales. |
| `Hazard` | Trampas: muerte y reaparición instantánea. |

Para niveles con **Tilemap**: pon cada Tilemap en la layer que corresponda, con `TilemapCollider2D` y `CompositeCollider2D`. El movimiento, el wall jump y los portales funcionan igual.

## Nivel de prueba
1. **Grutas**: correr y saltar pinchos.
2. **Vacío**: foso sin fondo y pinchos.
3. **Chimenea**: sube con wall jump entre las dos paredes oscuras.
4. **Hueco de dash**: 9 u, salto + Shift.
5. **Sala de portales**:
   - Portal azul en el piso claro y naranja en el techo claro: caída infinita que acumula velocidad.
   - En plena caída, mueve el naranja al **muro claro** (a media altura o más) y sales disparado sobre el foso de pinchos.
   - O muévelo al **piso claro del fondo** y sales disparado hacia arriba, hasta la repisa de la **Meta**.

## Menú `Purr-tales`
- **Abrir Escena de Prueba**: abre la escena del juego.
- **Reconstruir Escena de Prueba**: la vuelve a generar desde cero (pide confirmación, porque se pierden los cambios hechos a mano).

## Pendientes conocidos
- El gato es un sprite placeholder (`Generated/Gato_Placeholder.png`). Hay que reemplazarlo por el pack *Pet Cats* en `Player/Visual` y agregar su Animator.
- La cámara es `CameraFollow`. Más adelante se puede migrar a Cinemachine (paquete no instalado todavía).
