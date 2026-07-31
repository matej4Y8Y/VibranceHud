# PlexusX 1.0 Licence System — Implementation Plan

> Steps use checkbox (`- [ ]`) syntax so progress can be tracked in place.
> Tasks marked **[WAVE A]** have no dependencies on each other and can be worked in
> parallel. **[WAVE B]** tasks require Wave A to be merged. **[WAVE C]** requires Wave B.

**Goal:** Replace the beta licence system with one where keys cannot be forged, licences
cannot break across app versions, and a key can be tied to a single PC.

**Architecture:** The key stops being the licence. A key is a short identifier that grants
nothing on its own; it is redeemed once for a signed licence document. The document carries an
explicit expiry date and is signed with ECDSA P-256. Only the public key ships in the app, so
the app can verify licences but can never create one.

**Tech Stack:** C# / .NET 8 / WinForms. `System.Security.Cryptography.ECDsa` (built into .NET —
do not add a NuGet package for crypto). xUnit for tests.

## Global Constraints

- Target framework is `net8.0-windows`. Do not change it.
- **No new NuGet packages.** Everything needed is in the .NET base class library.
- All new licence code goes in namespace `VibranceHud.Licensing` (note: NOT the existing
  `VibranceHud.License`, which stays untouched so the beta keeps working).
- Do not modify any file under `License/` — that is the beta system and must keep compiling.
- All timestamps are UTC. Format with `"yyyy-MM-ddTHH:mm:ssZ"` and
  `CultureInfo.InvariantCulture`.
- Every public method that parses external input must fail closed: on malformed input return
  false / null, never throw and never grant access.
- Run `dotnet test tests/VibranceHud.Tests/VibranceHud.Tests.csproj` before every commit. It
  must report 0 failures.
- Commit after every task with the exact message given in the task.

---

## File Structure

| File | Responsibility | Task |
|---|---|---|
| `Licensing/PlanCatalog.cs` | The three plans and their durations | 1 |
| `Licensing/KeyCode.cs` | Short key format, generation, checksum | 2 |
| `Licensing/TrialPolicy.cs` | 4-day trial arithmetic | 3 |
| `Licensing/LicenceDocument.cs` | The licence record + canonical JSON | 4 |
| `Licensing/LicenceSigner.cs` | Signs a document (private key side) | 5 |
| `Licensing/LicenceVerifier.cs` | Verifies a signed licence (public key side) | 6 |
| `Licensing/ILicenceRedeemer.cs` | Redemption interface + result type | 7 |
| `Licensing/LocalRedeemer.cs` | Offline stand-in redeemer for development | 7 |

---

## Task 1: Plan catalogue **[WAVE A]**

**Files:**
- Create: `Licensing/PlanCatalog.cs`
- Test: `tests/VibranceHud.Tests/PlanCatalogTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `VibranceHud.Licensing.PlanCatalog.DurationFor(string planId)` returning
  `TimeSpan?`; `PlanCatalog.IsKnown(string planId)` returning `bool`; constants
  `PlanCatalog.Trial`, `PlanCatalog.Monthly`, `PlanCatalog.Lifetime600` (all `string`).

- [ ] **Step 1: Write the failing test**

Create `tests/VibranceHud.Tests/PlanCatalogTests.cs`:

```csharp
using System;
using VibranceHud.Licensing;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class PlanCatalogTests
    {
        [Fact]
        public void TrialLastsFourDays()
        {
            Assert.Equal(TimeSpan.FromDays(4), PlanCatalog.DurationFor(PlanCatalog.Trial));
        }

        [Fact]
        public void MonthlyLastsThirtyDays()
        {
            Assert.Equal(TimeSpan.FromDays(30), PlanCatalog.DurationFor(PlanCatalog.Monthly));
        }

        [Fact]
        public void Lifetime600LastsSixHundredDays()
        {
            Assert.Equal(TimeSpan.FromDays(600), PlanCatalog.DurationFor(PlanCatalog.Lifetime600));
        }

        /// <summary>An unknown plan must not resolve to a duration. Returning a default here
        /// is how the beta system accidentally granted a year to unrecognised keys.</summary>
        [Theory]
        [InlineData("")]
        [InlineData("premium")]
        [InlineData("MONTHLY")]
        [InlineData(null)]
        public void UnknownPlanHasNoDuration(string? planId)
        {
            Assert.Null(PlanCatalog.DurationFor(planId));
            Assert.False(PlanCatalog.IsKnown(planId));
        }

        [Fact]
        public void KnownPlansReportAsKnown()
        {
            Assert.True(PlanCatalog.IsKnown(PlanCatalog.Trial));
            Assert.True(PlanCatalog.IsKnown(PlanCatalog.Monthly));
            Assert.True(PlanCatalog.IsKnown(PlanCatalog.Lifetime600));
        }

        [Fact]
        public void PlanIdsAreLowercaseAndStable()
        {
            Assert.Equal("trial", PlanCatalog.Trial);
            Assert.Equal("monthly", PlanCatalog.Monthly);
            Assert.Equal("lifetime600", PlanCatalog.Lifetime600);
        }
    }
}
```

- [ ] **Step 2: Run the test and confirm it fails**

Run:
```bash
dotnet test tests/VibranceHud.Tests/VibranceHud.Tests.csproj --filter "FullyQualifiedName~PlanCatalog"
```
Expected: build error `CS0246: The type or namespace name 'Licensing' could not be found`.

- [ ] **Step 3: Write the implementation**

Create `Licensing/PlanCatalog.cs`:

```csharp
using System;

namespace VibranceHud.Licensing
{
    /// <summary>
    /// The plans PlexusX sells, and how long each lasts.
    ///
    /// Plan ids are written into signed licences, so these strings are permanent - changing
    /// one invalidates every licence already issued under it. Add new plans; never rename.
    ///
    /// An unknown plan deliberately has NO duration rather than a default. The beta system
    /// defaulted unrecognised tiers to its longest one, which meant a typo or a plan from a
    /// newer build granted a full year.
    /// </summary>
    public static class PlanCatalog
    {
        public const string Trial = "trial";
        public const string Monthly = "monthly";
        public const string Lifetime600 = "lifetime600";

        public static TimeSpan? DurationFor(string? planId) => planId switch
        {
            Trial => TimeSpan.FromDays(4),
            Monthly => TimeSpan.FromDays(30),
            Lifetime600 => TimeSpan.FromDays(600),
            _ => null,
        };

        public static bool IsKnown(string? planId) => DurationFor(planId) != null;
    }
}
```

- [ ] **Step 4: Run the test and confirm it passes**

Run:
```bash
dotnet test tests/VibranceHud.Tests/VibranceHud.Tests.csproj --filter "FullyQualifiedName~PlanCatalog"
```
Expected: `Passed! - Failed: 0, Passed: 8`

- [ ] **Step 5: Commit**

```bash
git add Licensing/PlanCatalog.cs tests/VibranceHud.Tests/PlanCatalogTests.cs
git commit -m "feat(licensing): plan catalogue with explicit durations

Unknown plans return no duration rather than a default. The beta system defaulted
unrecognised tiers to its longest, so a typo or a plan from a newer build granted a
full year."
```

---

## Task 2: Short key codes **[WAVE A]**

**Files:**
- Create: `Licensing/KeyCode.cs`
- Test: `tests/VibranceHud.Tests/KeyCodeTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `KeyCode.Generate()` returning `string` (format `2K7M-Q8XR-T9WD-N3FG`);
  `KeyCode.Normalise(string? input)` returning `string`;
  `KeyCode.IsWellFormed(string? input)` returning `bool`.

- [ ] **Step 1: Write the failing test**

Create `tests/VibranceHud.Tests/KeyCodeTests.cs`:

```csharp
using System.Collections.Generic;
using VibranceHud.Licensing;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class KeyCodeTests
    {
        [Fact]
        public void GeneratedKeyIsFourGroupsOfFour()
        {
            var key = KeyCode.Generate();
            var parts = key.Split('-');
            Assert.Equal(4, parts.Length);
            foreach (var p in parts) Assert.Equal(4, p.Length);
        }

        [Fact]
        public void GeneratedKeysAreWellFormed()
        {
            for (int i = 0; i < 200; i++)
                Assert.True(KeyCode.IsWellFormed(KeyCode.Generate()));
        }

        [Fact]
        public void GeneratedKeysAreUnique()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < 500; i++) Assert.True(seen.Add(KeyCode.Generate()));
        }

        /// <summary>Ambiguous characters are excluded so a key can be read aloud or copied
        /// off a screen without 0/O or 1/I confusion.</summary>
        [Fact]
        public void GeneratedKeysAvoidAmbiguousCharacters()
        {
            for (int i = 0; i < 200; i++)
            {
                var key = KeyCode.Generate().Replace("-", "");
                foreach (var c in key)
                    Assert.DoesNotContain(c, "OIL01");
            }
        }

        /// <summary>Users paste keys lowercase, with stray spaces, or with the dashes
        /// missing. All of those must land on the same canonical form.</summary>
        [Fact]
        public void NormaliseAcceptsLowercase()
        {
            var key = KeyCode.Generate();
            Assert.Equal(key, KeyCode.Normalise(key.ToLowerInvariant()));
        }

        [Fact]
        public void NormaliseAcceptsSurroundingWhitespace()
        {
            var key = KeyCode.Generate();
            Assert.Equal(key, KeyCode.Normalise("   " + key + "  "));
        }

        [Fact]
        public void NormaliseAcceptsMissingDashes()
        {
            var key = KeyCode.Generate();
            Assert.Equal(key, KeyCode.Normalise(key.Replace("-", "")));
        }

        [Fact]
        public void NormaliseIsIdempotent()
        {
            var key = KeyCode.Generate();
            Assert.Equal(key, KeyCode.Normalise(KeyCode.Normalise(key)));
        }

        /// <summary>The last character is a check digit, so a single mistyped character is
        /// caught before it ever reaches the server.</summary>
        [Fact]
        public void SingleCharacterTypoIsRejected()
        {
            var key = KeyCode.Generate();
            var chars = key.ToCharArray();
            int idx = 0;
            while (chars[idx] == '-') idx++;
            chars[idx] = chars[idx] == 'A' ? 'B' : 'A';
            var typo = new string(chars);

            Assert.NotEqual(key, typo);
            Assert.False(KeyCode.IsWellFormed(typo));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("2K7M-Q8XR-T9WD")]
        [InlineData("2K7M-Q8XR-T9WD-N3FG-EXTRA")]
        [InlineData("2K7M-Q8XR-T9WD-N3F!")]
        public void MalformedInputIsRejected(string? input)
        {
            Assert.False(KeyCode.IsWellFormed(input));
        }

        [Fact]
        public void NormaliseOfGarbageReturnsEmpty()
        {
            Assert.Equal("", KeyCode.Normalise(null));
            Assert.Equal("", KeyCode.Normalise("   "));
        }
    }
}
```

- [ ] **Step 2: Run the test and confirm it fails**

Run:
```bash
dotnet test tests/VibranceHud.Tests/VibranceHud.Tests.csproj --filter "FullyQualifiedName~KeyCode"
```
Expected: build error `CS0246`.

- [ ] **Step 3: Write the implementation**

Create `Licensing/KeyCode.cs`:

```csharp
using System;
using System.Security.Cryptography;
using System.Text;

namespace VibranceHud.Licensing
{
    /// <summary>
    /// The short code a customer receives, e.g. 2K7M-Q8XR-T9WD-N3FG.
    ///
    /// It carries no permissions and no duration - it is only an identifier redeemed once for
    /// a signed licence. That is deliberate: because the code grants nothing by itself,
    /// knowing the format buys an attacker nothing, and the code can be short enough to read
    /// out over voice or quote in a support message.
    ///
    /// The final character is a check digit, so a single mistyped or misread character is
    /// caught locally instead of becoming a failed redemption the user can't explain.
    /// </summary>
    public static class KeyCode
    {
        /// <summary>Crockford-style: no O, I, L, 0 or 1, so nothing is ambiguous when read
        /// off a screen or spoken aloud.</summary>
        private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

        private const int Groups = 4;
        private const int GroupSize = 4;
        private const int TotalChars = Groups * GroupSize; // 16, last one is the check digit

        public static string Generate()
        {
            var body = new char[TotalChars - 1];
            var buffer = new byte[body.Length];
            RandomNumberGenerator.Fill(buffer);
            for (int i = 0; i < body.Length; i++)
                body[i] = Alphabet[buffer[i] % Alphabet.Length];

            var raw = new string(body);
            return Format(raw + CheckDigit(raw));
        }

        /// <summary>Accepts what a user actually pastes - lowercase, extra spaces, missing
        /// dashes - and returns the canonical form, or "" if it can't be made sense of.</summary>
        public static string Normalise(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";

            var sb = new StringBuilder(TotalChars);
            foreach (var c in input.ToUpperInvariant())
            {
                if (c == '-' || char.IsWhiteSpace(c)) continue;
                if (Alphabet.IndexOf(c) < 0) return "";
                sb.Append(c);
                if (sb.Length > TotalChars) return "";
            }

            return sb.Length == TotalChars ? Format(sb.ToString()) : "";
        }

        public static bool IsWellFormed(string? input)
        {
            var normalised = Normalise(input);
            if (normalised.Length == 0) return false;

            var raw = normalised.Replace("-", "");
            var body = raw.Substring(0, raw.Length - 1);
            return raw[raw.Length - 1] == CheckDigit(body);
        }

        private static string Format(string raw)
        {
            var sb = new StringBuilder(TotalChars + Groups - 1);
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && i % GroupSize == 0) sb.Append('-');
                sb.Append(raw[i]);
            }
            return sb.ToString();
        }

        /// <summary>Position-weighted sum, so transposing two characters is caught as well as
        /// changing one. A plain sum would miss swaps entirely.</summary>
        private static char CheckDigit(string body)
        {
            int sum = 0;
            for (int i = 0; i < body.Length; i++)
                sum += (Alphabet.IndexOf(body[i]) + 1) * (i + 1);
            return Alphabet[sum % Alphabet.Length];
        }
    }
}
```

- [ ] **Step 4: Run the test and confirm it passes**

Run:
```bash
dotnet test tests/VibranceHud.Tests/VibranceHud.Tests.csproj --filter "FullyQualifiedName~KeyCode"
```
Expected: `Passed! - Failed: 0`

- [ ] **Step 5: Commit**

```bash
git add Licensing/KeyCode.cs tests/VibranceHud.Tests/KeyCodeTests.cs
git commit -m "feat(licensing): short customer key codes with a check digit

The code is an identifier, not a licence - it grants nothing on its own, so it can be
short enough to read aloud. Ambiguous characters are excluded and the last character is
a position-weighted check digit, so a mistyped or transposed character is caught locally
rather than becoming an unexplainable failed redemption."
```

---

## Task 3: Trial policy **[WAVE A]**

**Files:**
- Create: `Licensing/TrialPolicy.cs`
- Test: `tests/VibranceHud.Tests/TrialPolicyTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `TrialPolicy.Length` (`TimeSpan`); `TrialPolicy.IsExpired(DateTime startedUtc,
  DateTime nowUtc)` returning `bool`; `TrialPolicy.Remaining(DateTime startedUtc, DateTime
  nowUtc)` returning `TimeSpan`.

- [ ] **Step 1: Write the failing test**

Create `tests/VibranceHud.Tests/TrialPolicyTests.cs`:

```csharp
using System;
using VibranceHud.Licensing;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class TrialPolicyTests
    {
        private static readonly DateTime Start = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void TrialIsFourDays()
        {
            Assert.Equal(TimeSpan.FromDays(4), TrialPolicy.Length);
        }

        [Fact]
        public void FreshTrialIsNotExpired()
        {
            Assert.False(TrialPolicy.IsExpired(Start, Start));
        }

        [Fact]
        public void TrialIsLiveOnDayThree()
        {
            Assert.False(TrialPolicy.IsExpired(Start, Start.AddDays(3)));
        }

        [Fact]
        public void TrialEndsAtExactlyFourDays()
        {
            Assert.True(TrialPolicy.IsExpired(Start, Start.AddDays(4)));
        }

        [Fact]
        public void TrialIsExpiredAfterFourDays()
        {
            Assert.True(TrialPolicy.IsExpired(Start, Start.AddDays(4).AddSeconds(1)));
        }

        /// <summary>A clock moved backwards must not extend the trial. Without this, setting
        /// the system date back is a one-click trial reset.</summary>
        [Fact]
        public void ClockMovedBackwardsDoesNotExtendTheTrial()
        {
            Assert.Equal(TimeSpan.Zero, TrialPolicy.Remaining(Start, Start.AddDays(-10)));
            Assert.True(TrialPolicy.IsExpired(Start, Start.AddDays(-10)));
        }

        [Fact]
        public void RemainingCountsDown()
        {
            Assert.Equal(TimeSpan.FromDays(4), TrialPolicy.Remaining(Start, Start));
            Assert.Equal(TimeSpan.FromDays(1), TrialPolicy.Remaining(Start, Start.AddDays(3)));
        }

        [Fact]
        public void RemainingNeverGoesNegative()
        {
            Assert.Equal(TimeSpan.Zero, TrialPolicy.Remaining(Start, Start.AddDays(99)));
        }
    }
}
```

- [ ] **Step 2: Run the test and confirm it fails**

Run:
```bash
dotnet test tests/VibranceHud.Tests/VibranceHud.Tests.csproj --filter "FullyQualifiedName~TrialPolicy"
```
Expected: build error `CS0246`.

- [ ] **Step 3: Write the implementation**

Create `Licensing/TrialPolicy.cs`:

```csharp
using System;

namespace VibranceHud.Licensing
{
    /// <summary>
    /// How long the free trial lasts and whether a given one has ended.
    ///
    /// Pure arithmetic over a supplied clock so every boundary is testable without waiting
    /// four days. A clock earlier than the recorded start is treated as expired rather than
    /// as extra time - otherwise winding the system date back is a one-click trial reset.
    /// </summary>
    public static class TrialPolicy
    {
        public static readonly TimeSpan Length = TimeSpan.FromDays(4);

        public static bool IsExpired(DateTime startedUtc, DateTime nowUtc) =>
            Remaining(startedUtc, nowUtc) <= TimeSpan.Zero;

        public static TimeSpan Remaining(DateTime startedUtc, DateTime nowUtc)
        {
            if (nowUtc < startedUtc) return TimeSpan.Zero; // clock rolled back
            var used = nowUtc - startedUtc;
            var left = Length - used;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }
    }
}
```

- [ ] **Step 4: Run the test and confirm it passes**

Run:
```bash
dotnet test tests/VibranceHud.Tests/VibranceHud.Tests.csproj --filter "FullyQualifiedName~TrialPolicy"
```
Expected: `Passed! - Failed: 0, Passed: 8`

- [ ] **Step 5: Commit**

```bash
git add Licensing/TrialPolicy.cs tests/VibranceHud.Tests/TrialPolicyTests.cs
git commit -m "feat(licensing): 4-day trial policy

Pure arithmetic over a supplied clock so the boundary is testable without waiting four
days. A clock earlier than the recorded start counts as expired, so winding the system
date back isn't a one-click trial reset."
```

---

## Task 4: Licence document **[WAVE A]**

**Files:**
- Create: `Licensing/LicenceDocument.cs`
- Test: `tests/VibranceHud.Tests/LicenceDocumentTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `sealed record LicenceDocument(string Serial, string Plan, DateTime IssuedUtc,
  DateTime ExpiresUtc, string HardwareId)`; `LicenceDocument.ToCanonicalJson()` returning
  `string`; `static LicenceDocument.TryFromJson(string? json, out LicenceDocument? doc)`
  returning `bool`; `doc.IsExpiredAt(DateTime nowUtc)` returning `bool`.

- [ ] **Step 1: Write the failing test**

Create `tests/VibranceHud.Tests/LicenceDocumentTests.cs`:

```csharp
using System;
using VibranceHud.Licensing;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class LicenceDocumentTests
    {
        private static LicenceDocument Sample() => new(
            Serial: "2K7M-Q8XR-T9WD-N3FG",
            Plan: PlanCatalog.Monthly,
            IssuedUtc: new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc),
            ExpiresUtc: new DateTime(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc),
            HardwareId: "MXXBGGXAOCQP36SC");

        [Fact]
        public void RoundTripsThroughJson()
        {
            Assert.True(LicenceDocument.TryFromJson(Sample().ToCanonicalJson(), out var back));
            Assert.Equal(Sample(), back);
        }

        /// <summary>The signature covers these exact bytes, so serialising the same document
        /// twice must produce byte-identical output or valid licences fail to verify.</summary>
        [Fact]
        public void CanonicalJsonIsStable()
        {
            Assert.Equal(Sample().ToCanonicalJson(), Sample().ToCanonicalJson());
        }

        [Fact]
        public void ExpiryIsComparedAgainstTheSuppliedClock()
        {
            var doc = Sample();
            Assert.False(doc.IsExpiredAt(new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc)));
            Assert.True(doc.IsExpiredAt(new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc)));
        }

        [Fact]
        public void ExpiresExactlyAtTheStatedInstant()
        {
            Assert.True(Sample().IsExpiredAt(Sample().ExpiresUtc));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not json")]
        [InlineData("{}")]
        [InlineData("""{"serial":"2K7M-Q8XR-T9WD-N3FG"}""")]
        [InlineData("""{"serial":"X","plan":"monthly","issued":"nonsense","expires":"nonsense","hardware":"Y"}""")]
        public void MalformedJsonIsRejected(string? json)
        {
            Assert.False(LicenceDocument.TryFromJson(json, out var doc));
            Assert.Null(doc);
        }

        /// <summary>Times must survive as UTC. A licence read back as local time would expire
        /// at the wrong moment for anyone outside UTC.</summary>
        [Fact]
        public void TimesStayUtcAcrossTheRoundTrip()
        {
            LicenceDocument.TryFromJson(Sample().ToCanonicalJson(), out var back);
            Assert.Equal(DateTimeKind.Utc, back!.IssuedUtc.Kind);
            Assert.Equal(DateTimeKind.Utc, back.ExpiresUtc.Kind);
        }
    }
}
```

- [ ] **Step 2: Run the test and confirm it fails**

Run:
```bash
dotnet test tests/VibranceHud.Tests/VibranceHud.Tests.csproj --filter "FullyQualifiedName~LicenceDocument"
```
Expected: build error `CS0246`.

- [ ] **Step 3: Write the implementation**

Create `Licensing/LicenceDocument.cs`:

```csharp
using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VibranceHud.Licensing
{
    /// <summary>
    /// What a redeemed key actually grants. This is the thing that gets signed.
    ///
    /// Expiry is an explicit date rather than a plan the app has to look up. That is what
    /// ends the version-compatibility problem the beta had: a licence issued years from now,
    /// under a plan this build has never heard of, still expires on exactly the right day.
    /// The app never needs teaching what a plan means.
    /// </summary>
    public sealed record LicenceDocument(
        string Serial,
        string Plan,
        DateTime IssuedUtc,
        DateTime ExpiresUtc,
        string HardwareId)
    {
        private const string TimeFormat = "yyyy-MM-ddTHH:mm:ssZ";

        public bool IsExpiredAt(DateTime nowUtc) => nowUtc >= ExpiresUtc;

        /// <summary>
        /// The exact bytes the signature covers. Property order is fixed and written by hand
        /// rather than left to a serialiser's defaults - if this output ever shifts by a
        /// single byte, every licence already issued stops verifying.
        /// </summary>
        public string ToCanonicalJson()
        {
            var raw = new RawDocument
            {
                Serial = Serial,
                Plan = Plan,
                Issued = IssuedUtc.ToUniversalTime().ToString(TimeFormat, CultureInfo.InvariantCulture),
                Expires = ExpiresUtc.ToUniversalTime().ToString(TimeFormat, CultureInfo.InvariantCulture),
                Hardware = HardwareId,
            };
            return JsonSerializer.Serialize(raw);
        }

        public static bool TryFromJson(string? json, out LicenceDocument? doc)
        {
            doc = null;
            if (string.IsNullOrWhiteSpace(json)) return false;

            try
            {
                var raw = JsonSerializer.Deserialize<RawDocument>(json);
                if (raw == null) return false;
                if (string.IsNullOrEmpty(raw.Serial) || string.IsNullOrEmpty(raw.Plan)) return false;
                if (string.IsNullOrEmpty(raw.Issued) || string.IsNullOrEmpty(raw.Expires)) return false;

                if (!TryParseUtc(raw.Issued, out var issued)) return false;
                if (!TryParseUtc(raw.Expires, out var expires)) return false;

                doc = new LicenceDocument(raw.Serial, raw.Plan, issued, expires, raw.Hardware ?? "");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseUtc(string value, out DateTime utc) =>
            DateTime.TryParseExact(value, TimeFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc);

        private sealed class RawDocument
        {
            [JsonPropertyName("serial")] public string? Serial { get; set; }
            [JsonPropertyName("plan")] public string? Plan { get; set; }
            [JsonPropertyName("issued")] public string? Issued { get; set; }
            [JsonPropertyName("expires")] public string? Expires { get; set; }
            [JsonPropertyName("hardware")] public string? Hardware { get; set; }
        }
    }
}
```

- [ ] **Step 4: Run the test and confirm it passes**

Run:
```bash
dotnet test tests/VibranceHud.Tests/VibranceHud.Tests.csproj --filter "FullyQualifiedName~LicenceDocument"
```
Expected: `Passed! - Failed: 0`

- [ ] **Step 5: Commit**

```bash
git add Licensing/LicenceDocument.cs tests/VibranceHud.Tests/LicenceDocumentTests.cs
git commit -m "feat(licensing): signed licence document with an explicit expiry date

Expiry is a date inside the document rather than a plan the app looks up, which is what
ends the version-compatibility problem: a licence issued under a plan this build has
never heard of still expires on the right day. Canonical JSON is written by hand because
the signature covers those exact bytes."
```

---

## Task 5: Licence signing **[WAVE B — needs Task 4]**

**Files:**
- Create: `Licensing/LicenceSigner.cs`
- Test: `tests/VibranceHud.Tests/LicenceSignerTests.cs`

**Interfaces:**
- Consumes: `LicenceDocument` from Task 4.
- Produces: `LicenceSigner.CreateKeyPair(out byte[] privateKey, out byte[] publicKey)`;
  `LicenceSigner.Sign(LicenceDocument doc, byte[] privateKey)` returning `string` (the signed
  envelope JSON).

- [ ] **Step 1: Write the failing test**

Create `tests/VibranceHud.Tests/LicenceSignerTests.cs`:

```csharp
using System;
using VibranceHud.Licensing;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class LicenceSignerTests
    {
        private static LicenceDocument Sample() => new(
            "2K7M-Q8XR-T9WD-N3FG", PlanCatalog.Monthly,
            new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc),
            "MXXBGGXAOCQP36SC");

        [Fact]
        public void CreatesAUsableKeyPair()
        {
            LicenceSigner.CreateKeyPair(out var priv, out var pub);
            Assert.NotEmpty(priv);
            Assert.NotEmpty(pub);
            Assert.NotEqual(priv, pub);
        }

        [Fact]
        public void EveryKeyPairIsDifferent()
        {
            LicenceSigner.CreateKeyPair(out var priv1, out _);
            LicenceSigner.CreateKeyPair(out var priv2, out _);
            Assert.NotEqual(priv1, priv2);
        }

        [Fact]
        public void SignedEnvelopeCarriesDocumentAndSignature()
        {
            LicenceSigner.CreateKeyPair(out var priv, out _);
            var envelope = LicenceSigner.Sign(Sample(), priv);

            Assert.Contains("\"doc\"", envelope);
            Assert.Contains("\"sig\"", envelope);
        }

        [Fact]
        public void SigningIsNotDestructiveToTheDocument()
        {
            LicenceSigner.CreateKeyPair(out var priv, out _);
            var doc = Sample();
            LicenceSigner.Sign(doc, priv);
            Assert.Equal(Sample(), doc);
        }
    }
}
```

- [ ] **Step 2: Run the test and confirm it fails**

Run:
```bash
dotnet test tests/VibranceHud.Tests/VibranceHud.Tests.csproj --filter "FullyQualifiedName~LicenceSigner"
```
Expected: build error `CS0246`.

- [ ] **Step 3: Write the implementation**

Create `Licensing/LicenceSigner.cs`:

```csharp
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VibranceHud.Licensing
{
    /// <summary>The signed form of a licence: the document, plus a signature over it.</summary>
    public sealed class LicenceEnvelope
    {
        [JsonPropertyName("doc")] public string? Doc { get; set; }
        [JsonPropertyName("sig")] public string? Sig { get; set; }
    }

    /// <summary>
    /// Creates signed licences. THE PRIVATE KEY SIDE - this never runs on a customer machine.
    ///
    /// ECDSA P-256 rather than Ed25519 purely because .NET has it built in; a native crypto
    /// library would fight the single-file self-contained publish the app relies on.
    ///
    /// The whole point of splitting this from LicenceVerifier is that the app ships only the
    /// verifier and the public key. Extracting everything from the installer then lets someone
    /// check licences and never create one - which is exactly what the beta got wrong.
    /// </summary>
    public static class LicenceSigner
    {
        public static void CreateKeyPair(out byte[] privateKey, out byte[] publicKey)
        {
            using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            privateKey = ec.ExportECPrivateKey();
            publicKey = ec.ExportSubjectPublicKeyInfo();
        }

        public static string Sign(LicenceDocument doc, byte[] privateKey)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (privateKey == null || privateKey.Length == 0)
                throw new ArgumentException("Private key is required.", nameof(privateKey));

            var canonical = doc.ToCanonicalJson();
            var payload = Encoding.UTF8.GetBytes(canonical);

            using var ec = ECDsa.Create();
            ec.ImportECPrivateKey(privateKey, out _);
            var signature = ec.SignData(payload, HashAlgorithmName.SHA256);

            var envelope = new LicenceEnvelope
            {
                Doc = Convert.ToBase64String(payload),
                Sig = Convert.ToBase64String(signature),
            };
            return JsonSerializer.Serialize(envelope);
        }
    }
}
```

- [ ] **Step 4: Run the test and confirm it passes**

Run:
```bash
dotnet test tests/VibranceHud.Tests/VibranceHud.Tests.csproj --filter "FullyQualifiedName~LicenceSigner"
```
Expected: `Passed! - Failed: 0, Passed: 4`

- [ ] **Step 5: Commit**

```bash
git add Licensing/LicenceSigner.cs tests/VibranceHud.Tests/LicenceSignerTests.cs
git commit -m "feat(licensing): ECDSA licence signing (private key side)

Kept separate from verification so the app can ship only the verifier and the public key.
Extracting everything from the installer then lets someone check licences and never
create one - the exact thing the beta's symmetric secret got wrong. ECDSA P-256 because
.NET has it built in; a native crypto library would fight the single-file publish."
```

---

## Task 6: Licence verification **[WAVE B — needs Tasks 4 and 5]**

**Files:**
- Create: `Licensing/LicenceVerifier.cs`
- Test: `tests/VibranceHud.Tests/LicenceVerifierTests.cs`

**Interfaces:**
- Consumes: `LicenceDocument` (Task 4), `LicenceSigner` (Task 5).
- Produces: `LicenceVerifier.TryVerify(string? envelopeJson, byte[] publicKey, out
  LicenceDocument? doc)` returning `bool`.

- [ ] **Step 1: Write the failing test**

Create `tests/VibranceHud.Tests/LicenceVerifierTests.cs`:

```csharp
using System;
using System.Text;
using VibranceHud.Licensing;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class LicenceVerifierTests
    {
        private static LicenceDocument Sample() => new(
            "2K7M-Q8XR-T9WD-N3FG", PlanCatalog.Monthly,
            new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc),
            "MXXBGGXAOCQP36SC");

        [Fact]
        public void GenuineLicenceVerifies()
        {
            LicenceSigner.CreateKeyPair(out var priv, out var pub);
            var envelope = LicenceSigner.Sign(Sample(), priv);

            Assert.True(LicenceVerifier.TryVerify(envelope, pub, out var doc));
            Assert.Equal(Sample(), doc);
        }

        /// <summary>The point of the whole design: a different private key cannot produce a
        /// licence this public key accepts.</summary>
        [Fact]
        public void LicenceSignedByAnotherKeyIsRejected()
        {
            LicenceSigner.CreateKeyPair(out var attackerPriv, out _);
            LicenceSigner.CreateKeyPair(out _, out var ourPub);

            var forged = LicenceSigner.Sign(Sample(), attackerPriv);

            Assert.False(LicenceVerifier.TryVerify(forged, ourPub, out var doc));
            Assert.Null(doc);
        }

        /// <summary>Editing the licence to extend it must invalidate the signature.</summary>
        [Fact]
        public void TamperedDocumentIsRejected()
        {
            LicenceSigner.CreateKeyPair(out var priv, out var pub);
            var envelope = LicenceSigner.Sign(Sample(), priv);

            var tampered = new LicenceDocument(
                Sample().Serial, Sample().Plan, Sample().IssuedUtc,
                Sample().ExpiresUtc.AddYears(10), Sample().HardwareId);
            var swapped = envelope.Replace(
                Convert.ToBase64String(Encoding.UTF8.GetBytes(Sample().ToCanonicalJson())),
                Convert.ToBase64String(Encoding.UTF8.GetBytes(tampered.ToCanonicalJson())));

            Assert.False(LicenceVerifier.TryVerify(swapped, pub, out _));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not json")]
        [InlineData("{}")]
        [InlineData("""{"doc":"","sig":""}""")]
        [InlineData("""{"doc":"!!!notbase64!!!","sig":"!!!"}""")]
        public void MalformedEnvelopeIsRejected(string? envelope)
        {
            LicenceSigner.CreateKeyPair(out _, out var pub);
            Assert.False(LicenceVerifier.TryVerify(envelope, pub, out var doc));
            Assert.Null(doc);
        }

        [Fact]
        public void MissingPublicKeyIsRejected()
        {
            LicenceSigner.CreateKeyPair(out var priv, out _);
            var envelope = LicenceSigner.Sign(Sample(), priv);

            Assert.False(LicenceVerifier.TryVerify(envelope, Array.Empty<byte>(), out _));
        }
    }
}
```

- [ ] **Step 2: Run the test and confirm it fails**

Run:
```bash
dotnet test tests/VibranceHud.Tests/VibranceHud.Tests.csproj --filter "FullyQualifiedName~LicenceVerifier"
```
Expected: build error `CS0246`.

- [ ] **Step 3: Write the implementation**

Create `Licensing/LicenceVerifier.cs`:

```csharp
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VibranceHud.Licensing
{
    /// <summary>
    /// Checks a signed licence. THE PUBLIC KEY SIDE - this is what ships in the app.
    ///
    /// It can confirm a licence came from the holder of the private key and has not been
    /// altered. It cannot create one. Someone who pulls the app apart and extracts everything
    /// in it gains the ability to verify licences, which is worth nothing to them.
    ///
    /// Every failure path returns false. Nothing here throws, and nothing partially succeeds:
    /// an unreadable, truncated or hand-edited licence is simply not a licence.
    /// </summary>
    public static class LicenceVerifier
    {
        public static bool TryVerify(string? envelopeJson, byte[] publicKey, out LicenceDocument? doc)
        {
            doc = null;
            if (string.IsNullOrWhiteSpace(envelopeJson)) return false;
            if (publicKey == null || publicKey.Length == 0) return false;

            try
            {
                var envelope = JsonSerializer.Deserialize<LicenceEnvelope>(envelopeJson);
                if (envelope == null) return false;
                if (string.IsNullOrEmpty(envelope.Doc) || string.IsNullOrEmpty(envelope.Sig))
                    return false;

                byte[] payload = Convert.FromBase64String(envelope.Doc);
                byte[] signature = Convert.FromBase64String(envelope.Sig);

                using var ec = ECDsa.Create();
                ec.ImportSubjectPublicKeyInfo(publicKey, out _);
                if (!ec.VerifyData(payload, signature, HashAlgorithmName.SHA256)) return false;

                // Only parse the contents once the signature is confirmed, so nothing
                // attacker-controlled is interpreted before it has been authenticated.
                return LicenceDocument.TryFromJson(Encoding.UTF8.GetString(payload), out doc);
            }
            catch
            {
                doc = null;
                return false;
            }
        }
    }
}
```

- [ ] **Step 4: Run the test and confirm it passes**

Run:
```bash
dotnet test tests/VibranceHud.Tests/VibranceHud.Tests.csproj --filter "FullyQualifiedName~LicenceVerifier"
```
Expected: `Passed! - Failed: 0`

- [ ] **Step 5: Commit**

```bash
git add Licensing/LicenceVerifier.cs tests/VibranceHud.Tests/LicenceVerifierTests.cs
git commit -m "feat(licensing): licence verification (public key side)

This is what ships in the app: it confirms a licence came from the private key holder and
wasn't altered, and it cannot create one. Tests cover the cases that matter - a licence
signed by a different key, and a document edited to extend its expiry - both rejected.
Contents are only parsed after the signature checks out, so nothing attacker-controlled
is interpreted before it's authenticated."
```

---

## Task 7: Redemption interface and local stand-in **[WAVE C — needs Tasks 1–6]**

**Files:**
- Create: `Licensing/ILicenceRedeemer.cs`
- Create: `Licensing/LocalRedeemer.cs`
- Test: `tests/VibranceHud.Tests/LocalRedeemerTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1–6.
- Produces: `sealed record RedeemResult(bool Ok, string? SignedLicence, string? Error)`;
  `interface ILicenceRedeemer { Task<RedeemResult> RedeemAsync(string keyCode, string
  hardwareId, CancellationToken ct); }`; `LocalRedeemer` implementing it.

- [ ] **Step 1: Write the failing test**

Create `tests/VibranceHud.Tests/LocalRedeemerTests.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using VibranceHud.Licensing;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class LocalRedeemerTests
    {
        private const string Hardware = "MXXBGGXAOCQP36SC";

        private static LocalRedeemer NewRedeemer(out byte[] publicKey)
        {
            LicenceSigner.CreateKeyPair(out var priv, out var pub);
            publicKey = pub;
            return new LocalRedeemer(priv, PlanCatalog.Monthly);
        }

        [Fact]
        public async Task RedeemingAValidKeyReturnsAVerifiableLicence()
        {
            var redeemer = NewRedeemer(out var pub);
            var result = await redeemer.RedeemAsync(KeyCode.Generate(), Hardware, CancellationToken.None);

            Assert.True(result.Ok, result.Error);
            Assert.True(LicenceVerifier.TryVerify(result.SignedLicence, pub, out var doc));
            Assert.Equal(Hardware, doc!.HardwareId);
            Assert.Equal(PlanCatalog.Monthly, doc.Plan);
        }

        [Fact]
        public async Task LicenceExpiryMatchesThePlanDuration()
        {
            var redeemer = NewRedeemer(out var pub);
            var result = await redeemer.RedeemAsync(KeyCode.Generate(), Hardware, CancellationToken.None);
            LicenceVerifier.TryVerify(result.SignedLicence, pub, out var doc);

            var expected = PlanCatalog.DurationFor(PlanCatalog.Monthly)!.Value;
            var actual = doc!.ExpiresUtc - doc.IssuedUtc;
            Assert.Equal(expected, actual);
        }

        /// <summary>A mistyped key must be refused before anything is issued.</summary>
        [Theory]
        [InlineData("")]
        [InlineData("NOT-A-REAL-KEY-XXXX")]
        [InlineData("2K7M-Q8XR-T9WD")]
        public async Task MalformedKeyIsRefused(string bad)
        {
            var redeemer = NewRedeemer(out _);
            var result = await redeemer.RedeemAsync(bad, Hardware, CancellationToken.None);

            Assert.False(result.Ok);
            Assert.Null(result.SignedLicence);
            Assert.NotNull(result.Error);
        }

        [Fact]
        public async Task MissingHardwareIdIsRefused()
        {
            var redeemer = NewRedeemer(out _);
            var result = await redeemer.RedeemAsync(KeyCode.Generate(), "", CancellationToken.None);

            Assert.False(result.Ok);
            Assert.NotNull(result.Error);
        }
    }
}
```

- [ ] **Step 2: Run the test and confirm it fails**

Run:
```bash
dotnet test tests/VibranceHud.Tests/VibranceHud.Tests.csproj --filter "FullyQualifiedName~LocalRedeemer"
```
Expected: build error `CS0246`.

- [ ] **Step 3: Write the interface**

Create `Licensing/ILicenceRedeemer.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace VibranceHud.Licensing
{
    /// <summary>Outcome of exchanging a key for a licence.</summary>
    /// <param name="Ok">True when a licence was issued.</param>
    /// <param name="SignedLicence">The signed envelope, or null when refused.</param>
    /// <param name="Error">A sentence the user can act on, or null on success.</param>
    public sealed record RedeemResult(bool Ok, string? SignedLicence, string? Error);

    /// <summary>
    /// Exchanges a key code for a signed licence. The one place that decides yes or no.
    ///
    /// Behind an interface because the real implementation talks to the activation service,
    /// which does not exist yet. Everything else about licensing can be built and tested
    /// against <see cref="LocalRedeemer"/> and will not change when the service arrives.
    ///
    /// The service implementation must, when it is written:
    ///   - issue a licence for an unused key and record key + hardware id;
    ///   - issue AGAIN for the same hardware id, so reinstalling never consumes a key;
    ///   - refuse a different hardware id - this is the anti-sharing rule;
    ///   - treat a released key as unused, so a customer who changes GPU is not locked out.
    /// </summary>
    public interface ILicenceRedeemer
    {
        Task<RedeemResult> RedeemAsync(string keyCode, string hardwareId, CancellationToken ct);
    }
}
```

- [ ] **Step 4: Write the local implementation**

Create `Licensing/LocalRedeemer.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace VibranceHud.Licensing
{
    /// <summary>
    /// Issues licences locally, for development and tests only.
    ///
    /// It cannot enforce one-key-one-PC - it has no memory of what other machines have
    /// redeemed, which is precisely why the real implementation needs a service. It exists so
    /// the rest of the licence system can be finished and tested before that service does.
    ///
    /// NEVER ship this in a release build: it holds a private key, so anything using it can
    /// mint unlimited licences.
    /// </summary>
    public sealed class LocalRedeemer : ILicenceRedeemer
    {
        private readonly byte[] _privateKey;
        private readonly string _plan;

        public LocalRedeemer(byte[] privateKey, string plan)
        {
            _privateKey = privateKey ?? throw new ArgumentNullException(nameof(privateKey));
            _plan = plan;
        }

        public Task<RedeemResult> RedeemAsync(string keyCode, string hardwareId, CancellationToken ct)
        {
            if (!KeyCode.IsWellFormed(keyCode))
                return Task.FromResult(new RedeemResult(false, null,
                    "That key doesn't look right. Check it and try again."));

            if (string.IsNullOrWhiteSpace(hardwareId))
                return Task.FromResult(new RedeemResult(false, null,
                    "Couldn't identify this PC. Try restarting PlexusX."));

            var duration = PlanCatalog.DurationFor(_plan);
            if (duration == null)
                return Task.FromResult(new RedeemResult(false, null, "Unknown plan."));

            var issued = DateTime.UtcNow;
            var doc = new LicenceDocument(
                KeyCode.Normalise(keyCode), _plan, issued, issued + duration.Value, hardwareId);

            return Task.FromResult(new RedeemResult(true, LicenceSigner.Sign(doc, _privateKey), null));
        }
    }
}
```

- [ ] **Step 5: Run the test and confirm it passes**

Run:
```bash
dotnet test tests/VibranceHud.Tests/VibranceHud.Tests.csproj --filter "FullyQualifiedName~LocalRedeemer"
```
Expected: `Passed! - Failed: 0`

- [ ] **Step 6: Run the whole suite**

Run:
```bash
dotnet test tests/VibranceHud.Tests/VibranceHud.Tests.csproj
```
Expected: `Failed: 0`. The beta licence tests must still pass — nothing under `License/`
was touched.

- [ ] **Step 7: Commit**

```bash
git add Licensing/ILicenceRedeemer.cs Licensing/LocalRedeemer.cs tests/VibranceHud.Tests/LocalRedeemerTests.cs
git commit -m "feat(licensing): redemption interface with a local stand-in

Redemption sits behind an interface because the activation service doesn't exist yet;
everything else about licensing can now be finished and tested without it. The service's
rules are written on the interface so they aren't rediscovered later - especially the two
that are easy to miss and expensive to get wrong: the same PC redeeming again must
succeed, and a released key must be reusable, or every reinstall and every GPU upgrade
becomes a support ticket.

LocalRedeemer is development-only and documented as such - it holds a private key and
cannot enforce one-key-one-PC."
```

---

## What is deliberately NOT in this plan

These need decisions or infrastructure that doesn't exist yet. Do not attempt them:

- **The activation service.** Waiting on the website. `ILicenceRedeemer` is the seam.
- **The three-plan purchase screen.** Needs the real checkout URLs.
- **Embedding the production public key.** The private key must be generated once, on the
  owner's machine, and never committed. Generating it as part of this work would put it in
  git history.
- **PlexusX Keys.** Separate application, separate plan.
- **Removing the beta licence system.** `License/` stays until 1.0 actually ships.

## Verification before this plan is considered done

Run:
```bash
dotnet test tests/VibranceHud.Tests/VibranceHud.Tests.csproj
```
Expected: `Failed: 0`, and the total passing count is higher than before by at least 40.

Then confirm the beta system is untouched:
```bash
git diff --name-only HEAD~7 -- License/
```
Expected: no output.
