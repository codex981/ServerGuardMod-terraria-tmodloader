# 🛡 ServerGuard Mod - دليل التثبيت الكامل

## المتطلبات
- tModLoader 1.4.4.9 (v2026.3.3.0)
- .NET 6 SDK أو أعلى
- Visual Studio أو Visual Studio Code

---

## 📁 الخطوة 1: مكان الملفات

ضع مجلد `ServerGuardMod` كاملاً في:
```
Windows: %USERPROFILE%\Documents\My Games\Terraria\tModLoader\ModSources\
Linux:   ~/.local/share/Terraria/tModLoader/ModSources/
```

الهيكل النهائي يجب أن يكون:
```
ModSources/
└── ServerGuardMod/
    ├── ServerGuardMod.cs            ← الكلاس الرئيسي
    ├── build.txt                    ← معلومات المود
    ├── description.txt
    ├── Common/
    │   ├── Systems/
    │   │   ├── AccountDatabase.cs   ← قاعدة البيانات
    │   │   ├── ClientLoginSystem.cs ← نظام الدخول (كلايانت)
    │   │   ├── LoginUI.cs           ← الواجهة المرئية
    │   │   └── ServerLoadSystem.cs  ← تحميل السيرفر
    │   ├── Players/
    │   │   └── SGPlayer.cs          ← بيانات اللاعب
    │   ├── Commands/
    │   │   └── SGCommand.cs         ← أوامر الأدمن
    │   ├── AntiCheat/
    │   │   ├── MemoryWatcher.cs     ← مراقبة الذاكرة
    │   │   └── PacketFilter.cs      ← فلتر الباكيتات
    │   └── Network/
    │       ├── PacketType.cs        ← أنواع الرسائل
    │       └── PacketHandler.cs     ← معالج الرسائل
```

---

## 🔨 الخطوة 2: البناء

### من داخل tModLoader:
1. افتح tModLoader
2. اذهب إلى `Workshop → Develop Mods`
3. ابحث عن `ServerGuardMod`
4. اضغط **Build & Reload**

### من الكود (Visual Studio):
```bash
dotnet build
```

---

## ⚙️ الخطوة 3: تشغيل السيرفر

### للـ Dedicated Server:
```bash
tModLoaderServer.exe -mod ServerGuardMod
```

### من داخل اللعبة:
1. شغّل tModLoader
2. فعّل المود من `Workshop → Manage Mods`
3. أنشئ أو افتح Multiplayer

---

## 👑 الخطوة 4: إنشاء حساب الأدمن الأول

عند أول تشغيل، **كل الحسابات عادية**. لإنشاء أدمن:

### الطريقة 1 - من الكونسول:
```
> sg setadmin اسمك true
```

### الطريقة 2 - تعديل الملف يدوياً:
افتح الملف:
```
%USERPROFILE%\Documents\My Games\Terraria\tModLoader\saves\ServerGuard\accounts.json
```
وغيّر `"IsAdmin": false` إلى `"IsAdmin": true`

---

## 🎮 كيف يعمل للاعبين

1. اللاعب يدخل السيرفر → تظهر له شاشة تسجيل دخول
2. إذا كان لديه حساب → يكتب اسمه وكلمة مروره
3. إذا لم يكن لديه حساب → يضغط "حساب جديد"
4. بعد الدخول → تُطبَّق بياناته من السيرفر

---

## 🛡 أوامر الأدمن

| الأمر | الوظيفة |
|-------|---------|
| `/sg kick <اسم> [سبب]` | طرد لاعب |
| `/sg ban <اسم> [سبب]` | حظر لاعب |
| `/sg unban <اسم>` | إلغاء الحظر |
| `/sg god [اسم]` | وضع الألوهية |
| `/sg give <اسم> <ID> <كمية>` | إعطاء غرض |
| `/sg tp <اسم>` | نقل فوري |
| `/sg freeze <اسم>` | تجميد لاعب |
| `/sg setadmin <اسم> true/false` | منح/سحب صلاحيات أدمن |
| `/sg setpass <اسم> <كلمة>` | تغيير كلمة المرور |
| `/sg online` | قائمة المتصلين |
| `/sg accounts` | جميع الحسابات |
| `/sg broadcast <رسالة>` | إعلان لجميع اللاعبين |
| `/sg info <اسم>` | معلومات حساب |
| `/sg save` | حفظ البيانات |
| `/sg reload` | إعادة تحميل DB |

---

## 📂 ملفات البيانات

تُحفظ في:
```
saves/ServerGuard/
├── accounts.json    ← بيانات جميع الحسابات
└── cheat_log.txt   ← سجل محاولات الغش
```

---

## 🔒 كيف يحمي النظام السيرفر

| التهديد | الحل |
|---------|------|
| Cheat Engine - تعديل HP | السيرفر يتحقق ويصحح القيمة |
| Cheat Engine - تعديل Items | مقارنة مع قاعدة البيانات |
| أغراض من سيرفر آخر | كل غرض يُقارن مع السجل |
| Packet Injection | فلتر الباكيتات |
| Speed Hack | فحص السرعة كل 10 تيك |
| NoClip | فحص الموقع داخل البلوكات |
| حركة بدون تسجيل | تجميد كامل قبل الدخول |
| تكسير بدون تسجيل | `noBuilding = true` |

---

## ❗ ملاحظات مهمة

1. **أول اشتراك جديد** يتم حفظه فارغاً (مخزن فارغ، 100 HP)
2. البيانات تُحفظ تلقائياً كل **30 ثانية**
3. عند إيقاف السيرفر، تُحفظ بيانات **جميع** اللاعبين
4. كلمات المرور مُشفَّرة بـ **SHA256** ولا يمكن قراءتها
5. سجل الغش يُحفظ في `cheat_log.txt` للمراجعة

---

## 🐛 حل المشاكل الشائعة

**خطأ: "AccountDatabase لا يُحمَّل"**
→ تأكد أن مسار الحفظ صحيح في `AccountDatabase.cs`

**الـ UI لا تظهر**
→ تأكد من تفعيل المود وأن السيرفر يعمل بـ netMode = Server

**اللاعبون لا يستطيعون التسجيل**
→ تحقق من سجل السيرفر بحثاً عن أخطاء

