//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the translation memory tests.
/// </summary>
[TestClass]
public class TranslationMemoryTests {
    private static readonly CultureInfo _french = CultureInfo.GetCultureInfo("fr");
    private static readonly CultureInfo _german = CultureInfo.GetCultureInfo("de");

    /// <summary>
    /// Verifies that try lookup added entry returns target.
    /// </summary>
    [TestMethod]
    public void TryLookup_AddedEntry_ReturnsTarget() {
        var memory = new InMemoryTranslationMemory();
        memory.Add("Hello world", _french, "Bonjour le monde");

        var found = memory.TryLookup("Hello world", _french, out var target);

        found.Should().BeTrue();
        target.Should().Be("Bonjour le monde");
    }

    /// <summary>
    /// Verifies that try lookup unknown source returns false.
    /// </summary>
    [TestMethod]
    public void TryLookup_UnknownSource_ReturnsFalse() {
        var memory = new InMemoryTranslationMemory();
        memory.Add("Hello world", _french, "Bonjour le monde");

        var found = memory.TryLookup("Goodbye world", _french, out var target);

        found.Should().BeFalse();
        target.Should().BeNull();
    }

    /// <summary>
    /// Verifies that try lookup different culture returns false.
    /// </summary>
    [TestMethod]
    public void TryLookup_DifferentCulture_ReturnsFalse() {
        var memory = new InMemoryTranslationMemory();
        memory.Add("Hello world", _french, "Bonjour le monde");

        memory.TryLookup("Hello world", _german, out _).Should().BeFalse("entries are keyed by culture and exact source text");
    }

    /// <summary>
    /// Verifies that try lookup exact match only case differs returns false.
    /// </summary>
    [TestMethod]
    public void TryLookup_ExactMatchOnly_CaseDiffers_ReturnsFalse() {
        var memory = new InMemoryTranslationMemory();
        memory.Add("Hello world", _french, "Bonjour le monde");

        memory.TryLookup("hello world", _french, out _).Should().BeFalse("translation memory is exact-match; no case-insensitive or fuzzy matching");
    }

    /// <summary>
    /// Verifies that add existing entry replaces target.
    /// </summary>
    [TestMethod]
    public void Add_ExistingEntry_ReplacesTarget() {
        var memory = new InMemoryTranslationMemory();
        memory.Add("Hello world", _french, "Bonjour le monde");
        memory.Add("Hello world", _french, "Salut le monde");

        memory.TryLookup("Hello world", _french, out var target).Should().BeTrue();
        target.Should().Be("Salut le monde");
    }

    /// <summary>
    /// Verifies that members invalid arguments throw.
    /// </summary>
    [TestMethod]
    public void Members_InvalidArguments_Throw() {
        var memory = new InMemoryTranslationMemory();

        var nullSource = () => memory.Add(null!, _french, "target");
        nullSource.Should().Throw<ArgumentException>();

        var emptySource = () => memory.Add("  ", _french, "target");
        emptySource.Should().Throw<ArgumentException>();

        var nullCulture = () => memory.Add("source", null!, "target");
        nullCulture.Should().Throw<ArgumentNullException>();

        var nullTarget = () => memory.Add("source", _french, null!);
        nullTarget.Should().Throw<ArgumentNullException>();

        var lookupNullSource = () => memory.TryLookup(null!, _french, out _);
        lookupNullSource.Should().Throw<ArgumentException>();

        var lookupNullCulture = () => memory.TryLookup("source", null!, out _);
        lookupNullCulture.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that try lookup large source text matches exact hash key.
    /// </summary>
    [TestMethod]
    public void TryLookup_LargeSourceText_MatchesExactHashKey() {
        var source = string.Concat(Enumerable.Repeat("localization ", 500));
        var memory = new InMemoryTranslationMemory();
        memory.Add(source, _french, "localisation");

        memory.TryLookup(source, _french, out var target).Should().BeTrue();
        target.Should().Be("localisation");

        var mutated = source + "!";
        memory.TryLookup(mutated, _french, out _).Should().BeFalse();
    }
}
