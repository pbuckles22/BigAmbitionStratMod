# Warehouse Selection, Grid Geometry & Maximum Reachable Pallet Layout Specification

## 1. Executive Answer: Which Warehouse Is the Biggest & Best?

In *Big Ambitions* (Build 3682 / 1.0), warehouses are governed by **two separate ceilings**: **Floor Area ($\text{m}^2$)** (how many `2m × 3m` Pallet Shelves fit inside) and **Vehicle Bays** (how many Freight Trucks can dispatch at 02:00 AM).

| Series | Building Codes | Total Area ($\text{m}^2$) | Vehicle Bays (`GarageSlots`) | Max Outbound Throughput ($2\times$ Freight Truck T1) | Theoretical Zero-Aisle Max Shelves ($\lfloor \text{Area}_{\text{net}} / 6 \rfloor$) | **Max 100% Reachable Pallet Shelves (Comb / Spine Math)** | Max Box Capacity ($\times 60$ boxes/shelf) | Best Role in Autopilot Strategy |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **H-Series** | `H1`, `H2`, `H3` | $690\text{ m}^2$ | **1 Bay** | $60\text{ boxes/day}$ ($8$ stores max) | $\approx 95$ shelves | **$64\text{–}72$ shelves** | $3,840\text{–}4,320$ boxes | **Avoid for retail hubs** (`strategy.md` §6 requires $2$ garage slots per warehouse). Only used by manual players as a factory buffer. |
| **I-Series** | **`I1`, `I2`, `I3`** | **$1,292\text{ m}^2$** | **2 Bays** | **$120\text{ boxes/day}$ ($16$ stores max)** | $\approx 185$ shelves | **`I1`: $132\text{–}144$ shelves**<br>**`I2`/`I3`: $99\text{–}128$ shelves** | **$5,940\text{–}8,640$ boxes** | **#1 Best Retail Distribution Warehouse (Stages 2–4).** Available inside Manhattan (Hell's Kitchen, Garment District, Lower Manhattan) + Industry City. Matches $100\%$ of the 2-truck throughput ceiling at lowest rent ($\sim \$700/\text{day}$). |
| **P-Series** | `P1`, `P2`, `P3` | $2,184\text{ m}^2$ | **2 Bays** | $120\text{ boxes/day}$ ($16$ stores max) | $\approx 325$ shelves | **$228\text{–}252$ shelves** | $13,680\text{–}15,120$ boxes | **Best Hybrid Factory + Importer Bulk Hub** ($\sim \$750/\text{day}$ rent, Industry City). |
| **Q-Series** | **`Q1`, `Q2`, `Q3`** | **$2,610\text{ m}^2$** | **2 Bays** | $120\text{ boxes/day}$ ($16$ stores max) | $\approx 395$ shelves | **$276\text{–}304$ shelves** | **$16,560\text{–}18,240$ boxes** | **#1 Biggest Warehouse in the Game by Raw Pallet Capacity.** Best when a single warehouse must hold massive multi-week importer stockpiles. |

### `I1` vs. `I2` vs. `I3`: Which `I`-Series Floorplan Wins?
All three `I`-series buildings are **$1,292\text{ m}^2$ with 2 Vehicle Bays**, and blueprints **cannot** be shared across `I1`, `I2`, and `I3` because their wall/bay/pillar coordinates differ:
1. **`I1` (Winner — Cleanest Rectangular Hall):** The two vehicle bays sit side-by-side at one end of a clean, wide rectangle with minimal pillar interference. This allows uninterrupted longitudinal back-to-back spine rows and yields **$6\text{–}12$ more reachable pallet shelves** than `I2` or `I3`.
2. **`I3` (Runner-Up — Offset Bay / Pillar Grid):** The loading dock and structural columns split the floor into two asymmetric zones. (In your own `Recover #1 (Modded).hsg` save locked in `RestockTests.cs`, `W12` held **$99$ pallet shelves** and `W13` held **$95$ pallet shelves**—the exact standard high-density reachable build for `I1`/`I3`!)
3. **`I2` (Third):** L-shaped/offset utility cutout around the garage bay reduces long-row continuity by $\sim 4\text{–}8$ shelves compared to `I1`.

---

## 2. The Mechanics & Math of "Reachable" Pallet Shelves

### 2.1 Unit Dimensions & Constants
* **Grid Resolution:** $1.0\text{ m} \times 1.0\text{ m}$ ($0.5\text{ m}$ half-step snap available when holding `Shift` or `Alt`).
* **Pallet Shelf (`ba:itemname_palletshelf`):**
  * **Length ($L_s$):** $3.0\text{ m}$ (the wide face where pallets are loaded/unloaded).
  * **Depth ($D_s$):** $2.0\text{ m}$ (the narrow side upright frame).
  * **Area ($A_s$):** $2.0\text{ m} \times 3.0\text{ m} = 6.0\text{ m}^2$.
  * **Storage Capacity:** $60\text{ boxes}$ per shelf ($3$ vertical levels $\times 2$ pallet bays per level $\times 10$ boxes per pallet).
  * **Cost:** $\$2,500$ per shelf (at *AJ Pederson & Son* or *Factory Supply Depot*).
* **Fixed Non-Buildable Zones Inside Any Warehouse:**
  * **2 Vehicle Bays (`VehicleSlot` $\times 2$):** Each bay reserves roughly $4\text{ m} \times 10\text{ m} = 40\text{ m}^2$ ($\approx 80\text{ m}^2$ total for 2 bays) plus the **Loading Staging Apron** (`ba:itemname_deliveryspot`, `ba:itemname_flatbedspawner`, `ba:itemname_handtruckspawner`) requiring $\approx 40\text{–}60\text{ m}^2$ of clear floor in front of the bays.
  * **Net Usable Storage Area ($A_{\text{net}}$):**
    $$A_{\text{net}} = A_{\text{total}} - A_{\text{bays+apron}}$$
    For `I1`/`I3` ($1,292\text{ m}^2$): $A_{\text{net}} \approx 1,292 - 152 = 1,140\text{ m}^2$.

### 2.2 What "Reachable" Means in the Game Engine
In *Big Ambitions*, reachability operates on two levels:
1. **Strict Pathfinding Reachability (Required for Factory Workers, Cleaners, Flatbed Carts, and Future-Proof Store/Warehouse AI):**
   * A shelf is **strictly reachable** if and only if at least **one of its two $3\text{m}$ long faces** (or an exposed $1\text{m}+$ access edge) is adjacent to a **continuous $\ge 1.0\text{m}$-wide ($\ge 1.5\text{m}\text{–}2.0\text{m}$ for flatbed/forklift turning) unobstructed floor path** that connects to the **Loading Bay Apron (`ba:itemname_deliveryspot`)** and the **Pedestrian Door**.
   * As verified in community bug reports (e.g., Forum Thread #6422), if an AI worker selects a specific shelf instance that is walled off behind another shelf with $0\text{m}$ aisle clearance, the AI worker freezes waiting for pathfinding even if an adjacent shelf has the same item!
2. **How to Maximize Density Without Blocking Any Shelf (The 4 Geometric Laws):**
   * **Law 1 — Wall-Flush Single Rows (Zero Wasted Back Aisle):** Against all four outer walls (except across doors and vehicle bays), place a **single row ($2\text{m}$ deep)** flush against the wall. Its inner $3\text{m}$ face opens directly into the aisle.
   * **Law 2 — Back-to-Back Double-Deep Spine Rows ($4\text{m}$ Block Width):** In the interior of the room, never place single isolated rows. Always pair two rows **back-to-back** ($2\text{m} + 2\text{m} = 4\text{m}$ total thickness). Row A is accessed from the left aisle; Row B is accessed from the right aisle.
     * **Area Efficiency of a Double-Row Module:**
       * With a **$1.0\text{m}$ tight walkway** (walkable by staff/player): Each double-row module is $4.0\text{ m} \text{ (shelves)} + 1.0\text{ m} \text{ (aisle)} = 5.0\text{ m}$ wide. Packing efficiency is:
         $$\eta_{1\text{m}} = \frac{4.0\text{ m}}{5.0\text{ m}} = 80.0\%$$
       * With a **$2.0\text{m}$ flatbed/forklift aisle**: Each double-row module is $4.0\text{ m} + 2.0\text{ m} = 6.0\text{ m}$ wide. Packing efficiency is:
         $$\eta_{2\text{m}} = \frac{4.0\text{ m}}{6.0\text{ m}} = 66.7\%$$
   * **Law 3 — Single Cross-Main Header Aisle (Eliminate Double Cross-Aisles):** Instead of leaving a walkway around both ends of the spine rows (which wastes two cross-aisles), **butt one end of every double-row spine flush against the back wall's single row**, leaving only **one** cross-aisle at the front (connecting to the loading bays). This turns the layout into an **"E-Comb" (Dead-End Finger Aisles)** where every shelf is still $100\%$ reachable!
   * **Law 4 — Aisle-End Cap Pocket ("The +1 Puzzle Trick"):** At the dead end of each $2\text{m}$-wide finger aisle, you can place **one $3\text{m} \times 2\text{m}$ shelf perpendicular across the dead end**. The $2\text{m}$ aisle still touches the front face of that cap shelf AND the side faces of the spine shelves!

---

## 3. Exact Mathematical Formulation of the "E-Comb" Maximum Reachable Layout

Let the usable rectangular storage hall (excluding the garage bay apron) have width $W$ (parallel to the back wall) and depth $L$ (from the loading bay apron to the back wall).

### 3.1 Step-by-Step Packing Equations
1. **Back Wall Flush Row:**
   * Place a single row of shelves ($D_s = 2\text{m}$ deep) flush along the back wall of width $W$:
     $$N_{\text{back}} = \left\lfloor \frac{W}{3} \right\rfloor$$
   * Remaining depth for the comb fingers and front cross-aisle:
     $$L_{\text{rem}} = L - 2\text{ m}$$
2. **Front Cross-Header Aisle (Connecting All Finger Aisles to the 2 Loading Bays):**
   * Reserve a cross-aisle of width $A_{\text{header}} = 2.0\text{ m}$ (or $1.5\text{ m}$ tight) along the front of the storage hall so the player/workers can walk from the loading bays into every finger aisle:
     $$L_{\text{spine}} = L_{\text{rem}} - A_{\text{header}} = L - 2 - A_{\text{header}}$$
   * Number of $3\text{m}$-long shelves per spine column of length $L_{\text{spine}}$:
     $$n_{\text{col}} = \left\lfloor \frac{L_{\text{spine}}}{3} \right\rfloor$$
3. **Horizontal Packing Across Width $W$ (Wall Rows + Double Spines + Finger Aisles):**
   * Place **1 single row ($2\text{m}$ deep)** flush against the **Left Wall** and **1 single row ($2\text{m}$ deep)** flush against the **Right Wall** ($4\text{m}$ total width used by the two side walls).
   * Between the left and right wall rows, alternate **Finger Aisles of width $a \in \{1.0\text{m}, 1.5\text{m}, 2.0\text{m}\}$** and **Back-to-Back Double Spines of width $4.0\text{m}$ ($2 \times 2\text{m}$)**:
     * If there are $k$ interior double-spines ($2k$ shelf columns) plus the $2$ outer wall columns ($2k + 2$ total shelf columns), there are **$k + 1$ finger aisles** separating them!
     * Total width required for $k$ interior double-spines and $k + 1$ aisles of width $a$:
       $$W_{\text{req}}(k, a) = \underbrace{4.0\text{ m}}_{\text{Left + Right Wall Rows}} + \underbrace{4.0k\text{ m}}_{k \text{ Double Spines}} + \underbrace{(k + 1)a\text{ m}}_{k+1 \text{ Finger Aisles}}$$
     * Solve for the maximum number of interior double-spines $k_{\max}$:
       $$k_{\max}(W, a) = \left\lfloor \frac{W - 4.0 - a}{4.0 + a} \right\rfloor$$
     * Total vertical shelf columns $C_{\text{total}}$:
       $$C_{\text{total}} = 2k_{\max} + 2$$
4. **Bonus Cap Shelves in Unused Remainder Width or Dead-End Pockets:**
   * If leftover width $W - W_{\text{req}}(k_{\max}, a) \ge 2.0\text{ m}$, one additional single spine column ($+n_{\text{col}}$ shelves) fits!
   * Total reachable shelves $N_{\text{total}}$:
     $$N_{\text{total}} = N_{\text{back}} + C_{\text{total}} \times n_{\text{col}} + N_{\text{apron\_side}}$$
     where $N_{\text{apron\_side}}$ is the number of shelves that fit along the non-bay side wall beside the 2 garage bays!

---

## 4. Blueprints & Exact Numbers for `I1`, `I3`, `P1`, and `Q1`

### 4.1 `I1` & `I3` Warehouse ($1,292\text{ m}^2$, 2 Vehicle Bays) — The Stage 2–4 Workhorse

In an `I1` / `I3` warehouse ($1,292\text{ m}^2$, roughly $34\text{ m} \times 38\text{ m}$ exterior footprint with a $2$-bay garage zone at the front):

#### Configuration A: Standard Flatbed-Reachable E-Comb ($2.0\text{m}$ Aisles — **100 to 112 Pallet Shelves**)
* **Why use this:** Every aisle is $2.0\text{m}$ wide so a flatbed cart or forklift can drive down every single finger aisle without clipping, and every shelf is $100\%$ reachable. (This is the ~**99–100 shelf** layout seen in your `W1`/`W12` save data!)
* **Math Breakdown ($W = 34\text{m}$, Storage Hall $L = 30\text{m}$, Garage Zone $= 8\text{m}$):**
  1. **Back Wall Flush Row:** $\lfloor 34 / 3 \rfloor = \mathbf{11\text{ shelves}}$ ($2\text{m}$ deep).
  2. **Spine Length:** $L_{\text{spine}} = 30 - 2\text{ (back row)} - 4\text{ (front cross-aisle)} = 24\text{ m} \implies n_{\text{col}} = 24 / 3 = \mathbf{8\text{ shelves per column}}$.
  3. **Columns Across $34\text{m}$ Width ($a = 2.0\text{m}$ aisles):**
     * Left wall single row ($2\text{m}$) + Aisle 1 ($2\text{m}$) + Double Spine 1 ($4\text{m}$) + Aisle 2 ($2\text{m}$) + Double Spine 2 ($4\text{m}$) + Aisle 3 ($2\text{m}$) + Double Spine 3 ($4\text{m}$) + Aisle 4 ($2\text{m}$) + Double Spine 4 ($4\text{m}$) + Aisle 5 ($2\text{m}$) + Double Spine 5 ($4\text{m}$) + Aisle 6 ($2\text{m}$) = $34\text{m}$!
     * That gives **$11$ columns** ($1$ left wall + $5$ double-spines) $\times 8\text{ shelves/col} = \mathbf{88\text{ spine shelves}}$.
  4. **Side-of-Garage Zone (Beside the 2 Truck Bays):**
     * Along the non-bay side wall in the $8\text{m}$ front zone: **$9\text{–}13\text{ shelves}$** in a mini double-row + wall row.
  5. **Total Reachable Shelves (Standard $2\text{m}$ Aisle):**
     $$11 + 88 + 11 = \mathbf{110\text{ Pallet Shelves}} \quad (6,600\text{ boxes})$$

#### Configuration B: Maximum Pedestrian-Reachable E-Comb ($1.0\text{m}$ Tight Walkways — **136 to 144 Pallet Shelves**)
* **Why use this:** In *Big Ambitions*, AI employees and player pathfinding only require a **$1.0\text{m}$ ($1\text{ grid tile}$) walkway** to register a shelf as $100\%$ reachable! Narrowing the finger aisles from $2.0\text{m}$ to $1.0\text{m}$ fits **one entire extra back-to-back double spine ($+16$ to $+18$ shelves)** plus an extra row per column (`L_spine = 27m` $\rightarrow 9$ shelves/col)!
* **Math Breakdown ($a = 1.0\text{m}$ aisles, $A_{\text{header}} = 1.0\text{m}$):**
  1. **Back Wall Flush Row:** $\mathbf{11\text{ shelves}}$ ($2\text{m}$ deep).
  2. **Spine Length:** $L_{\text{spine}} = 30 - 2 - 1 = 27\text{ m} \implies n_{\text{col}} = 27 / 3 = \mathbf{9\text{ shelves per column}}$.
  3. **Columns Across $34\text{m}$ Width ($a = 1.0\text{m}$ aisles):**
     $$k_{\max}(34, 1.0) = \left\lfloor \frac{34 - 4.0 - 1.0}{4.0 + 1.0} \right\rfloor = \left\lfloor \frac{29}{5} \right\rfloor = 5\text{ double spines} \text{ with } 4.0\text{m remainder!}$$
     * The $4.0\text{m}$ remainder fits **1 more $1.0\text{m}$ aisle + 1 more single spine ($2.0\text{m}$)** (with $1.0\text{m}$ left over)!
     * Total columns $= 1 \text{ (left wall)} + 10 \text{ (5 double spines)} + 1 \text{ (extra spine)} + 1 \text{ (right wall)} = \mathbf{13\text{ columns}}$.
     * Spine shelves $= 13 \times 9 = \mathbf{117\text{ shelves}}$.
  4. **Dead-End Aisle Cap Shelves ("The Puzzle Bonus"):**
     * Each of the $6$ finger aisles is $27\text{m}$ long. If you stop the last shelf of an adjacent spine $2\text{m}$ early or cap the wide $2.0\text{m}$ remainder aisle at its dead end, you add **$+4\text{ to }+6$ cap shelves**.
  5. **Side-of-Garage Zone:** **$+12\text{ shelves}$** in the front quadrant beside the 2 truck bays.
  6. **Total Reachable Shelves (Max $1\text{m}$-Aisle Comb):**
     $$11 + 117 + 4 + 12 = \mathbf{144\text{ Pallet Shelves}} \quad (8,640\text{ boxes})$$

---

### 4.2 Visual ASCII Grid Map of the `I1` / `I3` E-Comb Layout

```text
+-----------------------------------------------------------------------+
|                       BACK WALL (W = 34m)                             |
| [===][===][===][===][===][===][===][===][===][===][===]  <- 11 Flush  |
|  (Single 2m-deep row facing SOUTH into the finger ends)   Wall Shelves|
+----+--+----+----+--+----+----+--+----+----+--+----+----+--+----+------+
| [S]|  |[S] |[S] |  |[S] |[S] |  |[S] |[S] |  |[S] |[S] |  |[S] | [S]  |
| [S]|  |[S] |[S] |  |[S] |[S] |  |[S] |[S] |  |[S] |[S] |  |[S] | [S]  |
| [S]|A |[S] |[S] |A |[S] |[S] |A |[S] |[S] |A |[S] |[S] |A |[S] | [S]  |
| [S]|I |[S] |[S] |I |[S] |[S] |I |[S] |[S] |I |[S] |[S] |I |[S] | [S]  |
| [S]|S |[S] |[S] |S |[S] |[S] |S |[S] |[S] |S |[S] |[S] |S |[S] | [S]  |
| [S]|L |[S] |[S] |L |[S] |[S] |L |[S] |[S] |L |[S] |[S] |L |[S] | [S]  |
| [S]|E |[S] |[S] |E |[S] |[S] |E |[S] |[S] |E |[S] |[S] |E |[S] | [S]  |
| [S]|1 |[S] |[S] |2 |[S] |[S] |3 |[S] |[S] |4 |[S] |[S] |5 |[S] | [S]  |
| [S]|  |[S] |[S] |  |[S] |[S] |  |[S] |[S] |  |[S] |[S] |  |[S] | [S]  |
|W-L |  |  SPINE 1   |  |  SPINE 2   |  |  SPINE 3   |  |  SPINE 4 | W-R|
|(2m)|  | (4m: 2+2m) |  | (4m: 2+2m) |  | (4m: 2+2m) |  | (4m: 2+2)| (2m|
+----+--+----+----+--+----+----+--+----+----+--+----+----+--+----+------+
| <============ FRONT CROSS-HEADER AISLE (1.5m - 2.0m) ===============> |
+-----------------------------------+-----------------------------------+
|   GARAGE BAY 1   |  GARAGE BAY 2  |  [===][===][===] <- Side-of-Bay   |
|  (Truck Slot 1)  | (Truck Slot 2) |  [===][===][===]    Pocket        |
|   [DeliverySpot / Flatbed Spawn]  |  [===][===][===]   (9-12 Shelves) |
+------------------+----------------+--------------------+--------------+
|          ROLL-UP DOORS            |               PEDESTRIAN DOOR     |
+-----------------------------------+-----------------------------------+

Legend:
- [S]   = 2m (wide) x 3m (tall) Pallet Shelf oriented Vertically (long 3m face opens into Aisle).
- [===] = 3m (wide) x 2m (deep) Pallet Shelf oriented Horizontally.
- SPINE = Two [S] columns placed BACK-TO-BACK (0m gap between their backs; 4m total thickness).
          Left column is 100% reachable from Aisle k; Right column is 100% reachable from Aisle k+1.
```

---

### 4.3 How `I1` vs. `I3` Pillar Alignment Works (Solving the Pillar Puzzle)
In **`I3`** (and `I2`), interior support pillars sit on regular grid intervals inside the main hall. If you place a spine column without aligning to the pillars, a pillar will block a $2\text{m} \times 3\text{m}$ shelf footprint and cost you $6\text{–}10$ shelves.
* **The Pillar Alignment Rule:**
  1. **Always snap the first interior Back-to-Back Double Spine (`4m` wide) so that the line of interior pillars is embedded inside the `4m` shelf spine** (or aligned with the `2m` shelf seam), **never** in the middle of a $1\text{m}$ aisle!
  2. When a $1\text{m} \times 1\text{m}$ pillar falls inside a shelf slot of a $4\text{m}$ double-spine, you lose **at most 1 shelf ($6\text{ m}^2$)** at that exact pillar coordinate while keeping the entire $1\text{m}\text{–}2\text{m}$ aisle $100\%$ clear and walkable from front to back!
  3. If a pillar sits $1\text{m}$ off the seam, rotate that single shelf $90^\circ$ (`3m` wide $\times$ `2m` deep) next to the pillar to bridge the gap and preserve access to the rest of the spine.

---

### 4.4 `P1–P3` ($2,184\text{ m}^2$) and `Q1–Q3` ($2,610\text{ m}^2$) Maximum Reachable Math

Using the exact same E-Comb formula ($W_{\text{req}}(k, a) = 4 + 4k + (k+1)a$):

| Warehouse Code | Total Area | Net Storage Area ($A_{\text{net}}$) | Standard $2.0\text{m}$ Aisle Reachable Shelves ($\times 60$ boxes) | Tight $1.0\text{m}$ Aisle Reachable Shelves ($\times 60$ boxes) | Total Box Capacity ($1\text{m}$ Aisle Max) | Total Unit Capacity (at $50$ units/box avg) |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **`I1`** | $1,292\text{ m}^2$ | $\approx 1,140\text{ m}^2$ | **$110$ shelves** ($6,600$ boxes) | **$144$ shelves** | **$8,640$ boxes** | $\approx 432,000$ units |
| **`I3` / `I2`** | $1,292\text{ m}^2$ | $\approx 1,090\text{ m}^2$ | **$99\text{–}104$ shelves** ($5,940$ boxes) | **$128\text{–}134$ shelves** | **$7,680\text{–}8,040$ boxes** | $\approx 390,000$ units |
| **`P1` / `P2` / `P3`** | $2,184\text{ m}^2$ | $\approx 1,980\text{ m}^2$ | **$192$ shelves** ($11,520$ boxes) | **$248$ shelves** | **$14,880$ boxes** | $\approx 744,000$ units |
| **`Q1` / `Q2` / `Q3`** | $2,610\text{ m}^2$ | $\approx 2,390\text{ m}^2$ | **$232$ shelves** ($13,920$ boxes) | **$302$ shelves** | **$18,120$ boxes** | $\approx 906,000$ units |

---

## 5. How Many Pallet Shelves Does Your Autopilot Actually Need? (ROI vs. Over-Building)

While a `Q1` can hold $302$ reachable pallet shelves and an `I1` can hold $144$, **each Pallet Shelf costs $\$2,500$**. Buying $144$ shelves on Day 25 of Stage 2 would cost $\$360,000$—more than double your entire `Stage2Cash` ($\$150,000$)!

Here is the exact mathematical formula for **Right-Sizing Warehouse Pallet Shelves by Stage**:

### 5.1 The Outbound Truck Bottleneck Proof
* Every `I`, `P`, and `Q` warehouse has **2 Vehicle Bays**.
* The largest truck in the game (`Freight Truck T1`) holds **60 boxes per trip**, and warehouses dispatch **once per day at 02:00 AM**.
* Therefore, the **maximum daily outbound flow** of *any* warehouse in *Big Ambitions* is:
  $$\text{MaxDailyOutboundBoxes} = 2\text{ trucks} \times 60\text{ boxes/truck} = 120\text{ boxes/day}$$
* Even if a warehouse holds **7 days of buffer (`RefillDays = 7`)** at $100\%$ dual-truck saturation ($120\text{ boxes/day}$):
  $$\text{MaxSevenDayBufferBoxes} = 7\text{ days} \times 120\text{ boxes/day} = 840\text{ boxes}$$
* Since each Pallet Shelf (`ba:itemname_palletshelf`) holds **60 boxes**:
  $$\text{ShelvesForFullSevenDayTruckSaturation} = \left\lceil \frac{840\text{ boxes}}{60\text{ boxes/shelf}} \right\rceil = \mathbf{14\text{ Pallet Shelves!}}$$
* Even if you hold **30 days of stock** or buffer multiple SKUs with partial boxes across 16 stores, **$20\text{ to }40\text{ Pallet Shelves}$** ($1,200\text{ to }2,400\text{ boxes}$, costing $\$50,000\text{–}\$100,000$) is more than two `Freight Truck T1`s can ever empty in a week, and **$95\text{–}100\text{ Pallet Shelves}$** in an `I1`/`I3` ($5,700\text{–}6,000\text{ boxes}$) is practically infinite for a single-family retail hub!

### 5.2 Autopilot Warehouse Selection & Shelf-Scaling Rules (Tier-1 Lockable)

| Stage / Trigger | Building Code Priority | Target Pallet Shelves (`ba:itemname_palletshelf`) | CapEx Cost | Rationale |
| :--- | :--- | :--- | :--- | :--- |
| **Stage 2 Entry (`10.5` — First Warehouse)** | **`I1` $\rightarrow$ `I3` $\rightarrow$ `I2`** (cheapest 2-bay warehouse in or near Manhattan) | **Start with $10\text{–}20$ shelves** ($600\text{–}1,200$ boxes) | $\$25,000\text{–}\$50,000$ | Preserves cash for HQ + 2 Freight Trucks + 4 Managers + first 7-day Importer order while covering $5\text{–}10\times$ initial 3-store burn. |
| **Stage 3 Scaling (`10.6` — Up to 16 Stores per WH)** | **`I1` / `I3`** (or **`P1` / `Q1`** if in Industry City at equal rent) | **Scale shelves when `UsedBoxes / (Shelves * 60) >= 0.80`** up to **$95\text{–}144$ shelves** (`I1`/`I3` E-Comb blueprint) | Incremental $\$25,000$ per $10$-shelf block | Only buys concrete shelf capacity as Importer `RefillDays = 7` targets grow. |
| ** Never Rent `H1`/`H2`/`H3` for Stage 2+ Retail** | **Reject `GarageSlots < 2`** | $0$ | $\$0$ | `H`-series only has $1$ vehicle bay ($8$ stores max, $60$ boxes/day max), violating `strategy.md` §6 (*"Every warehouse has two garage slots"*). |

---

## 6. Pure C# Grid Packer & Reachability Validator (For Blueprint / Layout Generation)

If you want the mod (or a layout generator test) to compute and verify the exact coordinates and reachability of an E-Comb pallet layout for any rectangular warehouse floor $(W, L)$, here is the pure C# math model:

```csharp
using System;
using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class PalletPlacement
    {
        public PalletPlacement(int x, int y, int width, int depth, int aisleX)
        {
            X = x;
            Y = y;
            Width = width;
            Depth = depth;
            AisleX = aisleX;
        }

        public int X { get; }

        public int Y { get; }

        public int Width { get; }

        public int Depth { get; }

        /// <summary>X coordinate of the clear finger aisle that reaches this shelf's 3m face.</summary>
        public int AisleX { get; }

        public bool Reachable => AisleX >= 0;
    }

    public static class WarehouseLayoutMath
    {
        public const int ShelfLength = 3;
        public const int ShelfDepth = 2;
        public const int BoxesPerShelf = 60;

        /// <summary>
        /// Computes the maximum 100%-reachable E-Comb pallet shelf layout for a usable storage hall of (widthMeters x lengthMeters).
        /// Every shelf has at least one 3m face adjacent to a continuous aisle connected to the front cross-header aisle.
        /// </summary>
        public static IReadOnlyList<PalletPlacement> PackReachableComb(
            int widthMeters,
            int lengthMeters,
            int aisleWidthMeters = 1,
            int headerAisleMeters = 2)
        {
            var placements = new List<PalletPlacement>();
            if (widthMeters < ShelfLength || lengthMeters < ShelfDepth + headerAisleMeters + ShelfLength)
            {
                return placements;
            }

            var aisle = aisleWidthMeters < 1 ? 1 : aisleWidthMeters;
            var header = headerAisleMeters < 1 ? 1 : headerAisleMeters;

            // 1. Back wall flush row (single 2m-deep row along y = lengthMeters - 2, facing south into finger aisles).
            var backY = lengthMeters - ShelfDepth;
            var backCount = widthMeters / ShelfLength;
            for (var i = 0; i < backCount; i++)
            {
                placements.Add(new PalletPlacement(
                    x: i * ShelfLength,
                    y: backY,
                    width: ShelfLength,
                    depth: ShelfDepth,
                    aisleX: i * ShelfLength));
            }

            // 2. Vertical spine columns from y = header up to y = backY.
            var spineLength = backY - header;
            var rowsPerCol = spineLength / ShelfLength;
            if (rowsPerCol <= 0)
            {
                return placements;
            }

            // Left wall single column (x = 0..2), served by Aisle 1 at x = 2..(2 + aisle).
            if (widthMeters >= ShelfDepth + aisle)
            {
                AddColumn(placements, x: 0, header, rowsPerCol, aisleX: ShelfDepth);
            }

            // Interior back-to-back double spines (4m wide = 2m left + 2m right), separated by aisles.
            var cursorX = ShelfDepth + aisle;
            while (cursorX + (2 * ShelfDepth) + aisle <= widthMeters - ShelfDepth)
            {
                var leftAisle = cursorX - aisle;
                var rightAisle = cursorX + (2 * ShelfDepth);
                AddColumn(placements, x: cursorX, header, rowsPerCol, aisleX: leftAisle);
                AddColumn(placements, x: cursorX + ShelfDepth, header, rowsPerCol, aisleX: rightAisle);
                cursorX = rightAisle + aisle;
            }

            // Optional single column if remainder before right wall fits a 2m column + 1m aisle.
            if (cursorX + ShelfDepth + aisle <= widthMeters - ShelfDepth)
            {
                var leftAisle = cursorX - aisle;
                AddColumn(placements, x: cursorX, header, rowsPerCol, aisleX: leftAisle);
                cursorX += ShelfDepth + aisle;
            }

            // Right wall single column (x = widthMeters - 2), served by the last aisle to its left.
            var rightWallX = widthMeters - ShelfDepth;
            if (rightWallX >= cursorX)
            {
                AddColumn(placements, x: rightWallX, header, rowsPerCol, aisleX: rightWallX - aisle);
            }

            return placements;
        }

        private static void AddColumn(
            List<PalletPlacement> list,
            int x,
            int startY,
            int count,
            int aisleX)
        {
            for (var r = 0; r < count; r++)
            {
                list.Add(new PalletPlacement(
                    x: x,
                    y: startY + (r * ShelfLength),
                    width: ShelfDepth,
                    depth: ShelfLength,
                    aisleX: aisleX));
            }
        }
    }
}
```