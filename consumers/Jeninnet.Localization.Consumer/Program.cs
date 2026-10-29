//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Platform/
// Licensed under the MIT License.
//

// End-to-end run of the consumer pipeline against the packed packages (package validation).
var result = await CheckoutScenario.RunAsync(AppContext.BaseDirectory);

await Console.Out.WriteLineAsync(result.Thanks);
await Console.Out.WriteLineAsync(result.Total);
await Console.Out.WriteLineAsync(result.ArabicThanks);
await Console.Out.WriteLineAsync("CSV:");
await Console.Out.WriteLineAsync(result.Csv);

return 0;
