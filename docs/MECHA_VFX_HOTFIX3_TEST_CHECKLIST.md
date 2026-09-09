# Darius v0.30.6-final — Mecha visual test checklist

1. Enter a run with **Mecha Kingdoms Darius**.
2. Press Q once without hitting anything.
   - No green WindowPattern grid should appear.
   - Windup outer indicator should read as a circular ground ring, not a stretched ellipse.
   - Its outer edge should visually coincide with the released Q outer ring / actual 4.25 m hit boundary.
3. Basic attack until Hemorrhage reaches five stacks and Noxian Might starts.
   - No full-body green grid.
   - No large rectangular white sheet behind/under Darius.
   - White airflow may remain, but should stay compact around Darius rather than reaching far across the screen.
4. Cast E in one direction, immediately turn/move another direction.
   - No stale old-direction slash/trail should remain behind Darius.
5. Cast R.
   - No giant white mesh strip should cross the screen.
6. End the run, return to lobby, then start another run.
   - Darius model should still render; no CharacterModelDisplay `Instantiate(null)` regression.
7. Quick regression: Classic, God-King and Dunkmaster Q once each.
   - Their already accepted presentation should be unchanged.
