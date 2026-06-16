# NOOSE-Website — Claude Code Guidelines

## Comment Style

Write comments like a real developer: **short, English, 2–3 words max**.

```cs
// visibility check
// soft delete
// best effort
// refresh claims
```

```js
// debounce
// node colors
// id → instance
```

Rules:
- **English only** — no German, ever
- **Inline `//`** — 2–3 words; describe the *why*, not the what
- **`catch { }` blocks** — `/* best effort */` or `/* ignore */`
- **XML `/// <summary>`** — one short English line: `/// <summary>Set classification on target.</summary>`
- **No block comments** — collapse multi-line explanations to a single short line or delete them
- **No "Phase X" references** in comments — just describe what the code does
