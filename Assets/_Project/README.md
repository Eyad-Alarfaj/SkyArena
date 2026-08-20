# SkyArena — 3D Flight Combat MVP

A deliberately small multiplayer dogfighting prototype: one arena, no story, no AI,
no progression. Unity 6 (6000.5.9f1) + URP + Photon PUN2, built for touch.

Everything visual is generated from primitives by an editor script, so the repo is
essentially just C# — there are no binary art assets to keep in sync.

---

## Getting it running

Open the project and press **Play**. That is the whole setup.

The first time the scripts compile, `SkyArenaBuilder` notices there is no arena
scene and generates the materials, the two networked prefabs, the arena and the
touch HUD, then opens the scene. After that it never runs automatically again.

To regenerate by hand at any point:

| Menu | What it does |
| --- | --- |
| `SkyArena ▸ Build Everything (Prefabs + Arena)` | Wipes and rebuilds every generated asset and the scene |
| `SkyArena ▸ Open Arena Scene` | Opens the arena (builds it first if missing) |
| `SkyArena ▸ Validate Setup` | Checks prefabs, scene wiring and the Photon App ID |

### Controls

| Input | Action |
| --- | --- |
| Left joystick | Pitch and roll (yaw follows the bank automatically) |
| `+` / `−` buttons | Throttle up / down |
| `GUN` | Hold to fire the machine gun (hitscan) |
| `MSL` | Hold to fire a missile. Guided when the reticle is red, unguided otherwise |

In the Editor, drag the on-screen stick with the mouse — uGUI treats the mouse
as a pointer, so no separate desktop control path is needed.

Pitch is **not** inverted by default (push up to climb). Flip `Invert Pitch` on the
`FlightController` in `PlayerPlane.prefab` for the classic pull-back-to-climb feel.

---

## Testing multiplayer

Two clients that share a Photon App ID and `Game Version` land in the same room
automatically — there is no lobby to navigate.

1. `File ▸ Build And Run` (or build to your phone) to get a second client.
2. Press Play in the Editor.

Both join the room named `SkyArena` and see each other. Your own plane is blue;
everyone else is red. `PlayerSettings.runInBackground` is enabled by the builder,
so the Editor keeps simulating while the standalone build has focus.

**No internet?** The launcher waits briefly, then drops into PUN's offline mode so
the game is still flyable solo. You will have no targets to lock, but flight, guns,
terrain and the HUD all work.

---

## Architecture

```
Assets/_Project/
├─ Editor/
│  ├─ SkyArenaBuilder.cs      generates materials, prefabs, arena, build settings
│  └─ SkyArenaUiFactory.cs    generates the whole touch Canvas
├─ Resources/                 PlayerPlane.prefab, Missile.prefab  (PhotonNetwork.Instantiate)
├─ Art/                       generated placeholder materials + ExplosionFx.prefab
├─ Scenes/Arena.unity         the single scene
└─ Scripts/
   ├─ Core/       GameManager, SpawnPoint
   ├─ Inputs/     VirtualJoystick, HoldButton, MobileInputController
   ├─ Flight/     FlightController, CameraFollow, PlayerAvatar
   ├─ Combat/     HealthSystem, Targetable, LockOnSystem, WeaponSystem,
   │              MissileLauncher, MissileController, ExplosionFx
   ├─ Networking/ PhotonLauncher, NetworkTransformSync
   └─ UI/         HUDController, LockOnIndicatorUI, RadarController
```

### The three rules the whole design leans on

**1. One scene.** Every client connects, joins the same named room and spawns in
place. There is no lobby scene and no `PhotonNetwork.LoadLevel`, which removes
scene-sync desyncs from the prototype entirely.

**2. The owner is authoritative over its own plane.** Any client may *request*
damage on any plane, but only the client that owns that PhotonView subtracts
health and decides that it died. The resulting alive/dead state is then
replicated, so every client agrees on what can be locked and hit.

**3. The spawned prefab knows nothing about the UI.** Gameplay code polls
`MobileInputController.Instance` and the scene HUD binds *itself* to whichever
plane turns out to be local. So the Canvas can be relaid out freely without
touching a single gameplay script.

### AI bots

Bots are the same airframe as the player. The only differences are which
component supplies the five pilot inputs, the paint, and that a bot must not
claim the chase camera.

```
IFlightInput  ──┬── HumanPilot  (reads the touch HUD)      → PlayerPlane.prefab
                └── AiPilot     (decides where to point)   → EnemyBot.prefab
```

`FlightController`, `WeaponSystem` and `MissileLauncher` read that interface off
their own GameObject, so a bot flies the same physics and fires the same weapons
as a player - it cannot cheat, because there is no separate code path for it.

**Only the master client runs bot brains.** `BotSpawner` spawns them, so a room
with four players still holds exactly `botCount` bots rather than four times
that. Other clients see bots purely through `NetworkTransformSync`, which means
a bot can never desync: there is exactly one brain per bot in the room.

Difficulty is not one multiplier. `AiProfile` separates speed, turn rate,
reaction lag, aim error, engagement ranges, missile willingness and
self-preservation, so "hard but fair" (accurate but slow) is expressible rather
than only "fast and unhittable".

| | Easy | Normal | Hard |
| --- | --- | --- | --- |
| Cruise / max speed | 40 / 62 | 52 / 85 | 64 / 108 |
| Roll rate (deg/s) | 70 | 110 | 135 |
| Reaction lag | 0.55 s | 0.28 s | 0.10 s |
| Aim error | 13° | 5° | 1.8° |
| Uses missiles | never | 45% | 90% |
| Target leading | none | partial | full |
| Runs away below | 45% hull | 30% | 20% |

### Networked state

| What | How |
| --- | --- |
| Position / rotation | `NetworkTransformSync` via `IPunObservable`, unreliable-on-change |
| Damage | `RpcApplyDamage` → applied only on the victim's owner |
| Alive / dead | `RpcSetAlive` → all clients, drives renderers, colliders and lock validity |
| Gun tracers | `RpcShowTracer` → others only; the shooter draws its own immediately |
| Missiles | `PhotonNetwork.Instantiate` / `Destroy`; guidance simulated by the firer only |

Snapshots are extrapolated along the sender's measured velocity by the observed
lag before being used as the interpolation goal — at 55 m/s a raw snapshot is
already stale by the time it arrives, and without this planes visibly rubber-band.

---

## Known limits of the prototype

These are scope decisions, not defects:

- **No kill feed, score or match end.** Death is a 3 second respawn, nothing more.
- **Planes do not collide** with terrain or each other. The bodies are kinematic;
  mountains block gunfire but you can fly through them. Altitude is clamped
  instead, and straying past the arena edge gently steers you back to the middle.
- **No client-side hit validation.** An attacker's client decides it hit you.
  Fine for a prototype, trivially cheatable in a real game.
- **Gun tracers cost one RPC per shot** (8/sec/player). At 8 players all firing
  this is meaningful traffic; drop `shotsPerSecond` or send every Nth tracer if
  it becomes a problem.

## Tuning

Every number lives in the Inspector on `PlayerPlane.prefab`:

| Component | Worth touching first |
| --- | --- |
| `FlightController` | `cruiseSpeed`, `pitchRate`, `rollRate`, `turnAssistRate`, `invertPitch` |
| `LockOnSystem` | `lockRange`, `lockAngle`, `timeToLock` |
| `WeaponSystem` | `damage`, `shotsPerSecond`, `range` |
| `MissileLauncher` | `damage`, `cooldown` |
| `HealthSystem` | `maxHealth`, `respawnDelay` |

Note that `SkyArena ▸ Build Everything` regenerates the prefabs from scratch and
will discard Inspector tweaks. Tune first, rebuild only when you change structure.
