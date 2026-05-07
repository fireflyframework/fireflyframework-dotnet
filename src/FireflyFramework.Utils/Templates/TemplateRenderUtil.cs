using System.Collections.Concurrent;
using System.IO;
using System.Text;
using iText.Html2pdf;
using iText.Kernel.Colors;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Action;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using Scriban;
using Scriban.Runtime;

namespace FireflyFramework.Utils.Templates;

/// <summary>
/// Static facade for HTML/PDF/image rendering. Mirrors <c>TemplateRenderUtil</c> from
/// fireflyframework-utils. Backed by Scriban (FreeMarker analogue) and iText7 (Flying Saucer
/// analogue).
/// </summary>
/// <remarks>
/// Supports file-, classpath- and string-based templates, async variants, watermarks,
/// encryption, metadata and bookmarks via <see cref="PdfOptions"/>.
/// </remarks>
public static class TemplateRenderUtil
{
    private static readonly ConcurrentDictionary<string, object?> SharedVariables = new();
    private static readonly ConcurrentDictionary<string, Template> Cache = new();
    private static string _templateDirectory = ".";
    private static bool _cachingEnabled = true;

    public static void AddSharedVariable(string name, object? value) => SharedVariables[name] = value;
    public static void RemoveSharedVariable(string name) => SharedVariables.TryRemove(name, out _);
    public static void ClearSharedVariables() => SharedVariables.Clear();

    public static void SetTemplateDirectory(string dir) => _templateDirectory = dir;
    public static void SetTemplateCachingEnabled(bool enabled) => _cachingEnabled = enabled;
    public static void ClearTemplateCache() => Cache.Clear();

    public static string RenderTemplateToHtml(string templateName, IDictionary<string, object?> dataModel)
    {
        var path = Path.Combine(_templateDirectory, templateName);
        var content = File.ReadAllText(path);
        return RenderTemplateStringToHtml(content, templateName, dataModel);
    }

    public static Task<string> RenderTemplateToHtmlAsync(string templateName, IDictionary<string, object?> dataModel) =>
        Task.Run(() => RenderTemplateToHtml(templateName, dataModel));

    public static string RenderTemplateStringToHtml(string content, string name, IDictionary<string, object?> dataModel)
    {
        var template = _cachingEnabled
            ? Cache.GetOrAdd(name, _ => Template.Parse(content))
            : Template.Parse(content);

        if (template.HasErrors)
        {
            throw new InvalidOperationException(
                $"Template '{name}' has errors: {string.Join("; ", template.Messages)}");
        }

        var ctx = new TemplateContext { LoopLimit = 100_000 };
        var scriptObject = new ScriptObject();
        foreach (var pair in SharedVariables)
        {
            scriptObject[pair.Key] = pair.Value;
        }

        foreach (var pair in dataModel)
        {
            scriptObject[pair.Key] = pair.Value;
        }

        ctx.PushGlobal(scriptObject);
        return template.Render(ctx);
    }

    public static Task<string> RenderTemplateStringToHtmlAsync(
        string content, string name, IDictionary<string, object?> dataModel) =>
        Task.Run(() => RenderTemplateStringToHtml(content, name, dataModel));

    public static byte[] RenderHtmlToPdfBytes(string html, PdfOptions? options = null)
    {
        options ??= new PdfOptions();
        using var ms = new MemoryStream();
        RenderHtmlToPdf(html, ms, options);
        return ms.ToArray();
    }

    public static Task<byte[]> RenderHtmlToPdfBytesAsync(string html, PdfOptions? options = null) =>
        Task.Run(() => RenderHtmlToPdfBytes(html, options));

    public static void RenderHtmlToPdfFile(string html, string outputPath, PdfOptions? options = null)
    {
        options ??= new PdfOptions();
        using var fs = File.Create(outputPath);
        RenderHtmlToPdf(html, fs, options);
    }

    public static byte[] RenderTemplateToPdfBytes(string templateName, IDictionary<string, object?> dataModel, PdfOptions? options = null)
    {
        var html = RenderTemplateToHtml(templateName, dataModel);
        return RenderHtmlToPdfBytes(html, options);
    }

    public static byte[] RenderTemplateStringToPdfBytes(
        string content, string name, IDictionary<string, object?> dataModel, PdfOptions? options = null)
    {
        var html = RenderTemplateStringToHtml(content, name, dataModel);
        return RenderHtmlToPdfBytes(html, options);
    }

    public static void RenderHtmlToPdf(string html, Stream output, PdfOptions options)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(options);

        var converter = new ConverterProperties();
        if (options.BaseUri is not null)
        {
            converter.SetBaseUri(options.BaseUri);
        }

        var writerProps = new WriterProperties();
        if (options.OwnerPassword is not null)
        {
            var permissions = 0;
            if (options.AllowPrinting) permissions |= EncryptionConstants.ALLOW_PRINTING;
            if (options.AllowCopy) permissions |= EncryptionConstants.ALLOW_COPY;
            if (options.AllowModify) permissions |= EncryptionConstants.ALLOW_MODIFY_CONTENTS;

            writerProps.SetStandardEncryption(
                Encoding.UTF8.GetBytes(options.UserPassword ?? string.Empty),
                Encoding.UTF8.GetBytes(options.OwnerPassword),
                permissions,
                EncryptionConstants.ENCRYPTION_AES_256);
        }

        using var writer = new PdfWriter(output, writerProps);
        using var pdf = new PdfDocument(writer);
        pdf.SetDefaultPageSize(options.PageSize);

        if (options.Title is not null) pdf.GetDocumentInfo().SetTitle(options.Title);
        if (options.Author is not null) pdf.GetDocumentInfo().SetAuthor(options.Author);
        if (options.Subject is not null) pdf.GetDocumentInfo().SetSubject(options.Subject);
        if (options.Keywords is not null) pdf.GetDocumentInfo().SetKeywords(options.Keywords);

        using (var doc = HtmlConverter.ConvertToDocument(new MemoryStream(Encoding.UTF8.GetBytes(html)), pdf, converter))
        {
            doc.SetMargins(options.MarginTop, options.MarginRight, options.MarginBottom, options.MarginLeft);

            if (!string.IsNullOrEmpty(options.WatermarkText))
            {
                ApplyWatermark(pdf, options);
            }
        }

        if (options.Bookmarks.Count > 0)
        {
            ApplyBookmarks(pdf, options.Bookmarks);
        }
    }

    private static void ApplyWatermark(PdfDocument pdf, PdfOptions options)
    {
        var color = ParseHexColor(options.WatermarkColor);
        for (var pageNum = 1; pageNum <= pdf.GetNumberOfPages(); pageNum++)
        {
            var page = pdf.GetPage(pageNum);
            var pageSize = page.GetPageSize();

            var canvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(page.NewContentStreamBefore(), page.GetResources(), pdf);
            using var layout = new iText.Layout.Canvas(canvas, pageSize);

            var paragraph = new Paragraph(options.WatermarkText)
                .SetFontSize(options.WatermarkFontSize)
                .SetFontColor(color, options.WatermarkOpacity)
                .SetTextAlignment(TextAlignment.CENTER);

            layout.ShowTextAligned(
                paragraph,
                pageSize.GetWidth() / 2,
                pageSize.GetHeight() / 2,
                pageNum,
                TextAlignment.CENTER,
                VerticalAlignment.MIDDLE,
                (float)(options.WatermarkRotation * Math.PI / 180.0));
        }
    }

    private static void ApplyBookmarks(PdfDocument pdf, List<PdfOptions.Bookmark> bookmarks)
    {
        var outlines = pdf.GetOutlines(false);
        foreach (var bookmark in bookmarks)
        {
            AddBookmark(pdf, outlines, bookmark);
        }
    }

    private static void AddBookmark(PdfDocument pdf, iText.Kernel.Pdf.PdfOutline parent, PdfOptions.Bookmark bookmark)
    {
        var node = parent.AddOutline(bookmark.Title);
        if (int.TryParse(bookmark.Destination, out var pageNum) && pageNum >= 1 && pageNum <= pdf.GetNumberOfPages())
        {
            var dest = iText.Kernel.Pdf.Navigation.PdfExplicitDestination.CreateFit(pdf.GetPage(pageNum));
            node.AddDestination(dest);
        }

        foreach (var child in bookmark.Children)
        {
            AddBookmark(pdf, node, child);
        }
    }

    private static Color ParseHexColor(string hex)
    {
        var trimmed = hex.TrimStart('#');
        if (trimmed.Length != 6) return ColorConstants.GRAY;
        var r = Convert.ToInt32(trimmed.Substring(0, 2), 16);
        var g = Convert.ToInt32(trimmed.Substring(2, 2), 16);
        var b = Convert.ToInt32(trimmed.Substring(4, 2), 16);
        return new DeviceRgb(r, g, b);
    }

    public static IReadOnlyList<string> ValidateTemplate(string content)
    {
        var template = Template.Parse(content);
        return template.HasErrors ? template.Messages.Select(m => m.Message).ToList() : Array.Empty<string>();
    }
}
