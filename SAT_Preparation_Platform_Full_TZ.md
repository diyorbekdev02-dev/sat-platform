# SAT O'QUV MARKAZI PLATFORMASI

## TO'LIQ TEXNIK TOPSHIRIQ (TZ) --- DIGITAL SAT PREPARATION PLATFORM

**Hujjat turi:** Full Technical Specification\
**Maqsad:** SAT'ga tayyorlash, online test ishlash, practice, xatolarni
tahlil qilish, progressni kuzatish va o'quvchilarni minimal darajada
boshqarish.\
**Versiya:** 1.0\
**Asosiy foydalanuvchilar:** Student, Teacher, Assistant, Admin, Parent\
**Asosiy mahsulot:** SAT Preparation Platform

------------------------------------------------------------------------

# 1. LOYIHANING ASOSIY MAQSADI

Platformaning asosiy maqsadi o'quv markazidagi SAT tayyorgarlik
jarayonini imkon qadar saytning o'ziga ko'chirish.

O'quvchi platformaga kirgandan keyin quyidagi siklni bajarishi kerak:

``` text
Diagnostic Test
      ↓
Natijani tahlil qilish
      ↓
Kuchsiz skilllarni aniqlash
      ↓
Mavzu/skill bo‘yicha Practice
      ↓
Xatolarni ko‘rib chiqish
      ↓
Qayta Practice
      ↓
Full SAT Mock
      ↓
Score
      ↓
Progress tahlili
      ↓
Keyingi Practice
```

Shuning uchun platformaning markazi **CRM emas**, balki:

``` text
SAT CONTENT
+
TEST ENGINE
+
PRACTICE ENGINE
+
SCORING
+
ANALYTICS
+
STUDENT PROGRESS
```

hisoblanadi.

CRM qismi faqat quyidagilarni boshqaradi:

-   o'quvchilar;
-   o'qituvchilar;
-   guruhlar;
-   guruhga biriktirish;
-   davomat;
-   o'quvchi progressini kuzatish.

------------------------------------------------------------------------

# 2. MUHIM QAMROV

## 2.1. Birinchi versiyada bo'lishi kerak

### Student

-   Login/Register
-   Dashboard
-   Diagnostic Test
-   Full SAT Mock
-   Section Test
-   Module Test
-   Topic Practice
-   Skill Practice
-   Custom Practice
-   Mistake Bank
-   Test Review
-   Score History
-   Skill Analytics
-   Time Analytics
-   Recommended Practice
-   Materials
-   Homework

### Teacher

-   Dashboard
-   Groups
-   Students
-   Student Profile
-   Student Progress
-   Test Results
-   Attendance
-   Homework
-   Practice/Test assignment

### Admin

-   Dashboard
-   Students
-   Teachers
-   Groups
-   Attendance
-   Question Bank
-   Question Editor
-   Test Builder
-   Test Publishing
-   Materials
-   Analytics
-   Users/Roles

### Parent

-   Child Score
-   Progress
-   Attendance
-   Recent Tests

### Notification

-   In-app
-   Telegram integration

------------------------------------------------------------------------

# 3. BIRINCHI VERSIYADAN TASHQARIDA

Quyidagilar birinchi release uchun majburiy emas:

-   Billing
-   Click
-   Payme
-   Uzum Bank
-   Payroll
-   Teacher salary calculation
-   HR
-   Revenue sharing
-   Full ERP
-   Accounting

Ular keyinchalik alohida modul sifatida qo'shilishi mumkin.

------------------------------------------------------------------------

# 4. FOYDALANUVCHI ROLLARI

## 4.1. SUPER ADMIN

To'liq huquq.

Ko'radi:

-   barcha studentlar;
-   barcha teacherlar;
-   barcha guruhlar;
-   barcha testlar;
-   barcha savollar;
-   barcha natijalar;
-   analytics;
-   attendance.

Amallar:

-   create;
-   read;
-   update;
-   archive;
-   publish;
-   assign;
-   delete faqat xavfsiz/soft-delete orqali.

------------------------------------------------------------------------

## 4.2. ADMIN / BRANCH ADMIN

O'quv markazi faoliyatini boshqaradi:

-   student qo'shish;
-   teacher qo'shish;
-   group yaratish;
-   studentni groupga biriktirish;
-   attendance;
-   test assignment;
-   material;
-   report.

Agar tizim bitta filial uchun qurilsa, `BRANCH_ADMIN` alohida role
bo'lmasligi ham mumkin.

------------------------------------------------------------------------

## 4.3. TEACHER

Teacher faqat o'ziga biriktirilgan guruh/studentlar bilan ishlaydi.

Teacher:

-   group ko'radi;
-   student ko'radi;
-   attendance belgilaydi;
-   test natijasini ko'radi;
-   student progressini ko'radi;
-   homework beradi;
-   practice/test tavsiya qiladi.

Teacher studentning passwordini ko'rmaydi.

Teacher boshqa teacherning guruhlarini ko'rmaydi.

------------------------------------------------------------------------

## 4.4. ASSISTANT

Teacherga o'xshash, lekin cheklangan huquq:

-   attendance;
-   student list;
-   homework checking;
-   progress ko'rish.

------------------------------------------------------------------------

## 4.5. STUDENT

Platformaning asosiy foydalanuvchisi.

Student:

-   test ishlaydi;
-   practice qiladi;
-   xatolarini ko'radi;
-   scoreini ko'radi;
-   progressini kuzatadi;
-   material o'qiydi;
-   homework bajaradi.

------------------------------------------------------------------------

## 4.6. PARENT

Faqat read-only.

Parent:

-   farzandining scoreini;
-   test history;
-   progress;
-   attendance;
-   weak skilllarini ko'radi.

Parent test ishlamaydi.

------------------------------------------------------------------------

# 5. UMUMIY SAYT STRUKTURASI

``` text
/
├── Login
├── Register
│
├── Student
│   ├── Dashboard
│   ├── Tests
│   │   ├── Available Tests
│   │   ├── My Attempts
│   │   └── Test Result
│   ├── Practice
│   │   ├── R&W
│   │   ├── Math
│   │   ├── Topics
│   │   ├── Skills
│   │   ├── Mistakes
│   │   └── Custom Practice
│   ├── Analytics
│   ├── Materials
│   ├── Homework
│   └── Profile
│
├── Teacher
│   ├── Dashboard
│   ├── Groups
│   ├── Students
│   ├── Attendance
│   ├── Tests
│   ├── Homework
│   └── Analytics
│
├── Admin
│   ├── Dashboard
│   ├── Students
│   ├── Teachers
│   ├── Groups
│   ├── Attendance
│   ├── Question Bank
│   ├── Test Builder
│   ├── Materials
│   ├── Reports
│   └── Settings
│
└── Parent
    ├── Dashboard
    ├── Progress
    ├── Tests
    └── Attendance
```

------------------------------------------------------------------------

# 6. STUDENT SAYTINI TASAVVUR QILISH

Student saytga kirganda birinchi navbatda Dashboard ko'radi.

## 6.1. Student Dashboard

``` text
┌───────────────────────────────────────────────────────────────┐
│ LOGO                         Practice  Tests  Analytics  👤   │
├───────────────────────────────────────────────────────────────┤
│                                                               │
│  Welcome, Ali!                                                │
│                                                               │
│  ┌──────────────────────┐    ┌────────────────────────────┐  │
│  │ CURRENT SAT SCORE     │    │ TARGET SCORE               │  │
│  │                       │    │                            │  │
│  │        1180           │    │           1400             │  │
│  │                       │    │  Progress: 73%             │  │
│  │ R&W 580 | Math 600    │    │                            │  │
│  └──────────────────────┘    └────────────────────────────┘  │
│                                                               │
│  Recommended Practice                                        │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │ Geometry — 43% accuracy                                  │ │
│  │ 15 questions · Medium                                    │ │
│  │                                  [Start Practice]         │ │
│  └─────────────────────────────────────────────────────────┘ │
│                                                               │
│  Weak Skills                                                  │
│  ┌────────────┬────────────┬────────────┐                    │
│  │ Geometry   │ Algebra    │ Vocabulary │                    │
│  │ 43%        │ 51%        │ 58%        │                    │
│  └────────────┴────────────┴────────────┘                    │
│                                                               │
│  Quick Actions                                                │
│  [Full SAT] [Practice] [Mistakes] [My Progress]              │
│                                                               │
│  Recent Tests                                                 │
│  Test #5       1180       2026-10-07                         │
│  Test #4       1140       2026-10-02                         │
│  Test #3       1090       2026-09-28                         │
│                                                               │
└───────────────────────────────────────────────────────────────┘
```

Dashboardda asosiy savol:

> "Bugun men nimani mashq qilishim kerak?"

bo'lishi kerak.

Shuning uchun `Recommended Practice` dashboardning muhim qismi
hisoblanadi.

------------------------------------------------------------------------

# 7. STUDENT SIDEBAR / NAVIGATION

Desktop:

``` text
┌─────────────────────┐
│ SAT CENTER          │
├─────────────────────┤
│ 🏠 Dashboard        │
│ 📝 Tests            │
│ 🎯 Practice         │
│ ❌ My Mistakes      │
│ 📊 Analytics        │
│ 📚 Materials        │
│ 📋 Homework         │
│                     │
│ ⚙ Profile           │
│ 🚪 Logout            │
└─────────────────────┘
```

Mobileda sidebar drawer ko'rinishida ochiladi.

------------------------------------------------------------------------

# 8. TESTLAR SAHIFASI

`Tests` sahifasi studentga mavjud testlarni ko'rsatadi.

``` text
┌──────────────────────────────────────────────────────────────┐
│ Tests                                                        │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│ [All] [Diagnostic] [Full SAT] [Section] [Assigned]           │
│                                                              │
│ ┌──────────────────────────────────────────────────────────┐ │
│ │ SAT Mock Test #5                                         │ │
│ │ Full SAT · Adaptive                                      │ │
│ │ R&W 64 min · Math 70 min                                │ │
│ │                                                          │ │
│ │ Assigned by: Teacher                                     │ │
│ │ Deadline: 15 Oct                                         │ │
│ │                                      [Start Test]        │ │
│ └──────────────────────────────────────────────────────────┘ │
│                                                              │
│ ┌──────────────────────────────────────────────────────────┐ │
│ │ Diagnostic Test                                         │ │
│ │ Estimate your current SAT level                         │ │
│ │                                      [Start Test]        │ │
│ └──────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
```

------------------------------------------------------------------------

# 9. TEST TURLARI

## 9.1. Diagnostic Test

Yangi studentning boshlang'ich darajasini aniqlaydi.

Natija:

-   Total Score;
-   R&W;
-   Math;
-   skill accuracy;
-   weak areas.

------------------------------------------------------------------------

## 9.2. Full SAT Mock

To'liq SAT formatidagi test.

Student real imtihonga yaqin sharoitda ishlaydi.

------------------------------------------------------------------------

## 9.3. Section Test

Faqat:

-   Reading & Writing

yoki

-   Math

------------------------------------------------------------------------

## 9.4. Module Test

Masalan:

``` text
Math Module 1
Math Module 2
R&W Module 1
R&W Module 2
```

------------------------------------------------------------------------

## 9.5. Topic Test

Masalan:

``` text
Math → Algebra
```

------------------------------------------------------------------------

## 9.6. Skill Practice

Masalan:

``` text
Linear Equations
Words in Context
Inference
Grammar
```

------------------------------------------------------------------------

# 10. TESTNI BOSHLASHDAN OLDINGI EKRAN

Student `Start Test` bosadi.

Avval Pre-Test Readiness chiqadi.

``` text
┌────────────────────────────────────────────────────────────┐
│ SAT MOCK TEST #5                                           │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ Before you start                                           │
│                                                            │
│ ✓ Stable internet connection                               │
│ ✓ Screen size is supported                                 │
│ ✓ Calculator is available in Math                          │
│                                                            │
│ Test Structure                                             │
│                                                            │
│ Reading & Writing                                          │
│ 64 minutes                                                 │
│ 2 modules                                                  │
│                                                            │
│ Break                                                      │
│ 10 minutes                                                 │
│                                                            │
│ Math                                                       │
│ 70 minutes                                                 │
│ 2 modules                                                  │
│                                                            │
│ ⚠ Once the test starts, the timer will begin.              │
│                                                            │
│                        [Cancel] [Start Test]               │
└────────────────────────────────────────────────────────────┘
```

------------------------------------------------------------------------

# 11. DEVICE CHECK

Startdan oldin:

-   browser;
-   screen;
-   internet;
-   LocalStorage;
-   IndexedDB;
-   WebSocket/Ping

tekshiriladi.

Status:

``` text
Internet             ✓ Good
Browser              ✓ Supported
Screen                ✓ Supported
Local Storage         ✓ Available
Session               ✓ Ready
```

Agar critical check fail bo'lsa:

``` text
[Start Test]
```

disabled qilinadi.

------------------------------------------------------------------------

# 12. TEST SESSION LOCK

Bitta student bitta testni bir vaqtning o'zida faqat bitta aktiv
sessionda ishlaydi.

Agar student ikkinchi tab ochsa:

``` text
This test is already open in another session.
Please return to your active test session.
```

ko'rsatiladi.

Backend session ownershipni tekshiradi.

------------------------------------------------------------------------

# 13. ASOSIY TEST OYNASI

Bu butun platformadagi eng muhim ekran.

``` text
┌───────────────────────────────────────────────────────────────┐
│ Reading & Writing — Module 1 of 2        ⏱ 58:42    Tools ▼ │
├───────────────────────────────────────────────────────────────┤
│                                                               │
│ ┌─────────────────────────┬─────────────────────────────────┐ │
│ │                         │                                 │ │
│ │ PASSAGE / CONTEXT       │ QUESTION                        │ │
│ │                         │                                 │ │
│ │ Lorem ipsum...          │ According to the passage,      │ │
│ │                         │ which statement is best...?     │ │
│ │ Lorem ipsum...          │                                 │ │
│ │                         │ ○ A. ...                       │ │
│ │ Lorem ipsum...          │ ○ B. ...                       │ │
│ │                         │ ○ C. ...                       │ │
│ │                         │ ○ D. ...                       │ │
│ │                         │                                 │ │
│ └─────────────────────────┴─────────────────────────────────┘ │
│                                                               │
│ [← Back]                      [Flag]            [Next →]       │
│                                                               │
│ Questions:                                                    │
│ [1✓] [2✓] [3] [4⚑] [5] [6✓] ... [27]                        │
└───────────────────────────────────────────────────────────────┘
```

------------------------------------------------------------------------

# 14. TEST HEADER

Headerda:

-   Section;
-   Module;
-   Timer;
-   Tools.

ko'rsatiladi.

Misol:

``` text
Reading & Writing — Module 1 of 2
58:42
```

------------------------------------------------------------------------

# 15. TIMER

Timer server time bilan synchronizatsiya qilinadi.

Format:

``` text
MM:SS
```

5 daqiqa qolsa:

``` text
05:00
```

timer critical statega o'tadi.

Timer doim ko'rinishi kerak.

Frontend timerga ishonib qolmasligi kerak.

Final deadline backend tomonidan tekshiriladi.

------------------------------------------------------------------------

# 16. TEST TOOLS

Tools menu:

``` text
Tools
├── Calculator
├── Reference Sheet
├── Annotation
└── Line Reader
```

Mathda calculator mavjud.

Reading & Writingda kerakli annotation tool mavjud.

------------------------------------------------------------------------

# 17. PASSAGE / CONTEXT PANEL

R&W savollarida:

-   passage;
-   chart;
-   table;
-   image;
-   context

chap panelda ko'rsatiladi.

Chap panel alohida scroll qilinadi.

------------------------------------------------------------------------

# 18. QUESTION PANEL

O'ng panel:

-   question;
-   options;
-   answer;
-   flag.

ko'rsatadi.

------------------------------------------------------------------------

# 19. ANSWER OPTION

Har bir option:

``` text
○ A. Answer text
```

ko'rinishida.

Tanlangan javob:

``` text
● B. Answer text
```

ko'rinishida.

------------------------------------------------------------------------

# 20. STRIKETHROUGH

Har bir option yonida eliminate action bo'ladi.

Masalan:

``` text
A. Answer
B. Answer  ~~strike~~
C. Answer
D. Answer  ~~strike~~
```

Bu faqat studentning shaxsiy UI holati.

Eliminated option avtomatik noto'g'ri deb hisoblanmaydi.

------------------------------------------------------------------------

# 21. FLAG FOR REVIEW

Student savolni:

``` text
[⚑ Flag]
```

qiladi.

Question Gridda flag ko'rsatiladi.

------------------------------------------------------------------------

# 22. MATH SPR

Mathdagi ochiq savol:

``` text
Enter your answer:

[____________]
```

Input quyidagilarni qo'llab-quvvatlaydi:

-   integer;
-   negative;
-   decimal;
-   fraction.

Masalan:

``` text
-2
3.14
2/3
9999
```

Validation aniq test specification bilan belgilanadi.

------------------------------------------------------------------------

# 23. QUESTION GRID

Pastki qismda savollar grid ko'rsatiladi.

States:

``` text
✓ Answered
□ Unanswered
● Current
⚑ Flagged
```

Masalan:

``` text
[1✓] [2✓] [3] [4⚑] [5✓]
[6]  [7✓] [8✓] [9]  [10⚑]
```

------------------------------------------------------------------------

# 24. NEXT / BACK

``` text
[← Back]                     [Next →]
```

Back oldingi savolga qaytaradi.

Next keyingi savolga o'tkazadi.

Module tugaganda student Review ekraniga o'tadi.

------------------------------------------------------------------------

# 25. MODULE REVIEW

Module tugaganda:

``` text
┌──────────────────────────────────────────────┐
│ Module Review                                │
├──────────────────────────────────────────────┤
│ Answered: 24 / 27                            │
│ Unanswered: 3                                │
│ Flagged: 4                                   │
│                                              │
│ Unanswered Questions                         │
│ #5   #12   #20                               │
│                                              │
│ Flagged Questions                            │
│ #3   #8   #16   #22                          │
│                                              │
│ [Return to Questions]   [Finish Module]      │
└──────────────────────────────────────────────┘
```

Student xohlagan savolga qaytadi.

------------------------------------------------------------------------

# 26. ADAPTIVE TEST

Platformada adaptive test engine bo'ladi.

## Module 1

Student Module 1ni ishlaydi.

Backend raw resultni hisoblaydi.

## Module 2

Module 1 natijasiga qarab Module 2 route aniqlanadi.

Boshlang'ich specificationda quyidagi thresholdlar ko'rsatilgan:

``` text
R&W:
27 questions
RawScore >= 15 → HARD
RawScore < 15 → EASY

Math:
22 questions
RawScore >= 12 → HARD
RawScore < 12 → EASY
```

Bu thresholdlar kod ichiga qattiq yozilmasligi kerak.

Admin/config orqali o'zgartiriladigan bo'lishi kerak.

------------------------------------------------------------------------

# 27. ADAPTIVE ROUTING SCREEN

Module 1dan keyin studentga:

``` text
Processing your module...

Please wait.
```

ko'rsatiladi.

Student routing algoritmini ko'rmaydi.

Backend:

``` text
Attempt
 ↓
Module 1 answers
 ↓
Raw score
 ↓
Routing rule
 ↓
Module 2
```

------------------------------------------------------------------------

# 28. BREAK

R&W tugagach:

``` text
┌────────────────────────────────────────────┐
│                BREAK                       │
│                                            │
│                  09:42                     │
│                                            │
│ Take a short break.                        │
│                                            │
│ Math section will start after the break.   │
└────────────────────────────────────────────┘
```

Break davomiyligi configuration orqali boshqariladi.

Boshlang'ich TZdagi qiymat:

``` text
10 minutes
```

------------------------------------------------------------------------

# 29. TEST AUTO-SAVE

Har bir javob saqlanadi.

Flow:

``` text
Student selects answer
        ↓
Frontend local state
        ↓
IndexedDB/local backup
        ↓
API
        ↓
Backend
        ↓
Database
```

Internet uzilsa:

``` text
Connection lost.
Your answers are saved locally.
Trying to reconnect...
```

ko'rsatiladi.

Internet qaytgach synchronization amalga oshiriladi.

------------------------------------------------------------------------

# 30. TIME EXPIRED

Timer tugaganda:

``` text
00:00
```

Backend testni avtomatik submit qiladi.

``` text
IN_PROGRESS
      ↓
AUTO_SUBMITTED
      ↓
SCORING
      ↓
COMPLETED
```

Student javoblari saqlanadi.

------------------------------------------------------------------------

# 31. TEST RESULT

Test tugagach:

``` text
┌────────────────────────────────────────────────────┐
│                 TEST COMPLETE                      │
├────────────────────────────────────────────────────┤
│                                                    │
│                  1180                              │
│                                                    │
│              SAT Estimated Score                   │
│                                                    │
│   Reading & Writing             Math               │
│        580                       600               │
│                                                    │
│   Previous: 1140                                  │
│   Improvement: +40                                │
│                                                    │
│ [Review Answers] [View Analytics] [Practice]      │
└────────────────────────────────────────────────────┘
```

------------------------------------------------------------------------

# 32. SCORING ENGINE

Raw score va scale score alohida saqlanadi.

Muhim:

**Scale conversion table backend configuration sifatida saqlanishi
kerak.**

Scoring algoritmi kodga qattiq bog'lanmasligi kerak.

Har bir result:

``` text
scoring_version
```

bilan saqlanadi.

Masalan:

``` text
SAT_2026_V1
```

Bu keyinchalik scoring algoritmi o'zgarganda eski natijalarni saqlab
qolish uchun kerak.

------------------------------------------------------------------------

# 33. TEST REVIEW

Student:

``` text
Review Test
```

bosadi.

``` text
Question #15

Your Answer:
B

Correct Answer:
C

Result:
WRONG

Time:
74 sec

Skill:
Linear Equations

Difficulty:
Medium

Explanation:
...
```

------------------------------------------------------------------------

# 34. EXPLANATION

Explanation:

-   text;
-   formula;
-   image;
-   video

bo'lishi mumkin.

Math:

``` text
Step 1:
...

Step 2:
...

Answer:
C
```

------------------------------------------------------------------------

# 35. MISTAKE BANK

Student noto'g'ri ishlagan savollar avtomatik `My Mistakes`ga tushadi.

``` text
My Mistakes

All
R&W
Math

Recent
Most Repeated
Unresolved
```

Misol:

``` text
Linear Equations
12 mistakes

Geometry
8 mistakes

Words in Context
5 mistakes
```

------------------------------------------------------------------------

# 36. MISTAKE REVIEW

Student mistake ustiga kiradi:

``` text
Question
Your Answer
Correct Answer
Why your answer is wrong
Correct solution
Explanation
```

Keyin:

``` text
[Practice This Skill]
```

------------------------------------------------------------------------

# 37. MISTAKE PRACTICE

Student:

``` text
[Practice My Mistakes]
```

bosadi.

System:

``` text
Wrong questions
+
same skill questions
+
similar difficulty
```

asosida practice yaratadi.

Faqat aynan bir xil savolni qaytarish majburiy emas.

------------------------------------------------------------------------

# 38. PRACTICE SAHIFASI

``` text
┌──────────────────────────────────────────────────────┐
│ Practice                                              │
├──────────────────────────────────────────────────────┤
│                                                      │
│ Choose a section                                     │
│                                                      │
│ ┌──────────────────┐    ┌──────────────────┐        │
│ │ Reading & Writing│    │ Math             │        │
│ │                  │    │                  │        │
│ │ 4 domains        │    │ 4 domains        │        │
│ └──────────────────┘    └──────────────────┘        │
│                                                      │
│ Quick Practice                                       │
│ [10 Questions] [20 Questions] [30 Questions]        │
│                                                      │
│ Recommended for You                                 │
│ [Geometry — 15 Q]                                    │
│ [Words in Context — 10 Q]                            │
│                                                      │
└──────────────────────────────────────────────────────┘
```

------------------------------------------------------------------------

# 39. PRACTICE TANLASH

Student quyidagilarni tanlaydi:

``` text
Section
Domain
Skill
Difficulty
Question Count
Time Limit
```

Misol:

``` text
Section: Math
Domain: Algebra
Skill: Linear Equations
Difficulty: Medium
Questions: 15
Time: 20 minutes
```

------------------------------------------------------------------------

# 40. PRACTICE MODES

## Quick Practice

10 ta savol.

## Standard Practice

20 ta savol.

## Intensive Practice

30+ savol.

## Timed Practice

Vaqt bilan.

## Untimed Practice

Vaqtsiz.

## Weakness Practice

Faqat weak skilllar.

## Mistake Practice

Oldingi xatolar.

------------------------------------------------------------------------

# 41. PRACTICE NATIJASI

``` text
Practice Complete

15 Questions
12 Correct
3 Wrong

Accuracy: 80%

Average Time: 58 sec

Skills:
Linear Equations: 80%
Systems: 75%

[Review]
[Practice Again]
[Dashboard]
```

------------------------------------------------------------------------

# 42. TOPIC STRUCTURE

Reading & Writing:

``` text
Reading & Writing
├── Information and Ideas
├── Craft and Structure
├── Expression of Ideas
└── Standard English Conventions
```

Math:

``` text
Math
├── Algebra
├── Advanced Math
├── Problem-Solving and Data Analysis
└── Geometry and Trigonometry
```

Domain/skilllar admin tomonidan boshqariladigan configuration bo'lishi
kerak.

------------------------------------------------------------------------

# 43. QUESTION BANK

Question Bank admin uchun asosiy CMS hisoblanadi.

``` text
┌─────────────────────────────────────────────────────────┐
│ Question Bank                         [+ New Question]  │
├─────────────────────────────────────────────────────────┤
│ Search: [________________________]                     │
│                                                         │
│ Section [All] Domain [All] Skill [All] Difficulty [All]│
│                                                         │
│ #   Section   Domain       Skill          Difficulty    │
│ 1   Math      Algebra      Linear Eq.     Medium        │
│ 2   R&W       Craft        Words Context  Hard          │
│ 3   Math      Geometry     Triangles      Easy          │
└─────────────────────────────────────────────────────────┘
```

------------------------------------------------------------------------

# 44. QUESTION YARATISH

Admin:

``` text
Section
Domain
Skill
Difficulty
Question Type
Passage
Question
Options
Correct Answer
Explanation
Video
Images
```

kiritadi.

------------------------------------------------------------------------

# 45. QUESTION EDITOR

Rich text editor kerak.

Qo'llab-quvvatlanadi:

-   bold;
-   italic;
-   lists;
-   tables;
-   image;
-   formula;
-   LaTeX;
-   links.

Math:

``` text
$x^2$

$$\frac{a}{b}$$
```

kabi formula formatlarini qo'llab-quvvatlash.

------------------------------------------------------------------------

# 46. QUESTION TYPE

## Multiple Choice

``` text
A
B
C
D
```

## SPR

Numeric answer.

------------------------------------------------------------------------

# 47. QUESTION METADATA

Har bir savol quyidagilar bilan bog'lanadi:

``` json
{
  "section": "MATH",
  "domain": "ALGEBRA",
  "skill": "LINEAR_EQUATIONS",
  "difficulty": "MEDIUM",
  "question_type": "MULTIPLE_CHOICE"
}
```

Bu ma'lumot analytics va practice uchun majburiy.

------------------------------------------------------------------------

# 48. QUESTION VERSIONING

Savol testda ishlatilganidan keyin matni o'zgartirilsa, eski attempt
buzilmasligi kerak.

Shuning uchun:

``` text
Question
 ├── Version 1
 ├── Version 2
 └── Version 3
```

saqlanadi.

Attempt eski version bilan bog'lanadi.

------------------------------------------------------------------------

# 49. QUESTION STATUS

``` text
DRAFT
ACTIVE
ARCHIVED
```

DRAFT practice/testga tushmaydi.

ACTIVE ishlatiladi.

ARCHIVED yangi testlarga tanlanmaydi.

------------------------------------------------------------------------

# 50. TEST BUILDER

Admin:

``` text
Create Test
```

bosadi.

``` text
Title
Test Type
Adaptive
Time
```

kiritadi.

Keyin:

``` text
Module 1
Module 2 Easy
Module 2 Hard
```

yaratiladi.

------------------------------------------------------------------------

# 51. MANUAL TEST BUILDER

Admin Question Bankdan savollarni tanlaydi.

``` text
Question Bank
       ↓
Select
       ↓
Add to Module
       ↓
Drag & Drop
       ↓
Save
```

Question order saqlanadi.

------------------------------------------------------------------------

# 52. AUTO TEST GENERATOR

Admin rule beradi:

``` text
Math
Algebra: 40%
Advanced Math: 30%
Geometry: 30%

Easy: 20%
Medium: 50%
Hard: 30%

Questions: 22
```

System mos savollarni Question Bankdan tanlaydi.

Agar yetarli savol topilmasa:

``` text
Not enough questions matching these rules.
Available: 17
Required: 22
```

chiqadi.

------------------------------------------------------------------------

# 53. TEST PUBLISH

Test status:

``` text
DRAFT
PUBLISHED
ARCHIVED
```

Publishdan oldin validation:

-   question yetarlimi;
-   correct answer bormi;
-   explanation bormi;
-   duplicate question bormi;
-   module configuration to'g'rimi;
-   time berilganmi.

------------------------------------------------------------------------

# 54. TEST ASSIGNMENT

Test:

-   barcha student;
-   group;
-   individual student

uchun berilishi mumkin.

Assignment:

``` text
Test
Target
Start Date
End Date
Attempts Allowed
```

------------------------------------------------------------------------

# 55. TEST ATTEMPT LIMIT

Admin:

``` text
Attempts Allowed:
1
```

yoki:

``` text
Attempts Allowed:
Unlimited
```

belgilashi mumkin.

Mock test odatda limit bilan berilishi mumkin.

Practice esa unlimited bo'lishi mumkin.

------------------------------------------------------------------------

# 56. MATERIALS

Student:

``` text
Materials
├── R&W
├── Math
├── Grammar
├── Vocabulary
├── Practice
└── Other
```

ko'radi.

Material:

``` text
Title
Description
File
Category
Created Date
```

------------------------------------------------------------------------

# 57. VIDEO EXPLANATION

Questionga video biriktiriladi:

``` text
Video URL
Title
Duration
```

Review vaqtida:

``` text
[Watch Explanation]
```

tugmasi ko'rinadi.

------------------------------------------------------------------------

# 58. STUDENT ANALYTICS

Analytics sahifasi:

``` text
┌────────────────────────────────────────────────────────┐
│ My Progress                                            │
├────────────────────────────────────────────────────────┤
│                                                        │
│ SAT Score Progress                                     │
│                                                        │
│ 1200 ┤                         ●                        │
│ 1150 ┤                    ●                            │
│ 1100 ┤               ●                                 │
│ 1050 ┤          ●                                      │
│      └────────────────────────────────────────────     │
│       Test1 Test2 Test3 Test4 Test5                    │
│                                                        │
│ R&W: 580        Math: 620                              │
│                                                        │
│ Accuracy: 76%                                         │
│ Average Time: 68 sec                                  │
└────────────────────────────────────────────────────────┘
```

------------------------------------------------------------------------

# 59. SCORE PROGRESSION

Saqlanadi:

``` text
Test Date
Total Score
R&W Score
Math Score
```

Chart orqali ko'rsatiladi.

------------------------------------------------------------------------

# 60. SKILL HEATMAP

Masalan:

``` text
Skill                         Accuracy

Linear Equations              43%   🔴
Geometry                      52%   🟡
Words in Context              76%   🟢
Inference                     81%   🟢
Grammar                       64%   🟡
```

Thresholdlar configuration orqali o'zgartirilishi mumkin.

Boshlang'ich recommendation:

``` text
< 50%   Weak
50-75%  Medium
> 75%   Strong
```

------------------------------------------------------------------------

# 61. WEAKNESS DETECTOR

System studentning barcha test/practice natijalarini agregatsiya qiladi.

Formula:

``` text
Accuracy =
Correct Answers / Total Attempted × 100
```

Masalan:

``` text
Geometry
Correct: 13
Attempted: 30

Accuracy = 43.3%
```

Weak skill sifatida belgilanadi.

------------------------------------------------------------------------

# 62. TARGETED PRACTICE GENERATOR

Agar studentda:

``` text
Accuracy < 50%
```

bo'lgan skilllar bo'lsa, system practice tavsiya qiladi.

Masalan:

``` text
Your weakest skill:
Geometry — 43%

Recommended:
15 questions
Medium difficulty

[Start]
```

------------------------------------------------------------------------

# 63. TIME ANALYTICS

Har bir question uchun:

``` text
time_spent_seconds
```

saqlanadi.

Analytics:

-   average time;
-   correct answer average;
-   wrong answer average;
-   time by difficulty;
-   time by skill.

------------------------------------------------------------------------

# 64. STUDENT PROFILE

``` text
┌──────────────────────────────────────────────────────┐
│ Student Profile                                      │
├──────────────────────────────────────────────────────┤
│                                                      │
│ Ali Valiyev                                          │
│ Phone: +998...                                       │
│ Group: SAT Intensive A                               │
│ Teacher: Teacher Name                                │
│                                                      │
│ Target Score: 1400                                  │
│ Current Score: 1180                                 │
│                                                      │
│ Attendance: 92%                                     │
│                                                      │
│ [Tests] [Analytics] [Mistakes] [Attendance]          │
└──────────────────────────────────────────────────────┘
```

------------------------------------------------------------------------

# 65. GROUP MANAGEMENT

Admin/Teacher:

``` text
Groups

SAT Intensive A
Teacher: Ali Teacher
Students: 18
Schedule:
Mon / Wed / Fri
18:00 - 20:00
```

Groupga kirganda:

``` text
Students
Attendance
Tests
Homework
Progress
```

tablari bo'ladi.

------------------------------------------------------------------------

# 66. GROUP STUDENTS

``` text
Name          Score    Attendance    Accuracy
Ali           1180     92%           76%
Vali          1260     96%           83%
Hasan         1040     78%           61%
```

Teacher weak studentlarni tez ko'rishi kerak.

------------------------------------------------------------------------

# 67. ATTENDANCE

Teacher:

``` text
Group: SAT Intensive A
Date: 08.10.2026

Ali       [PRESENT]
Vali      [PRESENT]
Hasan     [ABSENT]
Bek       [LATE] 15 min

                    [Save Attendance]
```

------------------------------------------------------------------------

# 68. ATTENDANCE STATUS

``` text
PRESENT
ABSENT
LATE
EXCUSED
```

LATE bo'lsa:

``` text
Late minutes
```

kiritiladi.

------------------------------------------------------------------------

# 69. ATTENDANCE HISTORY

Student:

``` text
Attendance

Present: 23
Absent: 1
Late: 2
Excused: 0

Rate: 92%
```

Calendar ko'rinishi ham bo'lishi mumkin.

------------------------------------------------------------------------

# 70. TEACHER DASHBOARD

``` text
┌───────────────────────────────────────────────────────┐
│ Teacher Dashboard                                     │
├───────────────────────────────────────────────────────┤
│                                                       │
│ My Groups: 3                                         │
│ My Students: 52                                      │
│                                                       │
│ Today's Classes                                      │
│ SAT Intensive A       18:00                           │
│ SAT Foundation B      20:00                           │
│                                                       │
│ Today's Attendance                                    │
│ [Take Attendance]                                     │
│                                                       │
│ Students Needing Attention                            │
│ Hasan — Score 1040 — Geometry 38%                    │
│                                                       │
│ Recent Results                                        │
│ Ali — 1180                                           │
│ Vali — 1260                                          │
└───────────────────────────────────────────────────────┘
```

------------------------------------------------------------------------

# 71. TEACHER STUDENT DETAIL

Teacher student profiliga kirganda:

``` text
Overview
Score
Progress
Skills
Tests
Mistakes
Attendance
Homework
```

ko'radi.

Teacher studentning answerini o'zgartira olmaydi.

------------------------------------------------------------------------

# 72. HOMEWORK

Teacher:

``` text
[Create Homework]
```

bosadi.

``` text
Title
Description
Group/Student
Questions/Test
Files
Deadline
```

belgilaydi.

------------------------------------------------------------------------

# 73. HOMEWORK STUDENT TOMONI

Student:

``` text
Homework

Algebra Practice
20 Questions
Deadline: 12 Oct
Status: Not Started

[Start]
```

------------------------------------------------------------------------

# 74. HOMEWORK RESULT

Teacher:

``` text
Student: Ali
Homework: Algebra Practice

Score: 17/20

Comment:
Good progress. Review linear equations.

[Save]
```

------------------------------------------------------------------------

# 75. PARENT DASHBOARD

Parent login:

``` text
┌─────────────────────────────────────────────────────┐
│ My Child                                            │
├─────────────────────────────────────────────────────┤
│                                                     │
│ Ali Valiyev                                         │
│                                                     │
│ Current SAT Score: 1180                            │
│ Target: 1400                                       │
│                                                     │
│ R&W: 580                                            │
│ Math: 600                                           │
│                                                     │
│ Attendance: 92%                                     │
│                                                     │
│ Last Test: 1180                                     │
│ Improvement: +40                                    │
│                                                     │
│ Weak Skills                                         │
│ Geometry — 43%                                      │
│                                                     │
└─────────────────────────────────────────────────────┘
```

------------------------------------------------------------------------

# 76. ADMIN DASHBOARD

Admin:

``` text
Students: 320
Teachers: 14
Groups: 18

Tests Taken Today: 86
Questions Solved Today: 2,430

Average SAT Score: 1140
Average Attendance: 91%

Top Performing Groups
Weakest Skills
Recent Activity
```

------------------------------------------------------------------------

# 77. ADMIN STUDENTS

Filter:

``` text
Name
Phone
Group
Teacher
Status
Score range
```

Actions:

``` text
View
Edit
Assign Group
Freeze
Archive
```

------------------------------------------------------------------------

# 78. ADMIN TEACHERS

Teacher:

``` text
Full Name
Phone
Email
Groups
Students
Status
```

------------------------------------------------------------------------

# 79. ADMIN GROUPS

Group:

``` text
Name
Course Type
Teacher
Assistant
Schedule
Room
Students
```

------------------------------------------------------------------------

# 80. SCHEDULE

Agar scheduling kerak bo'lsa:

``` text
Group
Teacher
Room
Days
Start Time
End Time
```

kiritiladi.

Collision tekshiriladi:

``` text
Teacher already has a class at this time.
```

yoki:

``` text
Room is already occupied.
```

Bu modul SAT test engine'dan alohida yordamchi modul hisoblanadi.

------------------------------------------------------------------------

# 81. TELEGRAM NOTIFICATION

Telegram ikki asosiy maqsadda ishlatiladi.

## Parent

Attendance:

``` text
⚠️ Farzandingiz Hasan bugungi
18:00 dagi SAT darsiga kelmadi.
```

## Student

Practice reminder:

``` text
🎯 Today's Practice

Geometry — 10 questions

[Start Practice]
```

------------------------------------------------------------------------

# 82. TEST RESULT TELEGRAM

Test tugagach parentga:

``` text
SAT Mock Result

Student: Ali Valiyev

Total: 1180
R&W: 580
Math: 600

Previous: 1140
Improvement: +40
```

yuborish mumkin.

PDF report keyingi bosqichda qo'shilishi mumkin.

------------------------------------------------------------------------

# 83. NOTIFICATION SETTINGS

User:

``` text
Notifications

☑ Test Results
☑ Attendance
☑ Homework
☑ Practice Reminder
```

sozlamalarini boshqarishi mumkin.

------------------------------------------------------------------------

# 84. DATABASE ARXITEKTURA

Asosiy database:

**PostgreSQL**

Entitylar:

``` text
users
roles

students
parents
parent_students

teachers
groups
group_members
enrollments
attendance

questions
question_versions
question_options
question_media
skills
domains

tests
test_modules
test_questions
test_assignments

test_attempts
attempt_questions
student_answers

practice_sessions
practice_questions

student_mistakes
student_skill_statistics
score_history

materials
videos

homeworks
homework_submissions

notifications
notification_logs

audit_logs
```

------------------------------------------------------------------------

# 85. USERS

``` sql
users
-----
id
full_name
phone
email
password_hash
role
is_active
created_at
updated_at
```

------------------------------------------------------------------------

# 86. STUDENTS

``` sql
students
--------
id
user_id
target_score
status
created_at
updated_at
```

------------------------------------------------------------------------

# 87. GROUPS

``` sql
groups
------
id
name
course_type
teacher_id
assistant_id
room_id
created_at
updated_at
```

------------------------------------------------------------------------

# 88. ENROLLMENTS

``` sql
enrollments
-----------
id
student_id
group_id
start_date
end_date
status
```

Studentning group history saqlanadi.

------------------------------------------------------------------------

# 89. ATTENDANCE

``` sql
attendance
----------
id
group_id
student_id
date
status
late_minutes
created_by
created_at
```

Unique:

``` text
group_id + student_id + date
```

------------------------------------------------------------------------

# 90. QUESTIONS

``` sql
questions
---------
id
section
domain_id
skill_id
difficulty
question_type
passage
question_text
explanation
status
created_by
created_at
updated_at
```

------------------------------------------------------------------------

# 91. QUESTION OPTIONS

``` sql
question_options
----------------
id
question_id
option_key
option_text
is_correct
```

------------------------------------------------------------------------

# 92. QUESTION MEDIA

``` sql
question_media
--------------
id
question_id
type
storage_key
url
sort_order
```

------------------------------------------------------------------------

# 93. TESTS

``` sql
tests
-----
id
title
type
is_adaptive
status
scoring_version
created_by
created_at
updated_at
```

------------------------------------------------------------------------

# 94. TEST MODULES

``` sql
test_modules
------------
id
test_id
module_number
section
route_type
time_limit_seconds
order
```

------------------------------------------------------------------------

# 95. TEST QUESTIONS

``` sql
test_questions
--------------
id
test_module_id
question_id
question_order
```

------------------------------------------------------------------------

# 96. TEST ASSIGNMENTS

``` sql
test_assignments
----------------
id
test_id
student_id nullable
group_id nullable
start_at
end_at
attempt_limit
```

------------------------------------------------------------------------

# 97. TEST ATTEMPTS

``` sql
test_attempts
-------------
id
student_id
test_id
status
started_at
completed_at
total_score
rw_score
math_score
scoring_version
```

------------------------------------------------------------------------

# 98. ATTEMPT QUESTIONS

``` sql
attempt_questions
-----------------
id
attempt_id
question_id
question_version_id
question_order
module_number
is_flagged
```

------------------------------------------------------------------------

# 99. STUDENT ANSWERS

``` sql
student_answers
---------------
id
attempt_question_id
student_answer
is_correct
time_spent_seconds
answered_at
```

------------------------------------------------------------------------

# 100. PRACTICE SESSIONS

``` sql
practice_sessions
-----------------
id
student_id
section
domain_id
skill_id
difficulty
question_count
time_limit_seconds
status
started_at
completed_at
```

------------------------------------------------------------------------

# 101. STUDENT MISTAKES

``` sql
student_mistakes
----------------
id
student_id
question_id
last_attempt_id
mistake_count
is_resolved
last_attempted_at
```

------------------------------------------------------------------------

# 102. SKILL STATISTICS

``` sql
student_skill_statistics
-----------------------
id
student_id
skill_id
attempted_count
correct_count
accuracy
average_time_seconds
updated_at
```

------------------------------------------------------------------------

# 103. SCORE HISTORY

``` sql
score_history
-------------
id
student_id
attempt_id
total_score
rw_score
math_score
created_at
```

------------------------------------------------------------------------

# 104. API ARXITEKTURA

Base:

``` text
/api/v1
```

Authentication:

``` http
POST /auth/login
POST /auth/register
POST /auth/refresh
POST /auth/logout
```

Student:

``` http
GET /student/dashboard
GET /student/tests
GET /student/tests/{id}
POST /student/tests/{id}/start

GET /student/attempts/{id}
POST /student/attempts/{id}/answers
POST /student/attempts/{id}/flag
POST /student/attempts/{id}/submit

GET /student/attempts/{id}/result
GET /student/attempts/{id}/review
```

Practice:

``` http
GET /practice/sections
GET /practice/domains
GET /practice/skills

POST /practice/start
POST /practice/{id}/answer
POST /practice/{id}/finish

GET /student/mistakes
POST /student/mistakes/practice
```

Analytics:

``` http
GET /student/analytics
GET /student/analytics/scores
GET /student/analytics/skills
GET /student/analytics/time
```

Teacher:

``` http
GET /teacher/groups
GET /teacher/groups/{id}/students
GET /teacher/students/{id}
GET /teacher/students/{id}/analytics

GET /teacher/attendance
POST /teacher/attendance

POST /teacher/homeworks
GET /teacher/homeworks
```

Admin:

``` http
GET /admin/questions
POST /admin/questions
GET /admin/questions/{id}
PUT /admin/questions/{id}
POST /admin/questions/{id}/archive

GET /admin/tests
POST /admin/tests
PUT /admin/tests/{id}
POST /admin/tests/{id}/publish
POST /admin/tests/{id}/archive

GET /admin/students
POST /admin/students

GET /admin/groups
POST /admin/groups
```

------------------------------------------------------------------------

# 105. BACKEND MODULAR ARXITEKTURA

Tavsiya:

``` text
API
Application
Domain
Infrastructure
```

Business modullar:

``` text
Identity
Students
Groups
Attendance

QuestionBank
Tests
TestAttempts
Scoring

Practice
Mistakes
Analytics

Materials
Homework
Notifications
```

------------------------------------------------------------------------

# 106. TEST ENGINE ARXITEKTURASI

``` text
Test API
   ↓
Test Session Service
   ↓
Attempt Service
   ↓
Question Service
   ↓
Answer Service
   ↓
Scoring Service
   ↓
Analytics Service
```

------------------------------------------------------------------------

# 107. PRACTICE ENGINE ARXITEKTURASI

``` text
Student Skill Statistics
          ↓
Weakness Detector
          ↓
Question Filter
          ↓
Question Selection
          ↓
Practice Session
          ↓
Answers
          ↓
Statistics Update
```

------------------------------------------------------------------------

# 108. QUESTION SELECTION

Question tanlashda:

``` text
Section
Domain
Skill
Difficulty
Question Status
Previous Attempt
```

hisobga olinadi.

Practice uchun allaqachon to'g'ri ishlangan savollarni doimiy
takrorlashdan qochish kerak.

------------------------------------------------------------------------

# 109. RECOMMENDATION ENGINE

Recommendation:

``` text
1. Weak skills
2. Recent mistakes
3. Low accuracy
4. High average time
```

asosida ishlaydi.

Priority:

``` text
Weak + Slow + Frequently Wrong
```

bo'lgan skill yuqori priority oladi.

------------------------------------------------------------------------

# 110. AUDIT LOG

Quyidagilar log qilinadi:

``` text
Question Created
Question Updated
Question Archived
Test Published
Test Archived
Student Created
Student Group Changed
Attendance Changed
Score Recalculated
```

------------------------------------------------------------------------

# 111. SECURITY

Majburiy:

-   HTTPS;
-   password hashing;
-   JWT;
-   refresh token;
-   RBAC;
-   rate limiting;
-   request validation;
-   SQL injection protection;
-   XSS protection;
-   secure file upload;
-   authorization checks;
-   audit log.

Student API orqali boshqa studentning ma'lumotini ololmasligi kerak.

------------------------------------------------------------------------

# 112. TEST SECURITY

Test attempt uchun:

``` text
student_id
attempt_id
test_id
session_id
```

tekshiriladi.

Har bir requestda:

``` text
Current User
+
Attempt Owner
+
Attempt Status
+
Test Access
```

tekshiriladi.

------------------------------------------------------------------------

# 113. PERFORMANCE

Maqsad:

``` text
Dashboard API       < 500 ms
Question Load       < 500 ms
Answer Save         < 300 ms
Analytics           < 1 sec
```

Katta analytics querylari background aggregation yoki cache orqali
optimallashtirilishi mumkin.

------------------------------------------------------------------------

# 114. CACHE

Redis:

-   active session;
-   timer state;
-   rate limit;
-   temporary synchronization;
-   notification queue

uchun ishlatilishi mumkin.

Final result PostgreSQLda saqlanadi.

------------------------------------------------------------------------

# 115. FILE STORAGE

PDF, image, video kabi fayllar object storageda saqlanadi.

Masalan:

``` text
S3-compatible storage
```

Database:

``` text
storage_key
```

saqlaydi.

------------------------------------------------------------------------

# 116. BACKGROUND JOBS

Kerakli joblar:

``` text
ScoreCalculationJob
AnalyticsCalculationJob
RecommendationJob
TelegramNotificationJob
CleanupExpiredSessionsJob
```

------------------------------------------------------------------------

# 117. ERROR HANDLING

API standard error:

``` json
{
  "success": false,
  "code": "TEST_SESSION_EXPIRED",
  "message": "Test session has expired."
}
```

Frontend user-friendly message ko'rsatadi.

------------------------------------------------------------------------

# 118. TEST HOLATLARI

``` text
DRAFT
READY
PUBLISHED
IN_PROGRESS
COMPLETED
EXPIRED
ARCHIVED
```

------------------------------------------------------------------------

# 119. STUDENT HOLATLARI

``` text
APPLICANT
ACTIVE
FROZEN
DROPPED
GRADUATED
```

------------------------------------------------------------------------

# 120. HOMEWORK HOLATLARI

``` text
NOT_STARTED
IN_PROGRESS
SUBMITTED
REVIEWED
OVERDUE
```

------------------------------------------------------------------------

# 121. DAVOMAT HOLATLARI

``` text
PRESENT
ABSENT
LATE
EXCUSED
```

------------------------------------------------------------------------

# 122. RESPONSIVE DESIGN

Platforma:

-   Desktop;
-   Laptop;
-   Tablet;
-   Mobile

uchun responsive bo'ladi.

Lekin **Full SAT Test interface desktop-first** bo'ladi.

Sababi testdagi split-screen va katta passage/question UI mobileda
noqulay.

Mobileda:

-   Dashboard;
-   Practice;
-   Analytics;
-   Materials

ishlashi kerak.

Full Mock test uchun mobile support alohida UX qarori sifatida
belgilanadi.

------------------------------------------------------------------------

# 123. ACCESSIBILITY

Kerakli minimum:

-   keyboard navigation;
-   visible focus;
-   readable contrast;
-   button labels;
-   semantic HTML;
-   screen readerga mos label.

------------------------------------------------------------------------

# 124. TESTDA KEYBOARD

Asosiy shortcutlar:

``` text
Next
Back
Flag
Question Grid
```

uchun keyboard navigation bo'lishi mumkin.

Shortcutlar help menu orqali ko'rsatiladi.

------------------------------------------------------------------------

# 125. OFFLINE / CONNECTION RECOVERY

Full offline test qilish shart emas.

Lekin temporary connection lossda:

``` text
Current answers
```

local storage/IndexedDBda saqlanadi.

Reconnection:

``` text
Local answers
     ↓
Compare server version
     ↓
Sync
     ↓
Server confirmation
```

------------------------------------------------------------------------

# 126. DATA CONSISTENCY

Server authoritative hisoblanadi.

Agar client va server answerlari farq qilsa:

``` text
server timestamp
```

asosida conflict resolution qilinadi.

------------------------------------------------------------------------

# 127. TEST RESULTNI QAYTA HISOBLASH

Admin score calculationni qayta ishga tushira olishi mumkin.

Bunda:

``` text
old score
new score
reason
admin
timestamp
```

audit logga yoziladi.

------------------------------------------------------------------------

# 128. CONTENT VALIDATION

Test publishdan oldin:

### Question

-   question text mavjud;
-   correct answer mavjud;
-   options mavjud;
-   skill mavjud;
-   difficulty mavjud.

### Test

-   module mavjud;
-   question count yetarli;
-   time limit mavjud;
-   scoring version mavjud.

Validationdan o'tmasa:

``` text
Cannot publish test.
3 questions are incomplete.
```

------------------------------------------------------------------------

# 129. ADMIN TEST PREVIEW

Publishdan oldin Admin:

``` text
[Preview Test]
```

bosishi mumkin.

Student ko'radigan interfeysning o'zi previewda ochiladi.

Bu juda muhim, chunki admin testni publish qilishdan oldin:

-   layout;
-   formula;
-   image;
-   answer;
-   explanation;
-   question order

ni tekshiradi.

------------------------------------------------------------------------

# 130. QUESTION PREVIEW

Admin:

``` text
[Preview]
```

bosganda student ko'radigan savol oynasi ochiladi.

------------------------------------------------------------------------

# 131. DUPLICATE QUESTION CHECK

Test builderda bir xil question bir module ichida ikki marta
qo'shilmasligi kerak.

System:

``` text
Question already exists in this module.
```

deydi.

------------------------------------------------------------------------

# 132. STUDENT PROGRESS LOGIC

Student progress faqat test scorega qarab hisoblanmaydi.

Quyidagilar ishlatiladi:

``` text
SAT Score
Skill Accuracy
Practice Count
Questions Solved
Mistakes
Average Time
Test Frequency
Attendance
```

------------------------------------------------------------------------

# 133. STUDENT MAIN KPI

Dashboardda quyidagi KPIlar:

``` text
Current Score
Target Score
Score Improvement
Overall Accuracy
Questions Solved
Practice Streak
Attendance
```

------------------------------------------------------------------------

# 134. PRACTICE STREAK

Agar student ketma-ket kunlar practice qilsa:

``` text
5 day streak
```

ko'rsatilishi mumkin.

Bu gamification sifatida optional.

------------------------------------------------------------------------

# 135. GAMIFICATION

Keyingi bosqich:

``` text
XP
Badges
Streak
Leaderboard
Achievements
```

qo'shilishi mumkin.

MVP uchun majburiy emas.

------------------------------------------------------------------------

# 136. LEADERBOARD

Agar markaz xohlasa:

``` text
Group Ranking
```

bo'lishi mumkin.

Lekin studentlarning natijasini ommaviy ko'rsatishda privacy qoidalari
hisobga olinadi.

MVP uchun optional.

------------------------------------------------------------------------

# 137. BENCHMARK

Analyticsda:

``` text
My Score
Group Average
Center Average
```

ko'rsatish mumkin.

Lekin bu funksiyani faqat yetarli statistik ma'lumot yig'ilgandan keyin
yoqish tavsiya qilinadi.

------------------------------------------------------------------------

# 138. PARENT PRIVACY

Parent faqat o'ziga biriktirilgan studentni ko'radi.

API:

``` text
GET /parent/children
GET /parent/children/{id}/progress
```

har doim authorization tekshiradi.

------------------------------------------------------------------------

# 139. MULTI-STUDENT PARENT

Bitta parentda bir nechta child bo'lishi mumkin.

``` text
Parent
├── Child A
└── Child B
```

Dashboardda child switcher:

``` text
[Ali ▼]
```

bo'ladi.

------------------------------------------------------------------------

# 140. MULTI-GROUP STUDENT

Student bir nechta kurs/guruhda bo'lishi mumkin.

Masalan:

``` text
SAT Math
SAT R&W
```

Enrollment alohida saqlanadi.

------------------------------------------------------------------------

# 141. ADMIN SETTINGS

Admin:

``` text
SAT Configuration
├── Sections
├── Domains
├── Skills
├── Difficulties
├── Test Durations
├── Adaptive Rules
├── Scoring Versions
└── Notification Settings
```

boshqaradi.

------------------------------------------------------------------------

# 142. ADAPTIVE RULE CONFIGURATION

Masalan:

``` text
Section: R&W
Module: 1
Question Count: 27
Cutoff: 15
Low Route: EASY
High Route: HARD
```

Math:

``` text
Question Count: 22
Cutoff: 12
```

Bu qiymatlar database/configda saqlanadi.

------------------------------------------------------------------------

# 143. SCORING CONFIGURATION

Scoring table:

``` text
scoring_version
section
module_route
raw_score
scaled_score
```

ko'rinishida versionlanadi.

Muhim: platformada ishlatiladigan scoring table biznes tomonidan
tasdiqlangan jadval asosida kiritiladi.

------------------------------------------------------------------------

# 144. SAT CONTENT HIERARCHY

``` text
Section
  ↓
Domain
  ↓
Skill
  ↓
Question
```

Misol:

``` text
Math
  ↓
Algebra
  ↓
Linear Equations
  ↓
Question #120
```

------------------------------------------------------------------------

# 145. CONTENT REUSE

Bitta question:

``` text
Question #120
```

quyidagilarda ishlatilishi mumkin:

``` text
Diagnostic
Practice
Mock Test
Topic Test
Teacher Homework
```

Lekin har bir attempt alohida answer/result saqlaydi.

------------------------------------------------------------------------

# 146. QUESTION RANDOMIZATION

Practice uchun savollar random tanlanishi mumkin.

Test uchun esa admin belgilagan order saqlanadi.

Agar randomization yoqilgan bo'lsa:

``` text
random_seed
```

saqlash tavsiya qilinadi.

Bu keyinchalik attemptni audit qilishga yordam beradi.

------------------------------------------------------------------------

# 147. TEST ASSIGNMENT SCREEN

Teacher:

``` text
Test: Algebra Practice

Assign to:
○ Group
○ Individual Student

Group:
[ SAT Intensive A ]

Start:
08.10.2026 10:00

Deadline:
12.10.2026 23:59

Attempts:
2

[Assign]
```

------------------------------------------------------------------------

# 148. STUDENT ASSIGNED TEST

Student Dashboard:

``` text
Assigned by Teacher

Algebra Practice
Deadline: 12 Oct
Attempts: 0/2

[Start]
```

Deadline o'tganidan keyin:

``` text
Expired
```

ko'rsatiladi.

------------------------------------------------------------------------

# 149. ANALYTICS FILTERS

Teacher/Admin:

``` text
Date Range
Group
Teacher
Student
Section
Domain
Skill
Difficulty
Test
```

bo'yicha filter qila oladi.

------------------------------------------------------------------------

# 150. QUESTION ANALYTICS

Admin:

``` text
Question #120

Attempts: 340
Correct: 102
Wrong: 238
Accuracy: 30%
Average Time: 82 sec
```

ko'radi.

Bu savolni qayta ko'rib chiqish uchun signal bo'ladi.

------------------------------------------------------------------------

# 151. CONTENT QUALITY CONTROL

Admin savolni:

``` text
DRAFT
REVIEW
APPROVED
ACTIVE
ARCHIVED
```

workflow orqali boshqarishi mumkin.

MVPda kamida:

``` text
DRAFT
ACTIVE
ARCHIVED
```

bo'lishi shart.

------------------------------------------------------------------------

# 152. TEACHER CONTENT ACCESS

Teacherga Question Bank access berish mumkin.

Agar berilsa:

-   question create;
-   question edit;
-   question preview

qila oladi.

Lekin:

``` text
Delete
Publish
Scoring Configuration
```

faqat Admin huquqida bo'lishi mumkin.

------------------------------------------------------------------------

# 153. ROLE PERMISSION MATRIX

  Funksiya               Admin           Teacher         Assistant   Student   Parent
  -------------------- ------- ----------------- ----------------- --------- --------
  Student management         ✓        O'z guruhi        O'z guruhi        \-       \-
  Group management           ✓              View              View        \-       \-
  Attendance                 ✓                 ✓                 ✓      View     View
  Question Bank              ✓          Optional                \-        \-       \-
  Test Builder               ✓                 ✓                \-        \-       \-
  Test Taking               \-          Optional          Optional         ✓       \-
  Analytics                  ✓   O'z studentlari   O'z studentlari      O'zi    Child
  Homework                   ✓                 ✓                 ✓         ✓     View
  Materials                  ✓                 ✓                 ✓         ✓     View
  Scoring Config             ✓                \-                \-        \-       \-
  User Roles                 ✓                \-                \-        \-       \-

------------------------------------------------------------------------

# 154. API AUTHORIZATION QOIDASI

Har bir request:

``` text
Authentication
      ↓
Role Check
      ↓
Resource Ownership
      ↓
Business Rule
      ↓
Response
```

orqali tekshiriladi.

Faqat role tekshirish yetarli emas.

Masalan Teacher `student_id` yuborib boshqa groupdagi studentni ko'ra
olmasligi kerak.

------------------------------------------------------------------------

# 155. ERROR STATES

Frontend barcha muhim state'larni ko'rsatishi kerak.

### Loading

``` text
Loading...
```

### Empty

``` text
No tests available.
```

### Error

``` text
Something went wrong.
[Try Again]
```

### Network

``` text
Connection lost.
Trying to reconnect...
```

### Permission

``` text
You don't have permission to access this page.
```

------------------------------------------------------------------------

# 156. EMPTY STATES

Masalan Mistakes:

``` text
🎉 No mistakes yet!

You haven't made any mistakes.
Keep practicing.
```

Analytics:

``` text
Not enough data yet.

Complete at least one test to see your progress.
```

------------------------------------------------------------------------

# 157. STUDENT ONBOARDING

Yangi student:

``` text
Register
 ↓
Profile
 ↓
Target Score
 ↓
Diagnostic Test
 ↓
Initial Analytics
 ↓
Recommended Study Plan
```

Target score optional.

Diagnostic test recommendation qilinadi.

------------------------------------------------------------------------

# 158. DIAGNOSTIC RESULT

``` text
Your Starting Point

Total: 1040

R&W: 510
Math: 530

Weak Skills:
1. Geometry 38%
2. Words in Context 44%
3. Algebra 49%

Recommended:
Geometry Practice
15 Questions
```

Bu studentga keyingi nima qilishini aniq ko'rsatadi.

------------------------------------------------------------------------

# 159. STUDY PLAN

MVPda avtomatik full calendar plan shart emas.

Lekin:

``` text
Recommended Practice
```

bo'lishi kerak.

Keyingi versiyada:

``` text
7 Day Plan
30 Day Plan
Target Score Plan
```

qo'shilishi mumkin.

------------------------------------------------------------------------

# 160. ASOSIY STUDENT USER FLOW

``` text
LOGIN
  ↓
DASHBOARD
  ↓
RECOMMENDED PRACTICE
  ↓
PRACTICE
  ↓
RESULT
  ↓
MISTAKES
  ↓
REVIEW
  ↓
FULL MOCK
  ↓
SCORE
  ↓
ANALYTICS
  ↓
NEXT RECOMMENDATION
```

------------------------------------------------------------------------

# 161. TEACHER USER FLOW

``` text
LOGIN
  ↓
TEACHER DASHBOARD
  ↓
GROUP
  ↓
STUDENTS
  ↓
ATTENDANCE
  ↓
STUDENT PROGRESS
  ↓
WEAK SKILLS
  ↓
ASSIGN PRACTICE / TEST
  ↓
RESULT
```

------------------------------------------------------------------------

# 162. ADMIN USER FLOW

``` text
LOGIN
  ↓
ADMIN DASHBOARD
  ↓
QUESTION BANK
  ↓
CREATE QUESTIONS
  ↓
TEST BUILDER
  ↓
PREVIEW
  ↓
PUBLISH
  ↓
ASSIGN
  ↓
MONITOR RESULTS
```

------------------------------------------------------------------------

# 163. FULL PRODUCT FLOW

``` text
                    SAT PLATFORM
                         │
        ┌────────────────┼────────────────┐
        │                │                │
     STUDENT           TEACHER          ADMIN
        │                │                │
        │                │                ├── Question Bank
        │                │                ├── Test Builder
        │                │                ├── Students
        │                │                └── Groups
        │                │
        ├── Diagnostic   ├── Attendance
        ├── Practice     ├── Students
        ├── Mock Test    ├── Progress
        ├── Mistakes     └── Assignment
        └── Analytics
                 │
                 ▼
          SAT ENGINE
                 │
       ┌─────────┼─────────┐
       │         │         │
    Questions  Scoring   Adaptive
       │         │         │
       └─────────┼─────────┘
                 │
                 ▼
             ANALYTICS
                 │
                 ▼
        RECOMMENDATION
```

------------------------------------------------------------------------

# 164. MVP DEVELOPMENT PRIORITY

## PHASE 1 --- CORE SAT ENGINE

1.  Authentication
2.  Question Bank
3.  Test Builder
4.  Test Attempt
5.  Timer
6.  Answers
7.  Submit
8.  Scoring
9.  Result
10. Review

Bu platformaning eng muhim qismi.

------------------------------------------------------------------------

## PHASE 2 --- PRACTICE

1.  Topic Practice
2.  Skill Practice
3.  Custom Practice
4.  Mistake Bank
5.  Practice Analytics
6.  Weakness Detection

------------------------------------------------------------------------

## PHASE 3 --- ACADEMIC MANAGEMENT

1.  Students
2.  Groups
3.  Teachers
4.  Enrollment
5.  Attendance
6.  Teacher Dashboard

------------------------------------------------------------------------

## PHASE 4 --- ANALYTICS

1.  Score progression
2.  Skill analytics
3.  Time analytics
4.  Question analytics
5.  Recommendation

------------------------------------------------------------------------

## PHASE 5 --- PARENT / TELEGRAM

1.  Parent dashboard
2.  Telegram attendance
3.  Result notification
4.  Practice reminder

------------------------------------------------------------------------

# 165. QABUL QILISH MEZONLARI (ACCEPTANCE CRITERIA)

## Test

-   Student testni boshlay oladi.
-   Timer ishlaydi.
-   Answer saqlanadi.
-   Flag ishlaydi.
-   Question Grid ishlaydi.
-   Back/Next ishlaydi.
-   Module Review ishlaydi.
-   Adaptive routing ishlaydi.
-   Break ishlaydi.
-   Time expired auto-submit qiladi.
-   Score chiqadi.
-   Review ishlaydi.

## Practice

-   Student skill tanlaydi.
-   System savollar beradi.
-   Result hisoblanadi.
-   Mistake saqlanadi.
-   Weak skill aniqlanadi.
-   Practice qayta boshlanadi.

## Teacher

-   Group ko'radi.
-   Student ko'radi.
-   Attendance belgilaydi.
-   Student progressini ko'radi.
-   Test assign qiladi.

## Admin

-   Question yaratadi.
-   Question edit qiladi.
-   Test yaratadi.
-   Test preview qiladi.
-   Test publish qiladi.
-   Student/group boshqaradi.

## Parent

-   Child score ko'radi.
-   Progress ko'radi.
-   Attendance ko'radi.

------------------------------------------------------------------------

# 166. MUHIM TEXNIK QOIDALAR

1.  Scoring algoritmi hard-code qilinmasin.
2.  Adaptive threshold configurationda saqlansin.
3.  Question versioning bo'lsin.
4.  Attempt vaqtida question snapshot/version saqlansin.
5.  Student answerlari yo'qolmasin.
6.  Timer server authoritative bo'lsin.
7.  Teacher boshqa teacher studentini ko'rmasin.
8.  Parent faqat o'z childini ko'rsin.
9.  Test publishdan oldin validation bo'lsin.
10. Question archive qilinganda eski attemptlar buzilmasin.
11. Test natijalari o'zgartirilsa audit log yozilsin.
12. Analytics real test/practice data asosida hisoblanishi kerak.

------------------------------------------------------------------------

# 167. LOYIHANING ASOSIY PRINSIPI

Platforma oddiy:

> "Online test ishlash sayti"

bo'lmasligi kerak.

U quyidagi ta'lim siklini amalga oshiradigan tizim bo'lishi kerak:

``` text
TEST
 ↓
RESULT
 ↓
ERROR ANALYSIS
 ↓
WEAK SKILL
 ↓
TARGETED PRACTICE
 ↓
RETEST
 ↓
SCORE IMPROVEMENT
```

Student har safar saytga kirganda:

> "Bugun nimani mashq qilishim kerak?"

degan savolga Dashboard javob berishi kerak.

------------------------------------------------------------------------

# 168. YAKUNIY MODUL STRUKTURASI

``` text
SAT PREPARATION PLATFORM
│
├── 01. AUTHENTICATION
│
├── 02. STUDENT DASHBOARD
│
├── 03. SAT TEST ENGINE
│   ├── Pre-Test Check
│   ├── Test Session
│   ├── Timer
│   ├── Question UI
│   ├── Navigation
│   ├── Flag
│   ├── Tools
│   ├── Adaptive Routing
│   ├── Break
│   ├── Auto Save
│   └── Auto Submit
│
├── 04. SCORING ENGINE
│
├── 05. TEST REVIEW
│
├── 06. PRACTICE ENGINE
│   ├── Topic
│   ├── Skill
│   ├── Custom
│   ├── Timed
│   ├── Weakness
│   └── Mistakes
│
├── 07. QUESTION BANK
│
├── 08. TEST BUILDER
│
├── 09. STUDENT ANALYTICS
│
├── 10. RECOMMENDATION ENGINE
│
├── 11. STUDENT MANAGEMENT
│
├── 12. GROUP MANAGEMENT
│
├── 13. ATTENDANCE
│
├── 14. TEACHER PANEL
│
├── 15. ADMIN PANEL
│
├── 16. PARENT PANEL
│
├── 17. HOMEWORK
│
├── 18. MATERIALS
│
├── 19. TELEGRAM NOTIFICATIONS
│
├── 20. AUDIT & SECURITY
│
└── 21. REPORTING
```

------------------------------------------------------------------------

# 169. MUHIM IZOH

Ushbu TZda platformaning **asosiy maqsadi SATga tayyorlash** sifatida
belgilangan.

Shuning uchun:

``` text
CRM = yordamchi modul
Attendance = yordamchi modul
Homework = yordamchi modul
Parent = yordamchi modul
Telegram = notification modul
```

asosiy yadro esa:

``` text
Question Bank
      ↓
Test Engine
      ↓
Practice Engine
      ↓
Scoring
      ↓
Mistake Analysis
      ↓
Skill Analytics
      ↓
Recommendation
      ↓
Student Improvement
```

hisoblanadi.

------------------------------------------------------------------------

# 170. DEVELOPMENT NATIJASIDA TALABA NIMA QILA OLISHI KERAK?

Talaba saytga kiradi:

``` text
1. Login qiladi
2. Dashboardni ko‘radi
3. Diagnostic test ishlaydi
4. 1050 score oladi
5. System Geometry zaifligini ko‘rsatadi
6. Geometry Practice tavsiya qiladi
7. 15 ta savol ishlaydi
8. 10/15 qiladi
9. Xatolarini ko‘radi
10. Explanation o‘qiydi
11. Yana Geometry practice qiladi
12. Full Mock test ishlaydi
13. 1120 score oladi
14. Progress chartda +70 ko‘radi
15. System keyingi zaif skillni ko‘rsatadi
16. Shu siklni davom ettiradi
```

Natijada sayt:

``` text
TEST PLATFORM
```

emas, balki:

``` text
SAT PREPARATION SYSTEM
```

sifatida ishlaydi.

------------------------------------------------------------------------

# 171. YAKUNIY MAHSULOT TA'RIFI

**Digital SAT Preparation Platform** --- SATga tayyorlanayotgan o'quvchi
uchun test ishlash, mavzu va skill bo'yicha mashq qilish, xatolarni
tahlil qilish, individual zaifliklarni aniqlash, progressni kuzatish va
qayta test qilish imkonini beruvchi web-platforma.

O'qituvchi va administrator platformadagi SAT kontentini, testlarni,
o'quvchilarni, guruhlarni va davomatni boshqaradi.

Platformaning eng asosiy funksiyasi:

**Student → Practice → Test → Analysis → Improvement**

siklini doimiy ishlatishdir.
