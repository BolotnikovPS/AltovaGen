# Измеренная API-поверхность `Altova.AltovaXML.dll` (v12.3, PIA)

Источник данных: файл `Altova.AltovaXML.dll` (13 312 байт, SHA-256 `3593A91996C8B01BD46CD6B9B4E18665A88B030C8607813818828AD73589B703`), прочитан через `System.Reflection.Metadata` (`PEReader` + `MetadataReader`). Date: 2026-10-02.

## Идентичность сборки

| Поле | Значение |
|---|---|
| Simple name | `Altova.AltovaXML` |
| Version | `12.3.0.0` |
| Culture | `neutral` |
| PublicKeyToken | `bab301ea12809d38` (strong-named PIA) |
| ImageRuntimeVersion | `v1.1.4322` |
| PEKind / Machine | `ILOnly` / `I386` (флаг `Required32Bit` отсутствует → AnyCPU-сборка) |
| PE-формат | `PE32` (0x010B), 3 секции |
| AssemblyRef | `mscorlib, Version=1.0.5000.0, PublicKeyToken=b77a5c561934e089` |
| Характер типов | только `[ComImport]`-интероперабельность (RCW/PIA), managed-логики нет |

## COM-идентификаторы (TypeLib)

| Тип | GUID |
|---|---|
| `IApplication` / `Application` | `F0158B7E-77D2-4365-A0C4-177E84F0CC1D` |
| `IXSLT1` / `XSLT1` | `7654E776-D167-4B91-A3EC-DC7C41BD2473` |
| `IXSLT2` / `XSLT2` | `5815D2CF-D975-4DE6-8E30-215AB3F8B2B8` |
| `IXQuery` / `XQuery` | `3787E161-FD6A-46B8-A146-3461FAA12419` |
| `IXMLValidator` / `XMLValidator` | `E994BCA7-9C98-4049-A633-483BBDD6CFAA` |
| coclass `ApplicationClass` | `533BAC07-C702-4D91-8D37-39FDC919A19C` |
| `_IApplicationEvents` + `_IApplicationEvents_EventProvider` / `_SinkHelper` | COM-события (в фасаде не воспроизводятся) |

## Полная поверхность членов

### `IApplication` / `Application`
- Свойства (только чтение): `XSLT1` → `IXSLT1`/`XSLT1`; `XSLT2` → `IXSLT2`/`XSLT2`; `XQuery` → `IXQuery`/`XQuery`; `XMLValidator` → `IXMLValidator`/`XMLValidator`.

### `IXSLT1` / `XSLT1`
Свойства (чтение/запись):
`InputXMLFileName`, `XSLFileName`, `InputXMLFromText`, `XSLFromText`, `XSLStackSize`, `DotNetExtensionsEnabled`, `JavaExtensionsEnabled`; (только чтение) `LastErrorMessage`.

Методы:
- `void Execute(string outputFileName)`
- `string ExecuteAndGetResultAsString()`
- `void AddExternalParameter(string name, string value)`
- `void ClearExternalParameterList()`

### `IXSLT2` / `XSLT2`
Всё, что у `IXSLT1`, **плюс** свойства `InitialTemplateName`, `InitialTemplateMode` (чтение/запись).

### `IXQuery` / `XQuery`
Свойства (чтение/запись):
`XQueryFileName`, `InputXMLFileName`, `XQueryFromText`, `InputXMLFromText`, `OutputEncoding`, `OutputIndent`, `OutputMethod`, `OutputOmitXMLDeclaration`, `DotNetExtensionsEnabled`, `JavaExtensionsEnabled`; (только чтение) `LastErrorMessage`.

Методы:
- `void Execute(string outputFileName)`
- `string ExecuteAndGetResultAsString()`
- `void AddExternalVariable(string name, string value)`
- `void AddExternalVariableAsXPath(string name, string xpathExpression)`
- `void ClearExternalVariableList()`

### `IXMLValidator` / `XMLValidator`
Свойства (чтение/запись):
`InputXMLFileName`, `SchemaFileName`, `DTDFileName`, `InputXMLFromText`, `SchemaFromText`, `DTDFromText`, `TreatXBRLInconsistenciesAsErrors`; (только чтение) `LastErrorMessage`.

Методы:
- `bool IsValid()`
- `bool IsWellFormed()`
- `bool IsValidWithExternalSchemaOrDTD()`

## Как перепроверить

```powershell
dotnet tool install -g dotnet-ildasm
dotnet-ildasm "E:\Git\MyProject\AltovaGen\Altova.AltovaXML.dll"

# либо без внешних инструментов, через метаданные:
$fs  = [IO.File]::OpenRead("E:\Git\MyProject\AltovaGen\Altova.AltovaXML.dll")
$pe  = [System.Reflection.PortableExecutable.PEReader]::new($fs)
$md  = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
"ILOnly=$([bool]($pe.PEHeaders.CorHeader.Flags -band 0x1)) 32BitRequired=$([bool]($pe.PEHeaders.CorHeader.Flags -band 0x2))"
$md.TypeDefinitions | ForEach-Object { $md.GetString($md.GetTypeDefinition($_).Namespace) + '.' + $md.GetString($md.GetTypeDefinition($_).Name) }
$pe.Dispose(); $fs.Dispose()
```

## Сверка с новым фасадом

Файл-манифест для теста `CompatSurface` должен содержать ровно перечисленные выше члены (тип → имя → вид: свойство/метод → тип возврата). Тест падает, если фасад теряет член.
