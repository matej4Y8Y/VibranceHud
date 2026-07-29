# Revoking a license key

Any key can be cut off after the fact. The app reads a list of revoked keys from this
repo on every launch, so revoking is a two-step process: edit the list, then push it.

## Revoke a key

```bash
dotnet run --project Tools/KeyGenerator/KeyGenerator.csproj -c Release -- --revoke AACO-R-P-XXXXXXXX-XXXXXXXX
git add license-revocations.json && git commit -m "revoke a key" && git push
```

**The push is what actually does it.** Editing the file locally changes nothing for
users — the app fetches it from `raw.githubusercontent.com`, so until it's pushed, the
revoked key keeps working.

## Undo a revocation

```bash
dotnet run --project Tools/KeyGenerator/KeyGenerator.csproj -c Release -- --restore AACO-R-P-XXXXXXXX-XXXXXXXX
git add license-revocations.json && git commit -m "restore a key" && git push
```

A mistaken revocation is fully reversible; there's no need to issue a replacement key.

## See what's revoked

```bash
dotnet run --project Tools/KeyGenerator/KeyGenerator.csproj -c Release -- --list-revoked
```

The list stores SHA-256 hashes, not keys, so it shows counts and hashes rather than
readable keys. To test a specific key, run `--revoke` on it — it reports `Already
revoked` if it's on the list (then `--restore` it if you were only checking).

## How fast it takes effect

Once pushed, the user loses access **the next time they launch PlexusX** — the app
refreshes the list during its startup update check, re-verifies, and exits with a
"deactivated by the developer" message if their key is on it.

It does **not** interrupt a session already in progress. Someone with the app open
stays working until they restart it.

## Limitations worth knowing

- **Offline users keep working.** The check needs to reach GitHub. A machine that never
  goes online (or is offline when you revoke) keeps using its cached copy until it
  next connects successfully.
- **It fails open, on purpose.** If GitHub is down, rate-limits the request, or the
  file is malformed, nobody is revoked. The alternative — failing closed — would lock
  out every paying user the moment something went wrong with hosting, which is a far
  worse outcome than a revoked key working a while longer.
- **Not a defence against a determined attacker.** Someone who blocks the domain in
  their hosts file, or patches the binary, keeps access. This stops ordinary sharing
  and lets you cut off a refunded or abused key — it isn't DRM.

## Why hashes instead of the keys themselves

`license-revocations.json` lives in a public repo. Publishing raw serials there would
turn it into a public list of real keys. Hashing means the app can check membership
without the file ever containing a usable key.
