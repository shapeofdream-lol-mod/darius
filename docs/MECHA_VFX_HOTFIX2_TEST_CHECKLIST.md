# Darius v0.30.5-final — Mecha test checklist

Test the Mecha skin in this order:

1. Q once with no enemy nearby.
   - No green WindowPattern grid should appear when Q starts.
   - The windup outer boundary should visually match the final attack boundary.
2. Place an enemy near the Q outer edge.
   - Visual outer boundary and actual 4.25 m hit edge should agree.
3. Basic attack repeatedly and reach 5 Hemorrhage stacks / Noxian Might.
   - Darius must not become covered by a full-body green grid.
   - Noxian Might must not flash opaque white rectangular/card planes on Darius.
   - The target's five-stack marker must not produce the old character-atlas card rectangle.
4. Cast E, then immediately rotate/move Darius.
   - No old-direction Mecha E slash should remain hanging behind Darius after the hook beat.
   - Target pull streak should still follow the target displacement.
5. Cast W and R once.
   - W should remain visually equivalent to v0.30.4.
   - R should retain the previous Mecha mesh-UV correction and must not restore the long white strip.
6. Finish/leave a run and enter another scene/run.
   - Darius model and all four Skin presentation resources should remain valid after lifecycle rebuild.

If a visual issue remains, send the new runtime log plus a short clip; visual evidence takes precedence over `built=...` success counts.
