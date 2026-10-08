# SAT Platform — Digital SAT tayyorlov platformasi

Blazor Web App (.NET 10, Interactive Server) + PostgreSQL (EF Core / Npgsql) + Telegram bot.
TZ: `SAT_Preparation_Platform_Full_TZ.md`.

## Nimalar bor

| Modul | Qisqacha |
|---|---|
| Auth | Telefon + parol; Telegram kodi bilan kirish/ro'yxatdan o'tish; botning o'zida ro'yxatdan o'tish; bir martalik kirish havolalari; rollar: SuperAdmin, Admin, Teacher, Assistant, Student, Parent |
| Test engine | Pre-test tekshiruv, modul taymerlari (server authoritative), bitta faol sessiya, avto-saqlash + lokal zaxira, flag, chizib tashlash, savollar jadvali, modul review, adaptiv routing (M1 natijasi → M2 Easy/Hard), tanaffus, vaqt tugaganda avto-yakunlash, kalkulyator, reference sheet, annotation, line reader, klaviatura |
| Scoring | Versiyalangan raw→scaled jadvallar (admin tahrirlaydi), adaptiv qoidalar konfigda, qayta hisoblash audit bilan |
| Practice | Tezkor/standart/intensiv/maxsus/zaif skilllar/xatolar rejimi, vaqtli va vaqtsiz, darhol feedback, takrorlanishni kamaytirish |
| Xatolar banki | Avtomatik to'planadi, skill bo'yicha guruhlanadi, hal qilingan/qilinmagan, xatolar ustida mashq |
| Analitika | Ball dinamikasi (SVG chart), skill heatmap, zaiflik detektori, tavsiya dvigateli, vaqt analitikasi, streak, maqsadga progress |
| Savollar banki | Rich text (Quill) + LaTeX (KaTeX) + rasm + jadval, MC/SPR, versiyalash (eski urinishlar buzilmaydi), holatlar, dublikat tekshiruvi, JSON import/export, savol analitikasi |
| Test builder | Modullar skeleti (adaptiv: M1, M2 Easy, M2 Hard), qo'lda tanlash, avto-generatsiya (% qoidalar), validatsiya, preview (o'quvchi ko'rinishi), chop etish, tayinlash (guruh/o'quvchi, muddat, urinishlar) |
| O'quv markaz | O'quvchilar, xodimlar, guruhlar (jadval, xona, to'qnashuv tekshiruvi), enrollment tarixi, davomat (+Telegram ota-onaga), uy vazifasi (mashq/test/fayl), materiallar |
| Panellar | Student, Teacher/Assistant (faqat o'z guruhlari), Admin, Parent (bir nechta farzand) |
| Telegram | Tasdiqlash kodlari, natija/davomat/vazifa bildirishnomalari, kunlik mashq eslatmasi |

## Lokal ishga tushirish

```
docker compose up -d                       # PostgreSQL (port 5433)
cd SatPlatform && dotnet run               # http://localhost:5100
```

Migratsiyalar va boshlang'ich ma'lumotlar (taksonomiya, scoring jadvali, admin, demo kontent) avtomatik qo'llanadi.

Demo hisoblar (Development, parol `Demo123!`):

| Rol | Telefon |
|---|---|
| Super admin | +998900000000 (parol `Admin123!`) |
| Admin | +998901111110 |
| O'qituvchi | +998901111111 (SAT Intensive A), +998901111112 |
| Assistent | +998901111113 |
| O'quvchi | +998902000000 … +998902000009 |
| Ota-ona | +998903000000 (Ali Valiyevning otasi) |

Telegram bot tokeni `SatPlatform/appsettings.Local.json` ga yoziladi (git'ga kirmaydi):

```json
{ "Telegram": { "BotToken": "123456:ABC...", "BotUsername": "MySatBot" } }
```

Token bo'lmasa Development rejimida tasdiqlash kodi ekranda ko'rsatiladi.

## Testlar

```
cd SatPlatform.Tests
dotnet test                                # unit + integratsion (satplatform_test bazasi) + E2E (ishlab turgan saytga qarshi)
```

E2E testlar `http://localhost:5100` da ishlab turgan saytni talab qiladi (`SAT_E2E_URL` bilan o'zgartiriladi) va Playwright Chromium'ni
(`bin/Debug/net10.0/playwright.ps1 install chromium`). Sayt ishlamasa E2E testlar o'tkazib yuboriladi.
`./run-dev.sh` saytni `.run` papkasidan ishga tushiradi, shunda manba kodni qayta build qilish mumkin.

## Deploy (Render + Neon, bepul)

Jonli sayt: https://sat-platform-9jc4.onrender.com (Render `sat-platform`, Neon `sat-platform`). GITHUB_TOKEN ixtiyoriy: usiz GitHub push o'tkazib yuboriladi, image mavjud `docker login ghcr.io` bilan yuklanadi.

Chat loyihasidagi kabi: `deploy\deploy.ps1` GitHub repo yaratadi va push qiladi, Docker image'ni ghcr.io ga yuklaydi, Neon'da baza ochadi,
Render'da web service yaratadi/yangilaydi.

```powershell
$env:GITHUB_TOKEN = "ghp_..."          # repo, write:packages
$env:NEON_API_KEY = "..."
$env:RENDER_API_KEY = "rnd_..."
$env:TELEGRAM_BOT_TOKEN = "123456:ABC..."   # @BotFather dan bot; jonli saytda @this_is_Personalbot ishlatiladi (chat botidan alohida!)
$env:SAT_ADMIN_PASSWORD = "kuchli-parol"    # +998900000000 super admin
$env:SAT_DEMO_PASSWORD = "Demo123!"         # demo hisoblar (ixtiyoriy)
powershell -File deploy\deploy.ps1
```

Muhit o'zgaruvchilari: `DATABASE_URL`, `Telegram__BotToken`, `Telegram__BotUsername`, `Seed__AdminPhone`, `Seed__AdminPassword`,
`Seed__DemoPassword`, `Seed__Demo` (false = demo kontent yaratilmaydi), `App__PublicUrl`, `App__KeepAlive`, `App__CenterName`.
Fayllar (rasm, materiallar) va Data Protection kalitlari bazada saqlanadi — diskka bog'liq emas.

## Tuzilishi

| Papka | Vazifasi |
|---|---|
| `SatPlatform/Data` | Entity'lar, enum'lar, `AppDbContext`, migratsiyalar |
| `SatPlatform/Services` | `AuthService`, `TestEngineService`, `TestBuilderService`, `PracticeService`, `ScoringService`, `AnalyticsService`, `ContentService`, `AcademyService`, `HomeworkService`, `NotificationService`, `SeedService`, `DemoContent*` |
| `SatPlatform/Services/Telegram` | Bot client va polling servisi (ro'yxatdan o'tish, kodlar, /login) |
| `SatPlatform/Components/Pages/Student` | Dashboard, testlar, test oynasi (`Attempt.razor`), natija, review, mashq, xatolar, analitika, materiallar, uy vazifasi |
| `SatPlatform/Components/Pages/Teacher` | O'qituvchi paneli, guruhlar, o'quvchilar, davomat, tayinlash, uy vazifalari, tahlil |
| `SatPlatform/Components/Pages/Admin` | Admin paneli, savollar banki, test builder, materiallar, o'quvchilar, xodimlar, guruhlar, sozlamalar, audit |
| `SatPlatform/Components/Pages/Parent` | Ota-ona paneli |
| `SatPlatform/wwwroot/app.css`, `app.js` | Dizayn va JS yordamchilar (taymer, lokal zaxira, KaTeX, Quill, kalkulyator) |
| `SatPlatform.Tests` | Unit (`ScoringTests`), integratsion (`EngineIntegrationTests`), E2E (`E2E/*`) |

## Yangi migratsiya

```
cd SatPlatform && dotnet ef migrations add <Nomi>
```
