# Сравнение Старого и Нового Планов

## Исходный План (из запроса пользователя)

### Шаг 1: Индексация и анализ структуры шрифтов
**Статус**: ✅ Реализовано корректно  
**Изменения**: Не требуются

- `IndexFonts()` работает правильно
- Собирает информацию о всех шрифтах на страницах
- `ExtractBaseFontName()` корректно удаляет subset-префиксы

### Шаг 2: Создание карты переназначения (Remapping)
**Статус**: ✅ Реализовано корректно  
**Изменения**: Не требуются

- `CreateFontMappingTable()` правильно группирует шрифты
- Создает корректную карту `"{page}_{resource}" -> "{new_resource}"`
- Валидация работает (`ValidateFontMapping()`)

### Шаг 3: Разрешение CID-конфликтов и создание объединенных объектов
**Статус**: ⚠️ Частично реализовано, требует переработки  
**Проблемы**: 
- `CidRemapper.RemapCidsWithConflictResolution()` работает, но...
- **Отсутствует физическое объединение файлов шрифтов**
- Просто копируется `FontFile2` из первого subset'а
- Глифы из других subset'ов **теряются**

**Что нужно изменить**:
```diff
- var fontFile2 = originalFontDescriptor.Get(PdfName.FontFile2);
- newFontDescriptor.Put(PdfName.FontFile2, fontFile2); // Просто копия

+ // КРИТИЧНО: Физически объединить файлы шрифтов
+ var fontToGids = CollectGidsFromAllSubsets(contexts);
+ byte[] mergedFontBytes = TrueTypeFont.Merge(fontToGids, fontName);
+ var mergedStream = new PdfStream(mergedFontBytes);
+ newFontDescriptor.Put(PdfName.FontFile2, mergedStream);
```

### Шаг 4: Применение изменений в документе
**Статус**: ✅ Реализовано  
**Проблемы**: Работает, но применяет неправильные данные из Шага 3

**Изменения**: Не требуются после исправления Шага 3

### Шаг 5: Сохранение и финальная проверка
**Статус**: ✅ Реализовано  
**Изменения**: Добавить валидацию читаемости текста

---

## Обновленный План (ACTION_PLAN.md)

### Ключевые изменения:

1. **Разделение Шага 3 на 6 подшагов**:
   - Фаза 3: Сбор используемых глифов
   - Фаза 4: Физическое объединение файлов (НОВОЕ)
   - Фаза 5: Объединение метрик Widths (НОВОЕ)
   - Фаза 6: Объединение ToUnicode (УЛУЧШЕНО)
   - Фаза 7: Сборка итогового словаря

2. **Добавление валидации** (Фаза 9):
   - Проверка типа шрифта
   - Проверка совместимости метрик
   - Проверка конфликтов ToUnicode
   - **Отказ от объединения при конфликтах**

3. **Вынесение в отдельный класс** (Фаза 2):
   - `FontSubsetMerger` - четкая ответственность
   - Упрощение `PdfFontOptimizer`

---

## Сравнение Шаг за Шагом

| Старый План | Новый План | Комментарий |
|-------------|------------|-------------|
| Шаг 1: Индексация | Фаза 1: Подготовка + Фаза 3: Сбор глифов | Разделено на анализ и сбор используемых глифов |
| Шаг 2: Маппинг | Без изменений | Работает корректно |
| Шаг 3: CID-конфликты | **Фаза 4-7: Полная переработка** | Добавлено физическое объединение и валидация |
| Шаг 4: Применение | Фаза 8: Интеграция | Упрощено после переработки Шага 3 |
| Шаг 5: Сохранение | Фаза 10: Тестирование | Расширено валидацией |
| — | **Фаза 9: Обработка ошибок** | Новая фаза - логирование и метрики |

---

## Почему Старый План Не Работал

### Проблема: Неполное понимание процесса объединения

Старый план предполагал, что `CidRemapper` решит все проблемы через перекодировку. Но:

1. **CID remapping не создает глифы**
   - Можно создать новую карту "CID 5 = 'D'"
   - Но если глифа 'D' нет в физическом файле шрифта - PDF-ридер не сможет его отобразить

2. **Копирование FontFile2 не работает**
   ```
   Subset-1: {A, B, C} ─┐
                        ├─> Скопировать -> Результат: {A, B, C}
   Subset-2: {D, E, F} ─┘                  Глифы D,E,F потеряны!
   ```

3. **Отсутствовала валидация**
   - Нет проверки совместимости метрик
   - Нет проверки конфликтов ToUnicode
   - Нет отката при ошибках

### В чем была ошибка

Старый план фокусировался на **PDF-структуре** (словари, ресурсы, ToUnicode), но игнорировал **физические файлы шрифтов**.

В iText Optimizer ключевым является:
```csharp
byte[] mergedFontBytes = TrueTypeFont.Merge(fontToGids, fontName);
```

Этот метод **физически объединяет TrueType таблицы** (glyf, loca, cmap, hmtx), создавая новый файл шрифта. Без этого объединение невозможно.

---

## Что Изменилось в Новом Плане

### 1. Акцент на физическом объединении (Фаза 4)

**Критически важный шаг**, который отсутствовал в старом плане:

```csharp
// Для каждого subset'а собрать используемые GID
var fontToGids = new Dictionary<TrueTypeFont, ICollection<int>>();

foreach (var context in contexts)
{
    var ttfParser = CreateTrueTypeFontParser(context.FontDict);
    var gids = ExtractGids(usedGlyphs[context]);
    fontToGids[ttfParser] = gids;
}

// ФИЗИЧЕСКИ объединить файлы шрифтов
byte[] mergedFontBytes = TrueTypeFont.Merge(fontToGids, fontName);

// Создать новый PdfStream с объединенным шрифтом
var mergedStream = new PdfStream(mergedFontBytes);
```

### 2. Валидация конфликтов (Фазы 5-6)

**Проверка согласованности метрик**:
```csharp
if (codeToWidth.ContainsKey(code) && codeToWidth[code] != width)
{
    // ОТКАЗ от объединения при конфликте!
    return MergeResult.Failed("Width conflict for code " + code);
}
```

**Проверка согласованности ToUnicode**:
```csharp
if (codeToUnicode.ContainsKey(code) && codeToUnicode[code] != unicode)
{
    // ОТКАЗ от объединения при конфликте!
    return MergeResult.Failed("ToUnicode conflict for code " + code);
}
```

### 3. Отдельный класс FontSubsetMerger (Фаза 2)

Вместо всего кода в `PdfFontOptimizer.CreateMergedFontDictionary()`:

```csharp
public class FontSubsetMerger
{
    public MergeResult TryMerge(List<FontContext> contexts, PdfDocument document)
    {
        // 1. Валидация входных данных
        if (!ValidateContexts(contexts, out var error))
            return MergeResult.Failed(error);
        
        // 2. Сбор используемых глифов
        var usedGlyphs = CollectUsedGlyphs(contexts, document);
        
        // 3. Физическое объединение файлов
        var mergedFontStream = MergeFontFiles(contexts, usedGlyphs);
        if (mergedFontStream == null)
            return MergeResult.Failed("Font file merging failed");
        
        // 4. Объединение метрик
        if (!MergeWidths(contexts, usedGlyphs, mergedDict, out error))
            return MergeResult.Failed(error);
        
        // 5. Объединение ToUnicode
        if (!MergeToUnicode(contexts, usedGlyphs, mergedDict, out error))
            return MergeResult.Failed(error);
        
        // 6. Сборка итогового словаря
        var mergedDict = CreateMergedDictionary(/* ... */);
        
        return MergeResult.Success(mergedDict);
    }
}
```

### 4. Обработка неудачных объединений (Фаза 8-9)

```csharp
foreach (var group in groupedByNewName)
{
    var merger = new FontSubsetMerger();
    var result = merger.TryMerge(group.Value, document);
    
    if (!result.Success)
    {
        // КОРРЕКТНАЯ обработка: просто пропустить эту группу
        Console.WriteLine($"❌ Merge failed: {result.FailureReason}");
        continue; // Не добавлять в mergedFonts
    }
    
    mergedFonts[group.Key] = CreateMergedFontInfo(result);
}
```

Старый план: создавал "объединенный" шрифт в любом случае, даже если объединение невозможно.  
Новый план: корректно отказывается от объединения при конфликтах.

---

## Устаревшие Части Старого Плана

### 1. "Собирается вся структура за один проход"

**Было**:
```
IndexFonts() -> CreateFontMappingTable() -> CreateMergedFonts()
```

**Стало**:
```
IndexFonts() -> CreateFontMappingTable() -> 
  -> FontSubsetMerger.TryMerge() для каждой группы ->
    -> Валидация -> Сбор глифов -> Физическое объединение -> Метрики -> ToUnicode
```

### 2. "CID remapping решает все конфликты"

**Было**: CID remapping создает новые коды и новую карту ToUnicode.

**Проблема**: Новые коды ссылаются на **несуществующие глифы** в физическом файле.

**Стало**: 
- Физическое объединение создает файл с **всеми** нужными глифами
- CID remapping нужен только в особых случаях
- При конфликтах метрик/ToUnicode - отказ от объединения

### 3. "Просто заменить ресурсы и обновить ToUnicode"

**Было**: `UpdatePageContentStreamWithRemapping()` заменяет `/F7` на `/F1_Merged` и CID.

**Проблема**: Если физический файл шрифта не содержит нужных глифов - ничего не поможет.

**Стало**: Сначала физически объединить файлы, затем обновлять ссылки.

---

## Ненужные Команды из Старого Плана

### Удалить:
- ❌ `UpdatePageContentStreamWithRemapping` с CID remapping - избыточно после физического объединения
- ❌ Сложная логика генерации новых CID - не нужна при правильном подходе
- ❌ `DiagnoseToUnicodeMappings` - можно заменить на валидацию в FontSubsetMerger

### Оставить:
- ✅ `IndexFonts()` - работает корректно
- ✅ `CreateFontMappingTable()` - работает корректно
- ✅ `ApplyGlobalFontDictionary()` - обновить для использования нового MergeResult

---

## Резюме

### Старый план был правильным в части:
1. ✅ Индексация шрифтов
2. ✅ Создание таблицы переназначения
3. ✅ Применение изменений в документе

### Старый план упускал критически важное:
1. ❌ Физическое объединение файлов шрифтов
2. ❌ Валидация согласованности метрик
3. ❌ Проверка конфликтов ToUnicode
4. ❌ Обработка неудачных объединений

### Новый план исправляет:
1. ✅ Добавляет физическое объединение (Фаза 4)
2. ✅ Добавляет валидацию на каждом шаге (Фазы 5-6)
3. ✅ Выносит логику в отдельный класс (Фаза 2)
4. ✅ Добавляет корректную обработку ошибок (Фаза 9)

---

## Рекомендация

**Следовать новому плану (ACTION_PLAN.md)**, используя старый код как основу:

1. Оставить без изменений: Фазы 1-2 (индексация, маппинг)
2. Создать новый класс: FontSubsetMerger
3. Реализовать критичные части: Физическое объединение, валидация
4. Интегрировать в существующий код
5. Добавить тесты на каждом шаге

**Прогноз**: 3-5 дней активной разработки для полной реализации.
