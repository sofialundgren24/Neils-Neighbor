# Asset-guide: Nomad → Blender → Unity

Så här gör du en modell som ersätter en platshållare i spelet. Allt som ska modelleras står i
[`Assets/_Game/Data/content.csv`](../Assets/_Game/Data/content.csv), en rad per modell. Spelet fungerar
redan med platshållare, så du kan byta ut dem en i taget i vilken ordning du vill.

## Kort version

1. **Nomad:** skulptera formen.
2. **Blender:** importera, förenkla till mobilbudget, UV-packa, måla texturen, sätt pivot.
3. **Exportera** `SM_<Namn>.fbx` + `T_<Namn>.png` till `Assets/_Game/Art/Incoming/`.
4. **Unity** byter platshållaren mot din modell automatiskt. Tryck Play.

## Namn: det enda som måste stämma

Id i `content.csv` → filnamn i TitleCase:

| id i content.csv | Modell | Textur |
| --- | --- | --- |
| `house_classic` | `SM_House_Classic.fbx` | `T_House_Classic.png` |
| `shop_flower` | `SM_Shop_Flower.fbx` | `T_Shop_Flower.png` |
| `play_swing_set` | `SM_Play_Swing_Set.fbx` | `T_Play_Swing_Set.png` |
| `decor_bench_wood` | `SM_Decor_Bench_Wood.fbx` | `T_Decor_Bench_Wood.png` |

Stämmer namnet hamnar modellen på samma prefab som platshållaren, och allt i spelet (butiken, startgatan,
sparfiler) pekar automatiskt på den nya modellen.

## Varianter (barnobjekt i Blender)

Varje variant är ett eget objekt på toppnivå i Blender-filen, med exakt detta namn:

| Typ | Varianter |
| --- | --- |
| Hus (`house_*`) | `Phase0_Decayed`, `Phase1_Repaired`, `Phase2_Painted`, `Phase3_Decorated`, `Scaffold` |
| Affärer och kvartersbyggnader (`shop_*`, `community_*`, `play_*`) | själva byggnaden + `Scaffold` |
| Blommor, träd, buskar | `Stage0_Sprout`, `Stage1_Growing`, `Stage2_Bloom` |
| Allt annat | inga varianter behövs |

`Scaffold` (byggnadsställning) visas medan något byggs eller renoveras. Spelet visar en variant i taget.

**Tips för husen:** Modellera `Phase3_Decorated` först (den finaste). Duplicera den och ta bort
blomlådor och dekor → `Phase2_Painted`. Gör väggarna enfärgade/oputsade → `Phase1_Repaired`.
Lägg till brädor för fönstren, trasiga takpannor och en sned planka → `Phase0_Decayed`.
Spelet gör dessutom `Phase0` grå automatiskt, så den behöver inte målas grå.

## Steg för steg

### 1. Nomad Sculpt
- Skulptera i högupplösning. Lägg detaljer som syns från ovan: takpannor, fönsterkarmar, plankor.
- Överdriv formerna något (större fönster, tjockare tak). Spelet ses ovanifrån på avstånd, så små detaljer försvinner.
- Exportera som `.glb` (eller `.obj`) till `ArtSource/nomad/`.

### 2. Blender: form och storlek
- **Skala:** 1 enhet = 1 meter. En cell i spelet är 0,5 m. Fotavtrycket (`w` × `h` i content.csv) är i celler,
  så `w=10, h=10` betyder 5 × 5 m. Håll modellen inom det.
- **Pivot:** mitt på fotavtrycket, nere vid marken (z = 0). *Object → Set Origin*, och flytta objektet så att origo hamnar rätt.
- **Riktning:** framsidan (dörren) mot **−Y** i Blender. Kameran tittar snett uppifrån, från samma håll.
- **Förenkla till mobilbudget:** lägg *Decimate*-modifier på Nomad-modellen eller gör en enkel retopo.
  Budget per variant (trianglar):

  | Typ | Max trianglar |
  | --- | --- |
  | Hus, butik | 6 000 |
  | Träd, damm | 1 500 |
  | Dekor | 800 |
  | Buske/häck | 500 |
  | Staket | 300 |
  | Blomma, gröda | 200 |

  Importen varnar i Unitys Console om du går över.

### 3. Blender: färg (textur)
- **UV:** *Smart UV Project* eller egen UV, packad i **en** textur per modell.
- **Måla:** *Texture Paint* direkt på den förenklade modellen, eller måla i högupplösning och baka
  (*Bake → Diffuse/Color*) från Nomad-modellen till den förenklade. Baka gärna in mjuka skuggor (AO) i färgen, det ger Township-känslan.
- **Storlek:** 1024 × 1024 räcker för hus, 512 för dekor, 256 för små växter. Unity begränsar ändå till 1024.
- Alla varianter (Phase0–3, Scaffold) kan dela samma textur. Lägg dem bredvid varandra i UV:n.
- **Ett material** per modell.

### 4. Exportera
- *File → Export → FBX*:
  - *Limit to: Selected Objects* (markera alla varianter)
  - *Apply Scalings: FBX All*, *Forward: −Z Forward*, *Up: Y Up*
  - *Geometry → Vertex Colors: None* (färgen ligger i texturen)
  - Ingen animation
- Spara till `Assets/_Game/Art/Incoming/SM_<Namn>.fbx`.
- Spara texturen till `Assets/_Game/Art/Incoming/T_<Namn>.png`.
- Spara `.blend`-filen i `ArtSource/` (den ignoreras av Unity men sparas i git via LFS).

### 5. Unity
Byt till Unity. Importen sker av sig själv:
- Modellen flyttas till `Art/Models/<kategori>/` och texturen till `Art/Textures/Models/`.
- Prefaben `PF_<Namn>` byggs om med din modell. Platshållaren försvinner.
- Ett material `M_<Namn>` skapas med din textur.
- Fotavtrycket räknas från modellens storlek.

Kontrollera Console efter gula varningar (budget, pivot, material). Tryck Play.

Ny version av samma modell? Lägg den i `Incoming` med samma namn igen. Allt uppdateras, inget går sönder.

## Ändra pris, tider, nivåer

Allt speldata står i CSV-filerna i `Assets/_Game/Data/`. Öppna dem i Excel/Numbers/Google Sheets,
spara som CSV och kör **NG → Content → Import All** i Unity.

| Fil | Innehåll |
| --- | --- |
| `content.csv` | Allt som kan placeras: pris, byggtid, XP, nivå, fotavtryck, växttid, skörd |
| `facades.csv` | Husens fasader/färgvarianter per nivå |
| `neighbors.csv`, `quests.csv` | Grannarna och deras uppdrag (tutorialen är de första) |
| `resources.csv` | Varor: pris och från vilken nivå de dyker upp i beställningar |
| `recipes.csv` | Recept i affären och trädgårdsbutiken |
| `levels.csv` | XP per nivå och belöningar (`python Tools/balance/generate_levels.py 30`) |
| `start_layout.csv` | Den nedgångna startgatan (`python Tools/content/generate_start_layout.py`) |
| `expansions.csv` | Områden som låses upp med nivå + mynt |

Fler nivåer = fler rader i `levels.csv` och högre `unlock` på nytt innehåll. Ingen kod behövs.

## Prestanda: så flyter det på svaga telefoner

Spelet väljer själv grafiknivå vid start (`Scripts/Game/DeviceTier.cs`):

| Nivå | Vilka enheter | Inställningar |
| --- | --- | --- |
| Low | < 3,5 GB RAM eller ≤ 4 kärnor | 85 % upplösning, ingen kantutjämning, enkla skuggor, ingen post-processing, 30 fps |
| Medium | Vanliga telefoner | Full upplösning, 2× kantutjämning, 30 fps |
| High | Nya telefoner, surfplattor, dator | 4× kantutjämning, HDR, bättre skuggor, 60 fps |

Det som spelar störst roll och som du styr som modellerare:
1. **Håll triangelbudgeten.** Trianglar per variant, inte summan. Bara en variant syns åt gången.
2. **Små texturer.** 1024 för hus, mindre för allt annat. Unity komprimerar dem automatiskt (ASTC/ETC2 på mobil).
3. **Ett material per modell.** Varje extra material kostar ett extra ritanrop.
4. **Inga separata småobjekt.** Slå ihop fönster, dörrar och detaljer till samma mesh per variant (*Ctrl+J*).
5. **Inga genomskinliga ytor** om det inte behövs (glas kan vara en ljusblå, ogenomskinlig färg).

Mät på riktigt: Debug-knappen i spelet visar FPS och vilken grafiknivå som valts.
