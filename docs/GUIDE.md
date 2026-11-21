# Neil's Neighbor – så hänger allt ihop

Den här guiden förklarar hur spelet är uppbyggt, hur du själv ändrar och skapar nivåer, och hur du gör
objekten (hus, blommor, dekor) i samma stil som Hay Day och Clash of Clans.

---

## 1. De tre lagren

Spelet består av tre lager som bara pratar med varandra åt ett håll:

```
  INNEHÅLL (du ändrar)            LOGIK (reglerna)                 VISNING (det man ser)
  Assets/_Game/Data/*.csv   →     Assets/_Game/Scripts/Game/  →    Assets/_Game/Scripts/World/
  (Excel-filer)                   "Har spelaren råd? Starta         + Scripts/UI/
                                   timer, ge XP ..."                 "Visa huset, spela färgvågen,
                                                                     rita knappen"
```

- **Innehåll** = *vad* som finns: hus, blommor, priser, tider, nivåer, uppdrag. Bara text i kalkylark.
- **Logik** = *reglerna*: vad som händer när du trycker på något. Ren kod, inga bilder.
- **Visning** = *hur det ser ut*: 3D-modeller, effekter, menyer.

Fördelen: du kan byta alla modeller utan att röra reglerna, och ändra alla priser och nivåer utan att röra koden.

### Så startar spelet
1. `GameBootstrap` väljer grafiknivå efter telefonens styrka.
2. Allt innehåll laddas (det som importerats från CSV-filerna).
3. Sparfilen laddas, eller så skapas en ny spelare med den grå startgatan.
4. Världen (`WorldView`), touch (`WorldInteraction`) och menyerna (`GameUI`) kopplas in.
5. Fyra gånger i sekunden kontrolleras alla timers. Var 30:e sekund sparas spelet.

### Så går en handling till (exempel: renovera ett hus)
1. Du trycker på *Renovera* i menyn (`GameUI`).
2. Menyn frågar logiken: `RenovationService.StartNext(hus)`.
3. Logiken kontrollerar nivå och mynt, drar mynten och startar en timer.
4. Logiken skickar meddelandet "huset har ändrats".
5. Visningen hör meddelandet och visar byggnadsställningen.
6. När timern är klar går huset till nästa fas. Efter sista fasen spelas färgvågen, och trädgården runt huset får färg för gott.

---

## 2. Var allt ligger

```
Assets/_Game/
  Data/                     ← INNEHÅLLET. Öppna i Excel / Numbers / Google Sheets.
    content.csv               allt som kan stå på kartan (en rad = en modell)
    levels.csv                XP per nivå och belöningar
    recipes.csv               recept i Blomsteraffären och Kvartersaffären
    resources.csv             varor (tulpaner, buketter, must ...)
    facades.csv               husens fasader per nivå
    neighbors.csv             grannarna
    quests.csv                uppdragen och tutorialen
    start_layout.csv          den grå startgatan (genereras av ett skript)
    expansions.csv            områden på kartan som låses upp

  Scripts/Game/             ← REGLERNA
    GameSession.cs            navet som håller ihop allt
    WorldService.cs           bygga, flytta, sälja, ogräs, charm, områden
    RenovationService.cs      husens faser, fasader, gatan i färg
    GrowthService.cs          växter som växer och skördas
    ProductionService.cs      affärernas recept och köer
    CommunityService.cs       lekparken och skolan (ger mynt med jämna mellanrum)
    InventoryService.cs       förrådet och försäljning
    OrderService.cs           Grannskapstavlan
    ProgressionService.cs     XP och nivåer
    WalletService.cs          mynt och diamanter
    Retention/QuestService.cs uppdrag och tutorial
    Retention/DailyRewardService.cs  dagens gåva
    GameBootstrap.cs          startar spelet och sparar
    DeviceTier.cs             grafiknivå efter telefonens styrka

  Scripts/World/            ← DET MAN SER I VÄRLDEN
    WorldView.cs              skapar och uppdaterar alla objekt i scenen
    WorldInteraction.cs       tryck, dra, byggläge, rutnätet
    PlaceableView.cs          husvarianter, grått → färg, studs
    Juice.cs                  partiklar (damm, vatten, glitter, konfetti)
    ProceduralMotion.cs       gungor som gungar, träd som vajar

  Scripts/UI/GameUI.cs      ← MENYERNA (utseendet styrs av UI/NG.uss)
  Scripts/State/            ← SPARFILEN (vad spelaren har)
  Scripts/Editor/           ← NG-menyerna i Unity (import, byggen, platshållare)

  Art/Incoming/             ← HIT lägger du nya modeller (SM_*.fbx + T_*.png)
```

---

## 3. Ändra och skapa nivåer själv

Allt görs i CSV-filerna. Ingen kod behövs.

### Arbetsgången (varje gång)
1. Öppna CSV-filen i Excel/Numbers.
2. Ändra eller lägg till rader. Spara som **CSV (kommaavgränsad), UTF-8**.
3. Gå till Unity och kör **NG → Content → Import All**.
4. Tryck **Play**. Använd **Meny → Utvecklarverktyg** för att ge dig själv XP, mynt och varor, eller spola fram tiden.

Rader som börjar med `#` är kommentarer. Text med kommatecken måste stå inom `"citattecken"`.

### Exempel 1: Ändra hur mycket XP en nivå kräver
I `levels.csv`, kolumnen `xp_to_next`: hur mycket XP som behövs från den nivån till nästa.
`coin_reward` och `gem_reward` är belöningen man får när man **når** nivån.

```
level,xp_to_next,coin_reward,gem_reward,unlock_ids
0,50,0,0,          ← tutorialen: 50 XP till nivå 1
1,100,80,2,        ← från nivå 1 till 2 krävs 100 XP
```

### Exempel 2: Lägga till ett nytt objekt på nivå 6
Lägg en rad i `content.csv`:

```
id,type,name,unlock,coins,gems,build_sec,xp,beauty,w,h,height,color,shop,params
decor_hammock,decor,Hängmatta,6,180,0,0,8,9,4,2,1.2,#E8A87C,1,
```

| Kolumn | Betyder |
|---|---|
| `id` | unikt namn, bara små bokstäver och `_`. Ändra aldrig efter release (sparfiler använder det). |
| `type` | `house`, `production` (affär), `community` (lekpark/skola), `flower`, `tree`, `bush`, `fence`, `decor`, `ground`, `water`, `clutter` |
| `name` | det som visas i spelet |
| `unlock` | nivån då den går att köpa |
| `coins` / `gems` | pris |
| `build_sec` | byggtid i sekunder (0 = direkt) |
| `xp` | XP när den placeras/byggs klart |
| `beauty` | charm-poäng |
| `w` / `h` | storlek i rutor (1 ruta = 0,5 m). 4 x 2 = 2 x 1 meter. |
| `height` | höjd på platshållaren (används tills din modell finns) |
| `color` | färg på platshållaren |
| `shop` | 1 = syns i byggmenyn |
| `params` | extra, beroende på typ (se nedan) |

**params per typ:**
- Blommor, träd, buskar: `grow=120;yield=tulip:2` (växer 120 s, ger 2 tulpaner). Utan `yield` är den bara dekor.
- Affärer: `queue=3;max=1` (3 platser i kön, man får bygga 1).
- Kvartersbyggnader: `income=25/6/240` (25 mynt och 6 XP var 240:e sekund).
- Hus: `phases=60/180/420;phase_coins=120/240/400;phase_xp=20/35/60` (tid, pris och XP för de tre faserna).
- Skräp: `action=clear` (ogräs) eller `action=water` (torrt gräs), `xp=2`.

Kör importen. En platshållare skapas direkt, och objektet syns i butiken på nivå 6.

### Exempel 3: Ny vara och nytt recept
`resources.csv`:
```
lemonade,Lemonad,6,30,#FFF27A
```
`recipes.csv` (receptets id börjar med `make_`):
```
make_lemonade,Lemonad,shop_general,6,apple:2/currant:1,lemonade:1,150,8
```
= Kvartersaffären kan på nivå 6 göra 1 lemonad av 2 äpplen och 1 vinbär på 150 sekunder, och det ger 8 XP.

### Exempel 4: Nytt uppdrag
`quests.csv`:
```
id,neighbor,order,unlock,requires,title,intro,outro,objectives,coins,gems,xp,focus
lemonade_stand,omar,60,6,omar_house,Lemonadstånd,"Varmt idag, {namn}! Gör tre lemonader?","Perfekt!",produce:lemonade:3,120,0,30,shop_general
```

| Kolumn | Betyder |
|---|---|
| `neighbor` | vem som ger uppdraget (id i `neighbors.csv`) |
| `order` | sortering, lägre kommer först |
| `unlock` | tidigaste nivå |
| `requires` | uppdrag som måste vara klart först (tomt = inget) |
| `intro` / `outro` | vad grannen säger. `{namn}` blir Neil eller Sofia. |
| `objectives` | mål, flera separerade med `\|` |
| `focus` | vad den gula "Här!"-pekaren pekar på: ett id, eller `ui:build` / `ui:orders` / `ui:storage` |

**Måltyper:**
`clear:clutter_weed:6` rensa · `renovate:house_classic:3` renovera klart · `own:shop_flower:1` ha byggt ·
`harvest:tulip:5` skörda · `produce:lemonade:3` tillverka · `deliver:2` leverera beställningar ·
`level:7` nå nivå · `collect:apple:10` ha i förrådet

### Exempel 5: Ny fasad till ett hus
`facades.csv`:
```
house1_mint,house_cottage,Mintgrön panel,6,#A8E6CF,100
```
= På nivå 6 kan Lilla villan målas mintgrön för 100 mynt.

### Exempel 6: Ett nytt område på kartan
`expansions.csv`: rutor x, y, bredd, höjd på kartan (96 x 96 rutor), nivå och pris.
Gamla spelare får nya områden direkt, eftersom områden räknas från nivån.

### Exempel 7: Ändra startgatan
Antingen redigera `start_layout.csv` direkt (id, x, y, rotation, grått), eller ändra skriptet
`Tools/content/generate_start_layout.py` och kör `python Tools/content/generate_start_layout.py`.
`decayed`: 0 = färg, 1 = grått tills närmaste hus är renoverat, 2 = grått tills alla fem hus är klara.
Startgatan påverkar bara **nya** sparfiler (Meny → Utvecklarverktyg → Ny sparfil).

### Tumregler för balans
- Nivå 1–5 ska ta ungefär en vecka med några korta pass per dag.
- Varje nivå ska ge något nytt att bygga och ett nytt mål (uppdrag).
- Husrenoveringarna är den stora XP-källan. Gör dem dyrare och längre för varje hus.
- Korta timers (under 3 minuter) är gratis att snabba på. Längre kostar diamanter.

---

## 4. Hur gör man objekten som i Hay Day och Clash of Clans?

### Hur de spelen gör
Spel som Hay Day och Clash of Clans är oftast byggda så här: objekten **modelleras i 3D**, och sedan
**renderas de till 2D-bilder** från en fast vinkel, ofta målade över för hand. I spelet är de alltså platta
bilder, men de ser 3D ut eftersom de är gjorda i 3D. Ja, det du beskriver stämmer: skulptera i 3D och ta 2D-bilder.

Det finns två sätt att göra det, och vårt spel klarar det första redan nu:

| | **A. Riktig 3D i spelet** (det vi har nu) | **B. 3D → 2D-bilder** (Hay Days metod) |
|---|---|---|
| Hur | Modellen används direkt i spelet | Modellen renderas till bilder i Blender |
| Looken | Lika fin om texturen är välmålad och ljuset bakat | Lättast att få exakt "målad" look |
| Polygoner | Begränsat (hus max ~6 000 trianglar) | Obegränsat, skulptera hur detaljerat som helst |
| Animation | Rörliga delar och riggar fungerar direkt | Varje rörelse blir en serie bilder |
| Rotera objekt | Fungerar | Kräver en bild per riktning |
| Färgvåg grått → färg | Fungerar | Fungerar (shader på bilderna) |
| Kod som krävs | Klar | Ett renderskript i Blender + sprite-stöd i spelet (jag kan bygga det) |

**Rekommendation:** börja med **A**. Den passar ihop med allt som redan är byggt, och dina animationer
fungerar direkt. Är du inte nöjd med looken efter de första två, tre husen kan vi byta till **B**. Då
behöver du inte göra om modellerna, eftersom samma Blender-filer renderas till bilder.

### Så får man "Hay Day-känslan" i 3D (metod A)
Looken kommer från formerna och målningen, inte från koden:

1. **Knubbiga proportioner.** Större tak, tjockare väggar, överdrivna fönster och dörrar. Inget ska vara tunt eller vasst.
2. **Rundade kanter överallt.** Använd *Bevel* i Blender (2–5 cm). Ljuset fångar kanterna och gör dem mjuka och "leksaksaktiga".
3. **Få, mättade färger.** 3–5 huvudfärger per objekt. Varmt ljus, kalla skuggor.
4. **Måla in ljuset i texturen.** Ljusare upptill, mörkare nertill och i skrymslen. Baka *Ambient Occlusion* i Blender och lägg den i färgtexturen.
5. **Tydlig siluett.** Objektet ska kännas igen även som en svart skugga.
6. **Detaljer som syns uppifrån.** Takpannor, plankor och fönsterluckor. Små detaljer på sidorna syns inte.

### Steg för steg (metod A)
1. **Nomad Sculpt:** skulptera formen fritt och detaljerat.
2. **Blender:**
   - Importera, förenkla (*Decimate* eller retopo) till budgeten: hus 6 000, träd 1 500, dekor 800, blommor 200 trianglar.
   - UV-packa och måla texturen (eller baka färg + AO från den detaljerade skulpturen till den förenklade).
   - Pivot mitt på fotavtrycket, nere vid marken. Framsidan mot −Y.
   - Varianter som egna objekt: hus `Phase0_Decayed` … `Phase3_Decorated` + `Scaffold`, växter `Stage0_Sprout` / `Stage1_Growing` / `Stage2_Bloom`.
   - Rörliga delar med prefix: `Swing_`, `Spin_`, `Bob_`, `Sway_`.
3. **Exportera** `SM_<Id>.fbx` + `T_<Id>.png` till `Assets/_Game/Art/Incoming/` (id i TitleCase, t.ex. `house_classic` → `SM_House_Classic.fbx`).
4. **Unity** byter platshållaren mot din modell automatiskt.

Alla detaljer (exportinställningar, budgetar, animationer) står i [ASSET_GUIDE.md](ASSET_GUIDE.md).

### Bra ordning att göra modellerna i
1. **Lilla villan** (alla 4 faser). Den syns först och avgör om stilen håller.
2. Startsortimentet: staket, buskar, träd, ogräs, torrt gräs, gata, trottoar.
3. Nivå 1: Blomsteraffären, tulpaner, solros, bänk, cafébord.
4. Sedan en nivå i taget.

Varje rad i `content.csv` är en modell, och spelet fungerar hela tiden med platshållare för det som saknas.

---

## 5. Diskutrymme

Unity behöver plats för att kompilera och bygga. Räkna med minst **10 GB ledigt**, gärna 20 GB när du ska bygga till telefon.

Om du inte får fram mer plats:
- Stäng Unity och ta bort mappen `Library` i projektet (ungefär 2 GB). Unity bygger upp den igen nästa gång, men det tar en stund och kräver lite plats under tiden.
- Ta bort `Temp` och `Logs` i projektet.
- Flytta stora filer (videor, bilder, gamla spel) till en extern disk eller molnet.
- Windows: *Inställningar → System → Lagring → Tillfälliga filer*.
- Unity Hub: ta bort editorversioner och moduler du inte använder.
