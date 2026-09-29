# Rich text colors

One subject: the "#" color control grammar for Reef text, and how a color run
reaches the glyphs that a renderer draws. It covers the value grammar, greedy
tokenization, the literal escape, and the fallback when a value is invalid.

## Grammar

A "#" control character starts a color run when a valid value follows it.
The run applies to every character after the value until the next "#" control
character.

- `#RRGGBB` is a 6 digit hex color. `#RRGGBBAA` is an 8 digit hex color with
  alpha. The 8 digit form wins when both could match.
- `#NAME` is a CSS/X11 color name. Names are case-insensitive.
- `#RESET` and `#R` reset to the element color. This is the inherit state.
- `##` draws one literal "#".
- Any "#" that does not parse draws the "#" literally.

Example: `#FF0000Hello #Rworld#BLUE!` draws `Hello` in red, `world` in the
element color, and `!` in blue.

## Greedy tokenization

Values are not delimited. The parser matches the longest valid token. It tries
forms in this order:

1. Hex. An 8 digit value, then a 6 digit value. A hex value must not be
   immediately followed by another hex digit.
2. A named color. The longest palette name that is a prefix of the following
   letters.
3. A reset. `RESET` (5 chars), then `R` (1 char).

A name or reset wins only when it is longer than the best match so far. Hex
wins ties. This makes `#FF0000Hello` parse `FF0000` then `Hello`, and
`#REDHello` parse the name `RED` then `Hello`.

The palette is a static dictionary in
`Reef/Text/RichTextColor.cs`. It holds the full CSS/X11 name set.

A parsed token eats one trailing space. The space after a tag is a delimiter,
not content. So `#FF0000 Hi` renders `Hi`, and `#RESET B` renders `B`. This
keeps a tag from leaving leading whitespace in the rendered text.

## Parsing and layout

`RichText.Parse` strips the control sequences and records one color per
remaining character. A null color means inherit the element color. The parser
is `Reef/Text/RichText.cs:22`.

`RichTextColor.TryParseValue` tokenizes one value and returns the color and
the length it consumed. It is `Reef/Text/RichTextColor.cs:58`. The hex, name,
and reset matchers are at `Reef/Text/RichTextColor.cs:85`, `:134`, and `:161`.

`Typeface` runs measure, wrap, and layout on the stripped text. Control
sequences take zero width. A color is not added to the glyph count.

## Where the color is applied

`GlyphLayout` carries a nullable color. A null color means the renderer uses
the element color. The field is `Reef/Text/GlyphLayout.cs:9`.

`GlyphLayout.ResolveColor` mixes the run color with the element color. It
returns the element color when no run is set. Otherwise it keeps the run RGB
and multiplies the run alpha by the element alpha. This lets a faded text
element fade its colored runs too. The method is `Reef/Text/GlyphLayout.cs:15`.

The two renderers call `ResolveColor` on each glyph:

- The pixel renderer: `Reef/PixelRenderer.cs:144`.
- The OpenGL renderer: `Swordfish/Graphics/SilkNET/OpenGL/Renderers/ReefRenderer.cs:161`.

Both re-run layout from the source string. They use the same strip-then-layout
path, so their glyph arrays agree with the cached layout.

## Editable text

Stripping shifts glyph indices away from source string indices. This matters
only where code maps a caret to a glyph by source index, such as the text box.
Its text has no "#" sequences, so stripped text equals source text and the
mapping is unchanged. Color tags in an editable text box are not supported.

## Source of truth

- `Reef/Text/RichText.cs`
- `Reef/Text/RichTextColor.cs`
- `Reef/Text/GlyphLayout.cs`
- `Reef/MSDF/Typeface.cs`
- `Reef/PixelRenderer.cs`
- `Swordfish/Graphics/SilkNET/OpenGL/Renderers/ReefRenderer.cs`

## Tests that pin this

- `Reef.Text.Tests/RichTextColorTests.cs` covers tokenization: 6/8 digit hex,
  names, case-insensitivity, longest name, and invalid values.
- `Reef.Text.Tests/RichTextTests.cs` covers parsing: stripped text, color
  alignment, reset, the literal escape, the trailing-space rule, and invalid
  control characters.
- `Reef.Text.Tests/GlyphLayoutTests.cs` covers color resolution: element color
  fallback and run-alpha by element-alpha mixing.