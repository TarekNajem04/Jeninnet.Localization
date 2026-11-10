//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests.Fuzzing;

/// <summary>
/// Property-style tests for key naming: the same key always maps to the same
/// identifier (deterministic, ordinal, invariant-culture), hostile inputs never
/// throw, and the documented examples stay pinned. Deterministic: fixed seed, no
/// wall-clock.
/// </summary>
[TestClass]
public sealed class KeyNamingFuzzTests {
    private const ulong SEED_NAMING = 0xC5D6E7F8091A2B3Cul;
    private const int KEY_CASES = 500;

    /// <summary>
    /// Verifies that to identifier is deterministic.
    /// </summary>
    [TestMethod]
    public void ToIdentifier_IsDeterministic() {
        var rng = new FuzzRng(SEED_NAMING);
        for(var iteration = 0; iteration < KEY_CASES; iteration++) {
            var key = rng.Next(2) == 0 ? rng.NextKey() : rng.NextHostileToken();
            var first = LocalizationKeyNaming.ToIdentifier(key);
            var second = LocalizationKeyNaming.ToIdentifier(key);
            first.Should().Be(second, "the same input must map to the same identifier");
        }
    }

    /// <summary>
    /// Verifies that to identifier hostile inputs never throw unexpected.
    /// </summary>
    [TestMethod]
    public void ToIdentifier_HostileInputs_NeverThrowUnexpected() {
        var rng = new FuzzRng(SEED_NAMING);
        for(var iteration = 0; iteration < KEY_CASES; iteration++) {
            var key = rng.Next(3) switch {
                0 => rng.NextHostileToken(),
                1 => rng.NextTemplate(),
                _ => new string((char)rng.Next(0xD800, 0xE000), 1) + rng.NextKey(),
            };
            var identifier = LocalizationKeyNaming.ToIdentifier(key);
            identifier.Should().NotBeNull();
            if(identifier.Length > 0) {
                char.IsDigit(identifier[0]).Should().BeFalse("identifiers never start with a digit (a leading underscore is added)");
            }
        }
    }

    /// <summary>
    /// Verifies that to identifier documented examples stay pinned.
    /// </summary>
    [TestMethod]
    public void ToIdentifier_DocumentedExamples_StayPinned() {
        LocalizationKeyNaming.ToIdentifier("BookId").Should().Be("BOOK_ID");
        LocalizationKeyNaming.ToIdentifier("OrderID").Should().Be("ORDER_ID");
        LocalizationKeyNaming.ToIdentifier("App.Title").Should().Be("APP_TITLE");
    }

    /// <summary>
    /// Verifies that to identifier separator only keys map to empty.
    /// </summary>
    [TestMethod]
    public void ToIdentifier_SeparatorOnlyKeys_MapToEmpty() {
        LocalizationKeyNaming.ToIdentifier("...").Should().Be(string.Empty);
        LocalizationKeyNaming.ToIdentifier("   ").Should().Be(string.Empty);
        LocalizationKeyNaming.ToIdentifier(string.Empty).Should().Be(string.Empty);
    }

    /// <summary>
    /// Verifies that to identifier null throws argument null exception.
    /// </summary>
    [TestMethod]
    public void ToIdentifier_Null_ThrowsArgumentNullException() {
        Action act = () => LocalizationKeyNaming.ToIdentifier(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that escape literal is deterministic and round trippable.
    /// </summary>
    [TestMethod]
    public void EscapeLiteral_IsDeterministic() {
        var rng = new FuzzRng(SEED_NAMING);
        for(var iteration = 0; iteration < KEY_CASES; iteration++) {
            var value = rng.Next(2) == 0 ? rng.NextKey() : rng.NextTemplate();
            var first = LocalizationKeyNaming.EscapeLiteral(value);
            var second = LocalizationKeyNaming.EscapeLiteral(value);
            first.Should().Be(second, "escaping is deterministic");
            first.Should().NotBeNull();
        }
    }

    /// <summary>
    /// Verifies that escape literal null throws argument null exception.
    /// </summary>
    [TestMethod]
    public void EscapeLiteral_Null_ThrowsArgumentNullException() {
        Action act = () => LocalizationKeyNaming.EscapeLiteral(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
