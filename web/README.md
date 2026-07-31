# PlexusX site

Static, no build step. `index.html` is self-contained on purpose — one file, no fonts or
scripts fetched from anywhere else, so it loads fast and cannot break because someone
else's CDN went down.

## Run it locally

```
python -m http.server 5173 --directory web
```

Then open http://localhost:5173

## Deploying

Any static host works. Cloudflare Pages: point it at this folder, no build command.

## What still needs wiring

The buttons are marked so the checkout code knows where to attach:

- `[data-checkout="monthly"]` and `[data-checkout="lifetime600"]` — payment links
- `[data-download]` — the installer
- `[data-discord]` — the invite

Search for those attributes; nothing else needs touching to go live.

## When accounts arrive

The plan is that buyers sign in (Steam or Google) and see their keys in a dashboard, and
that a monthly subscription needs no key at all — the account carries it. Nothing on this
page assumes keys are pasted by hand, so the pricing section stays as-is and `/dashboard`
is added alongside it.
