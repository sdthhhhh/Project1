# Duality — Codex Handoff

Working title: **Duality** (folder may still say BlindGame).  
Unity project root: `Project1/`  
Main gameplay scene: `Assets/Scenes/SampleScene.unity`

This doc explains systems written/wired in recent sessions so another agent can continue without re-deriving intent from code alone.

---

## 1. Scene flow (Build Settings order)

| Index | Scene | Role |
|------:|-------|------|
| 0 | `StartScene` | Main menu: Start / Settings / Credits |
| 1 | `IntroScene` | Opening narration CSV → loads SampleScene |
| 2 | `SampleScene` | Full game house |
| 3 | `3dTest` | Dev / test (ignore for shipping) |
| 4 | `EndScene` | Ending narration + killer choice → back to StartScene |

Flow:

```
StartScene --[START]--> IntroScene --[last line]--> SampleScene
SampleScene --[knife 2nd-level zoom close]--> EndScene
EndScene --[pre lines → choose LinFang/SuYu → post lines]--> StartScene
```

### Key scene scripts

| Scene | Controller | Notes |
|-------|------------|-------|
| StartScene | `StartMenuController` | Start → `IntroScene`. Settings: volume + mouse sensitivity via `GameSettings` / PlayerPrefs |
| IntroScene | `IntroNarrationController` | CSV dialogue; `nextSceneName = SampleScene` |
| EndScene | `EndingNarrationController` | Pre CSV → A/B choice → Post CSV; `nextSceneName = StartScene` |
| SampleScene | many interaction systems | See below |

Editor rebuild helpers (optional):

- `Assets/Editor/SetupStartScene.cs` — menu `BlindGame/Setup StartScene Menu`
- `Assets/Editor/SetupEndingNarration.cs` — menu `BlindGame/Setup EndScene Ending Narration`

---

## 2. Two different “diary” systems (do not mix)

There are **two** four-piece systems that look similar but are unrelated:

### A) Puzzle pieces (physical cover fragments)

- World objects: `INTERACTABLES/DiaryFragments/DiaryFragment01–04`
- Mesh sources: `Assets/3D/Diary/piece1–4.fbx`
- Script: `DiaryPuzzlePieceWorld` — **E to collect only**, no inspect UI
- Used later in diary **cover puzzle** (`DiaryInspectPuzzleController`)

### B) Text diary pages (readable Chinese-named pages)

- World objects named like `日记碎片1` … `日记碎片4`
- Component: `InspectableObject` with `ContentKind = TextPage`
- These are the **story pages** revealed by restores / puzzles

### Reveal wiring (important — was mixed up before)

| Trigger | Activates |
|---------|-----------|
| Beer restore complete | `日记碎片1` (TextPage) |
| Photo restore complete | `日记碎片2` (TextPage) |
| Item restoration complete | `日记碎片3` (TextPage) |
| Diary cover puzzle complete | `日记碎片4` (TextPage) |
| Shovel hotspot zoom close | `ENVIRONMENT/Livingroom/footprint` |

Code touchpoints:

- `BeerRestoreController.SetRevealOnRestoreActive`
- `PhotoRestoreController.SetRevealOnRestoreActive`
- `ItemRestorationSystem` reveal on complete
- `DiaryInspectPuzzleController.revealOnComplete`
- `InspectableHotspot.revealOnZoomClose` + `ApplyRevealOnZoomClose()`

**Rule:** Never point beer/photo/item reveals at `DiaryFragment01–04` puzzle meshes. That used to deactivate puzzle pieces at start.

---

## 3. Inspect system (core loop)

### Components

| Script | Role |
|--------|------|
| `InspectableObject` | World LMB inspect target. `Model3D` or `TextPage` |
| `InspectableRaycaster` | Player raycast → open UI / doors / restores |
| `InspectableUIController` | Inspect UI: 3D studio preview + rotate, or text page |
| `InspectableHotspot` | **Second layer** inside 3D inspect (magnifier zoom) |
| `InspectZoomController` | Magnifier overlay; opens/closes zoom; calls hotspot close side-effects |
| `RequireActiveToInspect` | Gates `CanInspect` + colliders until another GO is active |

### Dual-layer inspect pattern (like PILL / shovel / flower knife)

1. **Layer 1:** `InspectableObject` on parent (Model3D). Preview clones hierarchy → rotate moves children too.
2. **Layer 2:** child `InspectableHotspot` (often a dedicated empty like `KnifeHotspot` / `PasswordHotspot`).
3. While inspecting, turn object until hotspot faces camera → magnifier → click → zoom text/image.
4. Closing zoom runs `ApplyRevealOnZoomClose()` (activate GOs) and optional `loadSceneOnZoomClose`.

### Hotspot visibility rules (`InspectZoomController`)

- Hotspot `transform.forward` must face the preview camera (`visibleDotThreshold`).
- Occlusion raycasts **must not** treat other preview-clone parts as blockers (fixed: pot no longer blocks buried knife).
- Prefer a **dedicated child** for hotspot pose (position + forward), not the dense mesh root.

### Content kinds

```csharp
enum InspectContentKind { Model3D = 0, TextPage = 1 }
```

UI branches on `ContentKind`, **not** on `GetComponent<DiaryFragment>()`.

---

## 4. Flower pot + knife ending gate (SampleScene)

Hierarchy (approx):

```
ENVIRONMENT/Environment_Misc/PlantSet(1)/defaultMaterial.081   // pot (inspect root)
  └─ couteau
       └─ default                                              // knife mesh
            └─ KnifeHotspot                                    // 2nd-layer inspect
```

### Behaviour

1. Shovel mud clue zoom close → activates `Livingroom/footprint`.
2. `RequireActiveToInspect` on pot watches `footprint`; until then pot cannot be inspected.
3. Inspect pot (rotate includes knife).
4. Magnifier on `KnifeHotspot` → text like “A knife buried…”.
5. **Closing that zoom** loads `EndScene` via `InspectableHotspot.loadSceneOnZoomClose = "EndScene"`.

### Outline notes (knife / pot)

- Comic outline system: `MeshOutlineStyle` (+ `MeshOutlinePlayBuilder` at runtime).
- Dense meshes (>80k tris) skip **sealed** shells; may use shader extrusion / ThinSheet.
- Forced `ThinSheet` should be preferred over dense sealed builds when needed.
- Non-uniform scale + `minWorldOutlineWidth` can make outlines look huge (plant had this issue).
- Photos under `INTERACTABLES/Photoes`: many selected painting models were batch-set to outline **Tone = Red** (not White).

Do **not** casually retouch Photoes materials/outlines unless asked — user previously wanted comic OutlineBody + ThinSheet on photos, not Lit retouches.

---

## 5. Ending (EndScene)

Scripts:

- `EndingNarrationController` — replaces intro controller on EndScene
- `EndingChoice` — static `Selected` = `LinFang` | `SuYu`

CSV (Excel-friendly, same format as intro: `id,speaker,text`):

| Asset | When |
|-------|------|
| `Assets/Data/Ending/EndDialoguePre.csv` | Before choice |
| `Assets/Data/Ending/EndDialoguePost.csv` | After either choice (shared) |
| Optional `postChoiceLinFangCsv` / `postChoiceSuYuCsv` | Branch lines (slots exist, may be empty) |

Choice UI:

- Buttons **A. LinFang** / **B. SuYu**
- Keys: `A`/`1` or `B`/`2`

**Content status:** Pre/Post CSVs are **placeholders**. User will supply real prompts/copy later. Wire new text into those CSVs (and optional branch CSVs).

After final post line → load `StartScene`.

---

## 6. Start menu (StartScene)

- Brand title currently **DUALITY** (not Blind).
- `StartMenuController`: Start / Settings / Credits.
- `GameSettings`: PlayerPrefs volume + mouse sensitivity; applied via `AudioListener` and `FirstPersonLook.Start`.

---

## 7. Outline system cheat sheet

File: `Assets/Scripts/Debug/MeshOutlineStyle.cs`

| Field | Meaning |
|-------|---------|
| `tone` | Black / White / **Red** |
| `silhouetteMode` | Auto / Sealed / ThinSheet |
| `preserveOriginalMaterials` | true = keep Lit/textures, only add shells (photos) |
| `scaleWidthToBounds` | width from mesh size × factor |
| `outlineWidth` | absolute local width when scale-to-bounds off |
| `minWorldOutlineWidth` | floor in world units; `0` means default `0.003` |
| `buildOnAwake` | rebuild runtime shells (DontSave helpers) |

Generated children: `OutlineShell`, `OutlineCreases` (edit-mode often DontSave).  
Inactive-at-start objects need rebuild on enable / reveal enqueue (`MeshOutlineStyle.OnEnable`, hotspot reveal).

Batch tone change pattern (editor): set `MeshOutlineStyle.Tone = OutlineTone.Red` on selection; property setter calls `ApplyColors()`.

---

## 8. Other gameplay systems (exist, less touched recently)

- **Photo restore:** `PhotoRestoreController`
- **Beer restore:** `BeerRestoreController`
- **Misplaced items:** `ItemRestorationSystem`
- **Door codes:** `DoorPasswordLock` / `DoorPasswordUIController`
- **Countdown / eye-cover transition:** `CountdownTimer` + `SceneTransitionManager` (hands overlay; used for timer ending, not knife→EndScene)
- **Tasks UI:** `Assets/Scripts/Tasks/*`
- **Player move/look:** Mini First Person Controller (`FirstPersonLook` reads `GameSettings.MouseSensitivity`)

---

## 9. Conventions / gotchas for Codex

1. **Prefer Unity MCP / editor scripting** for scene wiring; save scenes after changes.
2. **Do not mix** puzzle `DiaryFragment0x` with text `日记碎片x`.
3. Inspect second layer = `InspectableHotspot`, not a second world `InspectableObject` on the child (world IO on child steals raycasts).
4. When gating interactables: `RequireActiveToInspect` or `DeactivateOnInteract.SetInteractionEnabled`.
5. Scene loads need Build Settings names exactly: `StartScene`, `IntroScene`, `SampleScene`, `EndScene`.
6. Avoid drive-by refactors of outline/photo materials; user is picky about comic look.
7. Chinese object names in hierarchy are intentional (`日记碎片*`, etc.).
8. PowerShell in this environment: use `;` not `&&`.
9. Unity MCP `execute_code` **does not support C# local functions** — inline helpers or use `Assets/Editor/*.cs` menu scripts.

---

## 10. Likely next work (user-facing)

- Replace EndScene placeholder dialogue with real copy (pre / choice / post / maybe branches).
- Flesh Credits text; finalize game name if not staying Duality.
- Any remaining outline polish (knife especially) — user may self-tune; don’t thrash `MeshOutlineStyle` unless asked.
- Balance / content for SampleScene clues, passwords, tasks.

---

## 11. Quick file index

```
Assets/Scripts/Interactions/
  InspectableObject.cs
  InspectableHotspot.cs          # revealOnZoomClose, loadSceneOnZoomClose
  InspectZoomController.cs       # magnifier + occlusion fix
  InspectableUIController.cs
  InspectableRaycaster.cs
  RequireActiveToInspect.cs
  DiaryPuzzlePieceWorld.cs
  DiaryInspectPuzzleController.cs
  PhotoRestoreController.cs
  BeerRestoreController.cs
  ItemRestorationSystem.cs
  DeactivateOnInteract.cs

Assets/Scripts/Intro/
  IntroNarrationController.cs
  IntroDialogueCsv.cs
  EndingNarrationController.cs
  EndingChoice.cs

Assets/Scripts/UI/
  StartMenuController.cs
  GameSettings.cs

Assets/Scripts/Debug/
  MeshOutlineStyle.cs
  MeshOutlinePlayBuilder.cs

Assets/Data/Intro/IntroDialogue.csv
Assets/Data/Ending/EndDialoguePre.csv
Assets/Data/Ending/EndDialoguePost.csv

Assets/Scenes/
  StartScene.unity
  IntroScene.unity
  SampleScene.unity
  EndScene.unity
```

---

## 12. Minimal “does it still work?” checklist

1. Play from **StartScene** → Start → Intro clicks → SampleScene.
2. Restore beer/photo/items → correct `日记碎片` TextPages appear (not puzzle meshes).
3. Collect puzzle pieces with E; diary cover puzzle reveals `日记碎片4`.
4. Shovel mud zoom close → footprints appear → pot becomes inspectable.
5. Pot inspect → knife magnifier → close zoom → **EndScene**.
6. End dialogue → choose LinFang or SuYu → post lines → **StartScene**.
