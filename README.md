# Neighborhood Garden – fas 0

Cozy grannskaps-renoveringsspel i Unity 6 (URP). Design och roadmap: se GDD-dokumentet
("Neighborhood Garden – GDD & teknisk roadmap").

Fas 0 ska bevisa två saker innan vi bygger mer innehåll:

1. **Stilen håller** – mättade färger, mjukt färgat ljus och grå→färg-transformationen, i 30 fps på en mellanklass-Android.
2. **Pipelinen håller** – ett eget Blender-objekt ska vara spelbart i Unity på under 5 minuter, utan kod.

## Kom igång

1. Installera **Unity 6 LTS** (6000.0 eller senare) via Unity Hub, med modulerna *Android Build Support*, *iOS Build Support* (Mac) och *Windows/Mac Build Support*.
2. Unity Hub → **Add → Add project from disk** → välj den här mappen.
3. Första öppningen tar några minuter (paketen i `Packages/manifest.json` laddas ner). Om Unity frågar om att aktivera det nya Input System – svara **Yes** (editorn startar om). Om Unity föreslår nyare paketversioner för din editor: acceptera.
4. Kör menyn **NG → Setup → Phase 0 (Everything)**. Den
   - skapar och aktiverar URP-assetet (`Assets/_Game/Settings/`),
   - skapar materialen `M_Stylized_Palette`, `M_Foliage`, `M_Ground`, `M_Path`,
   - bearbetar allt i `Art/Incoming` till prefabs + definitioner + Addressables,
   - bygger scenen `Assets/_Game/Scenes/Phase0_StyleTest.unity`.
5. Kör **NG → Balance → Import levels.csv** för att skapa `LevelTable` (nivå 1–20).
6. Tryck **Play**. Använd knapparna *Renovera* / *Byt målarfärg* / *Återställ*. Panorera med drag/WASD, zooma med scroll/nyp.

## Lägga till ett eget 3D-objekt (målet: < 5 min)

1. Modellera i Blender enligt specen i GDD:n (avsnitt *3D-asset-pipeline*). Kortversion:
   - 1 enhet = 1 m, pivot nere i mitten av fotavtrycket på z = 0, fotavtryck i multiplar av 0,5 m.
   - UV mot `Assets/_Game/Art/Textures/T_Palette_256.png` – skala varje ytas UV till en punkt i rätt färgruta.
   - Vertexfärg (attribut `Color`): **R** = AO (1 = ingen skuggning), **G** = vind (0 vid roten), **B** = målarmask.
   - Ett material per objekt. Avfasa synliga kanter (0,02–0,05 m).
   - Varianter som egna objekt på toppnivå: `Phase0_Decayed` … `Phase3_Decorated` för hus, `Stage0_Sprout`, `Stage1_Growing`, `Stage2_Bloom` för växter.
2. Döp filen `SM_<Kategori>_<Namn>.fbx` (kategorier: House, Flower/Plant, Tree, Hedge/Bush, Fence, Water/Pool, Path, allt annat blir Decor).
3. Exportera FBX: *Selected Objects*, *Apply Scalings: FBX All*, *Forward −Z*, *Up Y*, *Vertex Colors: Linear*, ingen animation.
4. Lägg filen i `Assets/_Game/Art/Incoming/`. Importeraren flyttar den till `Art/Models/<kategori>/`, skapar `PF_…`-prefab och `DEF_…`-definition och varnar i konsolen om budget, pivot eller material avviker.
5. Fyll i kostnad, byggtid, nivåkrav och skönhetspoäng i `DEF_…`-assetet. Klart.

Ny version av en befintlig modell: lägg den i `Incoming` med samma namn. Prefab och referenser behålls, och speldata i `DEF_…` skrivs aldrig över.

## Verktyg utanför Unity

| Skript | Vad det gör |
| --- | --- |
| `python Tools/palette/generate_palette.py` | Bygger `T_Palette_256.png` från `Tools/palette/palette.json`. Lägg alltid nya färger **sist**. |
| `blender -b --factory-startup -P Tools/blender/make_test_assets.py` | Bygger testhuset, tre blommor och ett träd och exporterar FBX till `Incoming` och `.blend` till `ArtSource/`. Lägg till `-- --preview` för en förhandsbild. |
| `python Tools/balance/generate_levels.py 20` | Skriver `Assets/_Game/Data/levels.csv` med XP-kurvan `round(50·L^1.55 + 25·L)`. |

`ArtSource/` innehåller redigerbara `.blend`-filer. Den ligger utanför `Assets/` med flit, så att Unity inte importerar dem.

## Kodstruktur

```
Assets/_Game/Scripts/
  Core/      EventBus, GameEvents, TimeService (serverjusterad UTC, snabbspolningspris)
  Data/      Definitioner (ScriptableObjects), LevelTable + XpFormula, ContentDatabase (Addressables)
  State/     PlayerState (ren data), SaveService (JSON, atomär skrivning, schemamigrering)
  World/     PlotGrid (0,5 m-rutnät), PlaceableView (decay/målning/varianter), StyleTestDemo
  Camera/    IsoCameraController (touch + mus/tangentbord)
  Editor/    NGAssetImporter, Phase0Setup, BalanceImporter, NGEditorPaths
Assets/_Game/Art/Shaders/NG_Stylized.shader   URP-shader: palett, ramp-ljus, färgad skugga, rim, vind, decay
```

## Kvar i fas 0

- [ ] Öppna i Unity 6, kör setup och rätta eventuella kompileringsfel (koden är skriven men inte kompilerad i Unity än).
- [ ] Bedöm stilen i stiltestscenen och justera shaderns ramp-, skugg- och rim-värden i `M_Stylized_Palette`.
- [ ] Mät FPS på en mellanklass-Android (grind: 30 fps).
- [ ] Mät tiden för ett eget asset från Blender till spelbart (grind: under 5 min).
- [ ] Skapa separata URP-assets för Mobile Low/High (skuggupplösning, MSAA, SSAO).
