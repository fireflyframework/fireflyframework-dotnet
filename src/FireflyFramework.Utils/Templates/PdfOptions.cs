// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using iText.Kernel.Geom;

namespace FireflyFramework.Utils.Templates;

/// <summary>
/// Fluent builder for PDF rendering. Mirrors <c>PdfOptions</c> from fireflyframework-utils:
/// page size, margins, fonts, watermark, encryption, metadata, bookmarks.
/// </summary>
public sealed class PdfOptions
{
    public PageSize PageSize { get; private set; } = iText.Kernel.Geom.PageSize.A4;
    public float MarginTop { get; private set; } = 36f;
    public float MarginRight { get; private set; } = 36f;
    public float MarginBottom { get; private set; } = 36f;
    public float MarginLeft { get; private set; } = 36f;
    public string? DefaultFont { get; private set; }
    public string? FontDirectory { get; private set; }
    public string? BaseUri { get; private set; }
    public string? WatermarkText { get; private set; }
    public float WatermarkOpacity { get; private set; } = 0.3f;
    public int WatermarkFontSize { get; private set; } = 60;
    public int WatermarkRotation { get; private set; } = 45;
    public string WatermarkColor { get; private set; } = "#A0A0A0";
    public string? OwnerPassword { get; private set; }
    public string? UserPassword { get; private set; }
    public bool AllowPrinting { get; private set; } = true;
    public bool AllowCopy { get; private set; } = true;
    public bool AllowModify { get; private set; }
    public string? Title { get; private set; }
    public string? Author { get; private set; }
    public string? Subject { get; private set; }
    public string? Keywords { get; private set; }
    public List<Bookmark> Bookmarks { get; } = new();

    public PdfOptions WithPageSize(PageSize size) { PageSize = size; return this; }

    public PdfOptions WithMargins(float top, float right, float bottom, float left)
    {
        MarginTop = top;
        MarginRight = right;
        MarginBottom = bottom;
        MarginLeft = left;
        return this;
    }

    public PdfOptions WithDefaultFont(string font) { DefaultFont = font; return this; }
    public PdfOptions WithFontDirectory(string dir) { FontDirectory = dir; return this; }
    public PdfOptions WithBaseUri(string uri) { BaseUri = uri; return this; }
    public PdfOptions WithWatermark(string text) { WatermarkText = text; return this; }

    public PdfOptions WithWatermark(string text, float opacity, int fontSize, int rotation, string color)
    {
        WatermarkText = text;
        WatermarkOpacity = opacity;
        WatermarkFontSize = fontSize;
        WatermarkRotation = rotation;
        WatermarkColor = color;
        return this;
    }

    public PdfOptions WithEncryption(string ownerPassword)
    {
        OwnerPassword = ownerPassword;
        return this;
    }

    public PdfOptions WithEncryption(
        string userPassword,
        string ownerPassword,
        bool allowPrinting,
        bool allowCopy,
        bool allowModify)
    {
        UserPassword = userPassword;
        OwnerPassword = ownerPassword;
        AllowPrinting = allowPrinting;
        AllowCopy = allowCopy;
        AllowModify = allowModify;
        return this;
    }

    public PdfOptions WithMetadata(string? title, string? author, string? subject, string? keywords)
    {
        Title = title;
        Author = author;
        Subject = subject;
        Keywords = keywords;
        return this;
    }

    public PdfOptions WithBookmark(string title, string destination)
    {
        Bookmarks.Add(new Bookmark(title, 0, destination, new()));
        return this;
    }

    public PdfOptions WithBookmark(string title, int level, string destination)
    {
        Bookmarks.Add(new Bookmark(title, level, destination, new()));
        return this;
    }

    public PdfOptions WithChildBookmark(string title, string destination)
    {
        if (Bookmarks.Count == 0)
        {
            Bookmarks.Add(new Bookmark(title, 0, destination, new()));
        }
        else
        {
            Bookmarks[^1].Children.Add(new Bookmark(title, Bookmarks[^1].Level + 1, destination, new()));
        }

        return this;
    }

    public PdfOptions ClearBookmarks() { Bookmarks.Clear(); return this; }

    public sealed record Bookmark(string Title, int Level, string Destination, List<Bookmark> Children);
}
