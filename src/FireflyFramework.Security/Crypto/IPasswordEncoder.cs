// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Security.Crypto;

/// <summary>Spring <c>PasswordEncoder</c> port.</summary>
public interface IPasswordEncoder
{
    string Encode(string raw);
    bool Matches(string raw, string encoded);
    bool UpgradeEncoding(string encoded) => false;
}

public sealed class BCryptPasswordEncoder : IPasswordEncoder
{
    private readonly int _workFactor;
    public BCryptPasswordEncoder(int workFactor = 12) { _workFactor = workFactor; }
    public string Encode(string raw) => BCrypt.Net.BCrypt.HashPassword(raw, _workFactor);
    public bool Matches(string raw, string encoded) => BCrypt.Net.BCrypt.Verify(raw, encoded);
}

public sealed class NoopPasswordEncoder : IPasswordEncoder
{
    public string Encode(string raw) => raw;
    public bool Matches(string raw, string encoded) => string.Equals(raw, encoded, StringComparison.Ordinal);
}
