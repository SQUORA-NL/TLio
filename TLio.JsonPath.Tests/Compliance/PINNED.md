# Vendored: JSONPath Compliance Test Suite

`cts.json` is a verbatim copy of `cts.json` from
<https://github.com/jsonpath-standard/jsonpath-compliance-test-suite>, pinned to commit

```
9d1a415a53f5dfb291bc874823892e49174e38eb
```

It is vendored (not fetched) so the test run is offline and reproducible. To update it, replace the
file with the one at a newer commit, change the SHA above, and read the diff in the test results.

`Rfc9535ComplianceTests` runs every entry against the `Rfc9535` dialect and requires all of them to
pass. Entries that cannot be required to pass are listed in `Rfc9535ComplianceTests.KnownExceptions`
with a justification; the target is that this list is empty.
