//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests.Fuzzing;

/// <summary>
/// Property-style tests for the message formatters: random templates and arguments
/// never throw except for the documented <see cref="FormatException"/> (ICU engine)
/// and <see cref="ArgumentNullException"/> (null inputs). The ICU skeleton subset is
/// covered with valid seeds plus hostile mutations. Deterministic: fixed seeds, the
/// invariant culture, no wall-clock.
/// </summary>
[TestClass]
public sealed class FormatterFuzzTests {
    private const ulong SEED_DEFAULT = 0x92A3B4C5D6E7F809ul;
    private const ulong SEED_ICU = 0xA3B4C5D6E7F8091Aul;
    private const ulong SEED_SKELETON = 0xB4C5D6E7F8091A2Bul;

    private const int TEMPLATE_CASES = 500;
    private const int SKELETON_CASES = 300;

    /// <summary>
    /// Verifies that default formatter random templates never throw.
    /// </summary>
    [TestMethod]
    public void DefaultFormatter_RandomTemplates_NeverThrow() {
        var rng = new FuzzRng(SEED_DEFAULT);
        var formatter = new DefaultMessageFormatter();
        for(var iteration = 0; iteration < TEMPLATE_CASES; iteration++) {
            var template = rng.NextTemplate();
            var parameters = NextParameters(rng);
            var result = formatter.Format(template, parameters);
            result.Should().NotBeNull();
        }
    }

    /// <summary>
    /// Verifies that default formatter mutated templates never throw.
    /// </summary>
    [TestMethod]
    public void DefaultFormatter_MutatedTemplates_NeverThrow() {
        var rng = new FuzzRng(SEED_DEFAULT);
        var formatter = new DefaultMessageFormatter();
        const string seed = "Hello {Name}, you have {Count} messages.";
        for(var iteration = 0; iteration < TEMPLATE_CASES; iteration++) {
            var template = rng.MutateText(seed);
            var parameters = NextParameters(rng);
            var result = formatter.Format(template, parameters);
            result.Should().NotBeNull();
        }
    }

    /// <summary>
    /// Verifies that default formatter null inputs throw argument null exception.
    /// </summary>
    [TestMethod]
    public void DefaultFormatter_NullInputs_ThrowArgumentNullException() {
        var formatter = new DefaultMessageFormatter();
        var parameters = new Dictionary<string, object?>();
        Action nullTemplate = () => formatter.Format(null!, parameters);
        Action nullParameters = () => formatter.Format("hi", null!);
        nullTemplate.Should().Throw<ArgumentNullException>();
        nullParameters.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that icu formatter random templates only format exception.
    /// </summary>
    [TestMethod]
    public void IcuFormatter_RandomTemplates_OnlyFormatException() {
        var rng = new FuzzRng(SEED_ICU);
        var formatter = new IcuMessageFormatter();
        for(var iteration = 0; iteration < TEMPLATE_CASES; iteration++) {
            var template = rng.Next(2) == 0 ? rng.NextTemplate() : MutateSeedTemplate(rng);
            var parameters = NextIcuParameters(rng);
            try {
                var result = formatter.Format(template, parameters, CultureInfo.InvariantCulture);
                result.Should().NotBeNull();
            }
            catch(Exception exception) {
                (exception is FormatException).Should().BeTrue($"ICU formatting must throw only FormatException, not {exception.GetType().Name}: {exception.Message}");
            }
        }
    }

    /// <summary>
    /// Verifies that icu formatter mutated seeds only format exception.
    /// </summary>
    [TestMethod]
    public void IcuFormatter_MutatedSeeds_OnlyFormatException() {
        var rng = new FuzzRng(SEED_ICU);
        var formatter = new IcuMessageFormatter();
        foreach(var seed in IcuSeeds()) {
            for(var iteration = 0; iteration < 60; iteration++) {
                var template = rng.MutateText(seed);
                var parameters = NextIcuParameters(rng);
                try {
                    var result = formatter.Format(template, parameters, CultureInfo.InvariantCulture);
                    result.Should().NotBeNull();
                }
                catch(Exception exception) {
                    (exception is FormatException).Should().BeTrue($"ICU seed '{seed}' mutated must throw only FormatException, not {exception.GetType().Name}: {exception.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Verifies that icu formatter skeletons only format exception.
    /// </summary>
    [TestMethod]
    public void IcuFormatter_Skeletons_OnlyFormatException() {
        var rng = new FuzzRng(SEED_SKELETON);
        var formatter = new IcuMessageFormatter();
        var parameters = new Dictionary<string, object?> {
            ["v"] = 1234.5,
            ["d"] = new DateTime(2026, 5, 4, 13, 30, 0, DateTimeKind.Utc),
            ["t"] = new DateTime(2026, 5, 4, 13, 30, 0, DateTimeKind.Utc),
        };
        for(var iteration = 0; iteration < SKELETON_CASES; iteration++) {
            var template = NextSkeletonTemplate(rng);
            try {
                var result = formatter.Format(template, parameters, CultureInfo.InvariantCulture);
                result.Should().NotBeNull();
            }
            catch(Exception exception) {
                (exception is FormatException).Should().BeTrue($"ICU skeletons must throw only FormatException, not {exception.GetType().Name}: {exception.Message}");
            }
        }
    }

    /// <summary>
    /// Verifies that icu formatter null inputs throw argument null exception.
    /// </summary>
    [TestMethod]
    public void IcuFormatter_NullInputs_ThrowArgumentNullException() {
        var formatter = new IcuMessageFormatter();
        var parameters = new Dictionary<string, object?>();
        Action nullTemplate = () => formatter.Format(null!, parameters, CultureInfo.InvariantCulture);
        Action nullParameters = () => formatter.Format("hi", null!, CultureInfo.InvariantCulture);
        Action nullCulture = () => formatter.Format("hi", parameters, null!);
        nullTemplate.Should().Throw<ArgumentNullException>();
        nullParameters.Should().Throw<ArgumentNullException>();
        nullCulture.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Draws ICU seeds.
    /// </summary>
    /// <returns>The seed templates.</returns>
    private static IReadOnlyList<string> IcuSeeds() => [
        "{count, plural, one{# book} other{# books}}",
        "{gender, select, male{He} female{She} other{They}}",
        "{v, number, integer}",
        "{v, number, currency}",
        "{v, number, percent}",
        "{v, number, ::percent}",
        "{v, number, ::currency/USD}",
        "{v, number, ::.00}",
        "{d, date, short}",
        "{t, time, short}",
        "{d, date, ::yMd}",
        "{t, time, ::Hm}",
        "{n, selectordinal, one{#st} two{#nd} other{#th}}",
        "'{not an argument}' '' {v}",
        "{count, plural, offset:1 one{# book} other{# books}}",
        "{count, plural, =0{none} one{# book} other{# books}}",
    ];

    /// <summary>
    /// Mutates a seed template.
    /// </summary>
    /// <param name="rng">The RNG.</param>
    /// <returns>The mutated template.</returns>
    private static string MutateSeedTemplate(FuzzRng rng) => rng.MutateText(PickSeed(rng));

    /// <summary>
    /// Picks a seed.
    /// </summary>
    /// <param name="rng">The RNG.</param>
    /// <returns>The seed template.</returns>
    private static string PickSeed(FuzzRng rng) {
        var seeds = IcuSeeds();
        return seeds[rng.Next(seeds.Count)];
    }

    /// <summary>
    /// Draws the next skeleton template.
    /// </summary>
    /// <param name="rng">The RNG.</param>
    /// <returns>The template.</returns>
    private static string NextSkeletonTemplate(FuzzRng rng) {
        const string fragments = "percentcurrency/USD.00yMdHmsazSK#/ -_+";
        var length = rng.Next(0, 24);
        var builder = new StringBuilder(length + 16);
        builder.Append(rng.Next(3) switch {
            0 => "{v, number, ::",
            1 => "{d, date, ::",
            _ => "{t, time, ::",
        });
        for(var index = 0; index < length; index++) {
            builder.Append(fragments[rng.Next(fragments.Length)]);
        }

        builder.Append('}');
        return builder.ToString();
    }

    /// <summary>
    /// Draws the next parameters.
    /// </summary>
    /// <param name="rng">The RNG.</param>
    /// <returns>The parameters.</returns>
    private static Dictionary<string, object?> NextParameters(FuzzRng rng) {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        var count = rng.Next(0, 5);
        for(var index = 0; index < count; index++) {
            parameters[rng.NextKey()] = rng.Next(3) switch {
                0 => null,
                1 => (object?)rng.Next(1000000),
                _ => rng.NextTemplate(),
            };
        }

        return parameters;
    }

    /// <summary>
    /// Draws the next ICU parameters.
    /// </summary>
    /// <param name="rng">The RNG.</param>
    /// <returns>The parameters.</returns>
    private static Dictionary<string, object?> NextIcuParameters(FuzzRng rng) => new(StringComparer.Ordinal) {
        ["count"] = rng.Next(-5, 10),
        ["n"] = rng.Next(-5, 10),
        ["v"] = rng.Next(4) switch {
            0 => (object?)rng.Next(-10000, 10000),
            1 => (object?)rng.NextDouble(),
            2 => null,
            _ => rng.NextTemplate(),
        },
        ["gender"] = rng.Next(3) switch {
            0 => (object?)"male",
            1 => (object?)"female",
            _ => rng.NextKey(),
        },
        ["d"] = (object?)new DateTime(2026, 5, 4, 13, 30, 0, DateTimeKind.Utc),
        ["t"] = (object?)new DateTime(2026, 5, 4, 13, 30, 0, DateTimeKind.Utc),
        ["Name"] = (object?)rng.NextKey(),
    };
}

/// <summary>
/// Deterministic double helpers for the formatter suite.
/// </summary>
internal static class FuzzRngDoubleExtensions {
    /// <summary>
    /// Draws a double in <c>[0, 1)</c>.
    /// </summary>
    /// <param name="rng">The RNG.</param>
    /// <returns>The drawn value.</returns>
    internal static double NextDouble(this FuzzRng rng) => (rng.NextUInt64() >> 11) * (1.0 / (1ul << 53));
}
