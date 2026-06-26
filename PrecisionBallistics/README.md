# Precision Ballistics

ექსპერტ-დონის ბალისტიკური კალკულატორი Windows-ისთვის (C# / WPF, .NET 8).
სრულად **ორიგინალური** კოდი — აგებულია public-domain ბალისტიკურ ფიზიკაზე
(G1/G7 დრაგ-მოდელები, point-mass ინტეგრაცია, Litz spin drift, Coriolis).
არ შეიცავს არცერთი მესამე მხარის აპლიკაციის კოდს, რესურსს ან მონაცემთა ბაზას.

---

## მოთხოვნები

- **Windows 10/11** (WPF-ის გამო მხოლოდ Windows)
- **.NET 8 SDK** — https://dotnet.microsoft.com/download/dotnet/8.0
- სურვილისამებრ: **Visual Studio 2022** (17.8+) `.NET desktop development` workload-ით

---

## აწყობა და გაშვება

### ვარიანტი A — ბრძანების სტრიქონი (ყველაზე მარტივი)

```powershell
cd PrecisionBallistics
dotnet build -c Release
dotnet run --project src/PrecisionBallistics.App
```

გასაშვები ფაილი აიწყობა აქ:
`src/PrecisionBallistics.App/bin/Release/net8.0-windows/PrecisionBallistics.exe`

### ვარიანტი B — Visual Studio

1. გახსენი `PrecisionBallistics.sln`
2. დააყენე `PrecisionBallistics.App` როგორც startup project
3. დააჭირე **F5** (Debug) ან **Ctrl+F5** (Release)

### სურვილისამებრ — ერთ-ფაილიანი თვითშემცველი .exe

```powershell
dotnet publish src/PrecisionBallistics.App -c Release -r win-x64 ^
  --self-contained true -p:PublishSingleFile=true
```

შედეგი: `...\bin\Release\net8.0-windows\win-x64\publish\PrecisionBallistics.exe`
(არ საჭიროებს .NET-ის ცალკე დაყენებას სამიზნე მანქანაზე).

---

## პროექტის სტრუქტურა

```
PrecisionBallistics/
├─ PrecisionBallistics.sln
└─ src/
   ├─ PrecisionBallistics.Core/      ← ბალისტიკის ძრავა (გარე dependency-ების გარეშე)
   │  ├─ Units.cs                     ← ერთეულების კონვერსია (SI შიგნით)
   │  ├─ DragTables.cs                ← G1/G7 standard drag ცხრილები
   │  ├─ Atmosphere.cs                ← ჰაერის სიმკვრივე, ბგერის სიჩქარე, density altitude
   │  ├─ Hardware.cs                  ← Cartridge / Rifle / Scope მოდელები
   │  ├─ Stability.cs                 ← Miller-ის გიროსკოპული სტაბილურობა (SG)
   │  ├─ ShotConditions.cs            ← ქარი, კუთხე, გეომეტრია, ჩართული ეფექტები
   │  ├─ TrajectorySolution.cs        ← შედეგის ტიპები
   │  ├─ BallisticSolver.cs           ← RK4 point-mass solver (ბირთვი)
   │  ├─ Truing.cs                    ← MV / BC კალიბრაცია რეალურ DOPE-ზე
   │  ├─ Profile.cs                   ← სრული პროფილი
   │  └─ ProfileStore.cs              ← JSON შენახვა/ჩატვირთვა + საწყისი ბიბლიოთეკა
   └─ PrecisionBallistics.App/        ← WPF ინტერფეისი
      ├─ App.xaml(.cs)                ← მუქი თემა + სტილები
      ├─ MainWindow.xaml(.cs)         ← მთავარი ფანჯარა
      ├─ MainViewModel.cs             ← ლოგიკა, ერთეულები, ბრძანებები
      ├─ RelayCommand.cs              ← MVVM infrastructure
      └─ ReticleView.cs              ← ორიგინალური MIL-ბადის holdover ვიზუალი
```

---

## ფუნქციონალი

- **G1, G7 და Custom (CDM)** დრაგ-მოდელები — Mach-ით ინტერპოლირებული standard drag
  ცხრილებიდან, ან მომხმარებლის Doppler-მრუდი (Mach,Cd)
- **3-DOF point-mass solver** RK4 ინტეგრაციით (ნაბიჯი 0.5 ms)
- ატმოსფერო: ტემპერატურა, წნევა, ტენიანობა, სიმაღლე → **ჰაერის სიმკვრივე** და **density altitude**
- **ქარის** სრული დეკომპოზიცია (head/tail + cross), **wind bracket** (lo–hi ჰოლდი)
- **Spin drift** (Litz), **aerodynamic jump**, **Coriolis** (ჰორიზონტ. + ვერტიკ.)
- **Miller SG** — სტაბილურობის შეფასება (stable / marginal / unstable)
- **MIL / MOA** + მეტრული/იმპერიული ერთეულები, **ქარი m/s ან mph**
- დისტანციაზე: elevation, windage, drop, სიჩქარე, ენერგია, Mach, ToF
- **transonic** დიაპაზონის გამოვლენა (სად ეცემა ბურთულა Mach 1-ს ქვემოთ)
- **პროფილების** სისტემა — JSON ბიბლიოთეკა + ცალკეული პროფილის import/export
- **truing** — MV კალიბრაცია ერთი ან რამდენიმე დაკვირვებული ვარდნით (best-fit)
- ორიგინალური **holdover რეტიკლი** — კურსორის წაკითხვა და კლიკით უახლოეს მანძილზე ჰოლდი

---

## ვალიდაცია

ძრავა გადამოწმდა ცნობილ რეფერენს-მონაცემთან:
**6.5 Creedmoor, 140gr ELD-M, G7 BC 0.315, MV 2710 fps, sight height 2",
zero 100 yd, 15°C / 29.92 inHg / 50% RH:**

| დისტ. | drop | elevation | სიჩქარე | ToF |
|------:|------:|----------:|--------:|------:|
| 500 yd | −51.7 in | 2.87 MIL | 2031 fps | 0.640 s |
| 1000 yd | −320.6 in | 8.90 MIL | 1452 fps | 1.513 s |

Miller SG = 1.79 · spin drift @1000yd = 8.0 in (0.22 MIL) — ემთხვევა
გამოქვეყნებულ Hornady/JBM მონაცემებს.

---

## ერთეულების შესახებ

- შიგა გამოთვლა — **SI** (მეტრი, m/s, კგ); გამოსავალი — შერჩეული ერთეულებით.
- BC, diameter, weight, twist, sight height, muzzle velocity ველები ბალისტიკაში
  ტრადიციულად **იმპერიულია** (lb/in², ინჩი, grain, fps) — ეს ველები ყოველთვის
  იმპერიულ ერთეულებშია, მეტრულ რეჟიმშიც.
- რიცხვები იყენებენ **წერტილს** ათწილადის გამყოფად (აპლიკაცია იძულებით
  InvariantCulture-ზეა, რომ რეგიონული პარამეტრებისგან დამოუკიდებლად მუშაობდეს).

## დაშვებები (გასათვალისწინებელი)

- look-angle კორექცია — "improved rifleman's rule" აპროქსიმაცია.
- aerodynamic jump — Litz-ის გამარტივებული ფორმულა; მაგნიტუდა მცირეა.
- spin drift — ემპირიული Litz ფორმულა SG-ზე დაყრდნობით.
- ეს არის გარე-ბალისტიკის საინჟინრო მოდელი; რეალურ პირობებში ყოველთვის
  დაადასტურე შენი DOPE სროლით და გამოიყენე truing.

---

## დამატებითი ფუნქციები

- **MV ერთეული fps ↔ m/s** — ზედა ზოლის „MV m/s" გადამრთველი (BC/diameter/weight ისევ
  იმპერიულია, მხოლოდ მუნდულის სიჩქარე იცვლება).
- **MV truing ვარდნით** — ღილაკი „True MV". შეიყვანე გასწორების მანძილი (zero),
  სროლის მანძილი და ჯგუფის ვარდნა დამიზნების წერტილიდან (cm/in), და აპი უკუ-ითვლის
  მუნდულის სიჩქარეს მიმდინარე პროფილის BC/ატმოსფეროზე დაყრდნობით. „Apply to load"
  ჩასვამს დათვლილ მნიშვნელობას ვაზნაში.
- **Holdover tree** რეტიკლი — სქროლი = ზუმი, გადათრევა = pan, მარჯვენა ღილაკი = reset;
  ბადე MIL/MOA და m/yd გადამრთველებს ითვალისწინებს. კურსორი აჩვენებს ცოცხალ
  MIL/MOA წაკითხვას, კლიკი → უახლოესი დისტანციის ჰოლდი.

### ახალი ხელსაწყოები (ზედა ზოლი)

- **True MV** — MV truing ერთი ან რამდენიმე დაკვირვებული ვარდნით (best-fit least-squares).
- **Range card** — შენი დისტანციების სია (მაგ. 300, 450, 600, 800) → კომპაქტური ჰოლდ-ბარათი,
  CSV ექსპორტი და ბეჭდვა (DOPE ბარათი ლულაზე დასაკრავად).
- **Compare** — ორი ლოუდის გვერდიგვერდ შედარება (elevation/velocity + Δ).
- **Graph** — ტრაექტორიის გრაფიკი: elevation (მარცხ. ღერძი) და სიჩქარე (მარჯვ. ღერძი)
  მანძილზე, transonic ნიშნულით.
- **Drag curve** — Custom Doppler მრუდის (Mach,Cd) ჩასმა ან ფაილიდან იმპორტი; drag model
  ავტომატურად გადადის Custom-ზე (G7-ზე დაბრუნება ერთ ღილაკზე).
- **Export DOPE CSV** — მიმდინარე სრული ცხრილის CSV ექსპორტი.
- **Import/Export profile** — ცალკეული პროფილის გაზიარება ერთ JSON ფაილად.
- **Wind bracket** — WIND სექციაში ჩართე და მიუთითე min/max; ცხრილის Wind სვეტი აჩვენებს
  „lo–hi" ჰოლდის დიაპაზონს ქარის გაურკვევლობისთვის.
- **Environment presets** — ATMOSPHERE-ის თავზე სწრაფი პრესეტები (ICAO, ცივი, ცხელი,
  მაღალმთიანი, ნოტიო).
- **Imperial wind (mph)** — „Metric" გადამრთველთან ერთად ქარის ერთეული m/s ↔ mph.
