# Neighborhood Garden

Cozy grannskaps-renoveringsspel i Unity 6 (URP). Design och roadmap: se GDD-dokumentet
("Neighborhood Garden – GDD & teknisk roadmap").

**Visionen:** spelaren kommer till en ful, grå och nedgången gata med fem hus. Varje hus renoveras i tre faser
(laga, måla, dekorera). När ett hus är färdigt sprids färgen över huset och trädgården, och den stannar för gott.
Sedan odlar spelaren, förädlar varor i affären och trädgårdsbutiken, levererar beställningar till grannarna,
går upp i nivå och låser upp nya hus, nytt innehåll och nya områden på kartan.

## Kom igång

1. Öppna projektet i Unity 6 (6000.x) via Unity Hub.
2. Kör **NG → Setup → Build Game (Everything)**. Den
   - skapar URP-inställningar i tre nivåer (svag mobil, mobil, dator),
   - importerar riktiga modeller från `Art/Incoming` och `Art/Models`,
   - läser nivåtabellen (`levels.csv`) och allt innehåll (`content.csv` m.fl.),
   - bygger platshållare för allt som saknar riktig modell,
   - bygger spelscenen `Assets/_Game/Scenes/Game.unity`.
3. Öppna `Game.unity` och tryck **Play**.

**Så spelar du nu:** tryck på ett hus → *Renovera*. Tryck *Bygg* → välj t.ex. Vete → dra eller tryck där det ska stå →
*Bekräfta*. Skörda när bubblan säger *Skörda*. Bygg Affären på nivå 2 och gör bröd. Leverera via *Beställningar*.
*Debug* ger mynt, XP och låter dig spola fram tiden.

Panorera med drag/WASD, zooma med scroll/nyp.

## Modeller

Se **[docs/ASSET_GUIDE.md](docs/ASSET_GUIDE.md)**: Nomad → Blender → Unity, namn, varianter, budgetar och prestanda.
Kortversion: döp filen `SM_<Id i TitleCase>.fbx` (+ `T_<…>.png`) och lägg den i `Assets/_Game/Art/Incoming/`.
Den ersätter platshållaren automatiskt.

## Så hänger koden ihop

```
Assets/_Game/
  Data/*.csv                 ALLT speldata (pris, tider, nivåer, startgatan, områden). Redigera här.
  Scripts/
    Data/      Definitioner (ScriptableObjects) som CSV:erna blir: PlaceableDefinition, RenovatableDefinition,
               PlantDefinition, ProductionDefinition, RecipeDefinition, ResourceDefinition, GameConfig, LevelTable
    State/     PlayerState: spelarens sparade data (ren data, inga regler). SaveService: JSON-sparfil.
    Game/      SPELLOGIKEN. GameSession + en tjänst per område:
                 WalletService       mynt och diamanter
                 InventoryService    lagret
                 ProgressionService  XP, nivåer, upplåsningar
                 WorldService        placera, flytta, sälja, bygga, låsa upp områden
                 RenovationService   husens faser och permanent färg
                 GrowthService       växter och skörd
                 ProductionService   affär och trädgårdsbutik
                 OrderService        beställningstavlan
               GameBootstrap (startar allt, sparar), DeviceTier (grafiknivå efter enhet)
    World/     Det man ser: WorldView (skapar/uppdaterar objekt i scenen), WorldInteraction (tryck, byggläge,
               rutnät), PlaceableView (varianter, grå→färg, målarfärg), PlotGrid (rutnätet)
    UI/        GameUI: spelets UI i UI Toolkit. Utseendet styrs av Assets/_Game/UI/NG.uss (färger, rundning,
               storlekar). GameHud: gamla IMGUI-UI:t, avstängt reserv.
    Camera/    IsoCameraController
    Core/      EventBus + GameEvents (logik → vy), TimeService (tid och snabbspolning), InputGate (UI vs värld)
    Editor/    NG-menyerna: ContentImporter (CSV → innehåll), PlaceholderBuilder, NGAssetImporter (modeller),
               GameSceneBuilder, BalanceImporter, Phase0Setup (URP + stiltestscen)
  Art/Shaders/NG_Stylized.shader   URP-shader: ramp-ljus, färgad skugga, kantljus, vind, grå→färg
```

**Flödet för en handling**, t.ex. att renovera:
1. Spelaren trycker på *Renovera* i `GameHud`.
2. `GameHud` anropar `RenovationService.StartNext(hus)`.
3. Tjänsten kontrollerar nivå och pengar, drar mynten, sätter en timer i `PlayerState` och publicerar `ObjectChanged`.
4. `WorldView` hör händelsen och visar byggnadsställningen.
5. När timern är klar (`GameSession.Tick`) går huset till nästa fas. Efter sista fasen: `complete = true`,
   färgvågen spelas och närliggande grå objekt får färg.

Reglerna finns bara i `Scripts/Game`. Vill du ändra *vad* som händer, börja där. Vill du ändra *hur det ser ut*,
börja i `Scripts/World` eller shadern.

## Verktyg utanför Unity

| Skript | Vad det gör |
| --- | --- |
| `python Tools/balance/generate_levels.py 30` | Skriver `levels.csv` med XP-kurvan `round(50·L^1.55 + 25·L)` |
| `python Tools/content/generate_start_layout.py` | Skriver `start_layout.csv` (startgatan) |
| `python Tools/palette/generate_palette.py` | Bygger färgpaletten `T_Palette_256.png` |
| `blender -b --factory-startup -P Tools/blender/make_test_assets.py` | Bygger de gamla testmodellerna (stuga, blommor, träd) |

## Status

- [x] Stil- och pipelinetest (fas 0), scenen `Phase0_StyleTest`
- [x] Spelloop med platshållare: renovering, bygg, odling, produktion, beställningar, nivåer, områden, sparning
- [x] Datastyrt innehåll för 20+ nivåer (64 objekt, 17 varor, 9 recept)
- [x] Grafiknivåer för svaga/starka enheter
- [ ] Riktiga modeller (se `content.csv`, en rad per modell)
- [x] UI i UI Toolkit med stilfil (`NG.uss`), väntar på slutlig grafik
- [x] Grannar, uppdrag, tutorial, daglig gåva
- [ ] Ljud, tutorial, butik/monetisering (IAP 5), molnsparning
