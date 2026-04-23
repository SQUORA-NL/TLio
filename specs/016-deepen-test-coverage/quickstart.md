# Quickstart: Deepen Test Coverage

## Core Patterns

### Function result access

```csharp
// For bool results (e.g. StartsWith, EndsWith, Contains, IsEmpty)
var result = fn.Execute(data, data, context);
Assert.That(result.Success, Is.True);
Assert.That(result.Data.First!.Value<bool>(), Is.True);

// For string results
Assert.That(result.Data[0].ToObject<string>(), Is.EqualTo("expected"));

// For int results
Assert.That(result.Data[0].ToObject<int>(), Is.EqualTo(3));
```

### Logging assertions

```csharp
using Microsoft.Extensions.Logging;

// Assert a warning was logged
Assert.That(context.GetLogEntries().Any(e =>
    e.Level == LogLevel.Warning && e.Message.Contains("argument")), Is.True);

// Assert an info entry was logged on success
Assert.That(context.GetLogEntries().Any(e =>
    e.Level == LogLevel.Information), Is.True);

// Assert an error was logged
Assert.That(context.GetLogEntries().Any(e =>
    e.Level == LogLevel.Error), Is.True);
```

### Standard test setup

```csharp
[SetUp]
public void Setup()
{
    context = JsonExecutionContext.CreateDefault();
    data = JToken.Parse(@"{ ""str"": ""Hello"", ""num"": 42 }");
}
```

### Argument setup for function tests

```csharp
// PathValue — resolves against the data document
fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.str") });

// FixedValue — literal value (string, number, bool)
fn.SetArguments(new Arguments<JToken>
{
    new PathValue<JToken>("$.str"),
    new FixedValue<JToken>(new JValue(0)),     // start index
    new FixedValue<JToken>(new JValue(5))      // length
});
```

## Text Function Test Template

```csharp
[Test]
public void ToString_IntegerValue_ReturnsStringRepresentation()
{
    var data = JToken.Parse(@"{ ""n"": 42 }");
    var fn = new ToStringFunction<JToken>();
    fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.n") });

    var result = fn.Execute(data, data, context);

    Assert.That(result.Success, Is.True);
    Assert.That(result.Data[0].ToObject<string>(), Is.EqualTo("42"));
    Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Information), Is.True);
}
```

## Math Edge Case Template

```csharp
[Test]
public void Sum_EmptyArray_ReturnsZero()
{
    var data = JToken.Parse(@"{ ""nums"": [] }");
    var fn = new Sum<JToken>();
    fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nums[*]") });

    var result = fn.Execute(data, data, context);

    Assert.That(result.Success, Is.True);
    Assert.That(result.Data[0].ToObject<double>(), Is.EqualTo(0));
}
```

## ToCsv Test Template

```csharp
[Test]
public void ToCsv_NullField_ProducesEmptyCell()
{
    var data = JObject.Parse(@"{ ""rows"": [{""a"":""val"",""b"":null}] }");
    var cmd = new ToCsv<JToken>
    {
        Path = "$.rows",
        CsvSettings = new CsvSettings { IncludeHeaders = true }
    };
    var result = cmd.Execute(data, context);
    Assert.That(result.Success, Is.True);
    // output should have "a,b\r\nval,\r\n" — null renders as empty cell
}
```
