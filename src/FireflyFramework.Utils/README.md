# FireflyFramework.Utils

Template rendering, PDF generation, and filtering markers. Mirrors
`org.fireflyframework:firefly-common-utils`.

## Public surface

### `TemplateRenderUtil`

Static facade around Scriban (FreeMarker analogue) for HTML rendering and
iText 7 + iText pdfHTML (Flying Saucer analogue) for PDF rendering.

Configuration helpers:

```csharp
using FireflyFramework.Utils.Templates;

TemplateRenderUtil.SetTemplateDirectory("./Templates");
TemplateRenderUtil.SetTemplateCachingEnabled(true);
TemplateRenderUtil.AddSharedVariable("supportEmail", "support@example.com");
```

HTML rendering:

```csharp
var html = TemplateRenderUtil.RenderTemplateToHtml(
    "invoice.scriban-html",
    new Dictionary<string, object?>
    {
        ["customerName"] = "Alice",
        ["lineItems"]    = lineItems,
        ["totalDue"]     = 1234.56m,
    });
```

PDF rendering with metadata, watermark, encryption, and bookmarks:

```csharp
var options = new PdfOptions()
    .WithMetadata(title: "Invoice INV-2026-001",
                  author: "Acme Corp",
                  subject: "January invoice",
                  keywords: "invoice,2026")
    .WithWatermark(text: "PAID",
                   opacity: 0.25f, fontSize: 80,
                   rotation: 30, color: "#888888")
    .WithEncryption(userPassword: customerPassword,
                    ownerPassword: ownerPassword,
                    allowPrinting: true,
                    allowCopy: false,
                    allowModify: false)
    .WithBookmark("Summary",  level: 0, destination: "1")
    .WithChildBookmark("Charges", destination: "1")
    .WithChildBookmark("Taxes",   destination: "2");

var pdfBytes = TemplateRenderUtil.RenderTemplateToPdfBytes("invoice.scriban-html", model, options);
```

Synchronous and async variants exist for every render entry point
(`RenderTemplateToHtmlAsync`, `RenderHtmlToPdfBytesAsync`, etc.).

### `PdfOptions`

Fluent builder for the PDF rendering pipeline:

| Method                                                                               | Purpose                                          |
|--------------------------------------------------------------------------------------|--------------------------------------------------|
| `WithPageSize(PageSize)`                                                              | Override the default A4 page size                |
| `WithMargins(top, right, bottom, left)`                                              | Override the default 36-point margins            |
| `WithDefaultFont(name)` / `WithFontDirectory(path)`                                  | Custom fonts                                     |
| `WithBaseUri(uri)`                                                                    | Resolve relative `<img>` / `<link>` references   |
| `WithMetadata(title, author, subject, keywords)`                                     | Sets PDF document info                           |
| `WithWatermark(text)` / `WithWatermark(text, opacity, fontSize, rotation, color)`    | Diagonal watermark on every page                 |
| `WithEncryption(ownerPassword)`                                                       | Owner-only AES-256 encryption                    |
| `WithEncryption(user, owner, allowPrinting, allowCopy, allowModify)`                 | Full permissions matrix                          |
| `WithBookmark(title, destination)` / `WithBookmark(title, level, destination)`       | Top-level outline entries (destination is a 1-based page number) |
| `WithChildBookmark(title, destination)`                                              | Nested outline entries under the previous bookmark |
| `ClearBookmarks()`                                                                    | Reset the outline tree                           |

### `[FilterableId]`

Marks an `Id`-suffixed property as filterable for the generic filter
engine in `FireflyFramework.Data`. By default the engine excludes
properties whose name ends in `Id`; this attribute opts back in with
exact-match semantics only (no LIKE / range).

```csharp
public sealed class Order
{
    [FilterableId]
    public Guid CustomerId { get; set; }
}
```

## Dependencies

| Reference                               | Used for                          |
|-----------------------------------------|-----------------------------------|
| `FireflyFramework.Kernel`               | Calendar version, base exception  |
| `Scriban`                               | HTML template rendering           |
| `itext7`                                | PDF generation                    |
| `itext7.bouncy-castle-adapter`          | AES-256 PDF encryption            |
| `itext7.pdfhtml`                        | HTML to PDF conversion            |

## Java mapping

| .NET                                  | Java original                                                                       |
|---------------------------------------|-------------------------------------------------------------------------------------|
| `TemplateRenderUtil`                  | `org.fireflyframework.common.utils.template.TemplateRenderUtil`                     |
| `PdfOptions`                          | `org.fireflyframework.common.utils.template.PdfOptions`                             |
| `[FilterableId]`                      | `org.fireflyframework.common.annotations.FilterableId`                              |
