# Test cheats — decision log

Every place the mod bends the game's rules while we test. A cheat here is not strategy. Each one names its log line, its switch, and the story that removes it.

| # | Cheat | Since | Log line | Switch | Why | Removed by |
|---|---|---|---|---|---|---|
| 1 | Player energy, hunger, happiness held at 100; energy and happiness drain off | early Epic 7 | `needs.held` (first frame of a save) | `LiveDump.HoldNeeds` in `ChaoticCeoMod.OnFrame` | The CEO avatar is not the product. Sleeping and eating would stall the clock. | Never — the avatar stays out of scope. |
| 2 | Employees held: satisfaction 100, open complaint cleared, next complaint a game year out, rival poach offer cancelled, age reset to 60 at 65 (one year before the retirement notice). A sick day on the calendar becomes never, and an absence from a call that already landed is cleared. Walking off a shift for another reason is not held. | 1.17.11.12 | `staff.held` (first hour of each day, and any hour that cleared a complaint, poach, age, or sick call) | `StaffHold.Enabled` | HR is a later problem. Quits, retirements, poaches, and sick calls churned the register and hid stocking bugs. | **14.10** HR policy. Turn the switch off before tuning HR. |
| 3 | Furniture delivery (storage shelf, rack) has no delivery fee and always arrives next day at 08:00 | 1.17.5 | `shelf.ordered`, `rack.ordered` | `WholesalerBridge.OrderFurniture` (`deliveryFee = 0f`) | The furniture store's own contract UI is not callable headless. | Open. Charge the fee when **14.4** reads daily cost. |
| 4 | Wholesale delivery contract signed with no delivery fee | 1.17.1 | `contract.signed` | `WholesalerBridge.Live` (`deliveryFee = 0f`) | Same: the contract is written to the save, not bought through the dialog. | Open. Same as 3. |
| 5 | A boxed storage shelf is placed straight into the building (no one carries it) | 1.17.8.1 | `shelf.placed` | `StorageShelfBridge.Place` | Furniture in cargo is never unboxed by staff. The player would do it by hand. | Never — placement is the mod's job. |

## Not cheats (the game does this too)

- Wholesale deliveries appear on shelves instantly. The game tops up the displays first, then drops the rest onto storage-shelf slots, filling one shelf before the next. Nobody carries boxes. Overflow is refunded with "no available space."
- Restocking pulls from any stock container in the building, including the delivery pallet on the floor.

## Rules

- A new cheat gets a row here, a named log line, and a switch in the same change.
- Remove a cheat in the story named in its last column, and strike the row there.
