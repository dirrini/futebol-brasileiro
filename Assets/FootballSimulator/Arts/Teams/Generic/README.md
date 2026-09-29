# Generic club visuals

These assets are explicitly non-official placeholders for clubs without a local visual binding.
They do not reproduce the crest or kits of a real club. São Paulo FC keeps its separate authored binding.

`Badge.svg` is the editable vector source. `node render-badge.cjs` (with `sharp` installed) exports `Badge.png`;
red and blue encode the two channels of the existing logo shader, rather than the displayed colors.
`GenericLogo.asset`, both kit assets and `GenericVisualTemplate.asset` are editable in the Unity Inspector.
The kits reuse the existing neutral KitMask1 layout: dark blue home, light away, orange and green goalkeepers.
The preparation screen starts with home and away kits respectively, and retains the existing manual kit switch.

Run **Tools → Futebol Brasileiro → Create generic club visuals** once to create the assets and declare
the template as `LegacyMatchBindings.DefaultVisualTemplate`. The command preserves existing authored
assets and per-club bindings. Sporting data and roster identities still come exclusively from the database.
