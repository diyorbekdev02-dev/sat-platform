using SatPlatform.Data;

namespace SatPlatform.Services;

public record HelpSection(string Heading, string[] Items);
public record HelpTopic(string Title, string Intro, HelpSection[] Sections);
public record QuickTip(string Title, string Text, string Icon);
public record Manual(string Intro, QuickTip[] Tips);

/// <summary>In-app, per-page user guide: opened from the «Qo'llanma» button in the top bar. Text may contain &lt;b&gt; and &lt;code&gt;.</summary>
public static class PageHelp
{
    /// <summary>"/teacher/groups/12?x=1" → "/teacher/groups/{id}"; question editor ids ("new" or a number) share one key.</summary>
    public static string Key(string path)
    {
        path = path.Split('?', '#')[0];
        var segs = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(s => s.All(char.IsDigit) ? "{id}" : s);
        var key = "/" + string.Join('/', segs);
        return key.StartsWith("/admin/questions/") ? "/admin/questions/{id}" : key;
    }

    public static HelpTopic? Find(string path) => Topics.GetValueOrDefault(Key(path));

    // ---------------------------------------------------------------- per-role manual (PDF + /help header)

    public static string Slug(UserRole? role) => role switch
    {
        UserRole.Admin or UserRole.SuperAdmin => "admin",
        UserRole.Teacher or UserRole.Assistant => "teacher",
        UserRole.Parent => "parent",
        _ => "student",
    };

    public static string PdfUrl(UserRole? role) => $"/qollanma-{Slug(role)}.pdf";

    public static Manual ManualFor(UserRole? role) => Manuals[Slug(role)];

    private static readonly Dictionary<string, Manual> Manuals = new()
    {
        ["admin"] = new("Administrator qo'llanmasi: savollar banki, test builder, o'quvchilar, xodimlar, guruhlar, materiallar, sozlamalar, audit, Telegram bot.",
        [
            new("Savollar banki", "Savol qo'shing, Faol holatga o'tkazing (izoh majburiy). Matn o'zgarsa yangi versiya yaratiladi.", "database"),
            new("Test builder", "Modullarga savol qo'shing yoki avto-generatsiya qiling, Preview qiling va Chop eting.", "tests"),
            new("Hisoblar", "O'qituvchi, assistent va admin hisoblarini faqat siz yaratasiz (Xodimlar va ota-onalar).", "user"),
            new("Sozlamalar", "Scoring jadvali, adaptiv qoidalar, modul vaqtlari va mustaqil ro'yxatdan o'tish.", "settings"),
        ]),
        ["teacher"] = new("O'qituvchi va assistent qo'llanmasi: guruhlar, o'quvchilar, davomat, test tayinlash, uy vazifasi, tahlil.",
        [
            new("Davomat", "Guruh va sanani tanlang, belgilang va saqlang — kelmagan o'quvchining ota-onasiga Telegram xabar boradi.", "calendar"),
            new("Test tayinlash", "Chop etilgan testni guruh yoki o'quvchiga muddat va urinishlar soni bilan tayinlang.", "tests"),
            new("Uy vazifasi", "Mashq, test yoki fayl turida vazifa bering va topshirilganlarni baholang.", "clipboard"),
            new("Zaif o'quvchilar", "Bosh sahifa e'tibor talab qiladigan o'quvchilarni ko'rsatadi.", "chart"),
        ]),
        ["parent"] = new("Ota-ona qo'llanmasi: farzandingizning ballari, testlari, davomati va zaif mavzularini qanday ko'rish.",
        [
            new("Farzandim", "Joriy ball, maqsad, aniqlik, davomat va ball dinamikasi bir sahifada.", "home"),
            new("Farzand qo'shish", "Profil → «Farzandni bog'lash» orqali o'quvchining ota-ona kodini kiriting.", "user"),
            new("Davomat", "Farzandingiz darsga kelmasa yoki kechiksa Telegramga xabar keladi.", "calendar"),
            new("Telegram bot", "@sat_notificationbot: /start — hisobni bog'lash, /login — parolsiz kirish havolasi.", "telegram"),
        ]),
        ["student"] = new("O'quvchi qo'llanmasi: kirish, testlar, test oynasi, mashq, xatolar, tahlil, materiallar, uy vazifasi, Telegram bot.",
        [
            new("Birinchi qadam", "Diagnostik testni ishlang — tizim zaif mavzularingizni aniqlab, har kuni nimani mashq qilishni aytadi.", "play"),
            new("Test oynasi", "A/B/C/D tugmalari — javob, F — belgilash, G — savollar jadvali, ← → — oldingi/keyingi savol.", "tests"),
            new("Internet uzilsa", "Javoblar brauzerda saqlanadi va ulanish qaytgach yuboriladi. Taymer serverda davom etadi.", "zap"),
            new("Telegram bot", "@sat_notificationbot: /start — hisobni bog'lash, /login — parolsiz kirish havolasi.", "telegram"),
        ]),
    };

    private static HelpSection S(string heading, params string[] items) => new(heading, items);
    private static HelpTopic T(string title, string intro, params HelpSection[] sections) => new(title, intro, sections);

    private static readonly HelpSection Phone = S("Telefon raqam",
        "Faqat <b>O'zbekiston</b> raqamlari qabul qilinadi: <code>+998 90 123 45 67</code>. Maydon o'zi guruhlaydi, <code>+998</code> ni o'chirib bo'lmaydi.",
        "<code>0901234567</code> yoki <code>998901234567</code> ko'rinishida joylashtirsangiz ham to'g'rilanadi.");

    private static readonly Dictionary<string, HelpTopic> Topics = new()
    {
        // ================================================================ O'QUVCHI
        ["/dashboard"] = T("Bosh sahifa", "Har kirganingizda shu yerda «bugun nimani mashq qilish kerak» degan savolga javob topasiz.",
            S("Birinchi qadam", "Yangi o'quvchida tepada <b>Diagnostik test</b> kartochkasi turadi. Uni ishlang: tizim darajangizni va zaif mavzularingizni aniqlaydi.",
                "Shundan keyin ball, tavsiyalar va grafiklar ishga tushadi."),
            S("Tepadagi ko'rsatkichlar", "<b>Joriy SAT ball</b> — oxirgi to'liq testning taxminiy bali (R&amp;W + Math).", "<b>Maqsad ball</b> — profilda belgilagan balingiz; pastida unga qancha qolgani ko'rinadi.",
                "<b>O'zgarish</b> — oldingi testga nisbatan farq (yashil — o'sish).", "<b>Streak</b> — ketma-ket necha kun mashq qilgansiz."),
            S("Bugun nimani mashq qilish kerak?", "Tizim eng zaif, eng sekin va eng ko'p xato qilingan skilllardan tavsiya beradi. <b>Boshlash</b> bosilsa mashq darhol boshlanadi.",
                "<b>Zaif skilllar</b> — aniqligi 50% dan past mavzular; <b>Barchasi →</b> to'liq tahlilga olib boradi."),
            S("Tugallanmagan ishlar", "Yarim qolgan test yoki mashq bo'lsa tepada sariq xabar chiqadi. <b>Davom ettirish</b> — vaqt va javoblar saqlangan holda qaytasiz."),
            S("Pastki qism", "<b>Tezkor harakatlar</b>: To'liq SAT, Mashq, Progressim.", "<b>Progressim</b>: yechilgan savollar, umumiy aniqlik, testlar/mashqlar soni, davomat.", "<b>Oxirgi testlar</b> jadvali: sana, umumiy, R&amp;W, Math ballari; qatorni bossangiz natijaga o'tasiz.")),

        ["/tests"] = T("Testlar", "Sizga ochiq bo'lgan barcha testlar va ishlagan urinishlaringiz.",
            S("Mavjud testlar", "Test turlari: <b>Diagnostik</b>, <b>To'liq SAT (mock)</b>, <b>Bo'lim</b> (faqat R&amp;W yoki Math), <b>Modul</b>, <b>Mavzu</b>.",
                "<b>Adaptiv</b> belgisi: 2-modul 1-modul natijangizga qarab osonroq yoki qiyinroq bo'ladi.", "Tayinlangan testda muddat va urinishlar soni ko'rsatiladi."),
            S("Holatlar", "<b>Testni boshlash</b> — yangi urinish.", "<b>Davom ettirish</b> — yarim qolgan urinish (vaqt serverda davom etgan bo'ladi).",
                "<b>Muddati o'tgan</b> — endi boshlab bo'lmaydi. <b>Urinishlar tugagan</b> — o'qituvchi ruxsat bergan soncha ishlagansiz. <b>Hali boshlanmagan</b> — boshlanish sanasi kelmagan."),
            S("Mening urinishlarim", "Barcha urinishlar: test, boshlangan vaqt, holat, umumiy/R&amp;W/Math ballari. <b>Natija</b> tugmasi hisobotni ochadi.")),

        ["/tests/{id}/start"] = T("Testni boshlash", "Test boshlanishidan oldingi tekshiruv va tuzilma.",
            S("Boshlashdan oldin", "Tizim internet, brauzer, ekran o'lchami va lokal saqlashni tekshiradi. Qizil belgi bo'lsa, avval uni tuzating.", "Testni <b>faqat bitta oynada</b> oching; ikkinchi oynada «Bu test boshqa oynada ochiq» xabari chiqadi."),
            S("Test tuzilishi", "Modullar, ularning savollar soni va vaqti ko'rsatiladi. Adaptiv testda 2-modul 1-modul natijasiga qarab tanlanadi.", "R&amp;W bo'limidan keyin <b>tanaffus</b> (10 daqiqagacha); uni erta tugatish mumkin."),
            S("Boshlash", "Tasdiqlagach taymer ishga tushadi. <b>Bekor qilish</b> — testsiz qaytish.", "Internet uzilsa javoblar brauzerda saqlanadi va ulanish qaytgach yuboriladi; taymer serverda davom etadi.")),

        ["/attempt/{id}/result"] = T("Test natijasi", "Test tugagach umumiy hisobot.",
            S("Ballar", "<b>SAT taxminiy ball</b> (200–800 + 200–800), pastida R&amp;W va Math ballari. <b>Oldingi</b> va <b>O'zgarish</b> — oldingi test bilan farq."),
            S("Tugmalar", "<b>Javoblarni ko'rish</b> — har bir savol bo'yicha tahlil.", "<b>Tahlil</b> — umumiy progress sahifasi. <b>Mashq</b> — zaif skilllar bo'yicha mashq."),
            S("Shu testdagi skilllar", "Har skill bo'yicha aniqlik va o'rtacha vaqt. Qizil — zaif (50% dan past), yashil — kuchli (75% dan yuqori).", "Test hali tugamagan bo'lsa, «Davom ettirish» tugmasi chiqadi.")),

        ["/attempt/{id}/review"] = T("Javoblarni ko'rish", "Testdagi har bir savol: nima javob berdingiz, to'g'ri javob nima va nima uchun.",
            S("Har savolda", "Sizning javobingiz, to'g'ri javob, sarflangan vaqt, skill va qiyinlik.", "<b>Izoh (explanation)</b> va bor bo'lsa video izoh."),
            S("Filtrlar", "<b>Xato</b> — faqat noto'g'ri javoblar; <b>Belgilangan</b> — test paytida flag qo'ygan savollaringiz."),
            S("Maslahat", "Xato savol ostidagi <b>Shu skillni mashq qilish</b> tugmasi o'sha mavzu bo'yicha mashq boshlaydi.", "← Natija — hisobotga qaytish.")),

        ["/practice"] = T("Mashq", "Testdan tashqari, mavzular bo'yicha tez mashq qilish joyi.",
            S("Tezkor mashq", "<b>10 / 20 / 30 ta savol</b> — tizim aralash savollar tanlaydi.", "<b>Zaif skilllar</b> — faqat aniqligi past mavzulardan.", "<b>Xatolarim ustida</b> — oldingi xatolar va shu skilldan yangi savollar."),
            S("Maxsus mashq", "Avval bo'limni (R&amp;W yoki Math) tanlang, so'ng domain va skillni. Qiyinlik, savollar soni (1–60) va vaqtni sozlaysiz."),
            S("Siz uchun tavsiya", "Tizim aniqlagan eng foydali mashqlar; <b>Boshlash</b> bosilsa darhol boshlanadi."),
            S("Mashqlar tarixi", "Rejim, sana, natija va aniqlik. Tugallanmagan mashq tepada <b>Davom ettirish</b> bilan turadi.")),

        ["/practice/new"] = T("Mashqni sozlash", "Mashq parametrlarini o'zingiz tanlaysiz.",
            S("Maydonlar", "<b>Bo'lim</b> — Hammasi, Reading &amp; Writing yoki Math.", "<b>Domain</b> va <b>Skill</b> — aniq mavzu (bo'sh qoldirsangiz aralash).", "<b>Qiyinlik</b> — Oson / O'rta / Qiyin yoki Aralash.",
                "<b>Savollar soni</b> — 1 dan 60 gacha.", "<b>Vaqt</b> — daqiqada; <code>0</code> = vaqtsiz."),
            S("Boshlash", "<b>Mashqni boshlash</b> bosilgach savollar chiqadi. Zaif skilllar rejimida tizim o'zi mavzuni tanlaydi.")),

        ["/practice/{id}"] = T("Mashq oynasi", "Testdan farqli, har javobdan keyin darhol natija ko'rinadi.",
            S("Qanday ishlaydi", "Variantni tanlang va <b>Javobni tasdiqlash</b> ni bosing — to'g'ri/noto'g'ri va izoh shu zahoti chiqadi.", "<b>Belgilash</b> — savolga keyin qaytish uchun. <b>Oldingi / Keyingi</b> — savollar orasida yurish."),
            S("Tugatish", "<b>Yakunlash</b> — mashqni tugatadi; javobsiz savollar hisobga olinmaydi.", "Chiqib ketsangiz mashq saqlanadi va Mashq sahifasida «Davom ettirish» chiqadi.")),

        ["/practice/{id}/result"] = T("Mashq natijasi", "Mashq tugagach qisqa hisobot.",
            S("Nimalar bor", "Aniqlik, o'rtacha vaqt va <b>Skilllar bo'yicha</b> natija (qaysi mavzu kuchli, qaysi biri zaif).", "Xato qilingan savollar avtomatik <b>Xatolarim</b> ga tushadi."),
            S("Tugmalar", "<b>Yana mashq qilish</b> — yangi mashq. <b>Bosh sahifa</b> — dashboardga qaytish.")),

        ["/mistakes"] = T("Xatolarim", "Test va mashqlarda xato qilgan savollaringiz avtomatik shu yerga to'planadi.",
            S("Filtrlar", "<b>Hammasi / R&amp;W / Math</b> — bo'lim.", "<b>So'nggi</b> — oxirgi xatolar birinchi; <b>Ko'p takrorlangan</b> — eng ko'p xato qilingan savollar birinchi.", "<b>Faqat hal qilinmagan</b> — to'g'ri yechmagan savollar."),
            S("Jadval", "Savol, skill, xato soni, oxirgi xato sanasi va holat. <b>Ko'rish</b> savolni izoh bilan ochadi."),
            S("Xatolarim ustida mashq", "Yuqoridagi tugma xatolardan mashq tuzadi. Savolni keyin to'g'ri yechsangiz u <b>hal qilingan</b> bo'ladi.", "Xato bo'lmasa 🎉 xabari chiqadi.")),

        ["/mistakes/{id}"] = T("Xato savol", "Bitta xato savol tafsiloti.",
            S("Ko'rinadigan narsalar", "Savol, <b>sizning oxirgi javobingiz</b>, to'g'ri javob va izoh."),
            S("Tugmalar", "<b>Shu skillni mashq qilish</b> — shu mavzu bo'yicha mashq.", "<b>Barcha xatolar ustida</b> — hamma xatolaringizdan mashq.", "← Xatolarim — ro'yxatga qaytish.")),

        ["/analytics"] = T("Mening progressim", "Natijalaringizning to'liq tahlili.",
            S("Yorliqlar", "<b>Skilllar</b> — har skill aniqligi: qizil (zaif, &lt;50%), sariq (o'rta), yashil (kuchli, &gt;75%).", "<b>Testlar</b> — barcha to'liq testlar va ball dinamikasi grafigi (umumiy, R&amp;W, Math).",
                "<b>Xatolar</b> — xatolaringiz tahlili.", "<b>Vaqt</b> — to'g'ri va xato javoblardagi o'rtacha vaqt, eng sekin mavzular.", "<b>Davomat</b> va <b>Uy vazifasi</b> — dars va topshiriqlar statistikasi."),
            S("Maslahat", "Avval <b>Skilllar</b> yorlig'idagi qizillarni toping, so'ng Mashq bo'limida shu skillni tanlab mashq qiling.")),

        ["/materials"] = T("Materiallar", "O'qituvchi va admin yuklagan o'quv materiallari.",
            S("Foydalanish", "Yuqoridagi kategoriya tugmalari (<b>Hammasi</b> va boshqalar) ro'yxatni toraytiradi.", "Fayl bo'lsa — yuklab olinadi, havola bo'lsa <b>Havolani ochish</b> yangi oynada ochadi.", "Guruhingizga tegishli materiallar ham shu yerda ko'rinadi."),
            S("Bo'sh bo'lsa", "«Materiallar yo'q» — hali hech narsa qo'shilmagan; o'qituvchingizdan so'rang.")),

        ["/homework"] = T("Uy vazifasi", "O'qituvchi bergan vazifalar, muddat va holat.",
            S("Vazifa turlari", "<b>Mashq</b> — «Boshlash» bilan mashq ochiladi, natija avtomatik yoziladi.", "<b>Test</b> — «Testlar sahifasida ochish» tugmasi orqali.", "<b>Fayl / matn</b> — <b>Topshirish</b> bosib javob matnini yozasiz yoki fayl yuklaysiz (PDF, rasm, Word; 25 MB gacha)."),
            S("Holatlar", "<b>Muddat o'tgan</b> — kech qoldingiz.", "Baholangach <b>Ball</b>, <b>O'qituvchi izohi</b> va <b>Natija</b> ko'rinadi.", "<b>Vazifa fayli</b> — o'qituvchi biriktirgan fayl, yuklab oling.")),

        // ================================================================ O'QITUVCHI / ASSISTENT
        ["/teacher"] = T("O'qituvchi paneli", "Kunlik ish uchun asosiy sahifa.",
            S("Kartochkalar", "<b>Mening guruhlarim</b> — guruhlar va o'quvchilar soni.", "<b>Bugungi darslar</b> — jadval bo'yicha bugungi guruhlar; <b>Davomat olish</b> bevosita davomatga olib boradi.",
                "<b>E'tibor talab qiladi</b> — balli yoki aniqligi past o'quvchilar soni.", "<b>Tekshirilmagan vazifalar</b> — topshirilgan, lekin baholanmagan."),
            S("Pastda", "<b>E'tibor talab qiladigan o'quvchilar</b> — ularni <b>Ochish</b> orqali progress panelida ko'rasiz.", "<b>So'nggi natijalar</b> — o'quvchi, guruh, ball, aniqlik va oxirgi faollik; <b>Barcha o'quvchilar →</b>.")),

        ["/teacher/groups"] = T("Guruhlar", "Sizga biriktirilgan guruhlar.",
            S("Nima ko'rinadi", "Jadvalda guruh nomi, o'qituvchi, kun, vaqt, xona, o'quvchilar soni, o'rtacha ball, aniqlik va davomat. Tepada qidiruv va filtrlar, <b>Export</b> — CSV fayl.", "Qatorni bossangiz guruh sahifasi ochiladi."),
            S("Bo'sh bo'lsa", "«Sizga hali guruh biriktirilmagan» — administrator guruhga sizni o'qituvchi (yoki assistent) qilib qo'shishi kerak.")),

        ["/teacher/groups/{id}"] = T("Guruh sahifasi", "Bitta guruhning barcha ma'lumotlari.",
            S("Yuqoridagi tugmalar", "<b>Davomat</b>, <b>Test tayinlash</b>, <b>Uy vazifasi</b> — guruh oldindan tanlangan holda ochiladi. <b>Tahrirlash</b> — guruh sozlamalari (administrator uchun)."),
            S("Yorliqlar", "<b>O'quvchilar</b> — ism, telefon, ball, maqsad, aniqlik, eng zaif skill, oxirgi faollik.", "<b>Progress</b> — guruhning zaif skilllari va <b>ball bo'yicha reyting</b>.",
                "<b>Davomat</b> — oxirgi 30 kun jadvali (Keldi/Kelmadi/Kechikdi/Sababli).", "<b>Testlar</b> — tayinlangan testlar, muddat, urinishlar va <b>Natijalar</b>.", "<b>Uy vazifalari</b> — topshirgan va tekshirilganlar soni."),
            S("Eslatma", "O'quvchi nomini yoki <b>Ochish</b> ni bossangiz uning to'liq progress paneli ochiladi.")),

        ["/teacher/students"] = T("O'quvchilar", "Guruhlaringizdagi barcha o'quvchilar.",
            S("Qidirish va tartiblash", "Qidiruv maydoni ism yoki telefon bo'yicha ishlaydi (raqamni <code>90 123</code> ko'rinishida ham yozish mumkin).", "Tartiblash: ism, ball, eng zaif, faollik."),
            S("Jadval", "Ball, aniqlik, davomat, <b>eng zaif skill</b> va oxirgi faollik. Raqamga bossangiz telefon qo'ng'irog'i ochiladi.", "<b>Ochish</b> — o'quvchining to'liq paneli."),
            S("Chegara", "Faqat o'z guruhingizdagi o'quvchilarni ko'rasiz.")),

        ["/teacher/students/{id}"] = T("O'quvchi paneli", "Bitta o'quvchining to'liq progressi.",
            S("Tugmalar", "<b>Test tayinlash</b> va <b>Uy vazifasi</b> — shu o'quvchiga alohida; <b>Tahrirlash</b> — administrator uchun."),
            S("Yorliqlar", "Tahlil, skilllar, testlar, xatolar, vaqt, davomat va uy vazifasi — o'quvchi ko'rgan tahlilning o'zi.", "Telefon va guruh sarlavha ostida; raqamni bossangiz qo'ng'iroq qilasiz."),
            S("Eslatma", "O'qituvchi o'quvchi javoblarini o'zgartira olmaydi, faqat ko'radi.")),

        ["/teacher/attendance"] = T("Davomat", "Dars davomatini belgilash.",
            S("Qadamlar", "1) <b>Guruh</b> va <b>Oy</b> ni tanlang — jadvalda oyning barcha dars kunlari chiqadi.  2) Katakchani bosib holatni tanlang.  3) <b>Saqlash</b>."),
            S("Holatlar", "<b>Keldi</b>, <b>Kechikdi</b>, <b>Sababli</b>, <b>Sababsiz</b>. Kun sarlavhasini bossangiz o'sha kuni hamma <b>Keldi</b> bo'ladi."),
            S("Bildirishnoma", "Saqlagach kelmagan va kechikkan o'quvchining <b>ota-onasiga Telegram orqali xabar</b> ketadi.")),

        ["/teacher/schedule"] = T("Dars jadvali", "Guruhlarning kunlik dars jadvali, xonalar bo'yicha.",
            S("Qanday ishlaydi", "Tepada kunni tanlang (Yak–Sha). <b>Xonalar</b> / <b>O'qituvchilar</b> tugmasi ustunlarni almashtiradi; chapda soatlar. Har dars rangli blok: guruh, vaqt, o'qituvchi (yoki xona) va o'quvchilar soni. Blokni bossangiz guruh sahifasi ochiladi."),
            S("Filtrlar va Export", "Guruh, o'qituvchi va xona bo'yicha saralash; <b>Export</b> — jadvalni CSV qilib yuklaydi."),
            S("Hafta", "<b>Hafta</b> — ustunlarda Dushanba–Yakshanba, har kunning darslari soatiga qarab joylashadi.")),

        ["/teacher/assign"] = T("Test tayinlash", "Chop etilgan testni guruh yoki o'quvchiga berish.",
            S("Yangi tayinlov", "<b>Test</b> — faqat chop etilgan testlar chiqadi.", "<b>Kimga</b> — <b>Guruh</b> yoki <b>Alohida o'quvchi</b>.", "<b>Boshlanish</b> va <b>Muddat</b> (ixtiyoriy); <b>Urinishlar soni</b> — bo'sh qoldirsangiz cheksiz.", "<b>Tayinlash</b> — o'quvchilarga bildirishnoma ketadi."),
            S("Tayinlovlar", "Pastdagi ro'yxatda hammasi; <b>Natijalar</b> — kim ishlagan, umumiy/R&amp;W/Math ball va <b>Javoblar</b>.", "«Hali hech kim ishlamagan» — hali urinish yo'q.")),

        ["/teacher/homework"] = T("Uy vazifalari", "Bergan vazifalaringiz ro'yxati.",
            S("Jadval", "Vazifa, kimga, tur, muddat, nechta o'quvchi <b>topshirgan</b> va nechtasi <b>tekshirilgan</b>. <b>Ochish</b> — baholash sahifasi."),
            S("Yangi vazifa", "<b>Yangi vazifa</b> tugmasi forma ochadi (mashq, test yoki fayl/matn).")),

        ["/teacher/homework/new"] = T("Yangi uy vazifasi", "Vazifa yaratish formasi.",
            S("Umumiy maydonlar", "<b>Sarlavha</b>, <b>Tavsif</b> (ixtiyoriy), <b>Kimga</b> (guruh yoki o'quvchi), <b>Muddat</b>, <b>Vazifa fayli</b> (ixtiyoriy)."),
            S("Vazifa turi", "<b>Savollar (mashq)</b> — bo'lim, domain, skill, qiyinlik va savollar soni; o'quvchi mashq sifatida ishlaydi, natija avtomatik yoziladi.", "<b>Test</b> — chop etilgan testni tanlaysiz (1 urinish bilan tayinlanadi).", "<b>Fayl / matn topshirish</b> — o'quvchi fayl yoki matn yuboradi, siz baholaysiz."),
            S("Yakun", "<b>Vazifa berish</b> bosilsa o'quvchilarga bildirishnoma ketadi.")),

        ["/teacher/homework/{id}"] = T("Vazifa va baholash", "Bitta vazifa bo'yicha barcha o'quvchilar.",
            S("Jadval", "O'quvchi, holat, topshirilgan vaqt, ball va izoh. Mashq/test turida <b>Javoblar</b> ham ko'rinadi."),
            S("Baholash", "Fayl/matn turida topshiriqni oching, <b>Ball</b> (maksimal ball bilan) va <b>Izoh</b> yozib <b>Saqlash</b> ni bosing; o'quvchiga bildirishnoma ketadi.", "<b>Fayl</b> — o'quvchi yuborgan fayl; <b>Matn</b> — yozma javob."),
            S("O'chirish", "Vazifani o'chirish qaytarilmaydi — ehtiyot bo'ling.")),

        ["/teacher/analytics"] = T("Tahlil", "Guruhlar va savollar bo'yicha chuqur tahlil.",
            S("Filtrlar", "<b>Guruh</b> (yoki Barcha guruhlar) va <b>Bo'lim</b> (Hammasi, Reading &amp; Writing, Math)."),
            S("Bo'limlar", "<b>Skilllar bo'yicha aniqlik</b> — qaysi mavzu zaif.", "<b>Guruhlar</b> jadvali: o'rtacha ball, aniqlik, davomat.", "<b>O'quvchilar</b> jadvali: ball, maqsad, testlar/mashqlar soni, <b>eng zaif</b> skill (eng zaiflar tepada)."),
            S("Savollar tahlili", "Kamida 5 ta urinish bo'lgan savollar: <b>eng qiyin</b>, <b>eng oson</b>, <b>eng sekin</b>, <b>eng ko'p ishlangan</b>. Skill, qiyinlik, urinishlar va o'rtacha vaqt ko'rinadi.", "Noto'g'ri ko'rinayotgan savol bo'lsa adminga #id ni ayting.")),

        // ================================================================ OTA-ONA
        ["/parent"] = T("Farzandim", "Farzandingizning o'qish holati.",
            S("Ko'rsatkichlar", "Joriy ball, maqsad, aniqlik, davomat va ball dinamikasi grafigi; zaif skilllar ro'yxati."),
            S("Yorliqlar", "Skilllar, Testlar (natija va javoblar), Xatolar, Vaqt, Davomat, Uy vazifasi."),
            S("Bir nechta farzand", "Tepada tanlash tugmalari chiqadi. Yana farzand qo'shish: <b>Profil → Farzandni bog'lash</b> va uning <b>ota-ona kodi</b>.", "Farzand bog'lanmagan bo'lsa «Profilga o'tish» tugmasi chiqadi."),
            S("Faqat ko'rish", "Ota-ona hech narsani o'zgartira olmaydi; farzandning testlarini ham ishlamaydi.")),

        ["/parent/tests"] = T("Farzandning testlari", "Farzandingiz ishlagan testlar.",
            S("Nima ko'rinadi", "Test nomi, sana, umumiy/R&amp;W/Math ballari. Natijani bossangiz javoblar bo'yicha tahlil ochiladi."),
            S("Maslahat", "Ballning o'sishiga qarang: bitta test emas, dinamika muhim.")),

        ["/parent/attendance"] = T("Farzandning davomati", "Darslarga qatnashish tarixi.",
            S("Belgilar", "<b>Keldi</b>, <b>Kelmadi</b>, <b>Kechikdi</b>, <b>Sababli</b> — sana bo'yicha."),
            S("Telegram", "Farzand kelmasa yoki kechiksa, Telegram botga darhol xabar keladi (Profil → Bildirishnomalar → Davomat yoqilgan bo'lsa).")),

        // ================================================================ ADMIN
        ["/admin"] = T("Admin paneli", "Butun o'quv markazning umumiy ko'rinishi.",
            S("Yuqori kartochkalar", "O'quvchilar (faol), o'qituvchilar/guruhlar, bugungi faollik, o'rtacha SAT ball, savollar banki (faol savollar) va chop etilgan testlar.", "<b>O'quvchi qo'shish</b> va <b>Sozlamalar</b> tezkor havolalar."),
            S("Jadvallar", "<b>Eng kuchli guruhlar</b> — guruh, o'quvchilar, o'rtacha ball, davomat; <b>Tahlil →</b>.", "<b>Markaz bo'yicha zaif skilllar</b> — qaysi mavzuga ko'proq e'tibor kerak.",
                "<b>Bank qamrovi</b> — savollari kam skilllar (Oson/O'rta/Qiyin bo'yicha); <b>Savollar →</b> bilan to'ldirasiz."),
            S("So'nggi faollik", "Oxirgi amallar; <b>Audit →</b> to'liq jurnalga olib boradi.")),

        ["/admin/questions"] = T("Savollar banki", "Barcha test va mashq savollari shu yerda saqlanadi.",
            S("Qidirish va filtr", "Qidiruv: matn, <code>#id</code> yoki teg. Filtrlar: bo'lim, domain, skill, qiyinlik, holat, tur (Multiple choice / SPR).", "Tepada jami savollar: <b>Faol</b>, <b>Qoralama</b>, <b>Arxiv</b>."),
            S("Tugmalar", "<b>Yangi savol</b> — muharrir ochiladi.", "<b>Import</b> — JSON massiv bilan ko'p savolni bir yo'la yuklash (maydonlar: skill, difficulty, type, passage, text, explanation, correctAnswer, options).", "<b>Export</b> — mavjud savollarni JSON qilib yuklab olish; namuna format sifatida ham ishlaydi."),
            S("Jadval", "Domain/Skill, savol matni, holat, <b>aniqlik</b> (o'quvchilar necha foiz to'g'ri javob berganini ko'rsatadi). ← → sahifalash.", "Ruxsatingiz yo'q bo'lsa 🔒 xabari chiqadi — administrator sizga «Savollar bankiga ruxsat» berishi kerak.")),

        ["/admin/questions/{id}"] = T("Savol muharriri", "Savolni yaratish yoki tahrirlash.",
            S("Asosiy maydonlar", "<b>Bo'lim</b>, <b>Domain</b>, <b>Skill</b>, <b>Qiyinlik</b> — hammasi majburiy.", "<b>Tur</b>: Multiple choice (A–D, to'g'risini belgilang) yoki SPR (son javob).", "<b>Teg</b> — o'zingiz uchun belgi (masalan <code>2026-mock-1</code>)."),
            S("Matnlar", "<b>Passage / kontekst</b> — chap panelda ko'rsatiladi (ixtiyoriy). <b>Savol matni</b> va <b>Variantlar</b>.", "Muharrir: qalin, kursiv, ro'yxat, jadval, rasm, havola. Formula LaTeX bilan: <code>$x^2$</code> yoki <code>$$\\frac{a}{b}$$</code>.",
                "SPR da bir nechta to'g'ri javobni <code>|</code> bilan ajrating: <code>2/3|0.667|.667</code>."),
            S("Izoh va video", "<b>Izoh (explanation)</b> — Faol savol uchun majburiy. <b>Video izoh URL</b> — YouTube va boshqalar."),
            S("Holat", "<b>Qoralama</b> — testga tushmaydi. <b>Tekshiruvda</b> — ko'rib chiqilmoqda. <b>Faol</b> — ishlatiladi. <b>Arxiv</b> — yangi testga tanlanmaydi, eski urinishlar buzilmaydi."),
            S("Versiya va statistika", "Savol matni o'zgartirilsa <b>yangi versiya</b> yaratiladi, eski urinishlar eski versiya bilan qoladi.", "Pastda: versiya, urinishlar, aniqlik, o'rtacha vaqt, yaratilgan va o'zgartirilgan sana.", "«O'xshash savollar bor» ogohlantirishi dublikatni oldini oladi."),
            S("Saqlash", "<b>Saqlash</b>, <b>O'chirish</b> (ishlatilgan savolni o'chirgandan ko'ra <b>Arxiv</b> holatiga o'tkazing), <b>← Bankga qaytish</b>.")),

        ["/admin/tests"] = T("Testlar", "Barcha testlar ro'yxati va yangisini yaratish.",
            S("Ro'yxat", "Nomi, tur, adaptivmi, modullar va savollar soni, holat (qoralama/chop etilgan/arxiv), urinishlar, o'zgartirilgan sana. <b>Ochish</b> — test builder.", "Tepadagi filtr tugmalari (<b>Hammasi</b> va holatlar) ro'yxatni toraytiradi."),
            S("Yangi test", "<b>Nomi</b>, <b>Tavsif</b>, <b>Tur</b> (Diagnostik, To'liq SAT, Bo'lim, Modul, Mavzu), <b>Bo'lim</b>, <b>Adaptiv</b> belgisi va <b>Vaqt</b>.", "<b>Yaratish</b> bosilsa modullar skeleti avtomatik tuziladi.")),

        ["/admin/tests/{id}"] = T("Test builder", "Testni yig'ish, tekshirish va chop etish.",
            S("Yuqoridagi tugmalar", "<b>Preview</b> — testni o'quvchi ko'rinishida ishlab ko'rasiz (statistikaga kirmaydi).", "<b>Sozlamalar</b> — nom, tavsif, tanaffus, scoring versiyasi, modul vaqtlari, savollarni aralashtirish, hammaga ochiqmi.",
                "<b>Chop etish</b> — validatsiya: modullar to'liq, savollar faol, vaqt bor, dublikat yo'q. Xatolar ro'yxati ko'rsatiladi; muvaffaqiyatda «✓ Validatsiyadan o'tdi».",
                "<b>Tayinlash</b>, <b>Qoralamaga qaytarish</b>, <b>Arxivlash</b> / <b>Arxivdan chiqarish</b>, <b>Nusxa</b>."),
            S("Modullar", "Har modulga: <b>Savol qo'shish</b> (bankdan filtr bilan tanlab <b>Tanlanganlarni qo'shish</b>) yoki <b>Avto-generatsiya</b> (savollar soni, qiyinlik va domain foizlari).", "Tartibni ↑↓, savolni ochish va olib tashlash tugmalari boshqaradi.",
                "Adaptiv testda R&amp;W va Math har biri uchun M1, M2 Easy, M2 Hard modullari bor."),
            S("Urinishlar", "Pastdagi jadvalda o'quvchilar natijalari: boshlangan vaqt, holat, umumiy/R&amp;W/Math. <b>Qayta hisoblash</b> — scoring o'zgarganda (audit jurnaliga yoziladi).")),

        ["/admin/materials"] = T("Materiallar", "O'quvchilarga ko'rinadigan o'quv materiallari.",
            S("Material qo'shish", "<b>Nomi</b>, <b>Kategoriya</b>, <b>Tavsif</b>, <b>Guruh</b> (bo'sh = Hammaga), <b>Fayl</b> (PDF, Word, rasm, video; 25 MB gacha) <b>yoki havola</b>."),
            S("Ro'yxat", "Nomi, kategoriya, fayl/havola, guruh va sana. Guruh tanlansa material faqat shu guruhga ko'rinadi."),
            S("Eslatma", "Katta videoni fayl o'rniga havola (YouTube, Drive) qilib qo'ying.")),

        ["/admin/students"] = T("O'quvchilar", "Markazning barcha o'quvchilari.",
            S("Qidirish va filtrlar", "Ism yoki telefon bo'yicha qidiruv; holat, guruh va ball oralig'i (&lt;1000, 1000–1200, 1200–1400, &gt;1400, Ballsiz)."),
            S("Jadval", "Ism, telefon (bossangiz qo'ng'iroq), guruh, holat, maqsad, aniqlik, davomat, oxirgi faollik. <b>Ochish</b> — o'quvchi sahifasi."),
            S("O'quvchi qo'shish", "<b>Ism-familiya</b>, <b>Telefon</b>, <b>Parol</b> (kamida 6 belgi), <b>Maqsad ball</b>, <b>Guruh</b> (ixtiyoriy). Yaratgach o'quvchiga <b>ota-ona kodi</b> beriladi."),
            Phone),

        ["/admin/students/{id}"] = T("O'quvchi sahifasi", "Bitta o'quvchining profili va boshqaruvi.",
            S("Ma'lumotlar", "<b>Ism-familiya</b>, <b>Telefon</b>, <b>Email</b>, <b>Holat</b> (Faol, Muzlatilgan, Ketgan, Bitirgan), <b>Maqsad ball</b>, <b>Izoh</b> (faqat xodimlar ko'radi).", "<b>Yangi parol</b> — bo'sh qoldirsangiz o'zgarmaydi. <b>Hisob faol</b> belgisini olib tashlasangiz kira olmaydi."),
            S("Guruhlar", "<b>Biriktirish</b> bilan guruhga qo'shasiz, <b>Chiqarish</b> bilan chiqarasiz. <b>Guruh tarixi</b> — avvalgi guruhlar."),
            S("Ota-onalar", "<b>Ota-ona kodi</b> — ota-onaga bering, ular Profilda kiritib farzandni bog'laydi. Yoki ro'yxatdan tanlab <b>Bog'lash</b>; <b>Uzish</b> bog'lanishni olib tashlaydi."),
            S("Progress", "Sahifa pastida o'quvchining to'liq tahlili: skilllar, testlar, xatolar, vaqt, davomat, uy vazifasi."),
            Phone),

        ["/admin/leads"] = T("Lidlar", "Yangi o'quvchi bo'lishi mumkin bo'lgan murojaatlar (CRM).",
            S("Ro'yxat va taxta", "<b>Ro'yxat</b> — jadval; <b>Taxta</b> — holatlar bo'yicha ustunlar (Yangi, Bog'lanildi, Sinov darsi, O'quvchi bo'ldi, Yo'qotildi). Lidni bossangiz tahrirlash oynasi ochiladi."),
            S("Filtrlar", "Qidiruv (ism yoki telefon), holat, manba va moderator. <b>Export</b> — CSV fayl."),
            S("Lid qo'shish", "<b>Ism</b>, <b>Telefon</b>, kurs, manba (Instagram, Telegram…), holat, moderator va izoh. Holatni lid bilan ishlash davomida o'zgartirib boring.")),

        ["/admin/payments"] = T("To'lovlar va balans", "O'quvchilarning balansi va to'lov sanalari.",
            S("Jadval", "Har o'quvchining <b>balansi</b> (to'lovlar yig'indisi minus hisoblangan qarz) va keyingi <b>to'lov sanasi</b>. Manfiy balans qizil bilan ko'rsatiladi."),
            S("Filtrlar", "<b>Qarzdorlar</b>, <b>Joriy oyda to'lov muddati</b>, <b>Muddati o'tgan</b> va guruh bo'yicha."),
            S("To'lov qo'shish", "<b>To'lov</b> tugmasi: turi (Naqd, Karta, O'tkazma yoki <b>Hisoblash</b> — oylik qarz yozish), summa, sana va keyingi to'lov sanasi. Pastda to'lovlar tarixi.")),

        ["/tasks"] = T("Topshiriqlar", "Xodimlar uchun ichki vazifalar.",
            S("Ishlash", "<b>Topshiriq qo'shish</b> — sarlavha, tavsif, mas'ul va muddat. Katakchani belgilab bajarilgan deb qo'ying. Muddati o'tgan bajarilmagan topshiriqning sanasi qizil."),
            S("Ko'rinish", "O'qituvchi faqat o'zining topshiriqlarini ko'radi; administrator hammasini ko'radi va xodim bo'yicha filtrlay oladi.")),

        ["/admin/users"] = T("Xodimlar va ota-onalar", "O'qituvchi, assistent, admin va ota-ona hisoblari (o'quvchilar alohida bo'limda).",
            S("Ro'yxat", "Ism, telefon (bossangiz qo'ng'iroq), rol, guruhlar, Telegram bog'langanmi, holat va oxirgi kirish. Qidiruv ism yoki telefon bo'yicha."),
            S("Foydalanuvchi qo'shish / tahrirlash", "<b>Rol</b>: O'qituvchi, Assistent, Admin, Ota-ona.", "<b>Savollar bankiga ruxsat</b> — o'qituvchiga savol yaratish va tahrirlash huquqi.", "<b>Hisob faol</b> — o'chirib qo'ysangiz kirolmaydi. Parol bo'sh bo'lsa mavjud parol saqlanadi."),
            S("Muhim", "O'qituvchi, assistent va admin hisoblarini <b>faqat administrator</b> yaratadi. Oxirgi super adminni o'chirib bo'lmaydi."),
            Phone),

        ["/admin/groups"] = T("Guruhlar", "Dars guruhlari va jadval.",
            S("Ro'yxat", "Nomi, kurs, o'qituvchi, assistent, jadval, xona, o'quvchilar soni, o'rtacha ball va holat. <b>Nofaol guruhlarni ko'rsatish</b> belgisi yopilganlarni ham chiqaradi."),
            S("Guruh yaratish", "<b>Nomi</b>, <b>Kurs turi</b> (masalan SAT), <b>O'qituvchi</b>, <b>Assistent</b>, <b>Xona</b>, <b>Boshlanish/Tugash</b>, <b>Dars kunlari</b> va vaqti.", "O'qituvchi yoki xona band bo'lsa tizim ogohlantiradi."),
            S("O'quvchilar", "Guruhni ochib «— o'quvchi qo'shish —» dan tanlang va <b>Qo'shish</b>; <b>Chiqarish</b> bilan olib tashlaysiz.")),

        ["/admin/groups/{id}"] = T("Guruh sahifasi", "Bitta guruhni tahrirlash va o'quvchilarini boshqarish.",
            S("Tahrirlash", "Nom, kurs, o'qituvchi, assistent, xona, jadval, <b>Faol</b> belgisi; <b>Saqlash</b> / <b>Bekor</b>."),
            S("O'quvchilar", "Ro'yxatda ism va telefon; <b>Chiqarish</b> tugmasi. Pastdagi tanlovdan yangi o'quvchi <b>Qo'shish</b>."),
            S("Eslatma")),

        ["/admin/analytics"] = T("Tahlil", "Butun markaz bo'yicha tahlil.",
            S("Filtrlar", "<b>Guruh</b> va <b>Bo'lim</b> bo'yicha toraytiring."),
            S("Bo'limlar", "Skilllar bo'yicha aniqlik, guruhlar va o'quvchilar jadvallari, savollar tahlili (eng qiyin/oson/sekin/ko'p ishlangan)."),
            S("Qanday ishlatiladi", "Zaif skilllarni toping → Savollar bankida o'sha skillga savol qo'shing → o'qituvchilarga shu mavzuni berishni tavsiya qiling.")),

        ["/admin/settings"] = T("Sozlamalar", "Platformaning barcha qoidalari. Diqqat bilan o'zgartiring.",
            S("Umumiy sozlamalar", "<b>O'quv markaz nomi</b>, <b>Zaif/Kuchli skill chegarasi (%)</b>, <b>Skill baholash uchun minimal savollar</b>, <b>Tanaffus</b>, <b>R&amp;W va Math modul vaqti</b> (soniyada), <b>Tavsiya qilingan savollar soni</b>, <b>Mashq eslatmasi soati</b> (Toshkent vaqti), <b>Standart scoring versiyasi</b>.",
                "<b>Mustaqil ro'yxatdan o'tish</b>: «Ruxsat berilgan» yoki «Faqat admin qo'shadi». <b>Saqlash</b> ni unutmang."),
            S("Adaptiv routing", "Har bo'lim uchun 1-modul savollar soni va <b>Cutoff</b>: shuncha to'g'ri javob yoki undan ko'p — <b>Hard</b>, kam bo'lsa — <b>Easy</b> 2-modul."),
            S("Scoring jadvali", "Raw (to'g'ri javoblar) → scaled (200–800) jadvali, Hard / Easy / Standard yo'nalishlar uchun, versiyalangan.", "<b>Yangi versiya</b> yarating, <b>Tahrirlash</b> bosib har qatorga <code>raw,scaled</code> kiriting, <b>Jadvalni saqlash</b>."),
            S("Domain va skilllar", "SAT taksonomiyasi: kod, nom. Yangi skill qo'shsangiz savollar bankida tanlash mumkin bo'ladi.")),

        ["/admin/audit"] = T("Audit jurnali", "Kim, qachon, nima qilgani.",
            S("Ustunlar", "<b>Vaqt</b>, <b>Kim</b>, <b>Amal</b>, <b>Obyekt</b>, <b>Tafsilot</b>."),
            S("Nimalar yoziladi", "Savol/test yaratish va o'zgartirish, chop etish, tayinlash, o'quvchi va guruh o'zgarishlari, davomat, ballarni qayta hisoblash, sozlamalar."),
            S("Foydalanish", "Noto'g'ri o'zgarishni topish yoki «bu ballni kim o'zgartirdi?» degan savolga javob uchun. Jurnalni tahrirlab bo'lmaydi.")),

        // ================================================================ UMUMIY
        ["/profile"] = T("Profil", "Shaxsiy ma'lumotlar, parol va bildirishnomalar.",
            S("Ma'lumotlar", "<b>Ism-familiya</b>, <b>Email</b> (ixtiyoriy) va o'quvchida <b>Maqsad SAT ball</b>; <b>Saqlash</b>."),
            S("Bildirishnomalar", "Test natijalari, davomat, uy vazifasi va tayinlangan testlar, <b>kunlik mashq eslatmasi</b> (Telegram) — alohida yoqiladi/o'chiriladi."),
            S("Parol", "<b>Joriy parol</b>, <b>Yangi parol</b> (kamida 6 belgi), <b>Takrorlang</b> → <b>Parolni o'zgartirish</b>. Telegram kodi bilan kirgan bo'lsangiz ham shu yerdan yangi parol qo'yasiz."),
            S("Telegram", "Bot bog'langanmi yoki yo'qmi shu yerda ko'rinadi. Bog'lash uchun botda <b>Start</b> bosib raqamingizni yuboring."),
            S("O'quvchi uchun", "<b>Ota-ona kodi</b> (<b>Nusxalash</b>) — ota-onangizga bering; <b>Guruhlarim</b> va <b>Bog'langan ota-onalar</b> ro'yxati."),
            S("Ota-ona uchun", "<b>Farzandlarim</b> va <b>Yana bir farzandni bog'lash</b> — farzandning ota-ona kodini kiriting va <b>Bog'lash</b>.")),

        ["/notifications"] = T("Bildirishnomalar", "Platformadagi barcha xabarlar.",
            S("Nimalar keladi", "Test natijalari, tayinlangan testlar, uy vazifalari va baholar, davomat."),
            S("Foydalanish", "Xabarni bossangiz tegishli sahifa ochiladi (<b>Ochish</b>). O'qilmaganlar soni yuqoridagi qo'ng'iroq belgisida ko'rinadi."),
            S("Telegram", "Xuddi shu xabarlar Telegram botga ham keladi; sozlash: Profil → Bildirishnomalar.")),
    };
}
