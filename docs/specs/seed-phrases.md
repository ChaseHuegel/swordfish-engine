# Seed Phrase Generation and Parsing

One subject: deterministic translation between 64-bit world seeds and four-word
sentences, plus parsing of phrases and custom seed inputs.

## Grammatical structure and bit layout

Every generated seed phrase follows a fixed four-word sequence:

`[Adjective] [Subject] [Verb] [Destination]`

Each category dictionary contains 1,024 (`2^10`) curated entries. The lower 40
bits of a 64-bit `long` seed map directly to four 10-bit indices (`0x3FF` mask):

| Bits | Slot | Category |
|---|---|---|
| `0..9` | 0 | Adjective |
| `10..19` | 1 | Subject |
| `20..29` | 2 | Verb |
| `30..39` | 3 | Destination |
| `40..63` | — | Unused during phrase encoding (used by 64-bit hash fallback) |

`SeedPhraseService.SeedToPhrase(long seed)` (`WaywardBeyond.Data/SeedPhraseService.cs:81`)
slices the lower 40 bits and joins the four dictionary words with single spaces.
This guarantees a bijective 1:1 mapping across all `2^40` (`1,099,511,627,776`)
possible four-word combinations.

## Dictionaries and constraints

The four word lists ship as columns in the localized tag CSV asset
`WaywardBeyond.Data/assets/lang/en/tags/seeds.en.csv`:

- `seeds.adjectives`
- `seeds.subjects`
- `seeds.verbs`
- `seeds.destinations`

Every word in the four dictionaries satisfies these rules:

- Each tag column contains 1,024 unique words.
- Every word uses strict ASCII letters (`[A-Za-z]`) in `TitleCase`.
- Every word is 3 to 9 characters long, so a generated four-word phrase never
  exceeds 39 characters.
- Lookups are case-insensitive (`StringComparer.OrdinalIgnoreCase`).

`SeedPhraseService` (`WaywardBeyond.Data/SeedPhraseService.cs:49`) takes
`IAssetDatabase<LocalizedTags>` in its constructor and resolves the four tag
lists from the active culture (falling back to `"en"`).
`DataComposition.RegisterDataServices` (`WaywardBeyond.Data/DataComposition.cs:17`)
calls `container.RegisterTags()` and registers `SeedPhraseService` as a shared
singleton.

## Normalization and resolution

`SeedPhraseService.ResolveSeed(string? input)` (`WaywardBeyond.Data/SeedPhraseService.cs:96`)
converts user input into a `long` seed:

1. Null, empty, or whitespace-only input returns `0L`.
2. `NormalizeInput` strips leading and trailing non-alphanumeric characters,
   converts ASCII letters to lowercase, replaces non-alphanumeric runs with a
   single space, and collapses consecutive spaces.
3. If the normalized string splits into four tokens and each token exists in its
   slot dictionary, `ResolveSeed` reconstructs the 40-bit `long` seed via bitwise
   shifts.
4. Otherwise, `ResolveSeed` hashes the normalized string with 64-bit FNV-1a
   (`FNV1a.ComputeHash64`, `WaywardBeyond.Data/FNV1a.cs:24`).

## Save creation, generation, and UI display

- `Level.Seed` (`WaywardBeyond.Data/Nsd/saves.nsd:32`) stores the resolved `long`
  seed.
- `SqliteLevelCatalog.Create` (`WaywardBeyond.Server/Saves/SqliteLevelCatalog.cs:37`)
  resolves the request seed string with `SeedPhraseService.ResolveSeed` and passes
  the `long` seed to `LevelGenerator`.
- `LevelGenerator` (`WaywardBeyond.Server/Generation/LevelGenerator.cs:21`) folds
  the 64-bit seed into 32 bits (`(int)(seed ^ (seed >>> 32))`) to seed asteroid
  placement, voxel noise, and deterministic structure UUIDs.
- `NewSavePage` (`WaywardBeyond.Client/UI/Layers/Menus/Main/NewSavePage.cs:23`)
  sets the seed input box maximum length to 40 characters. The random seed dice
  button and empty-seed fallback generate a random four-word phrase with
  `SeedPhraseService.SeedToPhrase(Random.Shared.NextInt64())`.
- `SelectSavePage` (`WaywardBeyond.Client/UI/Layers/Menus/Main/SelectSavePage.cs:377`)
  and `HomePage` (`WaywardBeyond.Client/UI/Layers/Menus/Main/HomePage.cs:343`)
  display `SeedPhraseService.SeedToPhrase(save.Level.Seed)`.

## Source of truth

- `WaywardBeyond.Data/SeedPhraseService.cs:15`
- `WaywardBeyond.Data/FNV1a.cs:24`
- `WaywardBeyond.Data/DataComposition.cs:17`
- `WaywardBeyond.Data/assets/lang/en/tags/seeds.en.csv`
- `WaywardBeyond.Data/Nsd/saves.nsd:32`
- `WaywardBeyond.Server/Saves/SqliteLevelCatalog.cs:37`
- `WaywardBeyond.Server/Generation/LevelGenerator.cs:21`
- `WaywardBeyond.Client/UI/Layers/Menus/Main/NewSavePage.cs:23`
- `WaywardBeyond.Client/UI/Layers/Menus/Main/SelectSavePage.cs:26`
- `WaywardBeyond.Client/UI/Layers/Menus/Main/HomePage.cs:24`

## Tests that pin this

- `Swordfish.Tests/SeedPhraseServiceTests.cs` pins dictionary constraints,
  bijective 40-bit round-trips, case-insensitivity, punctuation normalization,
  and 64-bit FNV-1a fallback hashing.
