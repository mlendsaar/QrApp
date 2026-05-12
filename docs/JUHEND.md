# QrApp — Kasutusjuhend

## Sisukord

1. [Mis on QrApp?](#mis-on-qrapp)
2. [Paigaldamine ja käivitamine](#paigaldamine-ja-käivitamine)
3. [Standardkasutus](#standardkasutus)
4. [OCR — tekst pildilt või PDF-ist](#ocr--tekst-pildilt-või-pdf-ist)
5. [Seadete muutmine](#seadete-muutmine)
   - [Hotkey](#hotkey)
   - [QR Code — suurus ja veaparandus](#qr-code--suurus-ja-veaparandus)
   - [Overlay — automaatne sulgemine, kinnitus ja lõikelaua jälgimine](#overlay--automaatne-sulgemine-kinnitus-ja-lõikelaua-jälgimine)
   - [OCR — tekstituvastus ja nupu kuvamine](#ocr--tekstituvastus-ja-nupu-kuvamine)
   - [Startup — automaatkäivitus](#startup--automaatkäivitus)
   - [Symbol Filter — teksti puhastusreeglid](#symbol-filter--teksti-puhastusreeglid)

> Juhend on alati kättesaadav ka rakenduse seest: vajuta overlay ülaribal **`?`** nuppu või seadetes ECC Level kõrval olevat **ⓘ** nuppu.

---

## Mis on QrApp?

QrApp on väike taustarakendus, mis muudab kopeeritud teksti QR-koodiks. Rakendus istub süsteemisalves ega sega tööd — ta ärkab alles siis, kui sa seda kutsud.

**Tüüpilised kasutusolukorrad:**

- Jaga veebilehe aadressi telefoni
- Saada sõnum (aadress, telefoninumber, parool) teise seadmesse skaneerimisega
- Kopeeri tekst arvutiekraanilt telefoni ilma trükkimata

---

## Paigaldamine ja käivitamine

1. Kopeeri `QrApp.exe` ükskõik millisesse kausta
2. Käivita — süsteemisalve ilmub QR-koodi ikoon
3. Rakendus on valmis kasutamiseks

**Automaatkäivitus:** vaikimisi lisab QrApp end Windowsi käivitusnimekirja, nii et ta on alati saadaval pärast arvuti sisselülitamist. Selle saab välja lülitada seadetes.

---

## Standardkasutus

### Samm 1 — Kopeeri tekst

Tõsta esile suvaline tekst mis tahes rakenduses ja vajuta **`Ctrl+C`**.

> Töötab brauseris, Notepadis, Word'is, PDF-vaaturis — kõikjal, kus saab teksti kopeerida.

### Samm 2 — Vajuta hotkey'd

Vajuta **`Ctrl+Shift+F2`** (vaikimisi kombinatsioon).

QrApp loeb lõikelaualt teksti, puhastab selle ja genereerib QR-koodi.

### Samm 3 — Skanneeri

Ekraanile ilmub aken kahe osaga, mis avaneb selle ekraani keskel, kus hetkel asub hiirekursor:

```
┌─────────────────────────────────────────────────────┐
│  [⬡ OCR]  (peidetud)        [📌] [👁] [?] [✕]      │  ← lohistatav riba
├──────────────────────────┬──────────────────────────┤
│  Kopeeritud tekst        │                          │
│  (muudetav)              │      QR-kood             │
│                          │                          │
│  142 / 1 663 baiti       │                          │
└──────────────────────────┴──────────────────────────┘
```

- **Vasak pool** — tekst, mida QR-kood sisaldab; saad seda käsitsi muuta
- **Parem pool** — QR-kood; skanneeri telefoni kaameraga
- QR uuendub automaatselt iga kord, kui teksti muudad
- **Ülemine riba on lohistatav** — saad akna ekraanil ringi tõsta enne, kui kasutad OCR-i
- **`📌` nupp** — kinnitab akna (vt. *Kinnita aken*); kui sisse lülitatud, ei sulgu aken hiireklikist väljapool
- **`👁` nupp** — lülitab sisse lõikelaua jälgimise (vt. *Lõikelaua jälgimine*); QR-kood värskendub iga kord, kui kopeerid uue teksti
- **`?` nupp** — avab selle juhendi
- **`✕` nupp** — peidab akna (sama mis `Esc`)

### Kinnita aken (📌)

Vaikimisi sulgub overlay automaatselt, kui klikid mujale (näiteks teisele aknale). See on mugav kiireks skannimiseks, aga segab, kui tahad QR-koodi võrrelda mõne muu materjaliga.

- Vajuta **`📌`** nuppu päises → aken jääb avatuks ka siis, kui klikid mujale
- Vajuta uuesti → tavakäitumine taastub
- Vaikeväärtuse saad seada **Seaded → Overlay → Pin overlay**

`Esc`, `✕` ja hotkey uuesti vajutamine sulgevad akna ka kinnitatud režiimis.

### Lõikelaua jälgimine (👁)

Kui see on sisse lülitatud, jälgib QrApp pidevalt lõikelauda ja genereerib uue QR-koodi iga kord, kui kopeerid uue teksti — hotkey vajutamine ei ole vajalik.

- Vajuta **`👁`** nuppu päises → jälgimine algab; kopeeri (`Ctrl+C`) suvaline tekst → QR-kood uueneb kohe
- Vajuta uuesti → jälgimine peatub
- Vaikeväärtuse saad seada **Seaded → Overlay → Watch clipboard**

> Soovitus: kombineeri `📌` (kinnita aken) ja `👁` (lõikelaua jälgimine) — saad QR-genereerimise „peo režiimi", kus iga uus `Ctrl+C` ilmutab uue QR-koodi ja aken jääb püsivalt nähtavale.

### Akna sulgemine

- Vajuta **`Esc`**
- Kliki akna väljaspool
- Vajuta **`✕`** nuppu
- Vajuta **hotkey'd uuesti** — eelmine aken sulgub ja avatakse uus, mis loeb lõikelaualt värske teksti

### Kui teksti on liiga palju

QR-kood mahutab kuni ~1 663 baiti (vaikimisi ECC Q tasemel). Pikema teksti puhul:

- **Kollane hoiatus** — lähened piirangule (80–100%)
- **Punane viga** — tekst ei mahu; lühenda seda vasakul tekstiväljal

---

## OCR — tekst pildilt või PDF-ist

Mõnikord ei saa teksti tavaliselt kopeerida — näiteks ekraanipildil, skaneeritud dokumendis või lukustatud PDF-is. Sel juhul saab kasutada OCR-funktsiooni, mis loeb teksti otse ekraanilt.

**OCR on vaikimisi peidetud.** Selle nähtavaks tegemiseks:

1. Ava seaded: süsteemisalve ikoonil parem-klikk → **Settings**
2. Sektsioonis **OCR** lülita sisse **Show OCR Region button**
3. Vajuta **Apply**

Edaspidi ilmub overlay ülaservas nupp **⬡ OCR Region**.

### OCR kasutamine

1. Vajuta hotkey'd — overlay avab kas tühja teksti või eelmise sisuga
2. Kliki **⬡ OCR Region**
3. Ekraan tumeneb — joonista hiirega ristkülik teksti ümber (nagu Snipping Tool)
4. QrApp loeb valitud piirkonna teksti ja täidab tekstivälja
5. Skanneeri QR-kood

Vajuta **Esc** joonistamise ajal, kui soovid tühistada — overlay taastub eelmise sisuga.

---

## Seadete muutmine

Ava seaded: süsteemisalve ikoonil **parem-klikk → Settings**

---

### Hotkey

**Mis see on:** klahvikombinatsioon, mis käivitab QR-koodi genereerimise.

**Muutmine:**
1. Kliki hotkey väljale — taust muutub sinakaks, tekst ütleb *"Press a key combination…"*
2. Hoia all soovitud modifikaatorklahvid (nt `Ctrl+Alt`) ja vajuta põhiklahvi (nt `F3`)
3. Kombinatsioon kuvatakse koheselt
4. Vajuta **Apply**

**Tagajärjed:**
- Uus hotkey registreeritakse kohe — vana lakkab töötamast
- Kui kombinatsioon on mõne teise rakenduse poolt hõivatud, kuvatakse veateade *"This hotkey is in use by another application"* ja eelmine hotkey jääb kehtima
- Vaikimisi: `Ctrl+Shift+F2` (F-klahvid ei konflikteeri brauserite ega Windowsiga)

> **Soovitus:** kasuta `Ctrl+Shift+F`-klahve (F1–F12) — need on praktiliselt alati vabad.

---

### QR Code — suurus ja veaparandus

#### Size (suurus)

**Mis see on:** genereeritud QR-koodi pildi suurus pikslites.

**Vahemik:** 200–600 px, samm 50 px. Eelvaade uuendub liuguri liigutamisel.

**Tagajärjed:**
- Suurem pilt = selgem, kergem skaneerida kaugelt; overlay aken kasvab koos QR-koodiga
- Väiksem pilt = mahutatavam ekraanile
- Suurus ei mõjuta QR-koodi andmemahtu — see sõltub ainult ECC tasemest ja teksti pikkusest
- Muudatus jõustub järgmise overlay avamise korral (vajuta hotkey'd uuesti)

#### ECC Level (veaparanduse tase)

**Mis see on:** kui palju QR-koodi saab olla kahjustunud ja see on siiski loetav.

| Tase | Taaste | Andmemaht (v40) | Kasutus |
|------|--------|-----------------|---------|
| L | 7% | 2 953 baiti | Puhas digikeskkond, ekraanilt skaneerides |
| M | 15% | 2 331 baiti | Üldine kasutus |
| **Q** | **25%** | **1 663 baiti** | **Vaikimisi — hea tasakaal** |
| H | 30% | 1 273 baiti | Prinditud koodid, võimalik mustus/kulumine |

**Tagajärjed:**
- Madalam tase (L) → rohkem teksti mahub QR-koodi, aga vähem vastupidav kahjustusele
- Kõrgem tase (H) → vastupidavam, aga vähem teksti mahub
- Ekraanilt skaneerides sobib L või Q hästi; prinditud koodile soovita H

---

### Overlay — automaatne sulgemine, kinnitus ja lõikelaua jälgimine

#### Auto-dismiss (automaatne sulgemine)

**Mis see on:** overlay sulgub automaatselt määratud sekundi pärast.

**Muutmine:** lülita linnuke sisse ja sisesta sekundite arv.

**Tagajärjed:**
- `0` sekundit (linnuke väljas) = overlay jääb avatuks kuni käsitsi sulgemiseni
- Kasulik kui skanneerid peavad saama koodi pärast — overlay ei sulgu enne, kui jõuad telefoni võtta

#### Pin overlay (kinnita aken)

**Mis see on:** kui sisse lülitatud, ei sulgu overlay enam automaatselt, kui klikid akna kõrvale.

**Vaikimisi:** välja lülitatud.

**Tagajärjed:**
- Sisse lülitatud → akna saab kõrval pidada ka teisi rakendusi kasutades. `Esc`, `✕` ja hotkey uuesti vajutamine sulgevad akna ikkagi.
- Välja lülitatud → tavakäitumine — overlay peitub iga klikiga väljapoole

> Sama saab vahetada lendult overlay päises oleva **📌** nupu kaudu.

#### Watch clipboard (lõikelaua jälgimine)

**Mis see on:** kui sisse lülitatud, jälgib QrApp avatud overlayl pidevalt lõikelauda ja genereerib uue QR-koodi iga kord, kui kopeerid uue teksti — hotkey vajutamine ei ole vajalik.

**Vaikimisi:** välja lülitatud.

**Tagajärjed:**
- Sisse lülitatud → iga `Ctrl+C` (kui sisu erineb praegusest) uuendab overlayl näidatavat teksti ja QR-koodi (kontroll iga ~500 ms)
- Välja lülitatud → tavakäitumine — uus tekst loetakse ainult hotkey vajutamisel
- Jälgimine töötab ainult senikaua, kui overlay on avatud

> Sama saab vahetada lendult overlay päises oleva **👁** nupu kaudu.

---

### OCR — tekstituvastus ja nupu kuvamine

Need seaded mõjutavad OCR-funktsiooni (vt. peatükk *OCR — tekst pildilt või PDF-ist*).

#### Show OCR Region button

**Mis see on:** lülitab sisse/välja OCR-nupu overlay ülaservas.

**Vaikimisi:** välja lülitatud (nupp on peidetud).

**Tagajärjed:**
- Sisse lülitatud → overlay ülaservas on näha nupp **⬡ OCR Region**
- Välja lülitatud → nupp on peidetud, overlay on puhtam

#### Upscale region before recognition (suurenda piirkonda enne tuvastust)

**Mis see on:** enne OCR-i tegemist suurendab QrApp valitud piirkonna pilti kuni 3× (bikuubilise interpolatsiooniga, max 4800 px küljel).

**Vaikimisi:** sisse lülitatud.

**Tagajärjed:**
- Sisse → väikese kirjaga tekst tuvastatakse oluliselt paremini; OCR võtab veidi kauem
- Välja → kasulik, kui piirkond on juba suur ja terav (säästab pisut aega)

#### Preserve line breaks in result (säilita reavahetused)

**Mis see on:** määrab, kuidas tuvastatud read ühendatakse.

**Vaikimisi:** sisse lülitatud.

**Tagajärjed:**
- Sisse → read eraldatakse reavahetusega (`\n`). Sobib loendite, koodi ja tabelite jaoks.
- Välja → read ühendatakse ühe tühikuga. Sobib lihtsale jooksvale tekstile, mis on pildis murtud mitmele reale.

---

### Startup — automaatkäivitus

**Mis see on:** QrApp käivitub automaatselt koos Windowsiga.

**Vaikimisi:** sisse lülitatud.

**Tagajärjed:**
- Sisse lülitatud → registrivõti `HKCU\...\Run\QrApp` kirjutatakse, rakendus käivitub sisselogimisel
- Välja lülitatud → registrivõti kustutatakse, rakendust tuleb käsitsi käivitada

---

### Symbol Filter — teksti puhastusreeglid

**Mis see on:** reeglid, mida rakendatakse automaatselt igale kopeeritud tekstile enne QR-koodi genereerimist.

**Vaikimisi reeglid** eemaldavad:
- BOM-märgi (`U+FEFF`) — sageli tekib UTF-8 failidest
- Null-laiusega tühikuid (`U+200B`) ja pehmeid sidekriipse (`U+00AD`) — nähtamatud märgid veebilehekülgedelt
- Asendab mitte-katkestava tühiku (`U+00A0`) tavalise tühikuga
- Normaliseerib reavahetused (`\r\n` → `\n`)
- Eemaldab ridade lõpus olevad tühikud (regex `\s+$`)

Need märgid ei ole tekstis nähtavad, kuid suurendavad QR-koodi mahtu asjatult.

**Reegli muutmine:**

| Veerg | Selgitus |
|-------|----------|
| Match | Tekst või muster, mida otsitakse |
| Replace | Millega asendatakse (tühi = kustutamine) |
| Regex | Linnuke lubab regulaaravaldiste kasutuse |

**Tagajärjed:**
- Reeglid rakendatakse järjest (ülalt alla)
- Vale regex-muster võib põhjustada vea — vigane reegel jäetakse vahele
- Reeglite kustutamine ei mõjuta juba genereeritud QR-koode

**Näited:**

- Kustuta kõik numbritevahelised tühikud: Match `(\d) (\d)`, Replace `$1$2`, Regex ✓
- Asenda http:// https://-ga: Match `http://`, Replace `https://`

---

### Apply ja Cancel

| Nupp | Toiming |
|------|---------|
| **Apply** | Salvestab kõik muudatused kohe (`settings.json`), registreerib uue hotkey, rakendab autostart muudatuse |
| **Cancel** | Tühistab kõik muudatused, aken sulgub, eelmised seaded jäävad kehtima |

> Seaded salvestatakse faili `%APPDATA%\QrApp\settings.json`. Kui see fail rikutakse, lähtestab rakendus seaded automaatselt vaikeväärtustele.
