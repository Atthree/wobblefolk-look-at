# Changelog

All notable changes to this package are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the package uses [Semantic Versioning](https://semver.org/).

## [0.1.0] - 2026-10-29

### Added

- Layered look-at chain: eyes → head → neck → upper chest → torso, each with weight, engage angle, yaw and asymmetric
  pitch limits, smoothing, speed cap and reaction delay.
- `LookSolver`: pure-logic yaw/pitch solver without Euler angles, with a spine twist budget, no accumulation past the
  target or limits, and behind-target handling with hysteresis (`ClampToLimits`, `ReturnToNeutral`).
- `LookProfile` asset with the Realistic, Robotic Snappy and Sluggish presets.
- Target providers: `MouseLookTarget`, `CameraForwardTarget`, `TransformLookTarget`, `InterestLookTarget` with
  `LookInterestPoint`.
- `LookAtController` with global weight, `LookAt(target, duration)` lock, `lookedAtTarget` / `lostTarget` events,
  eye LOD and gizmos.
- Drivers: `TransformLookDriver` (bones, blends over animation) and `JointLookDriver` (`ConfigurableJoint` motors).
- `EyeBehaviour`: convergence, saccades, idle glances, blinking with blend-shape, lid-bone or lid-scale eyelids.
- Optional Wobblefolk Active Ragdoll integration (`ActiveRagdollLookAt`, head only).
- Editor: *Wobblefolk > Setup Look At* window and Edit-mode preview in the controller Inspector.
- Samples: Basic Character, Active Ragdoll Integration.
- 69 EditMode and 2 PlayMode tests.
