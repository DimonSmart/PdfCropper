# План Работ по Объединению Подмножеств Шрифтов

## Дата создания: 2025-10-24

## Цель проекта
Объединить множество раздробленных подмножеств (subsets) шрифтов в PDF документе в единый минимальный набор объединенных шрифтов, устранив избыточность и значительно уменьшив размер файла.

## Анализ текущего состояния

### ✅ Что работает:
1. **Индексация шрифтов** - корректно собирает информацию о всех шрифтах в документе
2. **Группировка по базовым именам** - правильно определяет, какие subset'ы относятся к одному шрифту
3. **Создание таблицы переназначения** - корректно маппирует старые ресурсы на новые
4. **CID Remapping** - есть механизм разрешения конфликтов кодировок

### ❌ Критические проблемы:
1. **Отсутствует физическое объединение файлов шрифтов** - просто копируется FontFile2 из первого источника
2. **Не объединяются метрики (Widths/W)** - копируется массив из первого источника
3. **Неполное объединение ToUnicode** - создается новая карта, но она не согласована с физическим файлом шрифта
4. **Нет валидации и обработки конфликтов** - система не отказывается от объединения при несовместимости

## Архитектурное решение

Вынести логику объединения subset'ов в отдельный класс `FontSubsetMerger`, который будет следовать паттерну из iText Optimizer:

```
FontSubsetMerger
├── CollectUsedGlyphs() - сбор реально используемых глифов
├── MergeFontFiles() - физическое объединение файлов шрифтов
├── MergeWidths() - объединение метрик с проверкой согласованности
├── MergeToUnicode() - объединение ToUnicode с проверкой конфликтов
└── CreateMergedFontDictionary() - сборка итогового словаря
```

## Пошаговый план реализации

### Фаза 1: Подготовка и анализ (завершена ✅)

**Статус: Выполнено**

- ✅ Анализ кода из EXAMPLE (iText Optimizer)
- ✅ Сравнение с текущей реализацией
- ✅ Выявление критических различий
- ✅ Создание документации по анализу

---

### Фаза 2: Создание класса FontSubsetMerger

**Цель**: Вынести логику объединения в отдельный класс с четкой ответственностью

**Шаги**:

1. **Создать класс `FontSubsetMerger`**
   - Местоположение: `src/DimonSmart.PdfCropper.FontExperiments/FontSubsetMerger.cs`
   - Интерфейс:
     ```csharp
     public class FontSubsetMerger
     {
         public MergeResult TryMerge(List<FontContext> contexts, PdfDocument document);
     }
     
     public class MergeResult
     {
         public bool Success { get; set; }
         public PdfDictionary? MergedFontDict { get; set; }
         public string? FailureReason { get; set; }
         public Dictionary<int, string>? MergedToUnicode { get; set; }
         public Dictionary<(int, string, int), int>? CidRemapping { get; set; }
     }
     ```

2. **Реализовать валидацию входных данных**
   - Проверка: все контексты относятся к одному базовому шрифту
   - Проверка: все шрифты имеют одинаковый тип (Type0 или TrueType)
   - Проверка: все шрифты либо имеют ToUnicode, либо не имеют

**Тест**: `TestFontSubsetMergerCreation`

---

### Фаза 3: Сбор используемых глифов

**Цель**: Собрать информацию о **реально используемых** глифах из содержимого страниц

**Шаги**:

1. **Реализовать `UsedGlyphCollector`**
   - Создать listener на базе `IEventListener`
   - Перехватывать события `RENDER_TEXT`
   - Извлекать GlyphLine из PdfString
   - Собирать Set<Glyph> для каждого шрифта

2. **Интегрировать в FontSubsetMerger**
   ```csharp
   private Dictionary<FontContext, HashSet<Glyph>> CollectUsedGlyphs(
       List<FontContext> contexts, 
       PdfDocument document)
   {
       // Для каждого контекста собрать используемые глифы из содержимого страницы
   }
   ```

**Важно**: Собираем именно объекты Glyph (с Code + Unicode), а не просто коды символов.

**Тест**: `TestUsedGlyphCollection`

---

### Фаза 4: Физическое объединение файлов шрифтов

**Цель**: Создать новый физический файл шрифта с объединенным набором глифов

**Варианты реализации**:

#### Вариант A (Рекомендуемый): Использовать iText API

```csharp
private PdfStream? MergeTrueTypeFontFiles(
    Dictionary<FontContext, HashSet<Glyph>> usedGlyphs,
    string fontName)
{
    var fontToGids = new Dictionary<TrueTypeFont, ICollection<int>>();
    
    foreach (var entry in usedGlyphs)
    {
        var docFont = entry.Key.FontDict; // Получить DocTrueTypeFont
        var ttfParser = CreateFontParser(docFont); // Создать TrueTypeFont parser
        var gids = ExtractGids(entry.Value); // Извлечь GID
        fontToGids[ttfParser] = gids;
    }
    
    // КРИТИЧЕСКИ ВАЖНО: Вызвать физическое объединение
    byte[] mergedFontBytes = TrueTypeFont.Merge(fontToGids, fontName);
    
    return CreatePdfStream(mergedFontBytes);
}
```

**Проблема**: Метод `TrueTypeFont.Merge()` может быть недоступен в публичном API iText.

#### Вариант B (Запасной): Упрощенное решение без физического объединения

Если `TrueTypeFont.Merge()` недоступен:

1. **Проверить, что все subset'ы физически идентичны**
   - Сравнить FontFile2 по содержимому
   - Если идентичны - можно использовать любой из них
   - Если различны - **отказаться от объединения этой группы**

2. **Логировать причину отказа**
   ```csharp
   return new MergeResult 
   { 
       Success = false, 
       FailureReason = "Physical font file merging not available. Font files differ."
   };
   ```

**Решение**: Сначала попробовать Вариант A. Если API недоступен - реализовать Вариант B.

**Тест**: `TestPhysicalFontMerging`

---

### Фаза 5: Объединение метрик ширины (Widths / W array)

**Цель**: Создать согласованный массив метрик для объединенного шрифта

**Шаги**:

1. **Для TrueType шрифтов (простые):**
   ```csharp
   private bool MergeWidths(
       List<FontContext> contexts,
       Dictionary<FontContext, HashSet<Glyph>> usedGlyphs,
       PdfDictionary mergedFont,
       out string? failureReason)
   {
       var allCodes = new SortedSet<int>();
       var codeToWidth = new Dictionary<int, int>();
       
       foreach (var context in contexts)
       {
           var widths = ExtractWidthsArray(context.FontDict);
           var firstChar = GetFirstChar(context.FontDict);
           
           foreach (var glyph in usedGlyphs[context])
           {
               int code = glyph.GetCode();
               int width = widths[code - firstChar];
               
               allCodes.Add(code);
               
               // КРИТИЧЕСКИ ВАЖНО: Проверка согласованности
               if (codeToWidth.ContainsKey(code) && codeToWidth[code] != width)
               {
                   failureReason = $"Width conflict for code {code}: {codeToWidth[code]} vs {width}";
                   return false;
               }
               
               codeToWidth[code] = width;
           }
       }
       
       // Создать плотный массив [minCode..maxCode]
       var mergedWidths = CreateWidthsArray(allCodes, codeToWidth);
       mergedFont.Put(PdfName.Widths, mergedWidths);
       mergedFont.Put(PdfName.FirstChar, new PdfNumber(allCodes.Min));
       mergedFont.Put(PdfName.LastChar, new PdfNumber(allCodes.Max));
       
       return true;
   }
   ```

2. **Для Type0 шрифтов (CID):**
   - Аналогично, но работать с массивом `W` (CID ranges)
   - Проверять согласованность в пересечении

**Тест**: `TestWidthsMerging`, `TestWidthsConflictDetection`

---

### Фаза 6: Объединение ToUnicode

**Цель**: Создать согласованную карту CID→Unicode для объединенного шрифта

**Шаги**:

1. **Проверить единообразие наличия ToUnicode**
   ```csharp
   private bool ValidateToUnicodePresence(
       List<FontContext> contexts,
       out bool shouldHaveToUnicode,
       out string? failureReason)
   {
       bool hasToUnicode = false;
       bool lacksToUnicode = false;
       
       foreach (var context in contexts)
       {
           var toUnicode = context.FontDict?.Get(PdfName.ToUnicode);
           if (toUnicode != null) hasToUnicode = true;
           else lacksToUnicode = true;
       }
       
       // КРИТИЧЕСКИ ВАЖНО: Либо все имеют, либо все не имеют
       if (hasToUnicode && lacksToUnicode)
       {
           failureReason = "Inconsistent ToUnicode presence";
           return false;
       }
       
       shouldHaveToUnicode = hasToUnicode;
       return true;
   }
   ```

2. **Объединить маппинги с проверкой конфликтов**
   ```csharp
   private bool MergeToUnicode(
       List<FontContext> contexts,
       Dictionary<FontContext, HashSet<Glyph>> usedGlyphs,
       PdfDictionary mergedFont,
       out string? failureReason)
   {
       var codeToUnicode = new SortedDictionary<int, string>();
       
       foreach (var context in contexts)
       {
           var toUnicodeStream = GetToUnicodeStream(context.FontDict);
           var mappings = ParseToUnicode(toUnicodeStream);
           
           foreach (var glyph in usedGlyphs[context])
           {
               int code = glyph.GetCode();
               if (!mappings.TryGetValue(code, out var unicode))
                   continue;
               
               // КРИТИЧЕСКИ ВАЖНО: Проверка согласованности
               if (codeToUnicode.ContainsKey(code) && codeToUnicode[code] != unicode)
               {
                   failureReason = $"ToUnicode conflict for code {code}";
                   return false;
               }
               
               codeToUnicode[code] = unicode;
           }
       }
       
       var mergedToUnicodeStream = CreateToUnicodeStream(codeToUnicode);
       mergedFont.Put(PdfName.ToUnicode, mergedToUnicodeStream);
       
       return true;
   }
   ```

**Важно**: После физического объединения шрифтов GID обычно сохраняются, поэтому CID remapping может не понадобиться.

**Тест**: `TestToUnicodeMerging`, `TestToUnicodeConflictDetection`

---

### Фаза 7: Сборка итогового словаря шрифта

**Цель**: Создать корректный PdfDictionary для объединенного шрифта

**Шаги**:

1. **Клонировать базовый словарь**
   ```csharp
   var mergedDict = new PdfDictionary(contexts[0].FontDict);
   ```

2. **Удалить subset-специфичные поля**
   - Удалить старый subset prefix из BaseFont
   - Удалить уникальные поля (например, CharSet)

3. **Сгенерировать новый subset prefix**
   ```csharp
   string newPrefix = GenerateRandomSubsetPrefix(); // ABCDEF+
   string newBaseFontName = $"{newPrefix}+{baseFontName}";
   ```

4. **Установить обновленные компоненты**
   ```csharp
   mergedDict.Put(PdfName.BaseFont, new PdfName(newBaseFontName));
   
   var fontDescriptor = GetOrCreateFontDescriptor(mergedDict);
   fontDescriptor.Put(PdfName.FontName, new PdfName(newBaseFontName));
   fontDescriptor.Put(PdfName.FontFile2, mergedFontStream); // Из Фазы 4
   
   // Widths/W - из Фазы 5
   // ToUnicode - из Фазы 6
   ```

5. **Для Type0: Обновить CIDFont**
   ```csharp
   var descendantFonts = mergedDict.GetAsArray(PdfName.DescendantFonts);
   var cidFont = descendantFonts.GetAsDictionary(0);
   cidFont.Put(PdfName.BaseFont, new PdfName(newBaseFontName));
   // Обновить W, FontDescriptor и т.д.
   ```

**Тест**: `TestMergedFontDictionaryStructure`

---

### Фаза 8: Интеграция в PdfFontOptimizer

**Цель**: Использовать новый FontSubsetMerger в существующем коде

**Шаги**:

1. **Обновить `CreateMergedFonts()`**
   ```csharp
   private Dictionary<string, MergedFontInfo> CreateMergedFonts(
       Dictionary<string, string> mappingTable)
   {
       var mergedFonts = new Dictionary<string, MergedFontInfo>();
       var groupedByNewName = GroupContextsByNewName(mappingTable);
       
       foreach (var group in groupedByNewName)
       {
           var contexts = group.Value;
           var merger = new FontSubsetMerger();
           
           // КРИТИЧЕСКИ ВАЖНО: Использовать новый merger
           var result = merger.TryMerge(contexts, document);
           
           if (!result.Success)
           {
               Console.WriteLine($"❌ Font merge failed: {result.FailureReason}");
               continue; // Пропустить эту группу
           }
           
           mergedFonts[group.Key] = new MergedFontInfo
           {
               NewResourceName = group.Key,
               MergedFontDict = result.MergedFontDict,
               CidRemapping = result.CidRemapping
           };
       }
       
       return mergedFonts;
   }
   ```

2. **Упростить `CreateMergedFontDictionary()`**
   - Убрать весь код объединения
   - Оставить только вызов `FontSubsetMerger.TryMerge()`

3. **Обновить `ApplyGlobalFontDictionary()`**
   - Использовать CID remapping из MergeResult (если есть)
   - Обрабатывать случай, когда некоторые группы не были объединены

**Тест**: `TestCompleteOptimizationPipeline`

---

### Фаза 9: Обработка ошибок и edge cases

**Цель**: Обеспечить устойчивость к некорректным данным

**Шаги**:

1. **Добавить обработку неподдерживаемых шрифтов**
   - CFF fonts - отказ от объединения
   - Encrypted fonts - отказ
   - Symbolic fonts без ToUnicode - отказ

2. **Добавить логирование**
   ```csharp
   public interface IFontMergeLogger
   {
       void LogInfo(string message);
       void LogWarning(string message);
       void LogError(string message);
   }
   ```

3. **Добавить метрики**
   - Количество успешных объединений
   - Количество отказов с причинами
   - Экономия размера файла

**Тест**: `TestErrorHandling`, `TestUnsupportedFonts`

---

### Фаза 10: Тестирование и валидация

**Цель**: Убедиться в корректности работы на реальных данных

**Тесты**:

1. ✅ **TestFontIndexing_Step1** (уже есть)
2. ✅ **TestFontMappingTable** (уже есть)
3. **TestFontSubsetMerging** (новый)
   - Объединение 2-3 subset'ов одного шрифта
   - Проверка читаемости текста
   - Проверка метрик

4. **TestConflictDetection** (новый)
   - Subset'ы с разными ширинами
   - Subset'ы с разными ToUnicode
   - Проверка корректного отказа

5. **TestCompleteOptimization** (обновить существующий)
   - Полный pipeline на Ladders.pdf
   - Проверка текста
   - Сравнение размеров файлов

6. **TestMultipleFontTypes** (новый)
   - Документ с TrueType и Type0 шрифтами
   - Проверка корректной обработки каждого типа

---

## Метрики успеха

### Функциональные:
- ✅ Текст в оптимизированном PDF читается корректно
- ✅ Все символы отображаются правильно
- ✅ Метрики (ширины) корректны
- ✅ ToUnicode mapping работает

### Производительность:
- 🎯 Размер файла уменьшается минимум на 20%
- 🎯 Количество объектов шрифтов: ~3 (вместо 200+)
- 🎯 Все страницы ссылаются на одни и те же объекты шрифтов

### Надежность:
- ✅ Корректный отказ при обнаружении конфликтов
- ✅ Информативное логирование
- ✅ Обработка всех edge cases

---

## Риски и ограничения

### Технические риски:

1. **API TrueTypeFont.Merge может быть недоступен**
   - Митигация: Реализовать Вариант B (проверка идентичности)
   - Альтернатива: Использовать reflection или внутренние методы iText

2. **Сложность формата TrueType**
   - Митигация: Полагаться на iText API
   - Не пытаться реализовывать собственный parser

3. **Различия в кодировках**
   - Митигация: Строгая проверка совместимости
   - Отказ от объединения при несовместимости

### Ограничения подхода:

1. **Работает только с TrueType и Type0**
   - Type1, Type3 шрифты не поддерживаются

2. **Требуется согласованность метрик**
   - Если subset'ы созданы разными инструментами - могут быть конфликты

3. **Encrypted/Protected PDF**
   - Может потребоваться дополнительная обработка

---

## Порядок выполнения

### Приоритет 1 (Критически важно):
1. Фаза 2: Создание FontSubsetMerger
2. Фаза 3: Сбор используемых глифов
3. Фаза 4: Физическое объединение (или проверка идентичности)

### Приоритет 2 (Необходимо для корректности):
4. Фаза 5: Объединение Widths
5. Фаза 6: Объединение ToUnicode
6. Фаза 7: Сборка словаря

### Приоритет 3 (Интеграция):
7. Фаза 8: Интеграция в PdfFontOptimizer
8. Фаза 10: Тестирование

### Приоритет 4 (Полировка):
9. Фаза 9: Обработка ошибок

---

## Заключение

Текущий код требует значительной переработки. Основная проблема - **отсутствие физического объединения файлов шрифтов**. Без этого объединение subset'ов невозможно.

Рекомендуемый подход:
1. Создать отдельный класс FontSubsetMerger
2. Реализовать физическое объединение (или проверку идентичности)
3. Добавить валидацию и обработку конфликтов
4. Интегрировать в существующий pipeline

Ожидаемый результат: корректное объединение subset'ов с уменьшением размера файла на 20-50%.
