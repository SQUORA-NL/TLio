# Quickstart: FormatConverter

## What It Does

FormatConverter lets a single TLio script pipeline data across multiple formats. A `convert` command in the script signals a format boundary; the runner handles all conversion transparently — no explicit `ToIM`/`FromIM` calls needed.

## Project References

Reference only the adapter projects for the formats you use:

```xml
<ItemGroup>
  <ProjectReference Include="path/to/FormatConverter.Core/FormatConverter.Core.csproj" />
  <ProjectReference Include="path/to/FormatConverter.Json/FormatConverter.Json.csproj" />
  <ProjectReference Include="path/to/FormatConverter.Xml/FormatConverter.Xml.csproj" />
  <ProjectReference Include="path/to/FormatConverter.Yaml/FormatConverter.Yaml.csproj" />
  <!-- For TLio script integration: -->
  <ProjectReference Include="path/to/FormatConverter.TLio/FormatConverter.TLio.csproj" />
</ItemGroup>
```

## Step 1: Register Adapters

```csharp
using FormatConverter.Core;
using FormatConverter.Json;
using FormatConverter.Xml;
using FormatConverter.Yaml;
using FormatConverter.TLio;

// Create the converter and register adapters you want to use
var converter = new FormatConverter();
converter.Register(new JsonFormatAdapter());
converter.Register(new XmlFormatAdapter());
converter.Register(new YamlFormatAdapter());

// Create the multi-format runner (follows TLio registration conventions)
var runner = new MultiFormatScriptRunner(converter);
```

## Step 2: Write a Multi-Format Script

A TLio script can now include `convert` commands to switch formats mid-execution:

```json
[
  { "command": "set", "path": "/person/name", "value": "Alice" },
  {
    "command": "convert",
    "to": "json",
    "settings": { "textProperty": "#text", "attributePrefix": "@" }
  },
  { "command": "set", "path": "$.person.age", "value": 30 },
  { "command": "convert", "to": "yaml" },
  { "command": "set", "path": "person.active", "value": true }
]
```

## Step 3: Execute the Pipeline

```csharp
string xmlInput = "<person><name>Original</name></person>";
string yamlOutput = runner.Execute("xml", xmlInput, script);
// Result is valid YAML with all three modifications applied
```

## Step 4: Single-Step Conversion (No Script)

For simple format conversion without TLio commands:

```csharp
// Two-step (inspect IM between conversions)
IntermediateNode im  = converter.ToIM("json", jsonString, ConversionSettings.Empty);
string xmlOutput     = converter.FromIM("xml", im, new ConversionSettings { AttributePrefix = "@" });

// One-step convenience
string xmlOutput     = converter.Convert("json", jsonString, "xml", ConversionSettings.Empty);
```

## Step 5: Custom Settings Per Boundary

Override any convention per `convert` command — settings scope to that step only:

```json
{ "command": "convert", "to": "xml", "settings": { "inferTypes": true, "attributePrefix": "@" } }
```

## Step 6: Swap an Adapter

```csharp
// Replace with a custom licensed implementation — zero other changes needed
converter.Register(new MyLicensedXmlAdapter(licenseKey));
```

## Validation Checklist

```sh
# Build
dotnet build FormatConverter.sln

# Test (all green)
dotnet test FormatConverter.sln

# Verify no Newtonsoft dependency (must return zero matches)
grep -r "Newtonsoft" FormatConverter/ --include="*.csproj"
```
