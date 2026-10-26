# SwiftCollections Migration Guide

## Upcoming release

### Shared diagnostics extraction

Shared logging and exception guards now come from the independent
`Chronicler.Diagnostics` package (v1.0.0), in both standard and Lean builds.
Replace imports of `SwiftCollections.Diagnostics` for shared types with
`Chronicler.Diagnostics`. Rename `SwiftThrowHelper` to `ChronicleGuard` and
`SwiftThrowInterpolatedStringHandler` to `ThrowInterpolatedStringHandler`.
`SwiftCollections.Diagnostics.SwiftCollectionDiagnostics.Shared` stays in
SwiftCollections; library-specific logger facades and channel names stay intact.

Rebuild consumers and adapters together: moved types have new CLR identities,
including types exposed in logger signatures. There are no forwarding wrappers.
This extraction does not change recorded fields or record-hash identities.
Publish Chronicler.Diagnostics before dependent packages, then validate against
the coordinated released versions; local-stack builds alone do not prove the
published package graph is compatible.
