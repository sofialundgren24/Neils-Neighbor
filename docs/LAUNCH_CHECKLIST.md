# Neil's Neighbor – checklista till lansering

Bocka av här allteftersom. **Vem:** C = Claude (kod), S = Sofia, B = båda.
Status: [x] klart · [~] påbörjat · [ ] kvar

## 1. Spelets kärna
- [x] C Spelloop: renovera, bygga, odla, affärer, kvartersbyggnader, beställningar, nivåer, områden
- [x] C Nivå 0-tutorial (ogräs, vattning) + nivå 1–5 med 10 upplåsningar vardera
- [x] C Grannar och uppdrag, daglig gåva, fasader, förråd + sälj, charm
- [x] C Villkorsskärm, karaktärsval Neil/Sofia
- [x] C Effekter: studs, pop, partiklar, flygande belöningar, färgvåg, vind, rörliga delar
- [x] C "Välkommen tillbaka"-ruta med det som blev klart medan spelaren var borta
- [ ] C Nivå 6–10 (innehåll + uppdrag), när 1–5 är speltestade
- [ ] C Grannarnas önskemål om att måla/fixa (t.ex. "måla staketet") på Grannskapstavlan
- [ ] B Balansering: speltesta att nivå 1–5 tar lagom lång tid (mål: ~1 vecka)

## 2. Grafik och känsla
- [ ] S Alla modeller i `content.csv` (Nomad → Blender → `Art/Incoming`)
- [ ] S Animationer: grannar (Idle, Walk, Wave, Happy), dörrar, djur
- [ ] C Koppla in animationsklippen (grannar vinkar, jublar när uppdrag klaras)
- [ ] B Slutlig UI-stil i `NG.uss` (färger, typsnitt, ikoner, panelbakgrunder)
- [ ] S Ikoner för alla varor (17+) och valutor
- [ ] S Porträtt för grannarna och Neil/Sofia
- [ ] C Ljus/shader-finjustering när riktiga modeller finns
- [ ] C Dag/kväll-belysning (lyktorna tänds)

## 3. Ljud (väntar)
- [ ] C Ljudsystem (kopplas till samma händelser som effekterna)
- [ ] S Musik (2–3 lugna spår) och ljudeffekter (licensierade eller beställda)

## 4. Plattformar
- [x] C Byggmeny: NG → Build → Android / iOS / WebGL (inställningar sätts automatiskt)
- [x] C Mobil: safe area (notch), Androids bakåtknapp, grafiknivåer för svaga telefoner
- [x] C Webb: sparning i webbläsaren, komprimerad build
- [ ] S Installera **Android Build Support** och **iOS Build Support** i Unity Hub
- [ ] S Bestäm app-id (t.ex. `se.<dittföretag>.neilsneighbor`), ändras i `NG → Build`
- [ ] S iOS: Mac eller molnbygge (Unity Build Automation / Codemagic) för att bygga Xcode-projektet
- [ ] C Appikon och startskärm (när grafiken finns)
- [ ] B Testa på riktig Android-telefon, iPhone och i webbläsare
- [ ] C Steam (PC) när mobilen är klar

## 5. Konto, moln och data
- [x] C Inställningar: grafiknivå, villkor, integritetspolicy, radera min data, version
- [~] C Analys-händelser förberedda (tutorialsteg, nivå upp, köp) – loggas lokalt nu
- [ ] S Skapa projekt i **Unity Cloud** (gratis) och länka det i Unity
- [ ] C Inloggning: Unity Authentication (anonym → Apple/Google)
- [ ] C Molnsparning (Cloud Save) så samma spel finns på telefon och webb
- [ ] C Koppla analys till Unity Analytics
- [ ] C Kraschrapporter
- [ ] C Servertid (anti-fusk med klockan)
- [ ] C Remote config (ändra priser/tider utan ny version)

## 6. Notiser
- [ ] C Lokala notiser: "Dina tulpaner är klara", "Dagens gåva väntar" (paketet Mobile Notifications)

## 7. Köp i spelet
- [ ] S Bestäm vad som säljs (diamantpaket, startpaket, kosmetika) och priser
- [ ] C IAP 5: butik, köp, återställ köp (iOS-krav), kvittoverifiering
- [ ] S Skapa produkterna i App Store Connect och Google Play Console

## 8. Juridik och konton (Sofia)
- [ ] S Företag (enskild firma eller AB)
- [ ] S Apple Developer Program (99 USD/år, D-U-N-S för företag)
- [ ] S Google Play-konto (25 USD) – slutet test med 12 testare i 14 dagar krävs
- [ ] S Villkor och integritetspolicy som riktiga webbsidor (URL:erna sätts i `GameConfig`)
- [ ] S Åldersklassning (IARC) och dataformulär i butikerna
- [ ] S Kolla att namnet "Neil's Neighbor" är ledigt (varumärke, domän)

## 9. Kvalitet
- [ ] C Automatiska tester för spellogiken (XP, renovering, beställningar, sparning)
- [ ] C Prestandatest på svag Android (mål 30 fps), minne, laddtid
- [ ] C Svenska + engelska (alla texter via översättningstabell)
- [ ] C Tillgänglighet: textstorlek
- [ ] B Speltest med 5–10 personer, justera tutorialen

## 10. Lansering
- [ ] S Butikssidor: ikon, skärmbilder, trailer, texter (sv/en)
- [ ] B Mjuklansering i 1–2 länder, mät D1/D7
- [ ] B Global lansering
- [ ] B Plan för uppdateringar: nya grannar, säsongsevent, nivå 6+
