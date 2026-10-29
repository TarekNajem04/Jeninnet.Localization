//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Pseudo;

/// <summary>
/// Deterministic pseudo-localization transform: accents Latin
/// letters through a fixed ordinal map, copies <c>{…}</c> placeholder spans
/// verbatim (balanced braces, so nested ICU (International Components for Unicode) spans survive), wraps the result in
/// <c>[!! … !!]</c> markers, and pads with <c>~</c> to the expansion target. Pure
/// function of (template, percent): no timestamps, no randomness, no host-culture
/// dependence — byte-identical across machines and rebuilds.
/// </summary>
internal static class PseudoTemplateTransformer {
    private static readonly Dictionary<char, char> _accents = new() {
        ['a'] = '\u00E0',
        ['b'] = '\u0180',
        ['c'] = '\u00E7',
        ['d'] = '\u00F0',
        ['e'] = '\u00EB',
        ['f'] = '\u0192',
        ['g'] = '\u011D',
        ['h'] = '\u0125',
        ['i'] = '\u00EF',
        ['j'] = '\u0135',
        ['k'] = '\u0137',
        ['l'] = '\u0142',
        ['m'] = '\u1E41',
        ['n'] = '\u00F1',
        ['o'] = '\u00F6',
        ['p'] = '\u00FE',
        ['q'] = '\u02A0',
        ['r'] = '\u0159',
        ['s'] = '\u0161',
        ['t'] = '\u0165',
        ['u'] = '\u00FC',
        ['v'] = '\u1E7D',
        ['w'] = '\u0175',
        ['x'] = '\u1E8B',
        ['y'] = '\u00FF',
        ['z'] = '\u017E',
        ['A'] = '\u00C0',
        ['B'] = '\u0243',
        ['C'] = '\u00C7',
        ['D'] = '\u00D0',
        ['E'] = '\u00CB',
        ['F'] = '\u0191',
        ['G'] = '\u011C',
        ['H'] = '\u0124',
        ['I'] = '\u00CF',
        ['J'] = '\u0134',
        ['K'] = '\u0136',
        ['L'] = '\u0141',
        ['M'] = '\u1E40',
        ['N'] = '\u00D1',
        ['O'] = '\u00D6',
        ['P'] = '\u00DE',
        ['Q'] = '\u01A2',
        ['R'] = '\u0158',
        ['S'] = '\u0160',
        ['T'] = '\u0164',
        ['U'] = '\u00DC',
        ['V'] = '\u1E7C',
        ['W'] = '\u0174',
        ['X'] = '\u1E8A',
        ['Y'] = '\u0178',
        ['Z'] = '\u017D',
    };

    /// <summary>
    /// Transforms one source template.
    /// </summary>
    /// <param name="template">The source template.</param>
    /// <param name="expansionPercent">The padding percentage (0–100).</param>
    /// <returns>The pseudo-localized template.</returns>
    internal static string Transform(string template, int expansionPercent) {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentOutOfRangeException.ThrowIfLessThan(expansionPercent, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(expansionPercent, 100);

        var body = new StringBuilder(template.Length);
        var index = 0;
        while(index < template.Length) {
            if(template[index] != '{') {
                body.Append(_accents.TryGetValue(template[index], out var accented) ? accented : template[index]);
                index++;
                continue;
            }

            var start = index;
            index = SkipBalancedBraces(template, index);
            body.Append(template, start, index - start);
        }

        var padding = ((body.Length * expansionPercent) + 99) / 100;
        return "[!! " + body + new string('~', padding) + " !!]";
    }

    /// <summary>
    /// Skips one balanced <c>{…}</c> span (nested braces included).
    /// </summary>
    /// <param name="template">The source template.</param>
    /// <param name="start">The index of the opening brace.</param>
    /// <returns>The index just past the balanced span, or past the end when unbalanced.</returns>
    private static int SkipBalancedBraces(string template, int start) {
        var index = start;
        var depth = 0;
        while(index < template.Length) {
            if(template[index] == '{') {
                depth++;
            } else if(template[index] == '}' && --depth == 0) {
                return index + 1;
            }

            index++;
        }

        return index;
    }
}
