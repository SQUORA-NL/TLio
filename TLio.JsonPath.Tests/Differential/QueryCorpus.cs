namespace TLio.JsonPath.Tests.Differential;

/// <summary>
/// Hand-written queries that pin down each Newtonsoft behaviour the spec calls out, plus systematic
/// expansions (operator × literal × path). Nothing here asserts an expected value: the oracle decides.
/// </summary>
public static class QueryCorpus
{
    public static IEnumerable<string> Handwritten()
    {
        // ── roots, names, dot and bracket notation ─────────────────────────────────────────
        foreach (var q in new[]
        {
            "$", "", " ", "$ ", " $", "  $.store", "store", ".store", "$store", "$.store", "$.store.book", "$.store.bicycle.color",
            "$['store']", "$['store']['book']", "$.store['book'][0].title", "$['store'].book[0]['title']",
            "$['store','expensive']", "$[ 'store' , 'expensive' ]", "$['store', 'nope', 'expensive']", "$['nope','nada']",
            "$.expensive", "$.nope", "$.nope.deeper", "$.store.nope", "$.store.book.nope",
            "$.a-b", "$['a-b']", "$.$dollar", "$['$dollar']", "$.name with space", "$['name with space']", "$['o\\'quote']", "$['o\"quote']",
            "$.unié", "$['unié']", "$[\"store\"]", "$[\"a\",\"b\"]", "$['\\u0041']", "$['a\\tb']", "$['a\\qb']", "$['a\\\\b']", "$['a\\/b']", "$['unterminated", "$['a' 'b']", "$['a',]", "$[,'a']", "$[]", "$['']", "$.''",
            "$.store ", "$.store .book", "$.store. book", "$. store", "$.store.book ", "$ .store", "$.store..book", "$.store...book",
            "$.", "$..", "$...", "$.store.", "$.store..", "$..store", "$..store.book", "$..book.title", "$..[0]", "$..[*]", "$..*", "$..['author']", "$..['author','title']", "$..'author'",
            "$.*", "$.store.*", "$.store.*.*", "$.*.*", "$.*.book", "$..*.price", "$.store.book.*", "$.tags.*", "$['*']", "$.*[0]", "$.store.book[*].*", "$..book[*]",
            "$..a", "$.a..a", "$..a.a", "$..a..a", "$.a.a.a", "$..b", "$..z", "$.e..z", "$.e..y", "$..y..z", "$..['a','b']", "$..['x','z']",
            "@", "@.store", "$@", "$.@", "$.store@", "$.[store]", "$[store]", "$[0]", "$.0", "$.store[0]", "$.expensive[0]", "$.expensive.x", "$.tags.x",
            "..store", "..", "..*", "*", "[0]", "['store']", "[*]", "$[*]", "$[*].price", "$[*][*]",
            "$.store.book[0]", "$.store.book[-1]", "$.store.book[3]", "$.store.book[4]", "$.store.book[ 1 ]", "$.store.book[01]", "$.store.book[+1]", "$.store.book[1.0]", "$.store.book[1e0]", "$.store.book[0x1]",
            "$.store.book[0,1]", "$.store.book[1,0]", "$.store.book[0,0]", "$.store.book[ 0 , 1 ]", "$.store.book[0,]", "$.store.book[,0]", "$.store.book[0,,1]", "$.store.book[0 1]", "$.store.book[0,9]", "$.store.book[-1,0]",
            "$.store.book[:]", "$.store.book[::]", "$.store.book[1:]", "$.store.book[:2]", "$.store.book[1:3]", "$.store.book[-2:]", "$.store.book[:-2]", "$.store.book[-3:-1]", "$.store.book[3:1]", "$.store.book[::2]", "$.store.book[::-1]", "$.store.book[::-2]", "$.store.book[3:0:-1]", "$.store.book[1::-1]", "$.store.book[:1:-1]",
            "$.store.book[::0]", "$.store.book[0:2:0]", "$.store.book[1:2:3:4]", "$.store.book[ : ]", "$.store.book[ 1 : 3 ]", "$.store.book[1 :3]", "$.store.book[1: 3]", "$.store.book[-10:10]", "$.store.book[10:]", "$.store.book[-10:]", "$.store.book[:-10]", "$.store.book[2:2]", "$.store.book[0:4:3]", "$.store.book[5:2:-1]",
            "$.store.book[2147483647]", "$.store.book[2147483648]", "$.store.book[-2147483648]", "$.store.book[-2147483648:]", "$.store.book[:2147483647]", "$.store.book[::2147483647]", "$.store.book[1::2147483647]", "$.store.book[::-2147483648]", "$.store.book[99999999999]",
            "$.store.book[*]", "$.store.book[ * ]", "$.store.book[*", "$.store.book[*,0]", "$.store.book[0,*]", "$.store.book[*]title", "$.store.book[*].title", "$.store.book[0]title", "$.store.book[0][0]", "$.store.book[0]['title']", "$.store.book['title']", "$.store.bicycle[0]", "$.store.bicycle[*]", "$.store.bicycle[:]",
            "$.tags[1:4]", "$.tags[-1]", "$.tags[1:4:2]", "$.tags[4:1:-1]", "$.tags[::-1]", "$.tags[0:6:6]", "$.tags[0:6:7]", "$.tags[-1:-4:-1]", "$.tags[5:-8:-1]",
            "$.store.book[0].title[0]", "$.store.book[0].title.length", "$.store.book[0].price.x",
        })
            yield return q;

        // ── filters: syntax ────────────────────────────────────────────────────────────────
        foreach (var q in new[]
        {
            "$.store.book[?(@.price)]", "$.store.book[?(@.isbn)]", "$.store.book[?(@.nope)]", "$.store.book[?(@.isbn == null)]", "$.store.book[?(@.isbn != null)]",
            "$.store.book[?(@.price < 10)]", "$.store.book[?(@.price<10)]", "$.store.book[?( @.price < 10 )]", "$.store.book[ ?(@.price < 10) ]", "$.store.book[? (@.price < 10)]", "$.store.book[?(@.price < 10 )]", "$.store.book[?(@.price < 10)  ]",
            "$.store.book[?(@.price <= 8.95)]", "$.store.book[?(@.price >= 8.99)]", "$.store.book[?(@.price > 8.99)]", "$.store.book[?(@.price == 8.95)]", "$.store.book[?(@.price != 8.95)]", "$.store.book[?(@.price <> 8.95)]",
            "$.store.book[?(@.price === 8.95)]", "$.store.book[?(@.price !== 8.95)]", "$.store.book[?(@.price === 9)]", "$.store.book[?(@.price == '8.95')]", "$.store.book[?(@.price === '8.95')]",
            "$.store.book[?(@.category == 'fiction')]", "$.store.book[?(@.category=='fiction')]", "$.store.book[?(@.category == \"fiction\")]", "$.store.book[?(@.category != 'fiction')]", "$.store.book[?(@.category === 'fiction')]", "$.store.book[?(@.category !== 'fiction')]",
            "$.store.book[?(@.category == 'fiction' && @.price < 10)]", "$.store.book[?(@.category == 'fiction' || @.price < 10)]", "$.store.book[?(@.category == 'fiction' && @.price < 10 && @.isbn)]",
            "$.store.book[?(@.price < 10 || @.price > 20 && @.isbn)]", "$.store.book[?(@.price > 20 && @.isbn || @.price < 9)]", "$.store.book[?(@.price < 9 || @.price > 12 || @.isbn == null)]",
            "$.store.book[?(@.price < 9 && @.price > 8 && @.category == 'reference' || @.price > 22)]", "$.store.book[?(@.price < 9 || @.price > 12 && @.price < 20 || @.price > 22)]",
            "$.store.book[?(@.price < 10 & @.price > 8)]", "$.store.book[?(@.price < 10 | @.price > 20)]", "$.store.book[?(@.price < 10 &&)]", "$.store.book[?(&& @.price)]", "$.store.book[?(@.price < 10 && @.price > 8 )]",
            "$.store.book[?((@.price < 10))]", "$.store.book[?(!@.isbn)]", "$.store.book[?(!(@.isbn))]", "$.store.book[?@.price]", "$.store.book[?@.price < 10]", "$.store.book[?]", "$.store.book[?()]", "$.store.book[?(]", "$.store.book[?(@.price]", "$.store.book[?(@.price < ]", "$.store.book[?(@.price < 10]", "$.store.book[?(@.price < 10)", "$.store.book[?(@.price < 10)]]",
            "$.store.book[?(@)]", "$.store.book[?(@ == 1)]", "$.store.book[?(@.title)].title", "$.store.book[?(@.price)].price", "$.store.book[?(@.price)][0]", "$.store.book[?(@.price)][?(@.isbn)]", "$.store.book[?(@.price)][?(@.isbn)].title",
            "$.store.book[?(@.price < $.expensive)]", "$.store.book[?(@.price > $.store.bicycle.price)]", "$.store.book[?($.expensive == 10)]", "$.store.book[?($.nope)]", "$.store.book[?($.expensive)]", "$.store.book[?(@.price < $.nope)]", "$.store.book[?($.expensive > @.price)]", "$.store.book[?(10 > @.price)]", "$.store.book[?('fiction' == @.category)]", "$.store.book[?(null == @.isbn)]", "$.store.book[?(1 == 1)]", "$.store.book[?('a' == 'a')]", "$.store.book[?(true == true)]",
            "$.store.book[?(@.price < 10)].title", "$.store.book[?(@.price < 10)]['title','author']", "$.store.book[?(@.price < 10)].*", "$.store.book[?(@.price < 10)][0]", "$.store.book[?(@.price < 10)][-1]",
            "$..book[?(@.price < 10)]", "$..[?(@.price)]", "$..[?(@.price < 10)]", "$..[?(@.isbn)]", "$..[?(@.a)]", "$..[?(@ == 1)]", "$..[?(@)]", "$..[?(@ != 1)]", "$.store..[?(@.price)]", "$.store..book[?(@.price)]", "$..*[?(@.price)]", "$..['book'][?(@.price)]", "$..book..[?(@.price)]", "$..[?(@.a)].a", "$..[?(@.b)]",
            "$.store[?(@.price)]", "$.store.bicycle[?(@.price)]", "$.store[?(@.color)]", "$.store.bicycle[?(@ == 'red')]", "$.store.bicycle[?(@ != 'red')]", "$.store.bicycle[?(@)]", "$.store.bicycle[?(@ > 'a')]", "$.store.bicycle[?(@.x)]", "$[?(@.store)]", "$[?(@)]", "$[?(@ != 1)]", "$[?(@ == 5)]", "$.tags[?(@ == 'a')]", "$.tags[?(@ != 'a')]", "$.tags[?(@ > 'c')]", "$.tags[?(@ >= 'c')]", "$.tags[?(@ < 'c')]", "$.tags[?(@)]", "$.tags[?(@ =~ /[a-c]/)]", "$.tags[?(@ == 5)]", "$.tags[?(@ > 5)]",
            "$.nested[?(@.a)]", "$[?(@.a)]", "$.a[?(@.a)]", "$.a[?(@.a)].a", "$.b[?(@[0])]", "$.b[?(@[1])]", "$.b[?(@[0] == 1)]", "$.b[?(@.length)]", "$.b[?(@[2])]", "$.b[*][?(@ == 5)]", "$.b[?(@ == 5)]", "$.b[?(@[0] > 1)]", "$.e.x[?(@.y)]", "$.e.x[?(@.y[0].z == 1)]", "$.e.x[?(@.y[?(@.z == 3)])]", "$.e.x[?(@.y[?(@.z > 1)])].y", "$.e.x[?(@..z == 3)]", "$.e.x[?(@..z)]", "$.e.x[?(@..nope)]", "$.e..[?(@.z == 2)]", "$.e..y[?(@.z == 2)]",
            "$[?(@.id == 1)]", "$[?(@.id == '4')]", "$[?(@.id == 4)]", "$[?(@.id === 4)]", "$[?(@.id > 1)]", "$[?(@.id > '1')]", "$[?(@.id < '3')]", "$[?(@.owner)]", "$[?(@.owner.name)]", "$[?(@.owner.age)]", "$[?(@.owner.age > 35)]", "$[?(@.owner.age > '35')]", "$[?(@.owner == null)]", "$[?(@.owner != null)]", "$[?(@.tags)]", "$[?(@.tags[0])]", "$[?(@.tags[0] == 'x')]", "$[?(@.tags[*] == 'y')]", "$[?(@.tags[*])]", "$[?(@.tags.*)]", "$[?(@.owner.*)]", "$[?(@.owner.name == 'ann' || @.owner.name == 'bob')]", "$[?(@.owner.name =~ /^[ab]/)]", "$[?(@.owner.name =~ /^A/i)]", "$[?(@.owner.name =~ /^a/ && @.id == 1)]",
            "$[?(@.id == 1)].owner.name", "$[?(@.id)].id", "$[*].owner.name", "$[*].owner.age", "$..owner.name", "$..name", "$..age", "$..owner", "$..tags", "$..tags[0]", "$..tags[*]", "$..owner[?(@.age)]", "$..[?(@.age)]", "$..[?(@.name == 'bob')]",
        })
            yield return q;

        // ── regex ─────────────────────────────────────────────────────────────────────────
        foreach (var q in new[]
        {
            "$[?(@.s =~ /apple/)]", "$[?(@.s =~ /^apple$/)]", "$[?(@.s =~ /APPLE/i)]", "$[?(@.s =~ /^a/)]", "$[?(@.s =~ /e$/)]", "$[?(@.s =~ /a.*e/)]", "$[?(@.s =~ /\\d+/)]", "$[?(@.s =~ /^\\d+$/)]", "$[?(@.s =~ /a\\/b/)]", "$[?(@.s =~ /x\\/y/)]", "$[?(@.s =~ /x/y/)]",
            "$[?(@.s =~ /./)]", "$[?(@.s =~ /^.$/)]", "$[?(@.s =~ /^.$/s)]", "$[?(@.s =~ /a.b/s)]", "$[?(@.s =~ /a.b/)]", "$[?(@.s =~ /^b/m)]", "$[?(@.s =~ /(?<n>a)/x)]", "$[?(@.s =~ /(a)/x)]", "$[?(@.s =~ /a/ix)]", "$[?(@.s =~ /a/z)]", "$[?(@.s =~ /a/I)]",
            "$[?(@.s =~ 'apple')]", "$[?(@.s =~ '/apple/')]", "$[?(@.s =~ /(/)]", "$[?(@.s =~ /[/)]", "$[?(@.s =~ //)]", "$[?(@.s =~ /a)]", "$[?(@.s =~ /a/", "$[?(@.s =~ 5)]", "$[?(@.s =~ null)]", "$[?(@.s =~ @.s)]", "$[?(5 =~ /5/)]", "$[?(@.s !~ /a/)]",
            "$[?(@.s == /apple/)]", "$[?(/apple/ =~ @.s)]", "$[?(@.s =~ /é/)]", "$[?(@.s =~ /\\u00e9/)]", "$[?(@.s =~ /\\w+/)]", "$[?(@.s =~ /^[a-z]+$/)]", "$[?(@.s =~ /^\\p{L}+$/)]", "$[?(@.s =~ /^(?:a|b)/)]", "$[?(@.s =~ /(?i)APPLE/)]",
        })
            yield return q;

        // ── comparison semantics across types ─────────────────────────────────────────────
        foreach (var path in new[] { "$[?(@.v {op} {lit})]", "$[?({lit} {op} @.v)]" })
        foreach (var op in new[] { "==", "!=", "<>", "<", "<=", ">", ">=", "===", "!==" })
        foreach (var lit in new[] { "1", "1.0", "2", "2.5", "-3", "0", "-0", "-0.0", "100", "1e2", "1E2", "1e-2", "0.1", "0.30000000000000004", "9007199254740993", "9007199254740992", "12345678901234567890", "'1'", "'2.5'", "'abc'", "''", "'true'", "true", "false", "null", "1,000", "1,000.5", "-", "--1", "1.", ".5", "1e", "+1" })
            yield return path.Replace("{op}", op).Replace("{lit}", lit);

        foreach (var op in new[] { "==", "!=", "<", ">=", "===" })
        foreach (var lit in new[] { "'apple'", "'Apple'", "'apple pie'", "'10'", "'9'", "10", "9", "5", "true", "'true'", "'False'", "false", "null", "'null'", "'cafe\\u0301'", "'x/y'", "''" })
            yield return $"$[?(@.s {op} {lit})]";

        // ── dates (DateParseHandling.DateTime) ────────────────────────────────────────────
        foreach (var op in new[] { "==", "!=", "<", "<=", ">", ">=", "===", "!==" })
        foreach (var lit in new[]
        {
            "'2020-01-01T00:00:00Z'", "'2020-01-01T00:00:00.5Z'", "'2020-01-01T00:00:00.50Z'", "'2020-06-15T12:30:45'", "'2020-06-15T12:30:45+02:00'", "'2020-06-15T10:30:45Z'", "'2020-01-01'", "'2019-12-31T23:59:59Z'",
            "'2020-06-15T12:30:45-0500'", "'/Date(1577836800000)/'", "'not a date'", "'2020-01-01T00:00:00'", "'2021'", "1", "true", "null",
        })
            yield return $"$[?(@.d {op} {lit})]";
        yield return "$[?(@.d == @.e)]";
        yield return "$[?(@.d =~ /2020/)]";
        yield return "$[?(@.d =~ /Z$/)]";
        yield return "$[?(@.d)]";
        yield return "$[?(@.d > @.e)]";
        yield return "$[?(@.d >= @.e)]";
        yield return "$[?(@.d === @.e)]";
        yield return "$[?(@.d != @.e)]";

        // ── type edge cases on whole-document scalars and containers ─────────────────────
        foreach (var q in new[]
        {
            "$.n", "$.s", "$.t", "$.f", "$.z", "$.o", "$.a", "$.z.x", "$.z[0]", "$.z.*", "$.z[*]", "$.o.x", "$.o.*", "$.a[0]", "$.a[*]", "$.a.*", "$.a[:]", "$..*", "$..z", "$..[0]", "$.*", "$[*]", "$[0]", "$['n']", "$[?(@)]", "$[?(@ == 5)]", "$[?(@.x)]", "$[:]", "$[::-1]", "$[0,1]", "$..n", "$.n.x", "$.s.x", "$.s[0]", "$.s.length", "$.t.x",
        })
            yield return q;

        // ── whitespace and odd punctuation ─────────────────────────────────────────────────
        foreach (var q in new[]
        {
            "$ ['store']", "$['store'] ", "$['store'] .book", "$['store'] ['book']", "$[ 'store']", "$['store' ]", "$[\t'store']", "$.store\t.book", "$.store\n.book", "$.store.book[ 0 ]", "$.store.book[0 ]", "$.store.book[ 0]", "$.store.book[ ?(@.price)]", "$.store.book[?(@.price) ]", "$.store.book[?( @.price )]", "$.store.book[ ? (@.price)]",
            "$.store.book[?(@.price  <  10)]", "$.store.book[?(@.price	<	10)]", "$.store.book[?(  @.price<10  )]", "$.store.book[?(@.category=='fiction'&&@.price<10)]", "$.store.book[?(@.category == 'fiction' &&@.price < 10)]", "$.store.book[?(@.category == 'fiction'&& @.price < 10)]",
            "$.store.book(0)", "$.store.book(?(@.price))", "$.store.book(*)", "$.store.book(0:2)", "$.store.book[0)", "$.store.book(0]", "$.store.book(?(@.price)]", "$..book(?(@.price))", "$.store.book[?(@.price)(0)]",
            "$.store.book[0]é", "$.store.book[0]x", "$.store.book[0].x.y.z", "$..book[0].title", "$.store.book[0].title.x", "$.store.book[0]  .title", "$.store.book[0] .title", "$.store.book[0]. title",
            "$.'store'", "$.store.'book'", "$.['store']", "$..['store']", "$..[ 'store' ]", "$..['store'].book", "$..['store','x']", "$..[0,1]", "$..[0:2]", "$..[1:]", "$..[-1]", "$..book[0,1]",
            "$.store.book[?(@.price<10)].title", "$.store.book[?(@.price<10)][?(@.isbn)]", "$.store.book[?(@.price<10)]..title", "$.store.book..[?(@.price<10)]", "$.store..[?(@.price<10)].title",
            "$.store.book[?(@['price'] < 10)]", "$.store.book[?(@['price'])]", "$.store.book[?(@.['price'])]", "$.store.book[?(@..price)]", "$.store.book[?(@..price < 10)]", "$.store.book[?(@.*)]", "$.store.book[?(@.* == 'fiction')]", "$.store.book[?(@[*])]", "$.store.book[?(@[0])]", "$.store.book[?(@[0:1])]", "$.store.book[?(@['price','title'])]", "$.store.book[?(@.price.x)]", "$.store.book[?(@[?(@.x)])]", "$.store.book[?(@.price[?(@)])]",
            "$.store.book[?(@.title =~ /^S/ && @.price < 10)]", "$.store.book[?(@.title =~ /^S/ || @.price > 20)]", "$.store.book[?(@.title =~ /the/i)]", "$.store.book[?(@.title =~ /^[A-Z]/)]", "$.store.book[?(@.title =~ /\\s/)]", "$.store.book[?(@.author =~ /\\./)]",
            "$['store']['book'][?(@.price < 10)]", "$.store['book'][?(@.price < 10)].title", "$.store.book[?(@.price < 10)]['title']", "$.store.book[?(@.price < 10)]['title', 'author']", "$.store.book[?(@.price < 10)]..title",
            "$.store.book[?(@.price < 'abc')]", "$.store.book[?(@.price > 'abc')]", "$.store.book[?(@.price == 'abc')]", "$.store.book[?(@.price != 'abc')]", "$.store.book[?(@.title < 5)]", "$.store.book[?(@.title > 5)]", "$.store.book[?(@.title == 5)]", "$.store.book[?(@.isbn > 5)]", "$.store.book[?(@.isbn < 'a')]", "$.store.book[?(@.isbn >= null)]", "$.store.book[?(@.isbn <= null)]", "$.store.book[?(@.isbn < null)]", "$.store.book[?(@.price > null)]", "$.store.book[?(@.price < true)]", "$.store.book[?(@.price == true)]", "$.store.book[?(@.title > true)]", "$.store.book[?(@.title == null)]", "$.store.book[?(@.category > @.title)]", "$.store.book[?(@.category < @.title)]", "$.store.book[?(@.price > @.title)]", "$.store.book[?(@.title > @.price)]",
        })
            yield return q;

        // ── errors: malformed ─────────────────────────────────────────────────────────────
        foreach (var q in new[]
        {
            "$[", "$[0", "$['a'", "$['a", "$[?(", "$[?(@", "$[?(@.a", "$[?(@.a ==", "$[?(@.a == 1", "$[?(@.a == 1)", "$[?(@.a == 'x", "$[?(@.a == 'x'", "$[?(@.a == 'x')", "$[?(@.a == tru)]", "$[?(@.a == trueish)]", "$[?(@.a == nul)]", "$[?(@.a == falsy)]", "$[?(@.a = 1)]", "$[?(@.a ~ 1)]", "$[?(@.a === = 1)]", "$[?(@.a ==== 1)]", "$[?(@.a == == 1)]", "$[?(@.a 1)]", "$[?(1)]", "$[?('a')]", "$[?(null)]", "$[?(true)]", "$[?(@.a == @)]", "$[?(@.a == $)]", "$[?(@ == $)]", "$[?($ == $)]",
            "$[?(@.a == 1) && (@.b == 2)]", "$[?(@.a == 1 &&)]", "$[?(@.a == 1 &)]", "$[?(@.a == 1 &&& @.b)]", "$[?(@.a == 1 ||| @.b)]", "$[?(@.a == 1 | | @.b)]",
            "$[?(@.a == 1)", "$[?(@.a == 1))]", "$[?((@.a == 1)]", "$[?(@.a == 1) ]", "$[?(@.a == 1)x]", "$[?(@.a == 1)\t]",
            "$[01]", "$[-]", "$[--1]", "$[1-1]", "$[1 2]", "$[1,2 3]", "$[1:2:3:4]", "$[1:a]", "$[a]", "$[a:b]", "$[:a]", "$[*a]", "$[a*]", "$[**]", "$[*:1]", "$[1:*]", "$[1,*]", "$[1:2,3]", "$[1,2:3]", "$[1:2]:3", "$[:::]", "$[1::2::3]", "$[٣]", "$[1٣]",
            "$.a.b.", "$.a..", "$.a.[", "$.a[", "$.a]", "$.a)", "$.a(", "$.a()", "$.a(0)", "$.a(0", "$.a(?(@.x))", "$.a(?(@.x)", "$)", "$]", "$[]]", "$[[0]]", "$[[]", "$[0]]", "$[0][", "$[0][1", "$['a']['b'", "$['a']x", "$['a'] x", "$['a']'b'", "$['a'.'b']",
            "$.a b", "$.a  b", "$.a\tb", "$.a\nb", "$.a,b", "$.a;b", "$.a/b", "$.a\\b", "$.a=b", "$.a<b", "$.a>b", "$.a!b", "$.a&b", "$.a|b", "$.a?b", "$.a:b", "$.a#b", "$.a%b", "$.a'b", "$.a\"b", "$.a`b", "$.a~b", "$.a^b", "$.a{b}", "$.a@b", "$.a$b", "$.*a", "$.a*", "$.a*b", "$..*a",
        })
            yield return q;
    }
}
