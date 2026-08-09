# Chapter Rules

A Jellyfin media segment provider that derives **intro, recap and outro** segments from the
*positions* of chapter markers — and works out, per series, which position means what by
checking candidate rules against segments that are already known to be correct.

## Why

Audio fingerprinting and black-frame detection are expensive and occasionally confident about
the wrong thing. Meanwhile many rips already carry the answer as metadata: chapter markers set
by a ripper that did scene or black-frame detection. In practice the last chapter tends to start
exactly where the end credits begin, and an early chapter marks the end of the "previously on"
recap.

This plugin does not guess that those rules hold. It measures whether they do.

For each series it replays candidate rules against the segments other providers already produced,
scores how often each rule reproduces them, and only stores a rule that clears a confidence
threshold on a sufficient number of samples. A series where the rule does not hold gets no rule
at all.

## Results on a real library

Measured across 19 series (roughly 3000 episodes), calibration took **1.4 seconds** — the same
library needs over an hour of ffmpeg analysis:

| Series | Rule | Agreement |
|---|---|---|
| Gilmore Girls | Outro = last chapter | 100 % of 153 |
| Gilmore Girls | Intro = chapter 1 → 2 | 100 % of 153 |
| Emily in Paris | Outro = last chapter | 100 % of 50 |
| Vampire Diaries | Outro = last chapter | 96 % of 168 |
| Game of Thrones | Outro = last chapter | 99 % of 73 |
| Avatar | Outro = last chapter | 98 % of 49 |
| Türkisch für Anfänger | Outro = last chapter | 93 % of 28 |

Just as important, the cases it **refused**:

| Series | Why no rule |
|---|---|
| How I Met Your Mother | best candidate agreed on only 83 % of 183 samples |
| Modern Family | only 19 of 250 episodes have chapter markers |
| One Piece (outro) | 55 % — anime puts the next-episode preview after the credits, so the last chapter is not the outro |

The One Piece case is the point of the design. A plugin that simply assumed "last chapter is the
outro" would be wrong there. This one notices.

## How rules work

A rule anchors on a chapter index. Non-negative values count from the start (`0` is the first
chapter), negative values count from the end (`-1` is the last chapter).

| Type | Segment produced |
|---|---|
| Outro | anchor → end of file |
| Recap | start of file → anchor |
| Intro | anchor → the following chapter |

Every derived segment must also fall inside a configurable plausibility window. This is what stops
a chapter that happens to sit near the end of a file from being read as an eight minute outro.

## Installation

Requires Jellyfin 10.11.

1. Download `Jellyfin.Plugin.ChapterRules.dll` from the releases page.
2. Place it in `<config>/plugins/Chapter Rules_<version>/`.
3. Restart Jellyfin.
4. Run the **Calibrate chapter rules** scheduled task (it also runs daily at 03:00).

Calibration only writes rules. Segments appear on the next media segment refresh.

## Configuration

| Setting | Default | Meaning |
|---|---|---|
| Minimum confidence | `0.9` | Share of samples a rule must reproduce before it is used |
| Minimum samples | `5` | Known segments a series needs before any rule is trusted |
| Agreement tolerance | `10 s` | How far a derived boundary may sit from a known one and still count |
| Intro / Recap / Outro windows | see UI | Plausible segment lengths per type |

Lowering the confidence threshold enables borderline series. On the library above, Vampire
Diaries' recap rule sits at 85 % and needs the threshold at `0.8` to be accepted.

## Relationship to other plugins

This complements rather than replaces detection-based plugins:

- **Intro Skipper** analyses audio and video. Keep it for series without chapter markers.
- **Chapter Segments Provider** (official) matches chapter *names*. Use it when your chapters are
  named `Intro`, `Credits` and so on. Chapter Rules exists for the far more common case of
  chapters named `Chapter 1`, `Chapter 2`, …

Calibration deliberately ignores segments produced by Chapter Rules itself, so its own output can
never become evidence for its own rules.

## Limitations

- Only helps files that actually have chapter markers, and only when those markers are
  structurally meaningful. Evenly spaced markers every five minutes carry no information.
- Needs some already-correct segments per series to calibrate against. A library with no
  detection at all has nothing to learn from.
- One rule per segment type per series. Series whose structure changes between seasons may
  calibrate below the threshold and be skipped.

## Building

```sh
dotnet build Jellyfin.Plugin.ChapterRules/Jellyfin.Plugin.ChapterRules.csproj -c Release
```

## License

GPL-3.0-only.
