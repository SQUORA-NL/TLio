using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.ETL.Commands;
using TLio.Extensions.ETL.Commands.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests.ETLTests;

/// <summary>
/// Tests for the Resolve command.
/// Ported from JLio.UnitTests.CommandsTests.ETLTests.ResolveTests.
/// Uses direct command instantiation.
/// </summary>
[TestFixture]
public class ResolveTests
{
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Test]
    public void Resolve_Validation_EmptyPath_Fails()
    {
        var cmd = new Resolve<JToken> { Path = "" };
        Assert.That(cmd.Execute(JToken.Parse("{}"), context).Success, Is.False);
    }

    [Test]
    public void Resolve_Validation_NoResolveSettings_Fails()
    {
        var cmd = new Resolve<JToken> { Path = "$.items[*]" };
        Assert.That(cmd.Execute(JToken.Parse(@"{ ""items"": [] }"), context).Success, Is.False);
    }

    [Test]
    public void Resolve_Validation_EmptyResolveKeys_Fails()
    {
        var cmd = new Resolve<JToken>
        {
            Path = "$.items[*]",
            ResolveSettings = new List<ResolveSetting<JToken>>
            {
                new ResolveSetting<JToken>
                {
                    ReferencesCollectionPath = "$.refs[*]",
                    ResolveKeys = new List<ResolveKey>(),   // empty
                    Values = new List<ResolveValue<JToken>>
                    {
                        new ResolveValue<JToken> { TargetPath = "@.x", Value = new FixedValue<JToken>(JToken.Parse("1")) }
                    }
                }
            }
        };
        Assert.That(cmd.Execute(JToken.Parse(@"{ ""items"": [], ""refs"": [] }"), context).Success, Is.False);
    }

    // ── Match behaviour ───────────────────────────────────────────────────────

    [Test]
    public void Resolve_KeyMatch_SetsValueAtTargetPath()
    {
        var data = JToken.Parse(@"{
            ""orders"":   [{ ""productId"": 1, ""qty"": 2 }],
            ""products"": [{ ""id"": 1, ""name"": ""Widget"" }]
        }");

        var cmd = new Resolve<JToken>
        {
            Path = "$.orders[*]",
            ResolveSettings = new List<ResolveSetting<JToken>>
            {
                new ResolveSetting<JToken>
                {
                    ReferencesCollectionPath = "$.products[*]",
                    ResolveKeys = new List<ResolveKey>
                    {
                        new ResolveKey { KeyPath = "@.productId", ReferenceKeyPath = "@.id" }
                    },
                    Values = new List<ResolveValue<JToken>>
                    {
                        new ResolveValue<JToken>
                        {
                            TargetPath = "@.productName",
                            Value = new FixedValue<JToken>(JToken.Parse(@"""Widget"""))
                        }
                    }
                }
            }
        };

        var result = cmd.Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data["orders"]![0]!["productName"]?.Value<string>(), Is.EqualTo("Widget"));
    }

    [Test]
    public void Resolve_NoMatch_DoesNotSetProperty()
    {
        var data = JToken.Parse(@"{
            ""orders"":   [{ ""productId"": 99, ""qty"": 1 }],
            ""products"": [{ ""id"": 1, ""name"": ""Widget"" }]
        }");

        var cmd = new Resolve<JToken>
        {
            Path = "$.orders[*]",
            ResolveSettings = new List<ResolveSetting<JToken>>
            {
                new ResolveSetting<JToken>
                {
                    ReferencesCollectionPath = "$.products[*]",
                    ResolveKeys = new List<ResolveKey>
                    {
                        new ResolveKey { KeyPath = "@.productId", ReferenceKeyPath = "@.id" }
                    },
                    Values = new List<ResolveValue<JToken>>
                    {
                        new ResolveValue<JToken>
                        {
                            TargetPath = "@.productName",
                            Value = new FixedValue<JToken>(JToken.Parse(@"""Widget"""))
                        }
                    }
                }
            }
        };

        var result = cmd.Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That((data["orders"]![0] as JObject)!.ContainsKey("productName"), Is.False);
    }

    [Test]
    public void Resolve_MissingReferenceCollection_LogsWarning_Succeeds()
    {
        var data = JToken.Parse(@"{ ""orders"": [{ ""productId"": 1 }] }");

        var cmd = new Resolve<JToken>
        {
            Path = "$.orders[*]",
            ResolveSettings = new List<ResolveSetting<JToken>>
            {
                new ResolveSetting<JToken>
                {
                    ReferencesCollectionPath = "$.nonexistent[*]",
                    ResolveKeys = new List<ResolveKey>
                    {
                        new ResolveKey { KeyPath = "@.productId", ReferenceKeyPath = "@.id" }
                    },
                    Values = new List<ResolveValue<JToken>>
                    {
                        new ResolveValue<JToken>
                        {
                            TargetPath = "@.productName",
                            Value = new FixedValue<JToken>(JToken.Parse(@"""Widget"""))
                        }
                    }
                }
            }
        };

        // No reference collection found → logs warning but does not fail
        var result = cmd.Execute(data, context);
        Assert.That(result.Success, Is.True);
    }

    // ── Article X: logging assertions ────────────────────────────────────────

    [Test]
    public void Resolve_Success_LogsInfoEntry()
    {
        var data = JToken.Parse(@"{
            ""orders"":   [{ ""productId"": 1, ""qty"": 2 }],
            ""products"": [{ ""id"": 1, ""name"": ""Widget"" }]
        }");

        var cmd = new Resolve<JToken>
        {
            Path = "$.orders[*]",
            ResolveSettings = new List<ResolveSetting<JToken>>
            {
                new ResolveSetting<JToken>
                {
                    ReferencesCollectionPath = "$.products[*]",
                    ResolveKeys = new List<ResolveKey>
                    {
                        new ResolveKey { KeyPath = "@.productId", ReferenceKeyPath = "@.id" }
                    },
                    Values = new List<ResolveValue<JToken>>
                    {
                        new ResolveValue<JToken>
                        {
                            TargetPath = "@.productName",
                            Value = new FixedValue<JToken>(JToken.Parse(@"""Widget"""))
                        }
                    }
                }
            }
        };

        var result = cmd.Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Information), Is.True);
    }
}
