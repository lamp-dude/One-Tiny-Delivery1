# Delivery system setup

1. Create a `DeliverySystem` GameObject and add `DeliverySystem.cs`.
2. You do not need to make a panel, background, TMP text, Canvas, camera reference, or player anchor. `DeliverySystem` creates and wires a clean screen-space speech bubble at runtime, automatically using this project's `carCam` and vehicle target when needed.
3. Add routes in the order they should happen. Only the first route uses **Mission Start** and **Package Loaded** dialogue. At its pickup zone, the player finishes the opening conversation with Enter, then its cargo drops into the bed.
4. Each destination plays **Arrival**, then **Delivered** or **Dropped Cargo**, then (if another route exists) **Next Delivery** as one uninterrupted Enter-driven conversation. The next route's cargo drops after the final **Next Delivery** line. Later routes skip their own start dialogue; old serialized lines are preserved but unused.
5. Assign each job's cargo object and its safe cargo anchor on the truck. Set the drop distance. Cargo is marked dropped when it leaves that radius; customize the dropped-cargo destination text per job.
6. Put `DeliveryPickupZone` on the first giver with a 3D trigger collider and its matching delivery id. Later pickup zones are only needed if you want repeat-visit dialogue while that route is active.
7. Put `DeliveryDropoffZone` on each destination with a 3D trigger collider and its matching delivery id. Leaving a zone mid-conversation dismisses its bubble; re-entering resumes at the beginning of the current conversation section. The final route ends instead of looping back to the first.

The player object needs the `Player` tag (or change the tag field on both trigger components). At least one object involved in each trigger interaction needs a Rigidbody. Use **Enter** to reveal a line instantly, then press **Enter** again to advance it.
