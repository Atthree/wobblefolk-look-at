# Wobblefolk Look At

![Unity](https://img.shields.io/badge/Unity-6000.3%2B-black?logo=unity)
![Tests](https://img.shields.io/badge/tests-71%20passing-brightgreen)
![License](https://img.shields.io/badge/License-MIT-green)

**[English](#english)** · **[Türkçe](#türkçe)**

---

<a id="english"></a>

# English

A layered, procedural look-at system for Unity 6. When the player moves the mouse (or the right stick), the eyes get
there first, then the head, then the neck, and finally the shoulders and torso catch up, each with its own limits,
speed and reaction delay, like a person turning to look. It works on animated characters, on characters without any
animation, on NPCs, and on physics-driven active ragdolls.

## Table of contents

1. [Features](#features)
2. [Requirements and installation](#requirements-and-installation)
3. [Quick start](#quick-start)
4. [Setting up your own model](#setting-up-your-own-model)
5. [Architecture](#architecture)
6. [How the solver works](#how-the-solver-works)
7. [Eyes](#eyes)
8. [Profile reference](#profile-reference)
9. [Components](#components)
10. [Events and scripting](#events-and-scripting)
11. [Active ragdoll integration](#active-ragdoll-integration)
12. [Update order and jitter](#update-order-and-jitter)
13. [Working with an Animator](#working-with-an-animator)
14. [Performance and LOD](#performance-and-lod)
15. [Tests](#tests)
16. [Troubleshooting](#troubleshooting)
17. [Known limitations](#known-limitations)
18. [Credits and license](#credits-and-license)

## Features

| Area | What you get |
|---|---|
| **Layered chain** | Eyes → head → neck → upper chest → torso. Each link has its own weight, engage angle, yaw and asymmetric pitch limits, smoothing, speed cap and reaction delay. |
| **Natural timing** | The eyes reach the target first and drift back to centre as the head and body catch up, so the gaze never leaves the target. |
| **Safe limits** | Rotations never pile up past the target or the limits; the spine has a total twist budget. Targets behind the character either clamp to the reachable range or return to neutral, with hysteresis and without flipping shoulders. |
| **Targets** | Mouse / screen centre, camera forward, a transform, and interest points that pull the gaze by priority, distance and view angle. |
| **Drivers** | `TransformLookDriver` for animated or static characters (blends over the animation); `JointLookDriver` drives `ConfigurableJoint` motors for physics characters. |
| **Eyes** | Per-eye convergence on near targets, inner/outer limits, saccades and idle glances, blinking (blend shapes, lid bones or lid scale), distance LOD. |
| **Tooling** | *Wobblefolk > Setup Look At* finds the bones of a Humanoid (or by name) and sets everything up; preview the look in Edit mode by dragging a sphere. |
| **Quality** | Pure-logic solver with 71 tests (69 EditMode, 2 PlayMode with real physics). No allocations per frame. No Euler angles. |

## Requirements and installation

- **Unity 6000.3** or newer.
- **No required dependencies.**
  - The Input System is used if the project has it, otherwise the legacy Input Manager.
  - The active ragdoll integration compiles only when `com.wobblefolk.active-ragdoll` is installed.

**Install from Git:** *Window > Package Manager > + > Add package from git URL…*

```
https://github.com/Atthree/wobblefolk-look-at.git?path=/Packages/com.atthree.wobblefolk.lookat
```

Or clone this repository: it is a complete Unity project with the package embedded under
`Packages/com.atthree.wobblefolk.lookat`.

## Quick start

1. *Package Manager > Wobblefolk Look At > Samples* > import **Basic Character**, open `BasicCharacter.unity`, press
   Play and move the mouse.

   | Key | Action |
   |---|---|
   | 1 / 2 / 3 | Realistic / Robotic Snappy / Sluggish profiles |
   | I | Mouse ↔ interest points |
   | L | Look at a random object for 2 s |
   | B | Blink |

2. Edit the profile in `Profiles/` during Play mode to see the changes live.
3. With the active ragdoll package installed, import **Active Ragdoll Integration**: walk with WASD, and the ragdoll's
   head turns physically towards where the camera aims.

## Setting up your own model

1. Put the character in the scene. A Humanoid avatar is best; other rigs work if the bone names contain *head*,
   *neck*, *spine*, *chest* and *eye* with *left/right* or *_l/_r*.
2. **Wobblefolk > Setup Look At**, drag the character in. Missing bones can be assigned by hand; only the head is
   required.
3. Pick a preset or profile, a target (mouse with interest points, mouse, camera forward, a preview sphere, or
   none) and whether to add eye behaviour. Click **Setup Look At** (one undo step).
4. In the *Look At Controller* Inspector click **Create Preview Target** / **Preview in Edit Mode** and drag the
   sphere around the Scene view. Bones are put back before saving, before Play mode and when the preview stops.
5. Optional:
   - Mark objects with **Look Interest Point**.
   - Set eyelids on **Eye Behaviour** for blinking.

The character's **root must face its forward (+Z) in the rest pose**: yaw and pitch are measured in that frame.

## Architecture

```
Target providers ──▶ LookAtController ──▶ LookSolver (pure logic) ──▶ ILookDriver(s)
 MouseLookTarget       LateUpdate, order 1000   per-link yaw/pitch        TransformLookDriver (bones)
 CameraForwardTarget   look-at override          delays, smoothing,        JointLookDriver (joint motors)
 TransformLookTarget   events, eye LOD           limits, behind logic
 InterestLookTarget ◀─ LookInterestPoint         EyeBehaviour ──▶ saccades, blinks, lids
```

1. **Target providers** answer *where to look*. They are components implementing `ILookTargetProvider`; an
   `InterestLookTarget` wraps another provider and pulls the gaze to interest points.
2. **The solver** is plain C#, independent of transforms and physics. It turns the target direction (in the
   character frame) into yaw/pitch per link.
3. **Drivers** apply the result. Several drivers may run on one character, e.g. joints for the head and transforms
   for the eyes.

## How the solver works

- **No Euler angles.** The target direction in the character frame is split into yaw (about the character's up axis)
  and pitch, then rebuilt as `AngleAxis(yaw, up) * AngleAxis(-pitch, right)`. There is no roll and no gimbal lock.
- **Sharing the angle.** The total yaw/pitch is shared from the base of the spine upwards. Each link takes
  `weight × (remaining − engageAngle)`, clamped to its own limits, and the spine's summed yaw is capped by
  `maxBodyYaw`. The sum therefore never exceeds the target or the limits, and small glances move only the eyes and
  head.
- **Timing.**
  - Every link sees the target through its own reaction `delay`, read from a timestamped, allocation-free history.
  - It moves with a critically damped spring (`smoothTime`, optional `maxSpeed`), so there is no snapping.
  - The eyes aim at what the body has not turned yet, so they lead and then drift back.
- **Targets behind.**
  - Beyond `enterAngle` of yaw the target counts as behind until it is back within `exitAngle` (hysteresis).
  - `ClampToLimits` turns as far as the chain reaches, on the side the body is already turned to, so a target
    passing straight behind does not whip the head across.
  - `ReturnToNeutral` looks ahead again.

## Eyes

- **Convergence.** Each eye aims at the target from its own position (`convergence` 0–1), so the eyes cross slightly
  on near targets. Inner (towards the nose) and outer yaw limits are separate.
- **Saccades.** Small random jumps (`minAmplitude`–`maxAmplitude`) every `minInterval`–`maxInterval` seconds. After
  the target has rested for `idleTime`, there is a chance of a larger glance away.
- **Blinks.** Random intervals, close / hold / open timing, double blinks, and a blink on gaze jumps larger than
  `blinkOnGazeShift`. Eyelids are driven in one of three modes:
  - blend shapes
  - lid bones rotated about an axis
  - lid objects scaled between two sizes

  With none set, blinking is skipped.
- **Absolute eyes on physical heads.** When another driver moves the body (`driveBody` off), the eyes are aimed
  absolutely, so they stay on target while a physical head wobbles.

## Profile reference

All settings live in a `LookProfile` (*Create > Wobblefolk > Look Profile*). The right-click menu of the asset
applies a preset.

### Chain (defaults = Realistic preset)

| Link | Weight | Engage (°) | Max yaw (°) | Pitch up / down (°) | Smooth time (s) | Delay (s) |
|---|---|---|---|---|---|---|
| Torso | 0.3 | 45 | 20 | 5 / 10 | 0.45 | 0.25 |
| Upper chest | 0.35 | 30 | 25 | 10 / 15 | 0.35 | 0.15 |
| Neck | 0.4 | 10 | 30 | 15 / 20 | 0.2 | 0.06 |
| Head | 0.6 | 5 | 60 | 35 / 40 | 0.15 | 0.05 |
| Eyes | 1 (of the rest) | 0 | 35 | 25 / 30 | 0.04 | 0 |

`maxSpeed` (degrees/s, 0 = unlimited) caps any link.

| Setting | Default | Meaning |
|---|---|---|
| Max body yaw | 120° | Upper bound of the summed torso…head yaw |
| Behind: behaviour | Clamp To Limits | Or Return To Neutral |
| Behind: enter / exit angle | 115° / 100° | Hysteresis for targets behind |

### Presets

| Preset | Character |
|---|---|
| Realistic | Eyes lead, head follows, shoulders and torso catch up with human-like delays. |
| Robotic Snappy | Fast, stiff, no delays: a robot or security camera. |
| Sluggish | Slow, heavy, late: tired or drowsy. Speeds are also capped. |

### Eyes, saccades and blinks

| Group | Defaults |
|---|---|
| Eyes | convergence 1, inner yaw 25°, outer yaw 35° |
| Saccades | on, interval 0.4–1.8 s, amplitude 0.5–2.5°, idle after 2 s, glance chance 0.2, glance 10°, jump time 0.02 s, resting below 15°/s |
| Blinks | on, interval 2.5–6 s, close 0.06 s, hold 0.03 s, open 0.1 s, double chance 0.1, blink on gaze jumps over 35° |

## Components

| Component | Purpose |
|---|---|
| `LookAtController` | Runs the system each LateUpdate. It has a global `weight`, an eye LOD distance, events, `LookAt(transform/point, duration)` / `ClearLookAt()`, `Solver`, gizmos and an Edit-mode preview in its Inspector. |
| `TransformLookDriver` | Drives torso, upper chest, neck, head and eye transforms on top of the current pose. |
| `JointLookDriver` | Drives the same links through `ConfigurableJoint.targetRotation` for physics characters. |
| `MouseLookTarget` | Pointer or screen centre ray. It ignores the character's own colliders and has a fallback distance. |
| `CameraForwardTarget` | Camera forward ray. |
| `TransformLookTarget` | A transform plus an offset. |
| `InterestLookTarget` | Pulls the gaze to the best `LookInterestPoint`; it falls back to another provider. |
| `LookInterestPoint` | Marks something worth looking at (priority, radius, offset). |
| `EyeBehaviour` | Saccades and blinking with eyelids. |
| `ActiveRagdollLookAt` | One-component setup for a Wobblefolk Active Ragdoll (optional assembly). |

## Events and scripting

| Event | When |
|---|---|
| `lookedAtTarget` | The gaze settled within `onTargetAngle` of the target. |
| `lostTarget` | The gaze left the target by more than `lostTargetAngle`, or the target disappeared. |

```csharp
var look = GetComponent<LookAtController>();
look.LookAt(doorHandle, 2f);         // look at a transform for 2 s, then back to the provider
look.LookAt(new Vector3(0, 2, 5));   // or a point, until ClearLookAt()
look.weight = 0.5f;                  // blend with the animation
look.Events.lookedAtTarget.AddListener(() => Debug.Log("Seen"));

// NPC: just use a different provider.
var target = npc.AddComponent<TransformLookTarget>();
target.target = player.head;
npc.GetComponent<LookAtController>().TargetProvider = target;
```

## Active ragdoll integration

With [Wobblefolk Active Ragdoll](https://github.com/Atthree/wobblefolk-active-ragdoll) installed, the optional
`Wobblefolk.LookAt.ActiveRagdoll` assembly compiles (via `versionDefines`). Add **Active Ragdoll Look At** to the
ragdoll root, set a target provider and, optionally, eye transforms. On Start it:

- adds a look frame riding on the chest, so the head turns relative to the body;
- adds a `JointLookDriver` on the head joint, so the joint motor turns the head and it lags naturally when the body
  wobbles;
- adds a `TransformLookDriver` for the eyes in absolute mode, plus an `EyeBehaviour`;
- fades the look out while the ragdoll is fallen, getting up or unconscious.

Only the head is driven: the ragdoll's own arm system writes the chest joint (bending over to reach).
`JointLookDriver` itself uses only Unity's `ConfigurableJoint`, so it works with any physics character.

## Update order and jitter

Jitter in look-at systems almost always comes from update order. The required order each frame is:

1. **Animation** (Animator) writes the base pose.
2. **Cameras** move (most follow cameras and Cinemachine run in LateUpdate at default order).
3. **`LookAtController`** runs in LateUpdate with `[DefaultExecutionOrder(1000)]`: it reads the camera ray and
   writes the bones.
4. **Rendering.**

If the look ran before the camera, the mouse ray would come from last frame's camera and the head would tremble
while the camera moves. If your camera script runs later than order 1000, give it a lower order.

Physics characters: `JointLookDriver` sets joint targets in LateUpdate and the joint motors act in the following
FixedUpdate steps. Use Rigidbody interpolation on the visible bodies.

## Working with an Animator

- The `TransformLookDriver` runs after the Animator and **adds** each link's share on top of the animated pose,
  scaled by `weight`. Bones the Animator does not write are reset by the driver itself each frame, so offsets never
  pile up.
- **Conflicts.** When an animation already turns the head (e.g. a "look around" idle), the two add up. Options:
  - Lower `weight` during such clips (e.g. from an animation event or a state behaviour).
  - Remove head and neck rotation from those clips with an **Avatar Mask** on an override layer, so only the look
    system turns them.
  - Set the head and neck weights in the profile to 0 and let only the eyes move.
- Humanoid **IK Pass** / `SetLookAtPosition` are not needed and should be turned off to avoid two systems turning
  the head.

## Performance and LOD

- **Cost.** One character runs a few dozen arithmetic operations plus one raycast for a ray provider. Nothing
  allocates per frame: histories, raycast buffers and registries are preallocated.
- **Interest points** register themselves in a static list; no scene searches.
- **Eye LOD.** With `eyeLodDistance` set, characters farther from the LOD camera skip eyes, saccades and blinks.
- **Many NPCs.**
  - Use `CameraForwardTarget` / `TransformLookTarget` (no raycast).
  - Raise `InterestLookTarget.reevaluateInterval`.
  - Disable the controller on far characters (its drivers then restore the pose).

## Tests

- **EditMode (69):**
  - solver: limits, behind behaviour, hysteresis, accumulation, timing order, no snapping
  - math
  - target providers and interest selection
  - transform and joint drivers on bones with odd axes
  - eyes: convergence, saccades, blinks, LOD, lids
  - the setup tool and the Edit-mode preview
- **PlayMode (2, with the active ragdoll package):** the ragdoll turns its head through physics while standing, and
  the look fades out when it falls.

Run them from *Window > General > Test Runner*. The package is listed in this project's `testables`.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Head trembles while the camera moves | Update order: the camera must update before the look (see above). |
| Character looks the wrong way or sideways | The root must face +Z in the rest pose. Check *Character Root*. |
| Rotations add up over time | A bone is written by something else after the look, or two look controllers drive the same bones. |
| Eyes do not move | No eye bones assigned, eye LOD active, or the eye weight in the profile is 0. |
| Character never looks behind | By design: targets behind clamp to the reachable range or return to neutral (*Behind* settings). |
| Mouse does nothing | The cursor is locked (screen centre is used), or *Camera* is empty and there is no *MainCamera*. |
| No blinking | Set an eyelid mode and lids on *Eye Behaviour*. |
| Ragdoll head does not turn | The head joint needs a slerp drive (the ragdoll profile provides it) and the integration only drives the head. |

## Known limitations

- **No real FBX tested.** The setup tool was tested on generated Humanoid skeletons with deliberately odd bone axes,
  not yet on a real FBX character.
- **No neck roll** (head tilt); yaw and pitch only.
- **Blinks have no eyelid follow.** The lids do not follow the eyes' pitch.
- **Ragdoll: head only.** In the ragdoll integration only the head is driven; shoulders would need the ragdoll's chest
  joint to be shared with its arm system.

## Credits and license

Released under the **MIT License** (`LICENSE.md`). All code was written from scratch. Open-source projects studied for
ideas are listed in `THIRD_PARTY_NOTICES.md`.

---

<a id="türkçe"></a>

# Türkçe

Unity 6 için katmanlı, prosedürel bir bakış (look-at) sistemi. Oyuncu fareyi (ya da sağ analogu) oynattığında önce
gözler, sonra kafa, sonra boyun gider; en son omuzlar ve gövde yetişir. Her halkanın kendi limitleri, hızı ve tepki
gecikmesi vardır; tıpkı başını çevirip bakan bir insan gibi. Animasyonlu karakterlerde, hiç animasyonu olmayan
karakterlerde, NPC'lerde ve fizikle çalışan active ragdoll'larda çalışır.

## İçindekiler

1. [Özellikler](#özellikler)
2. [Gereksinimler ve kurulum](#gereksinimler-ve-kurulum)
3. [Hızlı başlangıç](#hızlı-başlangıç)
4. [Kendi modeline kurma](#kendi-modeline-kurma)
5. [Mimari](#mimari)
6. [Çözücü nasıl çalışır](#çözücü-nasıl-çalışır)
7. [Gözler](#gözler)
8. [Profil başvurusu](#profil-başvurusu)
9. [Bileşenler](#bileşenler)
10. [Olaylar ve kod](#olaylar-ve-kod)
11. [Active ragdoll entegrasyonu](#active-ragdoll-entegrasyonu)
12. [Güncelleme sırası ve titreme](#güncelleme-sırası-ve-titreme)
13. [Animator ile kullanım](#animator-ile-kullanım)
14. [Performans ve LOD](#performans-ve-lod)
15. [Testler](#testler)
16. [Sorun giderme](#sorun-giderme)
17. [Bilinen sınırlamalar](#bilinen-sınırlamalar)
18. [Atıflar ve lisans](#atıflar-ve-lisans)

## Özellikler

| Alan | Neler var |
|---|---|
| **Katmanlı zincir** | Gözler → kafa → boyun → üst göğüs → gövde. Her halkanın ağırlığı, devreye girme açısı, yaw ve ayrı yukarı/aşağı pitch limitleri, yumuşatması, hız sınırı ve tepki gecikmesi vardır. |
| **Doğal zamanlama** | Gözler hedefe ilk varır; kafa ve gövde yetiştikçe ortaya geri döner. Böylece bakış hedeften hiç ayrılmaz. |
| **Güvenli limitler** | Dönüşler hedefi ya da limitleri aşacak şekilde birikmez; omurganın toplam dönüş bütçesi vardır. Arkadaki hedefte ya ulaşılabilen yere kadar dönülür ya da nötre dönülür; histerezis vardır, omuzdan omuza savrulma olmaz. |
| **Hedefler** | Fare / ekran merkezi, kamera yönü, bir Transform ve bakışı öncelik, mesafe ve görüş açısına göre çeken ilgi noktaları. |
| **Sürücüler** | Animasyonlu ya da durağan karakterler için `TransformLookDriver` (animasyonun üstüne karışır); fizik karakterler için `ConfigurableJoint` motorlarını süren `JointLookDriver`. |
| **Gözler** | Yakın hedefte göz yakınsaması, iç/dış limitler, saccade ve boşta bakış kaydırma, göz kırpma (blendshape, kapak kemiği ya da kapak ölçeği), mesafe LOD'u. |
| **Araçlar** | *Wobblefolk > Setup Look At*, Humanoid'in (ya da isimden) kemiklerini bulup her şeyi kurar; Play'e girmeden bir küreyi sürükleyerek bakış önizlenir. |
| **Kalite** | Saf mantık çözücü ve 71 test (69 EditMode, gerçek fizikle 2 PlayMode). Her karede bellek ayırma yok. Euler açısı yok. |

## Gereksinimler ve kurulum

- **Unity 6000.3** veya üstü.
- **Zorunlu bağımlılık yok.**
  - Projede Input System varsa o kullanılır, yoksa eski Input Manager.
  - Active ragdoll entegrasyonu yalnızca `com.wobblefolk.active-ragdoll` yüklüyse derlenir.

**Git ile kurulum:** *Window > Package Manager > + > Add package from git URL…*

```
https://github.com/Atthree/wobblefolk-look-at.git?path=/Packages/com.atthree.wobblefolk.lookat
```

Ya da bu repoyu klonla: paket `Packages/com.atthree.wobblefolk.lookat` altında gömülü olan eksiksiz bir Unity
projesidir.

## Hızlı başlangıç

1. *Package Manager > Wobblefolk Look At > Samples* altından **Basic Character**'ı içe aktar, `BasicCharacter.unity`
   sahnesini aç, Play'e bas ve fareyi oynat.

   | Tuş | Ne yapar |
   |---|---|
   | 1 / 2 / 3 | Realistic / Robotic Snappy / Sluggish profilleri |
   | I | Fare ↔ ilgi noktaları |
   | L | 2 saniye rastgele bir nesneye bakış |
   | B | Göz kırpma |

2. Play modundayken `Profiles/` klasöründeki profili düzenle, değişiklikler anında görünür.
3. Active ragdoll paketi yüklüyse **Active Ragdoll Integration**'ı içe aktar: WASD ile yürü, ragdoll'un kafası
   kameranın baktığı yere fiziksel olarak döner.

## Kendi modeline kurma

1. Karakteri sahneye koy. En iyisi Humanoid avatar; diğer riglerde kemik adlarında *head*, *neck*, *spine*,
   *chest* ve *left/right* ya da *_l/_r* ile birlikte *eye* geçiyorsa çalışır.
2. **Wobblefolk > Setup Look At** penceresini aç, karakteri sürükle. Bulunamayan kemikler elle atanabilir; sadece
   kafa zorunludur.
3. Hazır profil ya da kendi profilini, hedefi (ilgi noktalı fare, fare, kamera yönü, önizleme küresi ya da hiçbiri)
   ve göz davranışı eklenip eklenmeyeceğini seç. **Setup Look At**'a bas (tek geri alma adımı).
4. *Look At Controller* Inspector'ında **Create Preview Target** / **Preview in Edit Mode**'a bas ve küreyi Scene
   görünümünde sürükle. Kemikler kaydetmeden önce, Play'e girerken ve önizleme kapanınca eski haline döner.
5. İsteğe bağlı:
   - Nesneleri **Look Interest Point** ile işaretle.
   - Göz kırpma için **Eye Behaviour**'da göz kapaklarını ayarla.

Karakterin **kökü dinlenme pozunda ileriye (+Z) bakmalıdır**: yaw ve pitch bu çerçevede ölçülür.

## Mimari

```
Hedef sağlayıcılar ──▶ LookAtController ──▶ LookSolver (saf mantık) ──▶ ILookDriver(lar)
 MouseLookTarget          LateUpdate, sıra 1000   halka başına yaw/pitch   TransformLookDriver (kemikler)
 CameraForwardTarget      bakış kilidi            gecikme, yumuşatma,      JointLookDriver (eklem motorları)
 TransformLookTarget      olaylar, göz LOD'u      limitler, arka mantığı
 InterestLookTarget ◀─ LookInterestPoint          EyeBehaviour ──▶ saccade, kırpma, kapaklar
```

1. **Hedef sağlayıcılar** *nereye bakılacağını* söyler. `ILookTargetProvider` uygulayan bileşenlerdir;
   `InterestLookTarget` başka bir sağlayıcıyı sarar ve bakışı ilgi noktalarına çeker.
2. **Çözücü** düz C#'tır, transform ve fizikten bağımsızdır. Karakter çerçevesindeki hedef yönünü halka başına
   yaw/pitch'e çevirir.
3. **Sürücüler** sonucu uygular. Bir karakterde birden fazla sürücü çalışabilir; örneğin kafa için eklem, gözler
   için transform.

## Çözücü nasıl çalışır

- **Euler açısı yok.** Karakter çerçevesindeki hedef yönü, yaw (karakterin yukarı ekseni etrafında) ve pitch olarak
  ayrıştırılır, sonra `AngleAxis(yaw, up) * AngleAxis(-pitch, right)` ile yeniden kurulur. Yuvarlanma (roll) ve
  gimbal kilidi olmaz.
- **Açının paylaştırılması.** Toplam yaw/pitch omurganın tabanından yukarı doğru paylaştırılır. Her halka
  `ağırlık × (kalan − devreye girme açısı)` kadarını alır ve kendi limitleriyle kesilir; omurganın toplam yaw'u
  `maxBodyYaw` ile sınırlıdır. Bu yüzden toplam hiçbir zaman hedefi ya da limitleri aşmaz, küçük bakışlarda sadece
  gözler ve kafa oynar.
- **Zamanlama.**
  - Her halka hedefi kendi tepki gecikmesiyle (`delay`) görür; bu, zaman damgalı ve bellek ayırmayan bir geçmişten
    okunur.
  - Halka kritik sönümlü bir yayla hareket eder (`smoothTime`, isteğe bağlı `maxSpeed`), anlık sıçrama olmaz.
  - Gözler, gövdenin henüz dönmediği kısma bakar; bu yüzden önce gider, sonra geri döner.
- **Arkadaki hedef.**
  - Yaw `enterAngle`'ı aşınca hedef "arkada" sayılır, `exitAngle` içine dönene kadar öyle kalır (histerezis).
  - `ClampToLimits`, gövdenin zaten döndüğü tarafta zincirin ulaşabildiği kadar döner; tam arkadan geçen hedef kafayı
    karşı tarafa savurmaz.
  - `ReturnToNeutral` tekrar öne bakar.

## Gözler

- **Yakınsama.** Her göz hedefe kendi konumundan bakar (`convergence` 0–1); yakın hedefte gözler hafifçe şaşılaşır.
  İç (buruna doğru) ve dış yaw limitleri ayrıdır.
- **Saccade.** Her `minInterval`–`maxInterval` saniyede bir küçük rastgele sıçramalar (`minAmplitude`–`maxAmplitude`).
  Hedef `idleTime` boyunca sabit kalınca, belli bir şansla bakış daha büyük bir sıçramayla kısa süre başka yere kayar.
- **Göz kırpma.** Rastgele aralıklar, kapanma / bekleme / açılma süreleri, çift kırpma ve `blinkOnGazeShift`'ten büyük
  bakış sıçramalarında kırpma. Göz kapakları üç yoldan biriyle sürülür:
  - blendshape
  - bir eksen etrafında dönen kapak kemiği
  - iki boyut arasında ölçeklenen kapak nesnesi

  Hiçbiri ayarlanmazsa kırpma atlanır.
- **Fiziksel kafada mutlak gözler.** Gövdeyi başka bir sürücü hareket ettiriyorsa (`driveBody` kapalı) gözler mutlak
  olarak hedefe ayarlanır; fiziksel kafa sallansa bile gözler hedefte kalır.

## Profil başvurusu

Tüm ayarlar bir `LookProfile`'dadır (*Create > Wobblefolk > Look Profile*). Asset'e sağ tıklayıp hazır profil
uygulanabilir.

### Zincir (varsayılan = Realistic)

| Halka | Ağırlık | Devreye girme (°) | Azami yaw (°) | Pitch yukarı / aşağı (°) | Yumuşatma (sn) | Gecikme (sn) |
|---|---|---|---|---|---|---|
| Gövde | 0.3 | 45 | 20 | 5 / 10 | 0.45 | 0.25 |
| Üst göğüs | 0.35 | 30 | 25 | 10 / 15 | 0.35 | 0.15 |
| Boyun | 0.4 | 10 | 30 | 15 / 20 | 0.2 | 0.06 |
| Kafa | 0.6 | 5 | 60 | 35 / 40 | 0.15 | 0.05 |
| Gözler | 1 (kalanın) | 0 | 35 | 25 / 30 | 0.04 | 0 |

`maxSpeed` (derece/sn, 0 = sınırsız) herhangi bir halkayı sınırlar.

| Ayar | Varsayılan | Anlamı |
|---|---|---|
| Max body yaw | 120° | Gövde…kafa toplam yaw'unun üst sınırı |
| Behind: davranış | Clamp To Limits | Ya da Return To Neutral |
| Behind: giriş / çıkış açısı | 115° / 100° | Arkadaki hedef için histerezis |

### Hazır profiller

| Profil | Karakter |
|---|---|
| Realistic | Gözler önden gider, kafa izler, omuzlar ve gövde insana benzer gecikmelerle yetişir. |
| Robotic Snappy | Hızlı, sert, gecikmesiz: robot ya da güvenlik kamerası. |
| Sluggish | Yavaş, ağır, geç: yorgun ya da uykulu. Hızlar da sınırlıdır. |

### Gözler, saccade ve kırpma

| Grup | Varsayılanlar |
|---|---|
| Gözler | yakınsama 1, iç yaw 25°, dış yaw 35° |
| Saccade | açık, aralık 0.4–1.8 sn, genlik 0.5–2.5°, 2 sn sonra boşta sayılır, bakış kaydırma şansı 0.2, kaydırma 10°, sıçrama süresi 0.02 sn, 15°/sn altı "sabit" |
| Kırpma | açık, aralık 2.5–6 sn, kapanma 0.06 sn, bekleme 0.03 sn, açılma 0.1 sn, çift kırpma şansı 0.1, 35°'den büyük bakış sıçramasında kırpma |

## Bileşenler

| Bileşen | Görevi |
|---|---|
| `LookAtController` | Sistemi her LateUpdate'te çalıştırır. Genel `weight` değeri, göz LOD mesafesi, olaylar, `LookAt(transform/nokta, süre)` / `ClearLookAt()`, `Solver`, gizmolar ve Inspector'ında Play'e girmeden önizleme vardır. |
| `TransformLookDriver` | Gövde, üst göğüs, boyun, kafa ve göz transform'larını mevcut pozun üstüne sürer. |
| `JointLookDriver` | Aynı halkaları fizik karakterler için `ConfigurableJoint.targetRotation` üzerinden sürer. |
| `MouseLookTarget` | İmleç ya da ekran merkezi ışını. Karakterin kendi collider'larını yok sayar ve bir yedek mesafesi vardır. |
| `CameraForwardTarget` | Kamera yönündeki ışın. |
| `TransformLookTarget` | Bir Transform ve ofset. |
| `InterestLookTarget` | Bakışı en iyi `LookInterestPoint`'e çeker; yoksa başka bir sağlayıcıya döner. |
| `LookInterestPoint` | Bakmaya değer bir şeyi işaretler (öncelik, yarıçap, ofset). |
| `EyeBehaviour` | Saccade ve göz kapaklı kırpma. |
| `ActiveRagdollLookAt` | Wobblefolk Active Ragdoll için tek bileşenlik kurulum (opsiyonel derleme). |

## Olaylar ve kod

| Olay | Ne zaman |
|---|---|
| `lookedAtTarget` | Bakış hedefin `onTargetAngle` içine oturduğunda. |
| `lostTarget` | Bakış hedeften `lostTargetAngle`'dan fazla saptığında ya da hedef kaybolduğunda. |

```csharp
var look = GetComponent<LookAtController>();
look.LookAt(kapiKolu, 2f);           // 2 sn bir Transform'a bak, sonra sağlayıcıya dön
look.LookAt(new Vector3(0, 2, 5));   // ya da bir noktaya, ClearLookAt()'e kadar
look.weight = 0.5f;                  // animasyonla karıştır
look.Events.lookedAtTarget.AddListener(() => Debug.Log("Gördü"));

// NPC: sadece başka bir sağlayıcı kullan.
var hedef = npc.AddComponent<TransformLookTarget>();
hedef.target = oyuncu.kafa;
npc.GetComponent<LookAtController>().TargetProvider = hedef;
```

## Active ragdoll entegrasyonu

[Wobblefolk Active Ragdoll](https://github.com/Atthree/wobblefolk-active-ragdoll) yüklüyse opsiyonel
`Wobblefolk.LookAt.ActiveRagdoll` derlemesi (`versionDefines` ile) derlenir. Ragdoll'un köküne **Active Ragdoll Look
At** ekle, bir hedef sağlayıcı ve istersen göz transform'larını ver. Start'ta şunları yapar:

- göğse bağlı bir bakış çerçevesi ekler, böylece kafa gövdeye göre döner;
- kafa eklemine bir `JointLookDriver` ekler; kafayı eklem motoru çevirir, gövde sallanınca kafa doğal şekilde geride
  kalır;
- gözler için mutlak modda bir `TransformLookDriver` ve bir `EyeBehaviour` ekler;
- ragdoll düşmüş, kalkıyor ya da baygınken bakışı söndürür.

Sadece kafa sürülür: göğüs eklemini ragdoll'un kendi kol sistemi yazar (uzanırken eğilmek için). `JointLookDriver`
sadece Unity'nin `ConfigurableJoint`'ini kullandığı için her fizik karakterle çalışır.

## Güncelleme sırası ve titreme

Bakış sistemlerindeki titremenin sebebi neredeyse her zaman güncelleme sırasıdır. Her karede gereken sıra şudur:

1. **Animasyon** (Animator) temel pozu yazar.
2. **Kameralar** hareket eder (çoğu takip kamerası ve Cinemachine varsayılan sırada LateUpdate'te çalışır).
3. **`LookAtController`**, `[DefaultExecutionOrder(1000)]` ile LateUpdate'te çalışır: kamera ışınını okur ve
   kemikleri yazar.
4. **Render.**

Bakış kameradan önce çalışsaydı fare ışını bir önceki karenin kamerasından gelir, kamera hareket ederken kafa
titrerdi. Kamera betiğin 1000'den sonra çalışıyorsa ona daha küçük bir sıra ver.

Fizik karakterler: `JointLookDriver` eklem hedeflerini LateUpdate'te yazar, eklem motorları sonraki FixedUpdate
adımlarında etki eder. Görünen gövdelerde Rigidbody interpolasyonu kullan.

## Animator ile kullanım

- `TransformLookDriver` Animator'dan sonra çalışır ve her halkanın payını animasyonlu pozun **üstüne ekler**,
  `weight` ile ölçekler. Animator'ün yazmadığı kemikleri sürücü her karede kendisi geri alır; ofsetler birikmez.
- **Çakışma.** Bir animasyon zaten kafayı çeviriyorsa (örneğin etrafa bakınan bir idle), ikisi toplanır.
  Seçenekler:
  - Bu kliplerde `weight`'i düşür (örneğin bir animation event ya da state behaviour'dan).
  - Bir override katmanında **Avatar Mask** ile kafa ve boyun dönüşünü o kliplerden çıkar; böylece onları sadece
    bakış sistemi çevirir.
  - Profilde kafa ve boyun ağırlıklarını 0 yap, sadece gözler oynasın.
- Humanoid **IK Pass** / `SetLookAtPosition` gerekmez; iki sistemin kafayı birlikte çevirmemesi için kapalı tut.

## Performans ve LOD

- **Maliyet.** Bir karakter birkaç düzine aritmetik işlem, ışın sağlayıcı kullanıyorsa bir de raycast çalıştırır.
  Hiçbir şey her karede bellek ayırmaz: geçmişler, raycast tamponları ve kayıt listeleri önceden ayrılmıştır.
- **İlgi noktaları** kendilerini statik bir listeye kaydeder; sahne taraması yapılmaz.
- **Göz LOD'u.** `eyeLodDistance` ayarlıysa, LOD kamerasından uzaktaki karakterlerde gözler, saccade ve kırpma
  atlanır.
- **Çok sayıda NPC.**
  - `CameraForwardTarget` / `TransformLookTarget` kullan (raycast yok).
  - `InterestLookTarget.reevaluateInterval`'ı artır.
  - Uzaktaki karakterlerde kontrolcüyü kapat (sürücüler pozu geri alır).

## Testler

- **EditMode (69):**
  - çözücü: limitler, arkaya bakma, histerezis, açı birikimi, zamanlama sırası, sıçramama
  - matematik
  - hedef sağlayıcılar ve ilgi seçimi
  - eksenleri bozuk kemiklerde transform ve eklem sürücüleri
  - gözler: yakınsama, saccade, kırpma, LOD, kapaklar
  - kurulum aracı ve Play'e girmeden önizleme
- **PlayMode (2, active ragdoll paketiyle):** ragdoll ayakta dururken kafasını fizikle çevirir, düşünce bakış söner.

*Window > General > Test Runner* ile çalıştır. Paket bu projenin `testables` listesinde.

## Sorun giderme

| Belirti | Çözüm |
|---|---|
| Kamera hareket ederken kafa titriyor | Güncelleme sırası: kamera bakıştan önce güncellenmeli (yukarıya bak). |
| Karakter yanlış yöne ya da yana bakıyor | Kök, dinlenme pozunda +Z'ye bakmalı. *Character Root*'u kontrol et. |
| Dönüşler zamanla birikiyor | Bakıştan sonra başka bir şey kemiği yazıyor ya da iki bakış kontrolcüsü aynı kemikleri sürüyor. |
| Gözler oynamıyor | Göz kemiği atanmamış, göz LOD'u devrede ya da profilde göz ağırlığı 0. |
| Karakter hiç arkasına bakmıyor | Tasarım gereği: arkadaki hedefte ulaşılabilen yere kadar dönülür ya da nötre dönülür (*Behind* ayarları). |
| Fare hiçbir şey yapmıyor | İmleç kilitli (ekran merkezi kullanılır) ya da *Camera* boş ve *MainCamera* yok. |
| Göz kırpmıyor | *Eye Behaviour*'da kapak modu ve kapakları ayarla. |
| Ragdoll'un kafası dönmüyor | Kafa ekleminin slerp drive'ı olmalı (ragdoll profili sağlar); entegrasyon sadece kafayı sürer. |

## Bilinen sınırlamalar

- **Gerçek FBX ile test edilmedi.** Kurulum aracı, kemik eksenleri kasıtlı olarak bozuk üretilmiş Humanoid iskeletler
  üzerinde test edildi, henüz gerçek bir FBX karakterle değil.
- **Kafa yana yatmaz (roll yok)**; sadece yaw ve pitch var.
- **Göz kapakları gözü izlemez.** Kapaklar gözlerin pitch'ini takip etmez.
- **Ragdoll'da sadece kafa.** Ragdoll entegrasyonunda yalnızca kafa sürülür; omuzlar için ragdoll'un göğüs ekleminin
  kol sistemiyle paylaşılması gerekir.

## Atıflar ve lisans

**MIT Lisansı** ile yayınlanmıştır (`LICENSE.md`). Bütün kod sıfırdan yazılmıştır. Fikir için incelenen açık kaynak
projeler `THIRD_PARTY_NOTICES.md` dosyasında listelenmiştir.
