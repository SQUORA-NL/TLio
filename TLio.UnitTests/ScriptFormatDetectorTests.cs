using NUnit.Framework;
using TLio.Core.Models;

namespace TLio.UnitTests;

/// <summary>
/// The notation a script is written in is normally detected from its text, so these cases are
/// what decides which parser a caller's script reaches.
/// </summary>
[TestFixture]
public class ScriptFormatDetectorTests
{
    [TestCase("[]")]
    [TestCase("  \n [ { \"command\": \"set\" } ]")]
    [TestCase("{\"command\":\"set\"}")]
    public void Detect_BracketOrBrace_IsJson(string text) =>
        Assert.That(ScriptFormatDetector.Detect(text), Is.EqualTo(ScriptFormat.Json));

    [TestCase("<script/>")]
    [TestCase("\n  <script><set path=\"$.a\">x</set></script>")]
    [TestCase("<?xml version=\"1.0\"?><script/>")]
    [TestCase("<!-- a note --><script/>")]
    public void Detect_OpeningTag_IsXml(string text) =>
        Assert.That(ScriptFormatDetector.Detect(text), Is.EqualTo(ScriptFormat.Xml));

    [TestCase("- command: set\n  path: $.a\n")]
    [TestCase("---\n- command: set\n")]
    [TestCase("# a comment\n- command: set\n")]
    public void Detect_Otherwise_IsYaml(string text) =>
        Assert.That(ScriptFormatDetector.Detect(text), Is.EqualTo(ScriptFormat.Yaml));

    /// <summary>
    /// A JSON script is claimed for JSON even though YAML would also parse it, so registering
    /// the YAML notation cannot change how an existing JSON script behaves.
    /// </summary>
    [Test]
    public void Detect_JsonAfterYamlDocumentMarker_IsStillJson() =>
        Assert.That(ScriptFormatDetector.Detect("---\n[{\"command\":\"set\"}]"),
            Is.EqualTo(ScriptFormat.Json));

    /// <summary>Four dashes are a YAML scalar, not the document marker.</summary>
    [Test]
    public void Detect_FourDashes_IsYaml() =>
        Assert.That(ScriptFormatDetector.Detect("----\n"), Is.EqualTo(ScriptFormat.Yaml));

    [TestCase("")]
    [TestCase("   \n\t ")]
    [TestCase(null)]
    public void Detect_NothingToGoOn_IsJson(string? text) =>
        Assert.That(ScriptFormatDetector.Detect(text), Is.EqualTo(ScriptFormat.Json));

    [Test]
    public void Detect_SkipsByteOrderMark() =>
        Assert.That(ScriptFormatDetector.Detect("﻿<script/>"), Is.EqualTo(ScriptFormat.Xml));

    [TestCase(".json", ScriptFormat.Json)]
    [TestCase("json", ScriptFormat.Json)]
    [TestCase(".XML", ScriptFormat.Xml)]
    [TestCase(".yaml", ScriptFormat.Yaml)]
    [TestCase(".yml", ScriptFormat.Yaml)]
    public void FromFileExtension_KnownExtension_IsItsNotation(string ext, ScriptFormat expected) =>
        Assert.That(ScriptFormatDetector.FromFileExtension(ext), Is.EqualTo(expected));

    [TestCase(".txt")]
    [TestCase("")]
    [TestCase(null)]
    public void FromFileExtension_SaysNothing_IsNull(string? ext) =>
        Assert.That(ScriptFormatDetector.FromFileExtension(ext), Is.Null);

    [TestCase("json", ScriptFormat.Json)]
    [TestCase("XML", ScriptFormat.Xml)]
    [TestCase(" yaml ", ScriptFormat.Yaml)]
    [TestCase("yml", ScriptFormat.Yaml)]
    public void TryParse_KnownName_Succeeds(string name, ScriptFormat expected)
    {
        Assert.That(ScriptFormatDetector.TryParse(name, out var format), Is.True);
        Assert.That(format, Is.EqualTo(expected));
    }

    [TestCase("toml")]
    [TestCase("")]
    [TestCase(null)]
    public void TryParse_UnknownName_Fails(string? name) =>
        Assert.That(ScriptFormatDetector.TryParse(name, out _), Is.False);
}
