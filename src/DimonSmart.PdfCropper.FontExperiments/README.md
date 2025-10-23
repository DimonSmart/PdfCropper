# DimonSmart.PdfCropper.FontExperiments

Экспериментальный проект для исследования и оптимизации шрифтов в PDF документах.

## Структура проекта

### DimonSmart.PdfCropper.FontExperiments
Основная библиотека с классом `PdfFontOptimizer` для работы с шрифтами PDF.

**Основные возможности:**
- Открытие PDF документов
- Анализ шрифтов на страницах
- Получение информации о шрифтах (имена ресурсов, базовые имена шрифтов)

### DimonSmart.PdfCropper.FontExperiments.Tests
NUnit тесты для проверки функциональности.

**Включает:**
- Тесты открытия PDF документов
- Проверку получения количества страниц
- Проверку корректного освобождения ресурсов
- Helper для генерации тестовых PDF файлов

## Запуск тестов

```powershell
dotnet test tests\DimonSmart.PdfCropper.FontExperiments.Tests\DimonSmart.PdfCropper.FontExperiments.Tests.csproj
```

## Класс PdfFontOptimizer

```csharp
public class PdfFontOptimizer
{
    public class FontInfo
    {
        public string ResourceName { get; set; }      // например "F7", "F8"
        public string BaseFontName { get; set; }      // например "Calibri", "TableauBook"
        public PdfDictionary? FontDict { get; set; }
        public PdfDictionary? FontsDict { get; set; } // родительский словарь
        public int PageNumber { get; set; }
    }
    
    public PdfFontOptimizer(string inputPath);
    public int GetPageCount();
    public void Close();
}
```

## Зависимости

- iText 8.0.1+
- iText.Bouncy-Castle-Adapter 8.0.1+
- .NET 8.0 / .NET 9.0
