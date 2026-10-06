# План: AltovaGen — свой кросс-платформенный XML/XSLT/XQuery-движок вместо `Altova.AltovaXML.dll`

- **Дата составления:** 2026-10-02
- **Рабочая папка:** `E:\Git\MyProject\AltovaGen`
- **Исходная задача владельца:** «в проекте используется зависимость `@Altova.AltovaXML.dll`, написанная под x86; проанализируй зависимость и напиши план реализации своего проекта, который не x86».
- **Зафиксированные требования:** (1) .NET 8+; (2) полный аналог функционала; (3) кроссплатформенность.
- **Тип плана:** direct-режим (git/gh в рабочей папке не инициализированы, ветки/PR не используются; шаги оформляются как самодостаточные инкременты с проверяемыми exit criteria).

---

## 0. TL;DR

1. `Altova.AltovaXML.dll` — **не «x86-библиотека»**, а **Primary Interop Assembly (RCW-обёртка) поверх COM-сервера AltovaXML**: `PEKind = ILOnly` (флаг `Required32Bit` отсутствует), все типы — `[ComImport]` с GUID'ами, ссылка только на `mscorlib 1.0.5000.0`, `ImageRuntimeVersion = v1.1.4322`.
2. Реальные источники x86-ограничения — **вне** этой DLL: 32-битный out-of-process COM-сервер `AltovaXML.exe` (и/или `PlatformTarget=x86`/`Prefer 32-bit` в проекте-потребителе). То есть «просто пересобрать в AnyCPU» проблему не решает: COM-активация в .NET 8 — **Windows-only**, а сам продукт — legacy-движок Altova, снятый с развития.
3. Следствие: строим **свой проект `AltovaGen`** — managed, AnyCPU, .NET 8+, без COM, с двумя движками под капотом:
   - **BCL** (`System.Xml`): XSLT 1.0, well-formedness, DTD, XSD 1.0 — бесплатно, кросс-платформенно, без внешних зависимостей;
   - **SaxonCS 12** (net8.0, XSLT 3.0 / XQuery 3.1 / XSD 1.1) — для того, что BCL не умеет; **проприетарная лицензия, нужен ключ** (см. §5, есть бесплатные обходные пути).
4. Совместимость обеспечивается **Compat-фасадом**, повторяющим пространство имён и типы `Altova.AltovaXML` (`new Application()`, `.XSLT1`, `.XSLT2`, `.XQuery`, `.XMLValidator`, `LastErrorMessage`, `AddExternalParameter(...)`). Миграция потребителя = смена ссылки на пакет + снятие `x86`, без переписывания вызовов (см. S4, S8).
5. Критический гейт — **S0 «Инвентаризация»**: пока неизвестно, какие из четырёх групп API реально используются. Если используются только XSLT 1.0 + XSD 1.0 + DTD — решение получается бесплатным (только BCL). Если XSLT 2.0/XQuery — нужен Saxon (платная лицензия либо бесплатный путь IKVM + Saxon-HE). Если XBRL — нужен отдельный контур (RaptorXML или Arelle).
6. Точных потребителей этой DLL в `E:\Git\MyProject` **не найдено** (поиск по коду пуст), а COM-сервер на этой машине **не зарегистрирован** — это блокирует запись «золотых» эталонов (S5) и требует от владельца указать путь к проекту-потребителю и/или машину с рабочим AltovaXML.

---

## 1. Анализ зависимости (evidence-first)

### 1.1 Измеренные факты

| Факт | Значение | Как проверено |
|---|---|---|
| Файл | `E:\Git\MyProject\AltovaGen\Altova.AltovaXML.dll`, 13 312 байт, SHA-256 `3593A919…B703` | `Get-FileHash` |
| Идентичность сборки | `Altova.AltovaXML, Version=12.3.0.0, Culture=neutral, PublicKeyToken=bab301ea12809d38`, `Flags=PublicKey` (strong-named) | `PEReader` + `MetadataReader` |
| Runtime-версия метаданных | `ImageRuntimeVersion = v1.1.4322` (эпоха .NET Framework 1.1) | reflection |
| PE-характеристики | `PEKind = ILOnly`, `Machine = I386 (0x014C)`, `PEMagic = PE32 (0x010B)`, 3 секции | `PEReader.PEHeaders`, `Assembly.GetPEKind` |
| Внешние ссылки | только `mscorlib, Version=1.0.5000.0, PublicKeyToken=b77a5c561934e089` | metadata |
| Состав типов | только COM-interop: интерфейсы `IApplication`, `IXSLT1`, `IXSLT2`, `IXQuery`, `IXMLValidator`, `_IApplicationEvents` (все `[ComImport]`) + coclass-классы и event-provider'ы | metadata |
| TypeLib-GUID'ы | `IApplication/Application = F0158B7E-77D2-4365-A0C4-177E84F0CC1D`; `IXSLT1/XSLT1 = 7654E776-D167-4B91-A3EC-DC7C41BD2473`; `IXSLT2/XSLT2 = 5815D2CF-D975-4DE6-8E30-215AB3F8B2B8`; `IXQuery/XQuery = 3787E161-FD6A-46B8-A146-3461FAA12419`; `IXMLValidator/XMLValidator = E994BCA7-9C98-4049-A633-483BBDD6CFAA`; coclass `ApplicationClass = 533BAC07-C702-4D91-8D37-39FDC919A19C` | metadata |
| COM-регистрация (ProgID и CLSID) | **отсутствует** в обоих представлениях реестра (`HKLM\SOFTWARE\Classes\…` и `…\WOW6432Node\…`) | `Test-Path` по 4 путям |
| Установленный Altova на машине | только `C:\Users\Pavel\Documents\Altova\XMLSpy2026\…` (современный XMLSpy, x64-линейка); каталогов `Program Files\Altova` / `Program Files (x86)\Altova` нет | filesystem |
| Ссылки на `Altova` в коде | **0 совпадений** в `E:\Git\MyProject` (`*.cs`, `*.csproj`, `*.sln`, `*.props/json/xml/config/ps1/md/py/ts`) | grep |
| SDK/рантаймы на машине | SDK 9.0.316, 10.0.100-rc.1, 10.0.401; рантаймы `Microsoft.NETCore.App` 6.0.36 / 7.0.20 / 8.0.29 / 8.0.31 / 9.0.18 / 9.0.20 / 10.0.12; OS Windows 10.0.26100, `PROCESSOR_ARCHITECTURE=AMD64` | `dotnet --list-sdks`, `--list-runtimes` |

### 1.2 Выводы

- **В1. Это PIA, а не реализация.** Сборка не содержит логики: это сгенерированные из TypeLib COM-интерфейсы. Реальный движок — внешний COM-сервер AltovaXML (32-битный). Отсюда и «x86».
- **В2. Сама DLL платформенно-нейтральна.** `PEKind = ILOnly` без `Required32Bit` означает AnyCPU. Утверждение «DLL написана под x86» неточно: 32-битность навязывает COM-сервер (32-битная регистрация/активация) и, возможно, `PlatformTarget=x86` у потребителя.
- **В3. Windows-only по определению.** Встроенная COM-интероперабельность (активация COM-объектов) в .NET 8 существует **только на Windows**. Даже если решить проблему разрядности, требование «кроссплатформенность» этой архитектурой не достигается — нужна замена движка, а не смена платформы сборки.
- **В4. Проверка «а может, и так заработает» дешева, но бесперспективна.** Быстрый эксперимент (S0.4): снять `x86`, поставить AltovaXML, попробовать активировать COM из 64-битного процесса. Он может дать временный обходной путь на Windows (out-of-process COM допускает кросс-разрядную активацию при регистрации в нужном представлении реестра), но кроссплатформенность не даёт. Рассматриваем его только как **источник эталонов** для S5.
- **В5. Продукт — legacy.** `Version=12.3`, метаданные v1.1, `mscorlib 1.0.5000.0`. Линейка AltovaXML заменена на RaptorXML (третье поколение, x64, Windows/Linux/macOS, .NET/COM/Java/HTTP API, но коммерческий — см. §5).
- **В6. На этой машине нет ни COM-сервера, ни потребителя.** Значит: (а) эталоны для дифференциального тестирования нужно либо получить на другой машине/из существующих ожидаемых результатов, либо начинать с контрактных тестов вместо байтового сравнения; (б) перед S1 нужен ответ владельца о расположении проекта-потребителя.

### 1.3 Функциональная поверхность, которую надо воспроизвести

Четыре независимые группы + COM-события (см. приложение в отдельном файле [`docs/altovaxml-api-surface.md`](../docs/altovaxml-api-surface.md)):

| Группа | Объект | Что делает | Чем закрываем |
|---|---|---|---|
| XSLT 1.0 | `Application.XSLT1` | трансформация XML по XSLT 1.0 из файла/текста, внешние параметры, результат в файл/строку | BCL `XslCompiledTransform` |
| XSLT 2.0 | `Application.XSLT2` | то же + `InitialTemplateName`, `InitialTemplateMode` | Saxon (SaxonCS / Saxon-HE) |
| XQuery | `Application.XQuery` | XQuery из файла/текста над входным документом, внешние переменные (значение и XPath-выражение), настройки сериализации | Saxon `XQueryCompiler`/`XQueryEvaluator` |
| Валидация | `Application.XMLValidator` | well-formedness, XSD-схема (файл/текст), DTD (файл/текст), XBRL-флаг | BCL (`XmlReader`, `XmlSchemaSet`) + Saxon для XSD 1.1 + RaptorXML/Arelle для XBRL |
| События | `_IApplicationEvents` | COM-события приложения | отбрасываем (в .NET-стиле — обычные .NET-события, если понадобятся) |

---

## 2. Целевые требования и инварианты (Definition of Done)

**Функциональные.** Совпадение поведения по каждой из четырёх групп API с точностью до задокументированных расхождений: тот же набор операций, тот же смысл параметров, те же результаты на корпусе реальных стилей/запросов/схем.

**Жёсткое требование (по решению владельца).** COM полностью вырезан из приложения:
- нет **ни одной** COM-активации (`CoCreateInstance`, `new` через `Type.GetTypeFromProgID`/`Type.GetTypeFromCLSID`, `New-Object -ComObject`, `Activator.CreateInstance(COMType)`) ни в рантайме, ни в коде;
- нет COM-interop-типов (`[ComImport]`, `[ComVisible]`-прокси, PIA) и сборок-обёрток;
- нет **регистрации COM** и установки/запуска 32-битного COM-сервера AltovaXML в установщике, CI и скриптах развёртывания;
- нет ссылок на `Altova.AltovaXML.dll` и самого файла рядом с приложением — он либо удаляется, либо остаётся только как `legacy/`-артефакт вне поставки;
- `AltovaGen` и потребитель работают **без какого-либо Windows-COM-стека** (в т.ч. на Linux/macOS).

**Инварианты (проверяемые машиной):**

| № | Инвариант | Проверка |
|---|---|---|
| I1 | Ни одной сборки с `Required32Bit`/`32BitPreferred` | `(CorFlags & 0x2) == 0 && (CorFlags & 0x00020000) == 0` для всех DLL в артефактах |
| I2 | Ноль COM в коде продукта | `grep -rn "ComImport\|CoClass\|Type.GetTypeFromProgID\|Marshal.GetActiveObject\|CoCreateInstance\|New-Object -ComObject\|ComVisible" src/ tools/ (без tools/LegacyGolden) tests/` → пусто |
| I2b | COM не регистрируется и 32-битный COM-сервер не ставится | `grep -rn "regsvr32\|AltovaXML.Application\|AltovaXML" installer/ ci/ deploy/` (кроме `docs/` и `legacy/`) → пусто |
| I2c | `Altova.AltovaXML.dll` не входит в поставку | тест-храповик: файл с именем `Altova.AltovaXML.dll` отсутствует в каталогах publish/pack |
| I3 | `dotnet publish -r linux-x64` и `-r osx-arm64` собираются и тесты проходят на Linux-контейнере | CI job `linux-x64` + `dotnet test` |
| I4 | Базовый путь (BCL-движок) не тянет внешних нативных/платных зависимостей | `dotnet list package --include-transitive` для `AltovaGen.Engine.Bcl` = только BCL |
| I5 | Публичный API Compat-фасада покрывает 100 % членов из [`docs/altovaxml-api-surface.md`](../docs/altovaxml-api-surface.md) | тест-рефлексия: сверка списка членов с зафиксированным манифестом |
| I6 | Ошибки не теряются: `LastErrorMessage` непустой при любом провале операции, текст содержит позицию | контрактные тесты на «плохих» входах |

**Не входит в план:** замена Altova-специфичных extension-функций (chart/barcode и пр.), поддержка `msxsl:script` на не-Windows, эмуляция `XSLStackSize` как точного числового предела, воспроизведение COM-событий, «байт-в-байт» вывод там, где спецификации допускают вариативность сериализации (см. §7.3).

---

## 3. Архитектура

```
                 ┌──────────────────────────────────────────────┐
 Потребитель →   │  AltovaGen.Compat  (namespace Altova.AltovaXML)│  ← тонкий фасад,
 (без правок)    │  Application / XSLT1 / XSLT2 / XQuery /        │    повторяет API PIA
                 │  XMLValidator  + LastErrorMessage              │
                 └───────────────┬──────────────────────────────┘
                                 │  зависит только от абстракций
                 ┌───────────────▼──────────────────────────────┐
                 │  AltovaGen.Abstractions                       │
                 │  IXsltEngine / IXQueryEngine / IValidationEngine│
                 │  TransformRequest/Result, QueryRequest/Result, │
                 │  ValidationReport, Diagnostic, EngineCapabilities│
                 └───┬───────────────┬───────────────┬───────────┘
                     │               │               │
      ┌──────────────▼──┐  ┌─────────▼────────┐  ┌───▼─────────────────┐
      │ Engine.Bcl      │  │ Engine.Saxon     │  │ Engine.RaptorXml    │
      │ XSLT 1.0, WF,   │  │ XSLT 2.0/3.0,    │  │ (опция) HTTP/CLI к  │
      │ DTD, XSD 1.0    │  │ XQuery 3.1,      │  │ RaptorXML Server,   │
      │ (бесплатно)     │  │ XSD 1.1 (лиценз.)│  │ XBRL/строгий Altova │
      └─────────────────┘  └──────────────────┘  └─────────────────────┘
                     │
      ┌──────────────▼───────────────────────────────┐
      │ LegacyBridge (только миграция, не в поставке) │
      │ x86-хелпер net48 → COM AltovaXML → golden     │
      └──────────────────────────────────────────────┘
```

**Принципы.**
1. **Движки — плагины.** Выбор реализации — по `EngineCapabilities` (что поддерживает движок) и по запросу (`version="2.0"` в стиле, наличие `xs:assert`, XBRL-контекст). Роутер `EngineRouter` в `AltovaGen.Abstractions` выбирает движок и умеет «повышать» с BCL на Saxon.
2. **Фасад без логики.** `AltovaGen.Compat` только накапливает состояние (как это делал COM-объект: «запомнил имена файлов/тексты/параметры, потом `Execute()`») и транслирует в абстракции. Это ровно семантика PIA, поэтому потребитель не замечает подмены.
3. **Явная модель ошибок.** `LastErrorMessage` — это снимок `Diagnostic[]` (сообщение + строка/колонка + движок + код), сериализуемый в строку в стиле Altova.
4. **Большой стек для рекурсии.** `XSLStackSize` напрямую не воспроизводим → в абстракциях есть `TransformOptions.StackSizeHint`; исполнение идёт в выделенном потоке с увеличенным стеком (`new Thread(..., maxStackSizeBytes)`), что защищает от `StackOverflowException` на глубокой рекурсии в XSLT.
5. **Никакого COM.**

---

## 4. Матрица маппинга: API Altova → реализация

### 4.1 `Application`

| Altova | AltovaGen |
|---|---|
| `new Application()` (COM coclass) | обычный C#-класс с конструктором без параметров; никакой активации |
| `.XSLT1` / `.XSLT2` / `.XQuery` / `.XMLValidator` | ленивые свойства, возвращающие объекты с тем же API; экземпляры без состояния между вызовами |
| `_IApplicationEvents` | не воспроизводится; при необходимости — .NET-событие `OnError` |

### 4.2 `XSLT1` / `XSLT2`

| Altova | Реализация (BCL / Saxon) | Замечания по совместимости |
|---|---|---|
| `InputXMLFileName` | `XmlReader.Create(path, ReaderSettings)` / Saxon принимает `Stream` или `InputSource` | Нужен единый `XmlResolver`/base-URI, чтобы `document()` и `xsl:include` работали относительно файла |
| `XSLFileName` | `XslCompiledTransform.Load(path)` / `XsltCompiler.Compile(stream)` | Кэш скомпилированных стилей по (path, mtime, size) |
| `InputXMLFromText` / `XSLFromText` | `XmlReader.Create(new StringReader(text))` | Кодировка: у Altova текст трактовался как Unicode-строка; сохраняем это, игнорируя XML-декларацию с `encoding=`, либо явно включаем `XmlReaderSettings` с `StringReader` (он игнорирует декларацию — совпадает с Altova) |
| `XSLStackSize` | `TransformOptions.StackSizeHint` → размер стека выделенного потока | Точный предел Altova не эмулируем; документируем как «мягкий» |
| `AddExternalParameter(name, value)` | `XsltArgumentList.AddParam(name, "", value)` / Saxon `SetStylesheetParameters(new Dictionary<QName,XdmValue>{{new QName(name), new XdmAtomicValue(value)}})` | Altova-параметры строковые и без namespace → ключ `QName(name)` без префикса. Если в стилях параметры в namespace — S0/S7 фиксирует это как расхождение, требующее явного маппинга |
| `ClearExternalParameterList()` | очистка словаря/`XsltArgumentList` | |
| `Execute(outputFileName)` | BCL: `Transform(input, args, XmlWriter.Create(out, OutputSettings))`; Saxon: `Serializer` в `FileStream` | Кодировка/индентация — из `xsl:output`; см. §7.3 |
| `ExecuteAndGetResultAsString()` | BCL: `Transform(..., XmlWriter.Create(sb, settings))`; Saxon: `Serializer` в `StringWriter` | Порядок/пробелы — предмет golden-тестов |
| `LastErrorMessage` | строка, собранная из `Diagnostic[]` | |
| `DotNetExtensionsEnabled` / `JavaExtensionsEnabled` | BCL: **аналога нет** — `XsltSettings.EnableScript` в .NET 8+ помечен `[Obsolete("XSLT Script blocks are not supported.", SYSLIB0062)]`, блоки `msxsl:script` не поддерживаются ни на одной ОС (проверено по документации .NET 8 и .NET 10). Saxon: интегральные extension-функции, регистрируемые из .NET | Если реальные стили используют `altova:`-функции или `msxsl:script` — это **обязательное переписывание стилей**, а не вопрос платформы (S7 + задача миграции стилей) |
| `InitialTemplateName` (только XSLT2) | Saxon `Xslt30Transformer.CallTemplate(new QName(name))` | BCL не умеет — гарантированно Saxon |
| `InitialTemplateMode` (только XSLT2) | Saxon `Xslt30Transformer.InitialMode = new QName(mode)` | idem |

### 4.3 `XQuery`

| Altova | Реализация (Saxon) | Замечания |
|---|---|---|
| `XQueryFileName` / `XQueryFromText` | `XQueryCompiler.Compile(stream/string)` | |
| `InputXMLFileName` / `InputXMLFromText` | контекстный элемент: `XQueryEvaluator.ContextItem = ...` | Это точный смысловой аналог «входного документа» Altova |
| `AddExternalVariable(name, value)` | `evaluator.SetExternalVariable(new QName(name), new XdmAtomicValue(value))` | тип значения: строка (как у Altova) |
| `AddExternalVariableAsXPath(name, expr)` | `XPathCompiler.Compile(expr)` → `XPathSelector.Evaluate()` → `SetExternalVariable` | |
| `ClearExternalVariableList()` | очистка | |
| `OutputMethod` | `Serializer` property `METHOD` (`xml`/`html`/`text`) | |
| `OutputEncoding` | `Serializer` property `ENCODING` | |
| `OutputIndent` | `Serializer` property `INDENT` (`yes`/`no`) | |
| `OutputOmitXMLDeclaration` | `Serializer` property `OMIT_XML_DECLARATION` | |
| `Execute` / `ExecuteAndGetResultAsString` | сериализация в файл / `StringWriter` | |
| `DotNetExtensionsEnabled` / `JavaExtensionsEnabled` | .NET extension-функции Saxon | |
| `LastErrorMessage` | из исключений + `Diagnostic` | |

### 4.4 `XMLValidator`

| Altova | Реализация | Замечания |
|---|---|---|
| `IsWellFormed()` | `XmlReader` проход с `DtdProcessing` по политике | |
| `IsValid()` (по `SchemaFileName`/`SchemaFromText`) | `XmlSchemaSet.Add(...)` + `XmlReaderSettings.ValidationType = Schema` | BCL умеет **XSD 1.0**. Если в схеме `vc:minVersion="1.1"`, `xs:assert`, `xs:alternative` → роутинг на Saxon (XSD 1.1) |
| `IsValidWithExternalSchemaOrDTD()` | то же + DTD; `XmlReaderSettings.ValidationType = Dtd` либо схема + `DtdProcessing = Parse` c резолвером | |
| `DTDFileName` / `DTDFromText` | `XmlResolver`-наследник, отдающий подготовленный DTD по system/public id | Реализуется в S3; тестируется на XML с `<!DOCTYPE ... SYSTEM "...">` |
| `InputXMLFileName` / `InputXMLFromText` | `XmlReader` | |
| `TreatXBRLInconsistenciesAsErrors` | нет аналога в BCL/Saxon | Если XBRL реально используется — только RaptorXML+XBRL (сертифицирован) или Arelle (открытый); решение — гейт G3/S10 |
| `LastErrorMessage` | агрегированный список ошибок валидации (как `ValidationEventHandler` + `XmlSchemaException`) | Altova сообщал несколько ошибок — сохраняем многострочный формат |

### 4.5 Известные расхождения (фиксируем заранее, чтобы не «ловить» их в продакшене)

| # | Расхождение | Митигация |
|---|---|---|
| R1 | `exsl:node-set` и другие EXSLT-расширения: BCL не поддерживает, Altova поддерживал | Роутинг таких стилей на Saxon (Saxon умеет `exsl:node-set`) |
| R2 | `msxsl:script` на Linux не работает (`XsltSettings.EnableScript` → `PlatformNotSupportedException`) | Переписать на `xsl:function`/Saxon extension-функции; задача S8 |
| R3 | Сериализация (индентация, XML-декларация, экранирование) может отличаться | §7.3: правила сравнения + явные настройки вывода |
| R4 | Имена параметров/переменных в namespace | Явный маппинг, реестр исключений |
| R5 | `XSLStackSize` | мягкий стек-хинт |
| R6 | XSD 1.1 и XBRL | Saxon / RaptorXML / Arelle |
| R7 | Altova-специфичные extension-функции (`altova:*`, chart/barcode) | Инвентаризация S0; переписывание или отказ по согласованию |

---

## 5. Выбор движка: альтернативы, лицензии, риски

| Вариант | Что закрывает | Платформы | Лицензия/цена | Вердикт |
|---|---|---|---|---|
| **BCL `System.Xml`** (`XslCompiledTransform`, `XmlSchemaSet`, `XmlReader`) | XSLT 1.0, WF, DTD, XSD 1.0 | все (.NET 8) | бесплатно | **Берём как базу** |
| **SaxonCS 12** (NuGet `SaxonCS`, 12.10.0, target `net8.0`) | XSLT 3.0, XQuery 3.1, XPath 3.1, XSD 1.1, schema-awareness | Windows/Linux/macOS | **проприетарная, лицензионный ключ обязателен** (есть time-limited evaluation) | **Берём при подтверждении лицензии** (гейт G2) |
| **Saxon-HE (NuGet `Saxon-HE` 10.9.0)** | XSLT 2.0/3.0, XQuery, XPath | target `net35` → .NET Framework; на .NET 8 напрямую не годится | MPL-2.0, бесплатно | Только через прослойку (ниже) |
| **Saxon-HE (JAR) + IKVM 8** (`IKVM` 8.16.1, `net8.0`, Win/Linux/macOS) | то же, без schema-awareness и XSD-валидации | все | IKVM — открытая (лицензию проверить), Saxon-HE — MPL-2.0 | **Бесплатный путь**, но требует спайка (производительность, старт, совместимость с Java 8 API) |
| **RaptorXML Server** (Altova, 3-е поколение) | XML/XBRL/JSON/YAML, XSLT 1.0/2.0/3.0(subset), XQuery 1.0/3.1, XSD 1.0/1.1; API: .NET (Windows), COM (Windows), Java (все), HTTP REST (все) | Windows/Linux/macOS | коммерческая, 30-дневный триал | **Резерв** для XBRL/строгого паритета; кросс-платформенно только через Java/HTTP |
| Оставить Altova XML COM | всё, что умел | **Windows + x86** | бесплатно/legacy | Только как источник эталонов (S5) |
| Свой XSLT/XQuery с нуля | — | — | — | Отвергнуто: нереалистично для полного аналога |

**Гейты решений:**
- **G1 (после S0):** нужны ли XSLT 2.0 и/или XQuery и/или XSD 1.1 → определяет, нужен ли Saxon вообще.
- **G2 (S6):** приемлема ли платная лицензия SaxonCS. Если нет — спайк IKVM+Saxon-HE (ограничение: нет XSD-валидации) или RaptorXML.
- **G3 (S10):** используется ли XBRL → RaptorXML+XBRL либо Arelle.

---

## 6. Шаги

Порядок и зависимости:

```
S0 ─┬─> S1 ──> S2 ──> S3 ──> S4 ──> S5 ──┬─> S7 ──> S8 ──> S9 ──> S11
    │                                     │
    └─────────> (параллельно) S6 ─────────┘
                                     └──> S10 (опционально)
```
Параллелизация: **S6 (Saxon-адаптер)** можно вести параллельно с S3–S5 (разные файлы, общий только `Abstractions` из S2). **S5 (golden-харнесс)** параллелен S6.

Оценки — в человеко-днях для одного исполнителя (см. приложение C).

---

### S0 — Инвентаризация потребителя, ассетов и фактического использования API ⛔ ГЕЙТ

**Цель.** Точно узнать, какие группы API используются, какие технологии внутри (XSLT-версия, XQuery, XSD 1.1, XBRL, extension-функции), где живут стили/схемы/запросы, и получить корпус реальных входных данных. Без этого плана дальнейшие шаги могут оказаться избыточными (переплата за Saxon) либо недостаточными (XBRL).

**Зависит от.** —. **Блокирует.** Все остальные шаги.

**Контекст для холодного агента.** В `E:\Git\MyProject\AltovaGen` лежит только `Altova.AltovaXML.dll`. Ссылок на Altova в `E:\Git\MyProject` не найдено. COM-сервер AltovaXML на машине не зарегистрирован. Разбор API-поверхности — в [`docs/altovaxml-api-surface.md`](../docs/altovaxml-api-surface.md).

**Задачи.**
1. Получить от владельца путь к проекту-потребителю (и/или репозиторий). Сложить в `docs/inventory/00-consumer-location.md`.
2. Найти потребителей:
   ```powershell
   rg -n --glob '!**/node_modules/**' --glob '!**/bin/**' --glob '!**/obj/**' `
      'AltovaXML|Altova\.AltovaXML|new Application\(\)|XSLT2|XQuery|XMLValidator' <CONSUMER_ROOT>
   ```
3. Определить `PlatformTarget`/`Prefer32Bit` и наличие `<COMReference>` / `<Reference Include="Altova.AltovaXML">` / `EmbedInteropTypes`:
   ```powershell
   rg -n 'PlatformTarget|Prefer32Bit|EmbedInteropTypes|AltovaXML|COMReference' -g '*.csproj' -g '*.props' -g '*.targets' <CONSUMER_ROOT>
   ```
4. Собрать корпус: список `*.xsl`/`*.xslt`/`*.xq`/`*.xquery`/`*.xsd`/`*.dtd` + типовые входные XML. Разложить в `tests/corpus/` (вход) — саму копию ассетов складывать только если лицензия/конфиденциальность позволяют.
5. Классифицировать корпус автоскриптом `tools/corpus-scan.ps1` (или `.cs`): для каждого стиля — `version=` в `xsl:stylesheet`, наличие `xsl:function`, `xsl:for-each-group`, `xsl:result-document`, `exsl:`, `msxsl:script`, `altova:`, `saxon:`, для схем — `vc:minVersion`, `xs:assert`, `xs:alternative`, для запросов — `xquery version`, `declare variable`, `update`.
6. Проверить дешёвый обходной путь (для эталонов и как временный мост): на машине с установленным AltovaXML снять `x86` у тестового консольного приложения и попробовать активацию COM из x64/AnyCPU:
   ```powershell
   $t=[Type]::GetTypeFromProgID('AltovaXML.Application'); "ProgID resolved: $($t -ne $null)"
   New-Object -ComObject AltovaXML.Application   # ожидаем успех только при корректной регистрации
   ```
7. Записать вывод: матрица «группа API → используется/нет → объём», список технологий, решение G1.
8. **Зафиксировать решение по гейту G1** в `docs/adr/0002-engine-selection.md`.

**Артефакты.** `docs/inventory/00-consumer-location.md`, `docs/inventory/01-api-usage.md`, `tools/corpus-scan.ps1`, `tests/corpus/**`, `docs/adr/0002-engine-selection.md`.

**Проверка.** `tools/corpus-scan.ps1` печатает таблицу: файл → версия XSLT/XQuery → использованные фичи; количество файлов совпадает с ручным подсчётом на подвыборке из 10 файлов.

**Exit criteria.** Известно: расположение потребителя; какие из {XSLT1, XSLT2, XQuery, XMLValidator} используются; есть ли XSD 1.1/XBRL/extension-функции; корпус собран; G1 закрыт письменным решением.

**Оценка.** 1–2 дня (зависит от скорости получения доступа). **Риск.** Без доступа к потребителю шаг не закрывается — тогда работаем по «широкому» варианту (BCL + Saxon), а S8 уточняется позже.

---

### S1 — Скелет решения и инварианты сборки

**Цель.** Пустой, но зелёный каркас, в котором невозможны x86/COM-регрессии.

**Зависит от.** S0 (можно начинать сразу после получения пути к потребителю).

**Контекст для холодного агента.** В папке — единственная DLL. Решение создаётся с нуля: `net8.0`, AnyCPU. На машине нет .NET 8 SDK (есть 9 и 10) — целиться в `net8.0` можно, reference packs подтянутся; при желании поставить SDK 8.

**Задачи.**
1. `dotnet new sln -n AltovaGen`; проекты:
   - `src/AltovaGen.Abstractions/AltovaGen.Abstractions.csproj`
   - `src/AltovaGen.Compat/AltovaGen.Compat.csproj`
   - `src/AltovaGen.Engine.Bcl/AltovaGen.Engine.Bcl.csproj`
   - `src/AltovaGen.Engine.Saxon/AltovaGen.Engine.Saxon.csproj`
   - `tests/AltovaGen.UnitTests/`, `tests/AltovaGen.ParityTests/`
   - `tools/LegacyGolden/` (net48, Windows-only, вне основной сборки — см. S5)
2. `Directory.Build.props`: `<TargetFramework>net8.0</TargetFramework>`, `<PlatformTarget>AnyCPU</PlatformTarget>`, `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<EnableNETAnalyzers>true</EnableNETAnalyzers>`, `<InvariantGlobalization>false</InvariantGlobalization>` (важно: XML/XSLT зависят от культуры и ICU).
3. `Directory.Packages.props` (Central Package Management): фиксация версий (SaxonCS — только в проекте Saxon).
4. `runsettings`, `.editorconfig`, `dotnet format` в CI.
5. Тест-храповик инварианта I1: `tests/AltovaGen.UnitTests/BuildInvariantsTests.cs` — читает `CorFlags` всех сборок выходной папки и падает, если есть `Required32Bit`/`32BitPreferred`.
6. Тест-храповик I2: тест, который `grep`-ает исходники на `ComImport|CoCreateInstance|GetTypeFromProgID` (fail-on-match).
7. `.gitignore`, `git init` (если владелец согласен; иначе — direct-режим).

**Артефакты.** `AltovaGen.sln`, `Directory.Build.props`, `Directory.Packages.props`, проекты, тесты инвариантов, `.editorconfig`.

**Проверка.**
```powershell
dotnet build AltovaGen.sln -c Release
dotnet test AltovaGen.sln -c Release --filter Category=BuildInvariants
```

**Exit criteria.** `dotnet build` зелёный; тесты инвариантов I1/I2 зелёные и **падают**, если искусственно добавить `[ComImport]`-тип или `<PlatformTarget>x86</PlatformTarget>` (проверить вручную и откатить).

**Оценка.** 0,5 дня.

---

### S2 — Абстракции: контракты движков, диагностика, стек-хинт

**Цель.** Единый, не зависящий от движка контракт, в который маппится API Altova.

**Зависит от.** S1.

**Контекст для холодного агента.** Маппинг — §4 плана. Ключевая идея: фасад ничего не считает сам, вся работа — в `IXsltEngine`/`IXQueryEngine`/`IValidationEngine`.

**Задачи.**
1. Модель запросов/результатов:
   - `XsltRequest { SourceRef Input, SourceRef Stylesheet, IReadOnlyDictionary<string,string> Parameters, int? StackSizeHint, string? InitialTemplateName, string? InitialTemplateMode, OutputSettings Output }`
   - `XQueryRequest { SourceRef Query, SourceRef? Input, IReadOnlyList<ExternalVariable> Variables, OutputSettings Output }`
   - `ValidationRequest { SourceRef Xml, SourceRef? Schema, SourceRef? Dtd, ValidationKind Kind }`
   - `SourceRef` — discriminated-union: `File(path)` | `Text(string)` | `Stream`.
   - `OutputSettings { Method?, Encoding?, Indent?, OmitXmlDeclaration? }`
   - `Result<T> { T? Value, IReadOnlyList<Diagnostic> Diagnostics, bool Success }`; `Diagnostic { Severity, Code, Message, Line, Column, Engine }`.
2. Интерфейсы движков + `EngineCapabilities { XsltVersions, SupportsXQuery, XsdVersions, SupportsXbrl, SupportsExslt, SupportsScriptExtensions }`.
3. `EngineRouter`: выбор по требованиям (`RequiredXsltVersion`, наличие EXSLT, XSD 1.1, XBRL); ошибка `NoEngineAvailableException` с перечнем причин.
4. `LargeStackExecutor`: запуск делегата в потоке с заданным размером стека (`new Thread(work, maxStackSizeBytes)`), корректный проброс исключений и диагностики. Тест: рекурсивный XSLT-шаблон, падающий на дефолтном стеке, проходит на 32 МБ.
5. Юнит-тесты на роутер и `SourceRef` (в т.ч. «текст с XML-декларацией о кодировке»).

**Артефакты.** `src/AltovaGen.Abstractions/**`, тесты.

**Проверка.** `dotnet test --filter Category=Abstractions`.

**Exit criteria.** Контракты покрывают все члены API из §4 (чек-лист в тесте); роутер выбирает BCL для XSLT 1.0 и Saxon для XSLT 2.0/`InitialTemplateName`; тест большого стека зелёный.

**Оценка.** 1 день.

---

### S3 — Движок BCL: XSLT 1.0, well-formedness, DTD, XSD 1.0

**Цель.** Бесплатная и кросс-платформенная база, покрывающая `IXSLT1` и большую часть `IXMLValidator`.

**Зависит от.** S2.

**Контекст для холодного агента.** Маппинг — §4.2, §4.4. Ограничения BCL: только XSLT 1.0; `XsltSettings.EnableScript` не работает вне Windows; EXSLT не поддержан; `XmlSchemaSet` — только XSD 1.0.

**Задачи.**
1. `BclXsltEngine`: компиляция стиля с кэшем по (path, mtime, length), `XsltSettings { EnableDocumentFunction = true, EnableScript = OperatingSystem.IsWindows() }`, трансформация в файл и в строку, `XsltArgumentList` для параметров.
2. Единый `XmlReaderFactory`: настройки (`DtdProcessing`, `XmlResolver`, `MaxCharactersFromEntities`, base URI), идентичное поведение для файла и текста.
3. `BclValidationEngine`:
   - `IsWellFormed` — обход `XmlReader`;
   - XSD 1.0 — `XmlSchemaSet` + `ValidationEventHandler`, поддержка `xs:include`/`xs:import` через `XmlResolver` с корректным base URI (иначе падают схемы с относительными include — типичная ловушка);
   - DTD — `ValidationType = Dtd` + `InMemoryDtdResolver`, отдающий DTD из текста (`DTDFromText`);
   - детектор XSD 1.1 (`vc:minVersion`, `xs:assert`, `xs:alternative`) → `EngineCapabilities` сообщает, что нужен Saxon, а не «тихо валидировать неправильно».
4. Свойства движка: `XsltVersions = [1.0]`, `XsdVersions = [1.0]`, `SupportsExslt = false`, `SupportsScriptExtensions = OperatingSystem.IsWindows()`.
5. Тесты: 12–15 стилей 1.0 (включая `document()`, параметры, `xsl:output` с разными кодировками), 8 схем XSD 1.0 (include/import, targetNamespace, элементы/атрибуты/ограничения), 5 DTD (внутренний, внешний, из текста), негативные кейсы (битый XML, несоответствие схеме → текст ошибки с координатами).

**Артефакты.** `src/AltovaGen.Engine.Bcl/**`, `tests/AltovaGen.UnitTests/**`.

**Проверка.**
```powershell
dotnet test --filter Category=Bcl
docker run --rm -v "${PWD}:/w" -w /w mcr.microsoft.com/dotnet/sdk:8.0 dotnet test --filter Category=Bcl
```

**Exit criteria.** Все тесты зелёные на Windows и Linux; `dotnet list package --include-transitive` для проекта = только BCL (I4); XSD 1.1 корректно распознаётся и приводит к явной ошибке «требуется Saxon», а не к ложному «valid».

**Оценка.** 2–3 дня.

---

### S4 — Compat-фасад `Altova.AltovaXML`

**Цель.** Потребитель компилируется и работает **без изменения кода вызовов**: `new Application()`, `app.XSLT1.InputXMLFromText = …`, `LastErrorMessage` и т.д.

**Зависит от.** S3.

**Контекст для холодного агента.** Полная поверхность — [`docs/altovaxml-api-surface.md`](../docs/altovaxml-api-surface.md). Оригинал — PIA: интерфейсы `IApplication`, `IXSLT1`, `IXSLT2`, `IXQuery`, `IXMLValidator` + coclass-классы (`Application`, `XSLT1`, `XSLT2`, `XQuery`, `XMLValidator`) и `*Class`-алиасы.

**Задачи.**
1. Пространство имён `Altova.AltovaXML` с типами: классы `Application`, `XSLT1`, `XSLT2`, `XQuery`, `XMLValidator` (ctor без параметров) + интерфейсы `I*` + устаревшие алиасы `*Class` (`[Obsolete]`, наследники) — чтобы работал и исходный, и бинарный сценарий.
2. Состояние и семантика как у COM-объекта: свойства-накопители (`InputXMLFromText`, `XSLFromText`, …), `Execute()`/`ExecuteAndGetResultAsString()`, очистка списков параметров/переменных, `LastErrorMessage` (обновляется на каждом вызове, не бросает исключение там, где Altova возвращала `false`).
3. Возвраты: `IsValid()`/`IsWellFormed()`/`IsValidWithExternalSchemaOrDTD()` → `bool`; ошибки — в `LastErrorMessage` (как у Altova), исключения не вылетают наружу (сохраняем контракт «bool + LastErrorMessage»).
4. Конструктор с внедрением `IEngineRouter` (для тестов и для DI), плюс ctor без параметров с дефолтной конфигурацией (BCL первым, Saxon вторым, если зарегистрирован).
5. Контрактные тесты (I5): тест-рефлексия сверяет список публичных членов с манифестом `tests/AltovaGen.UnitTests/CompatSurface.approved.txt` (утверждённый список из API-поверхности).
6. Тесты поведения: `LastErrorMessage` непуст на битом XML; повторный `Execute()` с изменённым входом даёт новый результат; `ClearExternalParameterList()` реально очищает.

**Артефакты.** `src/AltovaGen.Compat/**`, `CompatSurface.approved.txt`, тесты.

**Проверка.** `dotnet test --filter Category=Compat`; сборка демо-проекта, написанного «как для Altova».

**Exit criteria.** I5 зелёный (100 % членов); демо-потребитель на `AltovaGen.Compat` работает на Windows и Linux; ни одного `[ComImport]` (I2).

**Оценка.** 1–2 дня.

---

### S5 — Legacy-харнесс и «золотые» эталоны

**Цель.** Получить объективное доказательство паритета: эталонные результаты старого движка на реальном корпусе.

**Зависит от.** S0 (корпус), может идти параллельно S6.

**Контекст для холодного агента.** На текущей машине COM-сервер AltovaXML **не зарегистрирован** → эталоны здесь снять нельзя. Варианты: (а) машина/ВМ с установленным AltovaXML (32-бит) и рабочим потребителем; (б) уже существующие выходные файлы продового прогона; (в) если ничего нет — работаем по контрактным тестам + документированному описанию семантики (§4) и фиксируем это как принятый риск.

**Задачи.**
1. `tools/LegacyGolden/` — консольное приложение **net48 / x86**, которое:
   - ссылается на оригинальную `Altova.AltovaXML.dll` (PIA) как на COM-interop;
   - читает манифест заданий `tests/corpus/golden.manifest.json` (`{id, kind: xslt1|xslt2|xquery|validate, input, stylesheet|query|schema, params, outputMethod}`);
   - выполняет операцию через COM и складывает: результат, `LastErrorMessage`, хэш и размер;
   - пишет `tests/golden/<id>/output.*`, `meta.json` (версия движка, ОС, тайминги).
2. `--capture` (записать эталоны) и `--verify` (прогнать новый движок и сравнить) режимы; режим `--verify` живёт в `tests/AltovaGen.ParityTests` и на CI **не требует** Windows/Altova (работает по зафиксированным файлам).
3. Правила сравнения (§7.3) реализуются в `GoldenComparer`.
4. Прогон на 100 % корпуса; для каждого задания фиксируется статус: `match` / `diff-accepted` (с обоснованием в `tests/golden/<id>/accepted-diff.md`) / `diff-bug`.

**Артефакты.** `tools/LegacyGolden/**`, `tests/golden/**`, `tests/AltovaGen.ParityTests/**`, отчёт `docs/parity-report.md`.

**Проверка.**
```powershell
dotnet run --project tools/LegacyGolden -c Release -- --capture --manifest tests/corpus/golden.manifest.json
dotnet test tests/AltovaGen.ParityTests -c Release
```

**Exit criteria.** Для каждой группы API (из G1) есть ≥1 задание с подтверждённым `match`; расхождения классифицированы и обоснованы; отчёт `docs/parity-report.md` сгенерирован.

**Оценка.** 2–3 дня (плюс время на доступ к среде с AltovaXML). **Риск.** Отсутствие среды с AltovaXML ⇒ снижаем строгость (принятый риск R7 в §9).

---

### S6 — Saxon-адаптер: XSLT 2.0/3.0, XQuery 3.1, XSD 1.1 (гейт G2)

**Цель.** Закрыть то, что BCL не умеет, кросс-платформенно.

**Зависит от.** S2. **Параллелен** S3–S5. **Содержит гейт G2.**

**Контекст для холодного агента.** SaxonCS 12.10.0 (NuGet `SaxonCS`, target `net8.0`) — XSLT 3.0 / XQuery 3.1 / XPath 3.1 / XSD 1.1 / schema-awareness, **проприетарная лицензия с ключом**. Бесплатная альтернатива — Saxon-HE JAR (MPL-2.0) через IKVM 8.16.1 (`net8.0`, Win/Linux/macOS), но **без schema-awareness** ⇒ XSD-валидация остаётся на BCL (XSD 1.0), XSD 1.1 становится недоступен. Имена API SaxonCS 12 сверить по официальной документации (задача 1) — не полагаться на память.

**Задачи.**
1. **Спайк (0,5 дня):** консольный проект на net8.0, пробующий: компиляцию стиля 2.0 с `xsl:for-each-group`, вызов named template (`InitialTemplateName`), XQuery с `declare variable $v external`, установку export-свойств сериализации (`method`, `encoding`, `indent`, `omit-xml-declaration`), XSD 1.1 валидацию. Зафиксировать **точные** сигнатуры/имена (в `docs/adr/0002-engine-selection.md`), включая способ передачи лицензионного ключа (переменная окружения/файл/строка — по документации; ключ не хранить в репозитории).
2. **Гейт G2:** решение «покупаем SaxonCS» либо «идём через IKVM+Saxon-HE» (с явной потерей XSD 1.1) либо «остаёмся на BCL и сокращаем объём» (если G1 показал, что XSLT 2.0/XQuery не используются). Записать в ADR.
3. `SaxonXsltEngine`: трансформация в файл/строку, параметры (`SetStylesheetParameters`), `InitialTemplateName`/`InitialTemplateMode`, кэш `XsltExecutable` по хэшу стиля, пул `Processor` (создание Saxon Processor дорого — один на процесс, переиспользовать).
4. `SaxonQueryEngine`: `XQueryCompiler` → `XQueryEvaluator`, `ContextItem` для входного документа, `SetExternalVariable` (значение и XPath-выражение через `XPathCompiler`), сериализация с настройками.
5. `SaxonValidationEngine`: XSD 1.1 (`SchemaManager`/валидатор) с обработчиком невалидности → `Diagnostic[]`.
6. `EngineCapabilities` Saxon: XSLT {2.0, 3.0}, XQuery 3.1, XSD {1.0, 1.1}, EXSLT да, script-extensions нет.
7. Конфигурация: ключ лицензии из `SAXON_LICENSE`/файла; при отсутствии ключа движок помечает себя `Unlicensed` и роутер **явно** сообщает «нужен XSLT 2.0, но Saxon не лицензирован» вместо загадочного падения.
8. Тесты: только при наличии ключа/триала; иначе — `[Fact(Skip=…)]` + запись в отчёт «не покрыто по причине лицензии».

**Артефакты.** `src/AltovaGen.Engine.Saxon/**`, `docs/adr/0002-engine-selection.md` (обновлён), отчёт спайка.

**Проверка.**
```powershell
$env:SAXON_LICENSE = '<key>'
dotnet test --filter Category=Saxon
```
Плюс в Linux-контейнере.

**Exit criteria.** Стили 2.0 (с `for-each-group`, `xsl:function`, named template) и XQuery-запросы проходят на Windows и Linux; XSD 1.1 валидируется; отсутствие лицензии даёт понятную диагностику.

**Оценка.** 3–5 дней.

---

### S7 — Дифференциальное тестирование и отчёт о паритете

**Цель.** Исчерпывающе ответить на вопрос «полный аналог?» — с числами, а не ощущениями.

**Зависит от.** S4, S5, S6.

**Контекст для холодного агента.** Различия неизбежны в сериализации, порядке сообщений об ошибках, поддержке расширений. Задача — не «сделать байт-в-байт», а **классифицировать каждое расхождение** и закрыть те, что влияют на потребителя.

**Задачи.**
1. `AltovaGen.ParityTests`: прогон корпуса на всех доступных движках, сравнение с `tests/golden/**` через `GoldenComparer`.
2. Правила сравнения (§7.3): XML-семантическое сравнение (`XmlDocument`/`XNode.DeepEquals` с нормализацией пробелов и порядка атрибутов), побайтовое — только для `method=text`.
3. Генератор `docs/parity-report.md`: матрица «задание × группа API × движок → match/diff/accepted/N-A», сводные показатели (сколько `match`, сколько принятых расхождений).
4. Для каждого расхождения — запись: причина, риск для потребителя, план (исправить / переписать стиль / принять).
5. Скрипт-«регресс-храповик»: число `diff-bug` не может вырасти (аналогично approve-файлам).
6. Прогон стресс-кейсов: глубоко рекурсивный XSLT (проверка `LargeStackExecutor`), стиль с `exsl:node-set` (роутинг на Saxon), стиль с `msxsl:script` (ожидаемый отказ на Linux с внятным сообщением), схема с относительным `xs:include`.

**Артефакты.** `tests/AltovaGen.ParityTests/**`, `docs/parity-report.md`, `tests/golden/**/accepted-diff.md`.

**Проверка.** `dotnet test --filter Category=Parity`; отчёт генерируется и коммитится.

**Exit criteria.** Для каждого API-вызова потребителя из S0 есть подтверждённый `match` либо принятое расхождение с обоснованием; `diff-bug = 0`.

**Оценка.** 3–4 дня.

---

### S8 — Миграция потребителя

**Цель.** Прод-код перестаёт зависеть от x86 и COM.

**Зависит от.** S4, S7.

**Контекст для холодного агента.** Как правило, требуется: заменить ссылку на PIA на пакет/проект `AltovaGen.Compat`, снять `PlatformTarget=x86`/`Prefer32Bit`, убрать регистрацию COM-сервера из установщика/CI, заменить работу с `LastErrorMessage` (если где-то разбирался текст COM-ошибок).

**Задачи.**
1. Заменить ссылку: `<PackageReference Include="AltovaGen.Compat" />` (или `ProjectReference` на переходный период).
2. `<PlatformTarget>AnyCPU</PlatformTarget>`, убрать `Prefer32Bit`, `RuntimeIdentifier` при необходимости (`win-x64`, `linux-x64`).
3. Убрать из установщика/CI шаги установки и регистрации AltovaXML (в т.ч. `regsvr32`/установку 32-битного рантайма).
4. Прогнать функциональные тесты потребителя; отдельно — те сценарии, где раньше читался `LastErrorMessage`.
5. Кросс-платформенная проверка: `dotnet publish -r linux-x64` + smoke-тест в контейнере (тот же корпус).
6. Зафиксировать метрики до/после: время старта (у Altova был COM-старт), пиковая память, время трансформации на корпусе.
7. Обновить `README`/инструкцию по развёртыванию (больше не нужен 32-битный COM-сервер).

**Артефакты.** Изменённые проекты потребителя, `docs/migration-notes.md`, бенчмарк-отчёт `docs/perf-before-after.md`.

**Проверка.**
```powershell
dotnet build <CONSUMER_SLN> -c Release
dotnet test  <CONSUMER_SLN> -c Release
dotnet publish <CONSUMER_APP> -c Release -r linux-x64
docker run --rm -v "${PWD}:/w" -w /w mcr.microsoft.com/dotnet/runtime:8.0 dotnet /w/publish/<APP>.dll --smoke
```

**Exit criteria.** Потребитель собирается как AnyCPU, тесты зелёные на Windows и Linux, в установщике нет Altova/COM, `Altova.AltovaXML.dll` не лежит рядом с приложением.

**Оценка.** 2–4 дня (зависит от объёма потребителя).

---

### S9 — Релизный контур: CI-матрица, пакет, артефакты

**Цель.** Воспроизводимая сборка/поставка без Windows-специфики.

**Зависит от.** S8.

**Задачи.**
1. CI (GitHub Actions или иной): матрица `windows-latest` / `ubuntu-latest` / `macos-latest` (последний — по возможности), `dotnet test`, `dotnet pack`.
2. Job «инварианты»: I1 (CorFlags), I2 (grep COM), I4 (нет лишних зависимостей у BCL-движка), I5 (Compat-поверхность).
3. Публикация NuGet-пакета `AltovaGen.Compat` (+ `Abstractions`, `Engine.Bcl`), версия `0.x`, без SaxonCS в зависимостях BCL-пакета (Saxon — отдельный опциональный пакет).
4. SBOM (`dotnet CycloneDX` или аналог) + проверка лицензий зависимостей (`dotnet-project-licenses`), чтобы платная/несовместимая лицензия не протекла в базовый путь.
5. Артефакты релиза: nupkg + checksums + запись в `CHANGELOG.md`.

**Проверка.** CI зелёный на всех раннерах; `dotnet nuget verify`/установка пакета в чистый проект проходит.

**Exit criteria.** Релиз воспроизводится одной командой на чистой машине; базовый пакет не тянет Saxon.

**Оценка.** 1–2 дня.

---

### S10 — (Опционально, гейт G3) XBRL-контур

**Цель.** Закрыть `TreatXBRLInconsistenciesAsErrors`, если XBRL реально используется.

**Зависит от.** S0 (гейт G3).

**Контекст для холодного агента.** В BCL и Saxon-HE/CS нет XBRL. Варианты: **RaptorXML+XBRL Server** (Altova, сертифицирован XBRL International, Windows/Linux/macOS; API: Java везде, .NET/COM — Windows, HTTP REST — везде) либо **Arelle** (открытый XBRL-процессор, Python, кросс-платформенный; лицензию и точную семантику проверить).

**Задачи.** Выбрать вариант и записать ADR; реализовать `IValidationEngine` для XBRL (для RaptorXML — интеграция через HTTP REST → работает везде; для Arelle — sidecar-процесс/CLI); тесты на таксономии/инстансе потребителя.

**Exit criteria.** XBRL-сценарии потребителя проходят; решение G3 зафиксировано.

**Оценка.** 3–10 дней в зависимости от варианта.

---

### S11 — Decommission и документация

**Цель.** Убрать legacy-хвосты, чтобы x86 не вернулся.

**Зависит от.** S8, S9.

**Задачи.**
1. Удалить `Altova.AltovaXML.dll` из рабочей папки/из потребителя (после подтверждения, что ничего не ссылается); заархивировать копию в `legacy/` (только если политика позволяет) либо в внутреннее хранилище артефактов.
2. Убрать из репозитория всё, что упоминает COM-регистрацию AltovaXML.
3. ADR-0001/0003 (отказ от COM; лицензии), обновление `docs/`.
4. Финальный прогон полного CI.

**Exit criteria.** `rg -i 'altovaxml|comimport|platformtarget>x86'` по репозиториям потребителя и `AltovaGen` даёт только документацию/историю.

**Оценка.** 0,5 дня.

---

## 7. Тестовая стратегия

### 7.1 Уровни

| Уровень | Что проверяет | Где |
|---|---|---|
| Инварианты сборки | AnyCPU, отсутствие COM, отсутствие лишних зависимостей | `Category=BuildInvariants` |
| Контрактные | Полнота API Compat-фасада, семантика `LastErrorMessage`, повторные вызовы | `Category=Compat` |
| Юнит | Каждый движок отдельно на искусственных кейсах | `Category=Bcl`, `Category=Saxon` |
| Дифференциальные | Совпадение с эталонами Altova на реальном корпусе | `Category=Parity` |
| Кросс-платформенные | Те же тесты в Linux-контейнере и (по возможности) на macOS | CI-матрица |
| Нагрузочные | Глубокая рекурсия, большие документы, повторное использование Processor | `Category=Stress` |

### 7.2 Корпус

`tests/corpus/` — реальные ассеты потребителя + минимальные синтетические кейсы на непокрытые ветки (DTD из текста, схема с `xs:import`, `xsl:result-document`, `exsl:node-set`, `msxsl:script`, XSD 1.1, named template).

### 7.3 Правила сравнения результатов (утвердить до S5)

1. Для `OutputMethod = xml`: **семантическое** сравнение — разбор обоих результатов, сравнение деревьев с игнорированием insignificant whitespace вне `xml:space="preserve"`, порядка атрибутов и вида XML-декларации; кодировка — учитывается только при явном указании в запросе.
2. Для `OutputMethod = text`: **побайтовое** сравнение после нормализации переводов строк.
3. Для `html`: сравнение нормализованного HTML (декларация/`meta charset` — вне сравнения).
4. Для сообщений об ошибках: сравнивается **факт** ошибки и позиция (строка/столбец); текст — с точностью до формулировки, регресс-храповик на «ошибка там, где Altova её не видел» и наоборот.
5. Любое расхождение обязано иметь запись в `accepted-diff.md` с обоснованием; иначе тест падает.

### 7.4 Что нельзя проверить без внешних условий

- Реальные эталоны Altova, если нет машины с зарегистрированным AltovaXML (S5) — фиксируем как принятый риск.
- Saxon-путь без лицензионного ключа — тесты скипаются и попадают в отчёт как «не покрыто по причине лицензии».
- XBRL — только при наличии среды RaptorXML/Arelle.

---

## 8. Риски

| # | Риск | Влияние | Митигация |
|---|---|---|---|
| R1 | Неизвестен фактический объём использования XSLT 2.0/XQuery | Переплата за Saxon или лишняя работа | Гейт S0/G1: сначала инвентаризация, потом выбор движка |
| R2 | SaxonCS — платная лицензия | Бюджет/юридические ограничения | Гейт G2: бесплатный путь IKVM+Saxon-HE (с потерей XSD 1.1) либо RaptorXML |
| R3 | Altova-специфичные расширения/`msxsl:script`/EXSLT в стилях | Часть стилей не заработает «как есть» | Скрипт-сканер S0, роутинг на Saxon для EXSLT, отдельная задача переписывания |
| R4 | Расхождения сериализации | Диффы в потребителе | §7.3: правила сравнения + явные настройки вывода; golden-тесты |
| R5 | Нет среды с AltovaXML ⇒ нет эталонов | Снижение доказательности паритета | Использовать продовые выходные файлы; расширить контрактные тесты; зафиксировать принятый риск |
| R6 | XBRL в реальном использовании | Нужен отдельный коммерческий/внешний компонент | Гейт G3; RaptorXML (HTTP/Java) или Arelle |
| R7 | Производительность SaxonCS (старт/JIT, память) | Регресс по latency | Пул `Processor`, кэш `XsltExecutable`, бенчмарк S8.6 |
| R8 | Различия `XmlSchemaSet` и Altova в деталях XSD 1.0 | Ложные «valid/invalid» | Тесты на реальных схемах; при расхождениях — Saxon-валидатор XSD 1.0 |
| R9 | Лицензионные ограничения на копирование корпуса/стилей | Нельзя коммитить ассеты | Хранить корпус вне репозитория/в закрытом хранилище, в репо — только манифест и синтетика |

---

## 9. Открытые вопросы к владельцу (нужны ответы для S0/S6/S10)

1. **Где лежит проект-потребитель** (путь/репозиторий)? В `E:\Git\MyProject` ссылок на Altova нет; поиск по всему `E:\Git` не завершился из-за объёма.
2. Какие из четырёх объектов реально используются: `XSLT1`, `XSLT2`, `XQuery`, `XMLValidator`? Есть ли вызовы `InitialTemplateName`/`InitialTemplateMode`?
3. Есть ли среди стилей: `exsl:node-set`, `msxsl:script`, `altova:*`-функции, `xsl:result-document`, `document()`?
4. Требуется ли **XSD 1.1** и **XBRL** (`TreatXBRLInconsistenciesAsErrors`)?
5. Допустима ли **платная лицензия SaxonCS** (Saxon 12 for .NET)? Если нет — согласны ли на бесплатный путь (IKVM + Saxon-HE) с потерей XSD 1.1, либо на RaptorXML?
6. Есть ли машина/ВМ с установленным AltovaXML (32-бит) для снятия эталонов?
7. Нужна ли **бинарная** совместимость (подменить DLL без пересборки потребителя) или достаточно исходной (пересборка с новым PackageReference)? Исходная — надёжнее: PIA strong-named (`PublicKeyToken=bab301ea12809d38`), а наш фасад подписать тем же ключом невозможно.

---

## 10. Приложения

### A. Команды проверки (шпаргалка)

```powershell
# 1. Разрядность и PE-флаги любой сборки (ожидаем ILOnly без Required32Bit / 32BitPreferred)
$p = 'E:\Git\MyProject\AltovaGen\src\AltovaGen.Compat\bin\Release\net8.0\AltovaGen.Compat.dll'
$fs = [IO.File]::OpenRead($p)
$pe = [System.Reflection.PortableExecutable.PEReader]::new($fs)
$flags = $pe.PEHeaders.CorHeader.Flags
"ILOnly        = $([bool]($flags -band 0x1))"
"32BitRequired = $([bool]($flags -band 0x2))"   # должно быть False
"32BitPreferred= $([bool]($flags -band 0x20000))" # должно быть False
$pe.Dispose(); $fs.Dispose()

# 2. Отсутствие COM в исходниках (должно быть пусто)
rg -n 'ComImport|CoClass|GetTypeFromProgID|GetActiveObject|CoCreateInstance' src/

# 3. Кросс-платформенная сборка и тесты
dotnet publish src/AltovaGen.Compat -c Release -r linux-x64
docker run --rm -v "${PWD}:/w" -w /w mcr.microsoft.com/dotnet/sdk:8.0 `
  dotnet test AltovaGen.sln -c Release

# 4. Полный листинг API исходной PIA (для сверки фасада)
dotnet tool install -g dotnet-ildasm
dotnet-ildasm "E:\Git\MyProject\AltovaGen\Altova.AltovaXML.dll" | Select-String 'ComImport|interface|class'
```

### B. Эскизы кода

**B.1. Compat-фасад (фрагмент).**

```csharp
namespace Altova.AltovaXML;

public sealed class Application
{
    private readonly IEngineRouter _router;
    public Application() : this(EngineRouter.Default) { }
    public Application(IEngineRouter router) => _router = router;

    private XSLT1? _xslt1; private XSLT2? _xslt2; private XQuery? _xquery; private XMLValidator? _validator;
    public XSLT1 XSLT1 => _xslt1 ??= new XSLT1(_router);
    public XSLT2 XSLT2 => _xslt2 ??= new XSLT2(_router);
    public XQuery XQuery => _xquery ??= new XQuery(_router);
    public XMLValidator XMLValidator => _validator ??= new XMLValidator(_router);
}

public sealed class XSLT1
{
    private readonly IEngineRouter _router;
    private readonly Dictionary<string, string> _params = new(StringComparer.Ordinal);
    internal XSLT1(IEngineRouter router) => _router = router;

    public string? InputXMLFileName { get; set; }
    public string? XSLFileName { get; set; }
    public string? InputXMLFromText { get; set; }
    public string? XSLFromText { get; set; }
    public int XSLStackSize { get; set; }
    public string LastErrorMessage { get; private set; } = "";
    public bool DotNetExtensionsEnabled { get; set; }
    public bool JavaExtensionsEnabled { get; set; }

    public void AddExternalParameter(string name, string value) => _params[name] = value;
    public void ClearExternalParameterList() => _params.Clear();

    public void Execute(string outputFileName)
    {
        var req = BuildRequest();
        var result = _router.Xslt.Execute(req, OutputTarget.File(outputFileName));
        LastErrorMessage = result.Success ? "" : DiagnosticFormatter.ToAltovaString(result.Diagnostics);
        if (!result.Success) throw new AltovaEngineException(LastErrorMessage); // поведение уточнить по S5-эталонам
    }

    public string ExecuteAndGetResultAsString()
    {
        var result = _router.Xslt.Execute(BuildRequest(), OutputTarget.String);
        LastErrorMessage = result.Success ? "" : DiagnosticFormatter.ToAltovaString(result.Diagnostics);
        return result.Value ?? "";
    }
    // ...
}
```

**B.2. BCL-движок: XSLT 1.0 с большим стеком (фрагмент).**

```csharp
internal sealed class BclXsltEngine : IXsltEngine
{
    public EngineCapabilities Capabilities => new(XsltVersions: [new Version(1,0)],
        SupportsExslt: false, SupportsScriptExtensions: OperatingSystem.IsWindows());

    public TransformResult Execute(TransformRequest request, OutputTarget output)
    {
        var settings = new XsltSettings(enableDocumentFunction: true,
                                       enableScript: OperatingSystem.IsWindows());
        var xslt = new XslCompiledTransform();
        using var styleReader = XmlReaderFactory.Create(request.Stylesheet);
        xslt.Load(styleReader, settings, XmlReaderFactory.ResolverFor(request));

        var args = new XsltArgumentList();
        foreach (var (k, v) in request.Parameters) args.AddParam(k, string.Empty, v);

        var sb = output.IsFile ? null : new StringBuilder();
        Action work = () =>
        {
            using var input = XmlReaderFactory.Create(request.Input);
            if (output.IsFile)
            {
                using var writer = XmlWriter.Create(output.Path!, OutputWriterSettings(request.Output));
                xslt.Transform(input, args, writer);
            }
            else
            {
                using var writer = XmlWriter.Create(sb, OutputWriterSettings(request.Output));
                xslt.Transform(input, args, writer);
            }
        };
        LargeStackExecutor.Run(work, request.StackSizeHint ?? 16 * 1024 * 1024);
        return TransformResult.Ok(sb?.ToString());
    }
}
```

**B.3. DTD из текста (резолвер).**

```csharp
internal sealed class InMemoryDtdResolver(string dtdText, Uri? baseUri) : XmlResolver
{
    public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
    {
        // Отдаём подготовленный DTD независимо от запрошенного public/system id,
        // как это делал Altova при установке DTDFromText.
        return new StringReader(dtdText);
    }
    public override Uri ResolveUri(Uri? baseUri, string? relativeUri) => this.baseUri ?? new Uri("file:///memory/");
}
```

**B.4. Legacy-хелпер (net48/x86), фрагмент.**

```csharp
// tools/LegacyGolden/Program.cs — только Windows, только для снятия эталонов
var app = new Altova.AltovaXML.Application();      // COM-активация 32-битного сервера
var xslt = app.XSLT1;
xslt.XSLFileName = job.StylesheetPath;
xslt.InputXMLFileName = job.InputPath;
foreach (var (k, v) in job.Parameters) xslt.AddExternalParameter(k, v);
xslt.Execute(job.OutputPath);
File.WriteAllText(Path.ChangeExtension(job.OutputPath, ".err.txt"), xslt.LastErrorMessage ?? "");
```

### C. Оценка трудозатрат

| Шаг | Оценка, дней | Параллелится с |
|---|---|---|
| S0 инвентаризация (гейт G1) | 1–2 | — |
| S1 скелет + инварианты | 0,5 | — |
| S2 абстракции | 1 | — |
| S3 BCL-движок | 2–3 | S6 |
| S4 Compat-фасад | 1–2 | S6 |
| S5 golden-харнесс и эталоны | 2–3 | S6 |
| S6 Saxon-адаптер (гейт G2) | 3–5 | S3–S5 |
| S7 дифтесты и отчёт | 3–4 | — |
| S8 миграция потребителя | 2–4 | — |
| S9 CI/релиз | 1–2 | — |
| S10 XBRL (опция) | 3–10 | — |
| S11 decommission | 0,5 | — |
| **Итого (без XBRL, без запаса)** | **17–27** | критический путь ≈ 14–21 |

Диапазоны — для одного исполнителя; при двух параллельных потоках (BCL-путь и Saxon-путь) календарный срок сокращается примерно на 3–5 дней.

### D. Плановая мутация (протокол изменения плана)

- **Добавить шаг:** только после S0 (новые факты об использовании API) или G2/G3; шаг должен иметь собственные exit criteria и не ломать инварианты I1–I6.
- **Пропустить шаг:** S6 можно пропустить целиком, если G1 показал «XSLT 1.0 + XSD 1.0 только» — тогда Saxon-путь остаётся как задел (проект есть, пакет не подключается).
- **Разбить шаг:** S3 → (XSLT 1.0) + (валидация) при обнаружении больших объёмов схемной валидации.
- **Отменить:** S10 при G3 = «XBRL не используется»; S5 при отсутствии среды — заменяется на расширенные контрактные тесты с записью принятого риска.

Любая мутация фиксируется в `docs/adr/` с датой и причиной.
