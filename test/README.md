# Forge3D Manual Test Fixtures

Use these files from the ControlStation data import menu.

- `obstacles-basic.csv`: simple import sanity check.
- `obstacles-corridor.csv`: narrow corridor for vehicle footprint and clearance testing.
- `obstacles-blocked-goal.csv`: goal-blocking layout for failed planning / warning behavior.
- `obstacles-basic.json`: JSON import sanity check.
- `obstacles-invalid.csv`: validation edge case with one valid obstacle after bad rows.

Suggested flow:

1. Load Engineering Scenario.
2. Import one fixture.
3. Run Grid A* and Hybrid A*.
4. Start the mission and watch whether the vehicle turns before moving through sharp waypoint transitions.
5. Leave the mission running for more than 3 seconds and confirm automatic replanning keeps the path fresh.
