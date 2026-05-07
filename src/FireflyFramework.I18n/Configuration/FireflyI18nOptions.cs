// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.I18n.Configuration;

public sealed class FireflyI18nOptions
{
    public const string SectionName = "Firefly:I18n";

    public string DefaultLocale { get; set; } = "en";
    public string ResourceDirectory { get; set; } = "i18n";
    public string BaseName { get; set; } = "messages";
    public string Resolver { get; set; } = "AcceptHeader"; // AcceptHeader | Fixed | Cookie
    public string CookieName { get; set; } = "lang";
}
