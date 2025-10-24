# Сравнение Подходов к Объединению Шрифтов

## Краткое резюме

| Аспект | iText Optimizer (EXAMPLE) | PdfFontOptimizer (Текущий) | Статус |
|--------|--------------------------|---------------------------|--------|
| **Физическое объединение файлов** | ✅ Да (`TrueTypeFont.Merge`) | ❌ Нет (просто копия) | 🔴 КРИТИЧНО |
| **Объединение Widths** | ✅ С проверкой конфликтов | ❌ Простая копия | 🔴 КРИТИЧНО |
| **Объединение ToUnicode** | ✅ С проверкой конфликтов | ⚠️ Частично (CID remapping) | 🟡 ТРЕБУЕТ ДОРАБОТКИ |
| **Сбор используемых глифов** | ✅ Через listener | ⚠️ Есть попытка | 🟡 ТРЕБУЕТ ДОРАБОТКИ |
| **Валидация конфликтов** | ✅ Полная | ❌ Отсутствует | 🔴 КРИТИЧНО |
| **Обработка ошибок** | ✅ Отказ при конфликте | ❌ Нет проверок | 🔴 КРИТИЧНО |

## Ключевая разница

### iText Optimizer
```
Subset-1 (A,B,C) ──┐
                   ├──> TrueTypeFont.Merge() ──> Новый файл (A,B,C,D,E,F)
Subset-2 (D,E,F) ──┘
```
**Результат**: Физически новый файл шрифта со всеми глифами.

### PdfFontOptimizer (текущий)
```
Subset-1 (A,B,C) ──┐
                   ├──> Copy FontFile2 ──> Ссылка на Subset-1 (A,B,C)
Subset-2 (D,E,F) ──┘
```
**Результат**: Ссылка на первый subset. Глифы D,E,F **ОТСУТСТВУЮТ** в результате!

## Почему текущий подход не работает

### Пример проблемы

Пусть в документе:
- **Страница 1**: Использует `ABCDEF+Calibri` с глифами {A, B, C}
- **Страница 2**: Использует `GHIJKL+Calibri` с глифами {D, E, F}

#### Что делает iText Optimizer:
1. Собирает глифы: {A,B,C} + {D,E,F}
2. **Создает новый файл шрифта** с глифами {A,B,C,D,E,F}
3. Обновляет Widths для всех 6 глифов
4. Обновляет ToUnicode для всех 6 глифов
5. Обе страницы ссылаются на новый объединенный шрифт

**Результат**: ✅ Весь текст читается корректно

#### Что делает текущий PdfFontOptimizer:
1. Собирает глифы: {A,B,C} + {D,E,F}
2. **Копирует FontFile2** из первого шрифта (содержит только A,B,C)
3. Создает новый ToUnicode для всех 6 символов
4. Обе страницы ссылаются на "объединенный" шрифт

**Проблема**: ❌ Физический файл шрифта содержит только {A,B,C}. Глифы {D,E,F} **отсутствуют**!

**Результат**: 
- Страница 1: ✅ Текст читается (глифы A,B,C есть в файле)
- Страница 2: ❌ Текст искажен (глифы D,E,F отсутствуют в файле)

## Визуализация проблемы

### Корректный подход (iText Optimizer)

```
PDF Structure:                Font File (физический):
┌──────────────┐             ┌─────────────────────┐
│ Page 1       │             │ TrueType Tables:    │
│   /Font:     │             │   glyf: A,B,C,D,E,F │
│   /F1 -> #10 │────────┐    │   loca: ...         │
└──────────────┘        │    │   cmap: ...         │
                        │    │   hmtx: widths...   │
┌──────────────┐        │    └─────────────────────┘
│ Page 2       │        │              ▲
│   /Font:     │        │              │
│   /F1 -> #10 │────────┼──────────────┘
└──────────────┘        │
                        ▼
                 ┌─────────────┐
                 │ Object #10  │
                 │   /BaseFont │
                 │   /FontFile2│──> Физический файл
                 │   /Widths   │    с ВСЕМИ глифами
                 │   /ToUnicode│
                 └─────────────┘
```

### Неправильный подход (текущий)

```
PDF Structure:                Font File (физический):
┌──────────────┐             ┌─────────────────────┐
│ Page 1       │             │ TrueType Tables:    │
│   /Font:     │             │   glyf: A,B,C       │ ← ТОЛЬКО A,B,C!
│   /F1 -> #10 │────────┐    │   loca: ...         │
└──────────────┘        │    │   cmap: ...         │
                        │    │   hmtx: ...         │
┌──────────────┐        │    └─────────────────────┘
│ Page 2       │        │              ▲
│   /Font:     │        │              │
│   /F1 -> #10 │────────┼──────────────┘
└──────────────┘        │
                        ▼
                 ┌─────────────┐
                 │ Object #10  │
                 │   /BaseFont │
                 │   /FontFile2│──> Ссылка на старый
                 │   /Widths   │    файл (только A,B,C)
                 │   /ToUnicode│──> Новая карта (A-F) ⚠️
                 └─────────────┘       НО глифов D,E,F
                                       НЕТ в файле!
```

**Проблема**: ToUnicode говорит "CID 4 = 'D'", но в физическом файле нет глифа с GID 4!

## Что нужно исправить

### 1. Физическое объединение файлов шрифтов

**До** (текущий код):
```csharp
var fontFile2 = originalFontDescriptor.Get(PdfName.FontFile2);
if (fontFile2 != null)
{
    newFontDescriptor.Put(PdfName.FontFile2, fontFile2); // ❌ Просто копия!
}
```

**После** (правильный подход):
```csharp
// Собрать все используемые GID из всех subset'ов
var fontToGids = new Dictionary<TrueTypeFont, ICollection<int>>();
foreach (var context in contexts)
{
    var ttfParser = CreateTrueTypeFontParser(context.FontDict);
    var gids = ExtractGids(usedGlyphs[context]);
    fontToGids[ttfParser] = gids;
}

// КРИТИЧНО: Физически объединить таблицы TrueType
byte[] mergedFontBytes = TrueTypeFont.Merge(fontToGids, fontName);
var mergedStream = new PdfStream(mergedFontBytes);

newFontDescriptor.Put(PdfName.FontFile2, mergedStream); // ✅ Новый файл!
```

### 2. Проверка согласованности метрик

**До** (текущий код):
```csharp
var widths = sourceCidFont.Get(PdfName.W);
if (widths != null)
{
    cidFont.Put(PdfName.W, widths); // ❌ Просто копия без проверки!
}
```

**После** (правильный подход):
```csharp
var allCodeToWidth = new Dictionary<int, int>();

foreach (var context in contexts)
{
    var widths = ExtractWidths(context.FontDict);
    
    foreach (var code in usedCodes[context])
    {
        int width = widths[code];
        
        // КРИТИЧНО: Проверка конфликтов
        if (allCodeToWidth.ContainsKey(code) && allCodeToWidth[code] != width)
        {
            // ОТКАЗ от объединения!
            return MergeResult.Failed("Width conflict for code " + code);
        }
        
        allCodeToWidth[code] = width;
    }
}

var mergedWidths = CreateWidthsArray(allCodeToWidth);
cidFont.Put(PdfName.W, mergedWidths); // ✅ Объединенные метрики!
```

### 3. Проверка согласованности ToUnicode

**До** (текущий код):
```csharp
var toUnicodeStream = CreateRemappedToUnicodeCMap(mergedToUnicode, baseFontName);
mergedDict.Put(PdfName.ToUnicode, toUnicodeStream); // ❌ Без проверки конфликтов!
```

**После** (правильный подход):
```csharp
var codeToUnicode = new Dictionary<int, string>();

foreach (var context in contexts)
{
    var mappings = ExtractToUnicodeMappings(context.FontDict);
    
    foreach (var mapping in mappings)
    {
        int code = mapping.Key;
        string unicode = mapping.Value;
        
        // КРИТИЧНО: Проверка конфликтов
        if (codeToUnicode.ContainsKey(code) && codeToUnicode[code] != unicode)
        {
            // ОТКАЗ от объединения!
            return MergeResult.Failed("ToUnicode conflict for code " + code);
        }
        
        codeToUnicode[code] = unicode;
    }
}

var mergedToUnicode = CreateToUnicodeStream(codeToUnicode);
mergedDict.Put(PdfName.ToUnicode, mergedToUnicode); // ✅ Согласованный ToUnicode!
```

## Путь вперед

### Вариант 1: Использовать iText API (рекомендуется)
- Найти или получить доступ к `TrueTypeFont.Merge()`
- Реализовать полный pipeline как в EXAMPLE
- **Результат**: Полноценное объединение с поддержкой всех глифов

### Вариант 2: Упрощенное решение (если Вариант 1 невозможен)
- Проверять, что все subset'ы физически идентичны
- Если идентичны - использовать любой из них
- Если различны - отказаться от объединения
- **Результат**: Удаление дубликатов, но не объединение неполных subset'ов

### Вариант 3: Реализовать собственный merger (сложно)
- Изучить формат TrueType (glyf, loca, cmap, hmtx таблицы)
- Реализовать логику слияния таблиц
- **Не рекомендуется**: Очень сложно, легко сделать ошибку

## Заключение

Текущий код **не может** объединять неполные subset'ы шрифтов, так как отсутствует **физическое объединение файлов шрифтов**. 

Для корректной работы критически необходимо:
1. ✅ Реализовать физическое объединение (или проверку идентичности)
2. ✅ Добавить проверку согласованности метрик
3. ✅ Добавить проверку согласованности ToUnicode
4. ✅ Корректно обрабатывать конфликты (отказ от объединения)

Без этих изменений результат будет **искаженным текстом** в PDF.
