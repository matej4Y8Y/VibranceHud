# PlexusX 1.0 — licence system

Written 2026-07-31.

## Why this is being rebuilt

Three separate failures in the beta licence system, each of which would be worse once money
is involved.

**Keys can be forged by anyone with the installer.** Signing uses one symmetric secret, and
that secret must ship inside the app so it can verify keys. It was extracted from a public
clone and used to mint working "paid" keys in about two minutes — demonstrated, not
theorised. Nothing about a paywall holds while this is true.

**Keys break across versions.** The tier is a letter the build has to recognise, so a key
issued today is meaningless to a build from last week. Adding the week tier ('W') after 0.9.7
shipped meant a week key handed to a 0.9.7 user was read as a *year*, because unknown markers
defaulted to the most generous tier. That default is fixed, but the design still means every
new tier is a compatibility event.

**One key works on unlimited PCs.** Hardware binding happens locally at activation, so each
machine independently writes its own valid licence. A shared key works everywhere it's pasted.

## What 1.0 does instead

### The key is an identifier, not a licence

A short, readable code:

```
PLX7-K2MQ-8XRT-9WDN
```

It carries no permissions and no duration. On its own it grants nothing, so knowing the
format buys an attacker nothing. It is redeemed for a licence, once.

Chosen over a self-contained signed key (~120 characters, offline-verifiable) because a
signature is 64 bytes and there is no way to shrink that. A key people can read out, type and
quote in a support message is worth more than offline redemption, given activation happens
exactly once per machine.

### The licence is signed, and verified offline forever after

Redemption returns a small signed document:

```json
{
  "serial":  "PLX7-K2MQ-8XRT-9WDN",
  "plan":    "monthly",
  "issued":  "2026-07-31T14:22:05Z",
  "expires": "2026-08-31T14:22:05Z",
  "hardware":"MXXBGGXAOCQP36SC"
}
```

signed with ECDSA P-256, verified against a public key embedded in the app. ECDSA rather than
Ed25519 purely because .NET has it built in — no native library, which matters for a
single-file self-contained build.

**Expiry is a date inside the signed document, not a tier the build must recognise.** That is
what ends the version-compatibility problem permanently: a licence issued years from now,
under a plan that doesn't exist yet, still expires correctly on a build from today. The app
never has to be taught what a plan means.

Only the public key ships. It verifies; it cannot sign. Extracting it lets someone check
licences, not create them.

After redemption everything is local: the app re-reads the file, verifies the signature and
compares dates. No network, no server dependency for day-to-day use.

### Redemption is the only online moment

The app sends the key and a hardware fingerprint; the service returns a signed licence, or
refuses. Because refusal is decided in one place, "already used on another PC" becomes
possible for the first time.

Deferred, per decision: the app is built against a `ILicenceRedeemer` interface with a local
implementation that signs with a development key. Everything except the network call can be
built and tested now, and the real service drops in without touching licence handling.

## Rules the service will enforce

These are stated now so the interface doesn't have to change later.

- **Unused key** → issue licence, record key + fingerprint.
- **Same fingerprint again** → issue again. Reinstalling, or updating Windows, must not
  consume someone's key. Getting this wrong turns every reinstall into a support ticket.
- **Different fingerprint** → refuse. This is the anti-sharing rule.
- **Released key** → treated as unused. Needed because fingerprints change when someone
  swaps a GPU or drive, and a paying customer must not be locked out by a hardware upgrade.
- **Expired plan** → refuse.

## Trial

Four days, self-service, no key. The trial start is recorded against the hardware
fingerprint so that reinstalling does not grant another four days.

Honest limit: any local marker can be found and deleted, and a determined user will reset the
trial. The goal is that it isn't *accidental* — not that it's impossible. Making it genuinely
tamper-proof needs the trial to be server-side, which is a decision for after 1.0 ships.

## Migration from beta

Nothing carries over. Beta licences were signed with the symmetric secret; 1.0 does not
accept that format at all, so every beta key is inert by construction rather than by a rule
that could be missed.

Combined with the version gate already shipped in 0.9.9, the transition is: raise
`minimumVersion` to 1.0.0, beta builds lock, and beta keys are worthless in the build that
replaces them.

## What gets built, in order

1. **Licence format + verification** — signed document, ECDSA verify, expiry from dates.
   Entirely offline, entirely testable, no service required.
2. **PlexusX Keys** — local tool. Holds the private key, generates key batches, lists what
   was issued with plan/date/expiry/status, revokes and releases.
3. **App integration** — redemption flow, the three-plan screen, trial.
4. **Service** — the real redeemer, when the website exists.

Each is finished before the next begins. Steps 1–3 need nothing external, which is why the
service being undecided doesn't block starting.

## Testing

The parts that decide whether someone pays are pure functions and get direct tests: signature
verification (including a licence altered by one byte, and one signed by the wrong key),
expiry against a supplied clock, hardware mismatch, and malformed input failing closed.

Anything that reaches the network or the filesystem sits behind an interface, as
`IRegistryAccess` already does for the FPS tweaks — the licence rules must be testable
without a service to talk to.

The one thing tests cannot cover is a short-lived licence genuinely ending while the app
runs. That gets checked by hand before release: issue a two-minute licence, leave the app
open, watch it lock.
