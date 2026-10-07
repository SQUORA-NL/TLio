namespace TLio.JsonPath.Tests.Differential;

/// <summary>The documents the differential corpus runs against: one rich bookstore plus documents built to provoke type edge cases.</summary>
public static class Documents
{
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
    {
        ["store"] = """
        {
          "store": {
            "book": [
              { "category": "reference", "author": "Nigel Rees", "title": "Sayings of the Century", "price": 8.95, "isbn": null },
              { "category": "fiction", "author": "Evelyn Waugh", "title": "Sword of Honour", "price": 12.99 },
              { "category": "fiction", "author": "Herman Melville", "title": "Moby Dick", "isbn": "0-553-21311-3", "price": 8.99 },
              { "category": "fiction", "author": "J. R. R. Tolkien", "title": "The Lord of the Rings", "isbn": "0-395-19395-8", "price": 22.99 }
            ],
            "bicycle": { "color": "red", "price": 19.95 }
          },
          "expensive": 10,
          "name with space": 1,
          "a-b": 2,
          "$dollar": 3,
          "o'quote": 4,
          "unié": 5,
          "tags": ["a", "b", "c", "d", "e", "f"]
        }
        """,

        ["array"] = """[1, 2.5, "three", true, false, null, [10, 20, 30], {"k": "v", "n": 7}, "", 0, -1, 1e2, 100]""",

        ["nested"] = """
        {
          "a": { "a": { "a": 1, "b": [ { "a": 2 }, { "b": 3 } ] }, "b": 4 },
          "b": [ [1, 2, [3, 4]], [5], [] ],
          "c": { },
          "d": [ ],
          "e": { "x": [ { "y": [ { "z": 1 }, { "z": 2 } ] }, { "y": [ { "z": 3 } ] } ] }
        }
        """,

        ["numbers"] = """
        [
          { "v": 1 }, { "v": 1.0 }, { "v": 2 }, { "v": 2.5 }, { "v": -3 }, { "v": 0 }, { "v": -0.0 },
          { "v": 1e2 }, { "v": 100 }, { "v": 9007199254740993 }, { "v": 12345678901234567890 },
          { "v": 0.1 }, { "v": 0.30000000000000004 }, { "v": 1E-2 }, { "v": "1" }, { "v": "2.5" }, { "v": "abc" },
          { "v": true }, { "v": false }, { "v": null }, { "w": 1 }, { "v": "" }, { "v": [1] }, { "v": {"v": 1} }
        ]
        """,

        ["strings"] = """
        [
          { "s": "apple" }, { "s": "Apple" }, { "s": "banana" }, { "s": "apple pie" }, { "s": "" },
          { "s": "10" }, { "s": "9" }, { "s": "true" }, { "s": "False" }, { "s": "null" },
          { "s": "café" }, { "s": "😀" }, { "s": "a\nb" }, { "s": "x/y" }, { "s": 5 }, { "s": null }
        ]
        """,

        ["dates"] = """
        [
          { "d": "2020-01-01T00:00:00Z" },
          { "d": "2020-01-01T00:00:00.50Z" },
          { "d": "2020-01-01T00:00:00.5Z" },
          { "d": "2020-06-15T12:30:45" },
          { "d": "2020-06-15T12:30:45+02:00" },
          { "d": "2020-06-15T12:30:45-0500" },
          { "d": "2021-02-29T00:00:00Z" },
          { "d": "2020-01-01" },
          { "d": "2020-01-01 00:00:00" },
          { "d": "/Date(1577836800000)/" },
          { "d": "not a date" },
          { "d": "2020-01-01T00:00:00Z", "e": "2020-01-01T00:00:00Z" },
          { "d": "2020-01-01T24:00:00Z" },
          { "d": 20200101 }
        ]
        """,

        ["scalars-object"] = """{ "n": 5, "s": "x", "t": true, "f": false, "z": null, "o": {}, "a": [] }""",
        ["scalar-number"] = "5",
        ["scalar-string"] = "\"text\"",
        ["scalar-null"] = "null",
        ["empty-array"] = "[]",
        ["empty-object"] = "{}",

        ["objects-in-array"] = """
        [
          { "id": 1, "tags": ["x", "y"], "owner": { "name": "ann", "age": 30 } },
          { "id": 2, "tags": [], "owner": { "name": "bob" } },
          { "id": 3, "owner": null },
          { "id": "4", "tags": ["z"], "owner": { "name": "cy", "age": "41" } }
        ]
        """,
    };
}
