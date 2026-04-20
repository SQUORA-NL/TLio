# Quickstart: Special Character Escaping

**Feature**: 009-escape-special-chars

---

## Value Escaping

When a `value` field in a TLio script would otherwise be interpreted as a path (`@`, `$`) or function call (`=`), prefix it with a doubled trigger character.

### Set a field to the literal string `@adminRole`

```json
[{ "command": "set", "path": "$.role", "value": "@@adminRole" }]
```

### Set a field to the literal string `$total`

```json
[{ "command": "set", "path": "$.label", "value": "$$total" }]
```

### Set a field to the literal string `=formula`

```json
[{ "command": "set", "path": "$.expr", "value": "==formula" }]
```

### Use a literal `@` inside a quoted string

```json
[{ "command": "set", "path": "$.email", "value": "'user@@example.com'" }]
```

---

## Path Escaping

When a property name in your data contains the path delimiter (`.` for JSON/YAML) or other special characters, use bracket notation.

### Access a JSON property named `"version.major"`

```json
[{ "command": "set", "path": "$['version.major']", "value": "2" }]
```

### Access a YAML property named `"server.host"`

```json
[{ "command": "set", "path": "$['server.host']", "value": "localhost" }]
```

---

## Escape-sequence quick reference

| Want to write | Script value |
|---------------|-------------|
| String starting with `@` | `@@…` |
| String starting with `$` | `$$…` |
| String starting with `=` | `==…` |
| String containing `@` | `'…@@…'` |
| Property name with `.` | `$['name.with.dots']` |
