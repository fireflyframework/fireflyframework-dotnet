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

namespace FireflyFramework.Plugins.Api;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class PluginAttribute : Attribute
{
    public PluginAttribute(string id, string name, string version)
    {
        Id = id; Name = name; Version = version;
    }

    public string Id { get; }
    public string Name { get; }
    public string Version { get; }
    public string? Description { get; set; }
    public string? Author { get; set; }
    public string[] Dependencies { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ExtensionAttribute : Attribute
{
    public ExtensionAttribute(string pointId) => PointId = pointId;
    public string PointId { get; }
    public int Priority { get; set; }
}

[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
public sealed class ExtensionPointAttribute : Attribute
{
    public ExtensionPointAttribute(string id) => Id = id;
    public string Id { get; }
}
