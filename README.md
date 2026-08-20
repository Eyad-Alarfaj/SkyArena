# SkyArena

A 3D multiplayer flight-combat prototype for mobile — arcade dogfighting in a
shared arena, inspired by Ace Combat and cut down to the smallest thing that is
actually fun to play.

**Unity 6 (6000.5.9f1) · URP · Photon PUN2 · Android / iOS**

---

## What it is

One arena, up to 8 pilots, no story and no progression. You take off, you find
someone, you shoot them down. Bots fill the arena when there are not enough
humans in it.

| | |
| --- | --- |
| **Controls** | Virtual joystick for pitch and roll, touch buttons for throttle, guns and missiles |
| **Flight** | Arcade model — always flying forward, throttle trims speed, yaw follows the bank. No stalls |
| **Weapons** | Hitscan machine gun with replicated tracers, plus lock-on homing missiles |
| **Lock-on** | Hold an enemy in a 25° cone for one second; the reticle turns red when the shot is guided |
| **Multiplayer** | Photon PUN2, single scene, automatic room join. Falls back to offline solo with no internet |
| **AI** | Bots with Easy / Normal / Hard profiles, spawned and simulated by the master client only |
| **HUD** | Hull, speed, altitude, throttle, crosshair, lock reticle, weapon status and a heading-up radar |

All art is placeholder primitives generated from code — there are no binary
assets in this repository. The whole game is C#.

---

## Running it

Open the project in Unity 6000.5.9f1 and press **Play**.

On first compile an editor script notices the arena does not exist yet and
generates the materials, prefabs, terrain and the entire touch HUD, then opens
the scene. There is no manual setup step.

| Menu | |
| --- | --- |
| `SkyArena ▸ Build Everything` | Regenerate every generated asset and the scene |
| `SkyArena ▸ Open Arena Scene` | Open the arena, building it first if needed |
| `SkyArena ▸ Validate Setup` | Check prefabs, scene wiring and the Photon App ID |

To play against another human, make a build and run it alongside the Editor —
both clients join the same room automatically.

### Photon setup

Online play needs a free Photon App ID — credentials are not committed, so each
developer supplies their own:

1. Create a free **PUN** app at [dashboard.photonengine.com](https://dashboard.photonengine.com)
2. In Unity, open `Window ▸ Photon Unity Networking ▸ Highlight Server Settings`
3. Paste the App ID into **App Id PUN**

Without one the game still runs — `PhotonLauncher` falls back to offline solo
mode rather than hanging on a connection screen.

---

## Architecture

```
Assets/_Project/
├─ Editor/        SkyArenaBuilder, SkyArenaUiFactory   generate everything
└─ Scripts/
   ├─ Core/       GameManager, SpawnPoint
   ├─ Input/      VirtualJoystick, HoldButton, MobileInputController,
   │              IFlightInput, HumanPilot
   ├─ AI/         AiPilot, AiProfile, BotSpawner
   ├─ Flight/     FlightController, CameraFollow, PlayerAvatar, BotAvatar
   ├─ Combat/     HealthSystem, Targetable, LockOnSystem, WeaponSystem,
   │              MissileLauncher, MissileController, ExplosionFx
   ├─ Networking/ PhotonLauncher, NetworkTransformSync
   └─ UI/         HUDController, LockOnIndicatorUI, RadarController
```

Four decisions shape everything else:

**One scene.** Every client connects, joins the same named room and spawns in
place. No lobby scene, no networked level loading, and therefore no scene-sync
desyncs.

**The owner is authoritative over its own aircraft.** Any client may *request*
damage on any plane, but only the client owning that PhotonView subtracts health
and decides it died. The alive/dead result is then replicated, so everyone
agrees on what can be locked and hit.

**Pilots are an interface.** `IFlightInput` exposes the five things a pilot can
do. `HumanPilot` reads the touch HUD, `AiPilot` decides where to point, and the
flight and weapon components do not know which one they have. A bot flies the
same physics and fires the same weapons as a player because there is no second
code path for it to use.

**The aircraft knows nothing about the UI.** The scene HUD binds *itself* to
whichever plane turns out to be local, so the Canvas can be rebuilt freely
without touching gameplay code.

### AI difficulty

Difficulty is seven independent knobs rather than one multiplier, so "accurate
but slow" is expressible instead of only "fast and unhittable".

| | Easy | Normal | Hard |
| --- | --- | --- | --- |
| Cruise / max speed | 40 / 62 | 52 / 85 | 64 / 108 |
| Roll rate °/s | 70 | 110 | 135 |
| Reaction lag | 0.55 s | 0.28 s | 0.10 s |
| Aim error | 13° | 5° | 1.8° |
| Uses missiles | never | 45% | 90% |
| Target leading | none | partial | full |
| Flees below | 45% hull | 30% | 20% |

Aim error is Perlin drift applied to the bot's *steering*, so a weak bot
visibly wobbles its nose around you rather than rolling a hidden dice.

---

## Status

Playable prototype. Flight, guns, missiles, lock-on, damage, respawn,
multiplayer sync and AI bots all work.

Next up: scoring and a match timer, audio, then a lobby.

Known scope limits — deliberate, not defects:

- No score, kill feed or match end. Death is a 3 second respawn.
- Aircraft do not collide with terrain; altitude is clamped instead.
- Hit detection is client-authoritative. Fine for a prototype, cheatable in the wild.

Developer notes, tuning tables and the full networking breakdown live in
[`Assets/_Project/README.md`](Assets/_Project/README.md).
