# SafeSeat 0.1.0 unsigned prototype

This folder contains a framework-dependent Windows controlled-input research prototype. It needs an existing compatible .NET 8 Windows Desktop runtime. It does not provide independent arbitrary Windows applications, and physical two-seat acceptance remains NOT TESTED.

The current release is unsigned. Use only an application launch permitted by the existing Windows protections; do not elevate, add exclusions, install a local trust root or disable a protection to launch it. No signing-provider acceptance is claimed.

Read [safety and recovery](SAFETY_AND_RECOVERY.md), [hardware validation](HARDWARE_VALIDATION.md) and the accompanying acceptance checklist before testing. Observation starts off. Source identification and the controlled demonstration each require explicit action. Escape, Stop and foreign-app focus stop observation. Normal Windows input continues and remains shared.

The application saves only explicitly requested hashed assignments in `profiles/default.json` beside the executable. Disposable text and optional aggregate metrics remain in process memory. No input text, clipboard contents, screenshots or raw event trace are saved by the application. Removing visible text is not a forensic erasure guarantee.

Close the app to remove its portable folder and optional local profile. There is no driver, service, startup registration or OS patch to uninstall. The package includes neither the SDK nor shared runtime.
