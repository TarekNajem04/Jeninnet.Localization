# Localization parity gate (reusable action)

Runs [`jeninnet-localization-keys`](../../src/tooling/Jeninnet.Localization.Tools.Keys/README.md)
over catalog and source roots. Findings fail the job (`exit 1`); a clean tree passes (`exit 0`).
With `sarif-path` set (the default), the SARIF report is uploaded to code scanning —
this requires the calling workflow to grant `security-events: write`.

```yaml
jobs:
  parity:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      security-events: write
    steps:
      - uses: actions/checkout@v7.0.1
      - uses: ./path-to/Jeninnet.Localization/.github/actions/parity
        with:
          catalog-dirs: |
            Localization
          source-dirs: |
            src
            samples/MyApp
          tool-version: 1.0.0-preview.3
```

With a committed baseline, only new findings fail — adopt the gate incrementally:

```yaml
      - uses: ./path-to/Jeninnet.Localization/.github/actions/parity
        with:
          catalog-dirs: Localization
          source-dirs: src
          baseline-path: parity-baseline.json
```

Record a baseline with a one-off run (`write-baseline-path` always exits clean),
commit the file, then switch the gate to `baseline-path`. Fixed findings are
reported informationally and never fail the run.

Inputs mirror the tool flags (`--catalog`, `--source`, `--keys-file`, `--exclude`,
`--default-culture`, `--report`, `--sarif`, `--baseline`, `--write-baseline`); multiline
inputs take one entry per line. `tool-source` installs the tool from another feed
(for example a local feed of a just-packed tool). The action outputs `exit-code`
(`0` clean, `1` findings, `2` usage error).
