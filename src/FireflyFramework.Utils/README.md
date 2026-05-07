# FireflyFramework.Utils

Cross-cutting utilities used by every higher tier: HTML and PDF
template rendering and the discoverability marker for filterable
ID-typed properties. Mirrors `org.fireflyframework:firefly-common-utils`.

This is the second project in the dependency graph after `Kernel` —
it depends only on Kernel and a small fixed set of third-party NuGets
(Scriban for templates, iText 7 for PDF, an iText / Bouncy Castle
adapter for AES-256 PDF encryption, iText pdfHTML for the HTML-to-PDF
bridge).

---

## Why a separate utils project?

Templating and PDF rendering pull in a meaningful pile of third-party
code (iText 7 alone is ~7 MB). Hiding those behind their own assembly
keeps consumers that don't need them — most service authors don't —
from paying for the transitive dependency. Adapter projects that
*do* render templates (typically the notification / callback /
e-signature paths) opt in by referencing this project explicitly.

The `[FilterableId]` attribute lives here for the same reason — it's
referenced by entity definitions in `Models` projects of consumer
services and read reflectively by `FireflyFramework.Data`'s generic
filter engine. Putting it in `Kernel` would force every project to
take a transitive dependency on a marker that only `Data` cares about.

---

## Mental model

The two areas are independent of each other:

```
                ┌──────────────────────────┐
                │  TemplateRenderUtil      │
                │  (static facade)         │
                ├──────────────────────────┤
                │  RenderTemplateToHtml    │ ◄── Scriban template engine
                │  RenderHtmlToPdfBytes    │ ◄── iText 7 (encryption, metadata, bookmarks)
                │  RenderHtmlToPdfFile     │     iText pdfHTML (HTML → PDF)
                │  ValidateTemplate        │
                └──────────────────────────┘

                ┌──────────────────────────┐
                │  [FilterableId]          │ ◄── consumed by FireflyFramework.Data
                │  marker attribute        │     (entities annotate `Id`-named
                │                          │      properties to opt them in to
                │                          │      generic filtering)
                └──────────────────────────┘
```

`TemplateRenderUtil` is **static and stateful** by design. The shared
template directory, the parsed-template cache, and the shared
variables (e.g. `supportEmail`, `companyName`) are all process-global.
That matches the Java module's behaviour and makes the most common
"render this email body / PDF invoice / signed document" call sites
boilerplate-free.

If you need per-tenant or per-request isolation — e.g. one tenant gets
a different brand template directory than another — you're better off
*not* using the shared directory; pass the template content directly to
`RenderTemplateStringToHtml` instead.

---

## Quick start

### Render an email body from a template file

```csharp
using FireflyFramework.Utils.Templates;

// One-time setup at host startup.
TemplateRenderUtil.SetTemplateDirectory("./Templates");
TemplateRenderUtil.AddSharedVariable("supportEmail", "support@example.com");

// Per-request render.
var html = TemplateRenderUtil.RenderTemplateToHtml(
    templateName: "welcome.scriban-html",
    dataModel: new Dictionary<string, object?>
    {
        ["customerName"] = "Ada Lovelace",
        ["activationUrl"] = "https://example.com/activate/abc123",
    });
```

### Generate a watermarked, encrypted PDF invoice

```csharp
var options = new PdfOptions()
    .WithMetadata(
        title: "Invoice INV-2026-001",
        author: "Acme Corp",
        subject: "January 2026 invoice",
        keywords: "invoice,2026,acme")
    .WithWatermark("DRAFT",
        opacity: 0.25f, fontSize: 80, rotation: 30, color: "#888888")
    .WithEncryption(
        userPassword: customerPassword,
        ownerPassword: ownerPassword,
        allowPrinting: true, allowCopy: false, allowModify: false);

var pdf = TemplateRenderUtil.RenderTemplateToPdfBytes(
    "invoice.scriban-html",
    invoiceModel,
    options);

await File.WriteAllBytesAsync("invoice.pdf", pdf);
```

---

## Public surface

### `TemplateRenderUtil` — the static facade

Single static class that exposes every entry point. Three flavours
of input are supported:

| Input | Method |
|---|---|
| Named template file (resolved against `SetTemplateDirectory`) | `RenderTemplateToHtml(name, model)` / `RenderTemplateToPdfBytes(name, model, options)` |
| Inline template string with a logical name (the name is the cache key) | `RenderTemplateStringToHtml(content, name, model)` / `RenderTemplateStringToPdfBytes(content, name, model, options)` |
| Pre-rendered HTML (you brought your own templating) | `RenderHtmlToPdfBytes(html, options)` / `RenderHtmlToPdfFile(html, path, options)` |

#### Configuration entry points

```csharp
TemplateRenderUtil.SetTemplateDirectory("./Templates");          // search root for named templates
TemplateRenderUtil.SetTemplateCachingEnabled(true);              // parse once and reuse (default: on)
TemplateRenderUtil.ClearTemplateCache();                          // drop every parsed template (e.g. after hot-reloading)

TemplateRenderUtil.AddSharedVariable("supportEmail", "x@y.com"); // visible to every render
TemplateRenderUtil.RemoveSharedVariable("supportEmail");
TemplateRenderUtil.ClearSharedVariables();
```

Shared variables are merged with the per-call data model on every
render — per-call values **win** if a name collides, so a model
field can override a shared default.

#### Async variants

Every synchronous render method has an `*Async` overload that wraps
the work in `Task.Run`. The render itself is CPU-bound (no I/O after
the template file is read), so the async overload is purely for
scheduling: you can `await` it without blocking the request thread.

```csharp
var html = await TemplateRenderUtil.RenderTemplateToHtmlAsync("welcome.scriban-html", model);
var pdf  = await TemplateRenderUtil.RenderHtmlToPdfBytesAsync(html, options);
```

#### Validating a template at deployment time

`ValidateTemplate(content)` returns the list of Scriban parser errors
for a template string, or an empty list if the template is well-formed.
Use this in CI to catch broken templates before they reach production:

```csharp
foreach (var path in Directory.EnumerateFiles("./Templates", "*.scriban-html"))
{
    var errors = TemplateRenderUtil.ValidateTemplate(File.ReadAllText(path));
    if (errors.Count > 0)
    {
        Console.Error.WriteLine($"{path}: {string.Join("; ", errors)}");
        Environment.Exit(1);
    }
}
```

### `PdfOptions` — the fluent PDF builder

Configures every aspect of the PDF pass: page geometry, fonts,
metadata, watermarking, AES-256 encryption with a permissions matrix,
and a tree of outline bookmarks.

| Method | Purpose |
|---|---|
| `WithPageSize(PageSize)` | Override the default A4 size (use `iText.Kernel.Geom.PageSize.LETTER` etc.) |
| `WithMargins(top, right, bottom, left)` | Margins in points; default 36 (½ inch) on every side |
| `WithDefaultFont(name)` / `WithFontDirectory(path)` | Custom fonts; the directory is scanned for embeddable TTF/OTF files |
| `WithBaseUri(uri)` | Base URI for resolving relative `<img>` / `<link>` references in the HTML |
| `WithMetadata(title, author, subject, keywords)` | Populates PDF document info dictionary |
| `WithWatermark(text)` | Diagonal grey watermark on every page (defaults: 30% opacity, 60pt, 45°, `#A0A0A0`) |
| `WithWatermark(text, opacity, fontSize, rotation, color)` | Full watermark control |
| `WithEncryption(ownerPassword)` | Owner-only AES-256 — anyone can read, only the owner can change permissions |
| `WithEncryption(user, owner, allowPrinting, allowCopy, allowModify)` | Full reader/owner password pair with permissions matrix |
| `WithBookmark(title, destination)` / `WithBookmark(title, level, destination)` | Top-level outline entry. `destination` is a 1-based page number |
| `WithChildBookmark(title, destination)` | Nested outline entry under the previous bookmark; mirrors HTML `<h1>/<h2>` nesting |
| `ClearBookmarks()` | Reset the outline tree |

The setter pattern returns `this` so calls chain. `Bookmarks` is the
underlying `List<Bookmark>` if you need to manipulate it directly
(rare).

### `[FilterableId]` — the marker attribute

```csharp
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class FilterableIdAttribute : Attribute { }
```

Pure marker — no payload. Lives in
`FireflyFramework.Utils.Annotations`. Apply to any `Id`-suffixed
property that should be includable in the generic-filter DSL exposed
by `FireflyFramework.Data`:

```csharp
public sealed class OrderEntity
{
    public Guid Id { get; set; }              // auto-included (primary key)
    public Guid CustomerId { get; set; }       // auto-EXCLUDED (Id-suffixed)

    [FilterableId]
    public Guid TenantId { get; set; }         // explicitly opted back in
}
```

The default exclusion exists to stop callers from inadvertently
filtering by every foreign-key field — a common source of accidental
table scans on large tables. `[FilterableId]` is the deliberate
opt-in.

---

## Common patterns

### Compiling templates ahead of time

For services that ship a fixed set of templates with the deployment,
warm the cache during host startup so the first user-facing render
doesn't pay parse cost:

```csharp
using var scope = app.Services.CreateScope();
foreach (var path in Directory.EnumerateFiles("./Templates", "*.scriban-html"))
{
    var name = Path.GetFileName(path);
    TemplateRenderUtil.RenderTemplateStringToHtml(
        File.ReadAllText(path), name,
        new Dictionary<string, object?>());           // empty model, just to parse + cache
}
```

### Tenant-isolated rendering

Don't use `SetTemplateDirectory` for per-tenant template trees — it's
a process-global. Instead, fetch the template content from a
tenant-aware store (e.g. a database row or a tenant-prefixed S3 key)
and call `RenderTemplateStringToHtml`:

```csharp
var tenantTemplate = await _templateStore.LoadAsync(tenantId, "welcome", ct);
var html = TemplateRenderUtil.RenderTemplateStringToHtml(
    content: tenantTemplate.Content,
    name: $"{tenantId}:welcome",                       // unique cache key
    dataModel: model);
```

The `name` argument is also the cache key, so prefixing it with the
tenant ID gives you per-tenant cache isolation while still benefiting
from caching within a tenant.

### PDF watermarking based on document state

```csharp
var options = new PdfOptions().WithMetadata("Quote Q-2026-007", ...);

if (quote.Status == QuoteStatus.Draft) options.WithWatermark("DRAFT");
else if (quote.Status == QuoteStatus.Expired) options.WithWatermark("EXPIRED", opacity: 0.4f, fontSize: 100, rotation: 0, color: "#CC0000");

var pdf = TemplateRenderUtil.RenderTemplateToPdfBytes("quote.scriban-html", quote, options);
```

---

## Pitfalls and gotchas

**`SetTemplateDirectory` is process-global.** Two services in the
same process (rare in production but common in test runs) will share
it. If you parallelise tests that override the directory, expect
flaky tests. The fix is to use `RenderTemplateStringToHtml` with
inline content per test instead.

**The cache uses the `name` argument as the key.** If you call
`RenderTemplateStringToHtml(content, "welcome", model)` and
`RenderTemplateStringToHtml(differentContent, "welcome", model)` in
sequence with caching enabled, the second call returns the *first*
parsed template. Either disable caching during template development
or use unique names.

**`Template.HasErrors` is checked once at parse time.** Runtime
errors (missing variables, type mismatches in expressions) surface
as Scriban exceptions during `Render`, not as `HasErrors`. Use
`ValidateTemplate` only for syntactic validation.

**iText 7 is licensed AGPL.** This project depends on it transitively.
If you ship the framework's PDF capabilities to customers, ensure your
distribution complies with AGPL or that you have a commercial iText
license. The `Apache-2.0` `<PackageLicenseExpression>` on this project
covers Firefly's own code; it does not relicense iText.

**`Bookmark.Destination` is a 1-based page number string.** It's a
string for parity with the Java API (where it could in principle be a
named anchor), but the .NET implementation only honours numeric values
for now. Non-numeric values silently produce an outline entry with no
target.

---

## Internals (for the curious)

The Scriban template cache is a `ConcurrentDictionary<string, Template>`.
We use `GetOrAdd` so duplicate concurrent parses for the same key
race to the dictionary; the loser's `Template.Parse` result is
discarded. That's cheaper than guarding with a lock because Scriban
parses in single-digit microseconds for typical templates.

Shared variables are merged into a fresh `ScriptObject` on every
render rather than once at configuration time. That's slightly more
work but keeps the share-state read-only — a render call cannot
accidentally write to a shared variable and corrupt the next caller.

The PDF watermarker draws the watermark *behind* the page content
using `page.NewContentStreamBefore()` because drawing it in the
default content stream sometimes lands above semi-transparent
elements on the rendered page. Drawing first, with the watermark
underneath, is what the Java line does and what most office-PDF
software expects.

The encryption permissions matrix is a bit-mask in iText's API; we
expose the three booleans (`AllowPrinting`, `AllowCopy`, `AllowModify`)
that cover 90 % of business-document use cases. The remaining iText
permission bits (annotate, fill forms, screen-reader access,
high-quality print) aren't currently surfaced — if you need them, drop
to `iText.Kernel.Pdf.WriterProperties` directly.

---

## Dependencies

| Reference | Used for |
|---|---|
| `FireflyFramework.Kernel` (project) | Calendar version, base exception (transitive) |
| `Scriban` (NuGet) | Logic-less HTML templating (FreeMarker analogue from the Java line) |
| `itext7` (NuGet) | PDF document model and writer |
| `itext7.bouncy-castle-adapter` (NuGet) | AES-256 encryption — iText defers to BouncyCastle for the cipher |
| `itext7.pdfhtml` (NuGet) | HTML → PDF conversion (the Flying Saucer analogue) |

All NuGets are pinned in the central `Directory.Packages.props`. There
is no transitive version float for any of them.

---

## Java mapping

| .NET | Java original |
|---|---|
| `TemplateRenderUtil` | `org.fireflyframework.common.utils.template.TemplateRenderUtil` |
| `PdfOptions` | `org.fireflyframework.common.utils.template.PdfOptions` |
| `PdfOptions.Bookmark` | `org.fireflyframework.common.utils.template.PdfOptions.Bookmark` |
| `[FilterableId]` | `@org.fireflyframework.common.annotations.FilterableId` |

The template syntax is *different* between runtimes: Java uses
FreeMarker (`${variable}`, `<#if>`), .NET uses Scriban
(`{{ variable }}`, `{% if %}`). Templates are not source-compatible —
porting from Java to .NET requires translating template syntax even
though the rendering API is parallel.

---

## See also

* [`FireflyFramework.Data`](../FireflyFramework.Data/README.md) — consumer of `[FilterableId]` for the generic filter engine.
* [`FireflyFramework.Notifications.Core`](../FireflyFramework.Notifications.Core/README.md) — uses `TemplateRenderUtil` for transactional-email body rendering.
* [`docs/CONFIGURATION.md`](../../docs/CONFIGURATION.md) — `Firefly:*` configuration sections.
