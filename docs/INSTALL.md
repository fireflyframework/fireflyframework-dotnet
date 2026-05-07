# Installing Firefly Framework

This guide covers every way to consume the Firefly Framework packages
from a .NET 10 project. Pick the path that matches where the version
you want to install is published.

| Source | When to use | Setup effort |
|---|---|---|
| [**NuGet.org**](#nugetorg-public-stable-releases) | Stable, public releases (no `-preview` / `-rc` suffix) | None — `dotnet add` works out of the box |
| [**GitHub Packages**](#github-packages-pre-release--preview-builds) | Pre-release / preview / RC builds, internal mirror | One-time `nuget.config` + GitHub PAT |
| [**Local project references**](#local-project-references-contributors) | Developing against the framework itself | None — clone and reference projects directly |

---

## Prerequisites

* **.NET 10 SDK** — `dotnet --version` must report a `10.0.*` build.
* A target project — for a brand-new one, see the [README quickstart](../README.md#build-your-first-firefly-service).

---

## NuGet.org (public stable releases)

Stable releases are pushed to NuGet.org at the repository's calendar
version (e.g. `26.04.01`). No setup is needed: NuGet.org is the
default source on every fresh .NET install.

```bash
dotnet add package FireflyFramework.Starter.Core
```

Browse the catalogue at <https://www.nuget.org/packages?q=FireflyFramework>.

---

## GitHub Packages (pre-release & preview builds)

Pre-release versions (anything with a `-preview`, `-rc`, or similar
suffix) and internal mirror copies of stable versions are published to
GitHub Packages at <https://github.com/orgs/fireflyframework/packages>.

GitHub Packages requires an authenticated source even for **public**
packages. The credential is a [GitHub Personal Access Token (PAT)](https://github.com/settings/tokens)
with the **`read:packages`** scope.

### One-time setup

#### 1. Create the PAT

1. Open <https://github.com/settings/tokens/new>.
2. Set a meaningful name (e.g. *firefly-nuget-read*) and an expiry that suits your team policy.
3. Select **`read:packages`** (and only that — least-privilege keeps the token safe to commit to a CI secret store).
4. Click **Generate token** and copy the value somewhere safe.

#### 2. Tell NuGet about the source

Pick one of the two equivalent paths.

**Option A — `dotnet nuget add source`** (one-time, machine-global):

```bash
dotnet nuget add source https://nuget.pkg.github.com/fireflyframework/index.json \
  --name firefly-github \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_PAT \
  --store-password-in-clear-text
```

The `--store-password-in-clear-text` flag is required on macOS / Linux because the Windows credential manager isn't available there.

**Option B — Project-local `nuget.config`** (commit-friendly, per-repo):

Create a `nuget.config` next to your `.sln` file:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="firefly-github" value="https://nuget.pkg.github.com/fireflyframework/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <firefly-github>
      <add key="Username" value="%GITHUB_USERNAME%" />
      <add key="ClearTextPassword" value="%GITHUB_PACKAGES_PAT%" />
    </firefly-github>
  </packageSourceCredentials>
</configuration>
```

`%VAR%` is expanded by NuGet from environment variables. Set them in
your shell profile or `.envrc`:

```bash
export GITHUB_USERNAME=your-github-username
export GITHUB_PACKAGES_PAT=ghp_xxxxxxxxxxxx
```

That way the `nuget.config` is safe to commit — the credentials live in
the environment, not the repository.

#### 3. Install a pre-release package

```bash
dotnet add package FireflyFramework.Starter.Core --version 26.05.01-preview
```

The `--version` flag is required for pre-release versions (NuGet won't
pick a `-preview` build automatically). Use `--prerelease` to take the
latest pre-release build instead:

```bash
dotnet add package FireflyFramework.Starter.Core --prerelease
```

### Using GitHub Packages from CI

In a GitHub Actions workflow inside the **same organisation**, the
auto-provided `GITHUB_TOKEN` is enough — no PAT required:

```yaml
- name: Authenticate to GitHub Packages
  run: |
    dotnet nuget add source https://nuget.pkg.github.com/fireflyframework/index.json \
      --name firefly-github \
      --username ${{ github.actor }} \
      --password ${{ secrets.GITHUB_TOKEN }} \
      --store-password-in-clear-text

- name: Restore
  run: dotnet restore
```

For workflows in **other** organisations, store a PAT with `read:packages`
in a repository secret (e.g. `FIREFLY_NUGET_PAT`) and reference it the
same way.

---

## Local project references (contributors)

If you're working *on* the framework itself, or you want to test an
unreleased change before it ships, reference the projects directly
instead of installing packages.

```bash
git clone https://github.com/fireflyframework/fireflyframework-dotnet.git
cd fireflyframework-dotnet
dotnet build FireflyFramework.sln
```

Then in your consumer project:

```bash
dotnet add reference \
  ../path/to/fireflyframework-dotnet/src/FireflyFramework.Starter.Core/FireflyFramework.Starter.Core.csproj
```

Or, in the `.csproj` directly:

```xml
<ProjectReference
  Include="..\path\to\fireflyframework-dotnet\src\FireflyFramework.Starter.Core\FireflyFramework.Starter.Core.csproj" />
```

This bypasses NuGet entirely; rebuilds in the framework checkout flow
into the consumer at the next `dotnet build`.

---

## Listing every available version

Once a source is configured, you can introspect what's published:

```bash
# All Firefly packages (truncated by default; pass --take 100 to expand)
dotnet package search FireflyFramework --source firefly-github

# Every version of one package
dotnet package search FireflyFramework.Starter.Core --source firefly-github --exact-match
```

Or open the GitHub Packages UI: every package has a *Versions* tab
listing each published build.

---

## Troubleshooting

**`Unable to load the service index for source https://nuget.pkg.github.com/...`**
The PAT is missing or expired. Verify with `dotnet nuget list source`
that the source is configured, then re-issue the PAT with `read:packages`.

**`401 Unauthorized` on `dotnet restore`**
The credentials in `nuget.config` (or environment) don't match a token
with `read:packages`. The `Username` field is the GitHub username; the
`ClearTextPassword` is the PAT.

**`The package 'FireflyFramework.X' is not found`**
The version you asked for isn't on the source you queried. Check the
GitHub Packages UI for the actual published versions. Pre-release builds
always need either `--version <exact>` or `--prerelease` on the install.

**`Package downgrade detected`**
Project transitive dependencies pinned a higher version than the one
you're installing. Either bump your direct install, or pin the
transitive in `Directory.Packages.props`.

---

## See also

* [`README.md`](../README.md) — five-minute pitch and the from-scratch quickstart.
* [`docs/CONFIGURATION.md`](CONFIGURATION.md) — every `Firefly:*` config section once a package is installed.
* [`docs/SERVICE-SCAFFOLDING.md`](SERVICE-SCAFFOLDING.md) — promoting a hello-world to the canonical 5-project layout.
