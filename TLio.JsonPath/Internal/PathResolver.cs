#nullable disable
using System.Text.Json.Nodes;

namespace TLio.JsonPath.Internal;

/// <summary>
/// Turns a match's last step (container + member name or index) into its full RFC 9535 §2.7 normalized path.
/// Selecting records only that last step, so the common case — a caller that wants the nodes — pays nothing
/// for paths. The first time anyone asks for one, the resolver walks the document <em>once</em> from the root
/// and writes down the path of every container that has a match in it; each later request is a lookup plus one
/// concatenation. Asking for the paths of all 20,000 matches of <c>$[*].x</c> is therefore one walk, not
/// 20,000 searches of a 20,000-element array.
/// </summary>
internal abstract class PathResolver
{
    /// <summary>The path of the node that is at <paramref name="name"/> / <paramref name="index"/> inside <paramref name="parent"/>.</summary>
    public abstract string PathOf(JsonNode parent, string name, int index);

    /// <summary>The matches whose paths this resolver may be asked for. Set once, after evaluation, before the list is handed out.</summary>
    public abstract void SetMatches(List<JsonPathMatch> matches);
}

internal sealed class PathResolver<TNode, TModel> : PathResolver where TModel : struct, IJsonModel<TNode>
{
    private readonly TNode _root;
    private readonly object _gate = new();
    private List<JsonPathMatch> _matches;
    private Dictionary<object, string> _containerPaths;

    public PathResolver(TNode root) => _root = root;

    public override void SetMatches(List<JsonPathMatch> matches) => _matches = matches;

    public override string PathOf(JsonNode parent, string name, int index)
    {
        if (parent == null) return "$"; // the root itself

        string parentPath;
        lock (_gate)
        {
            _containerPaths ??= Build();
            if (!_containerPaths.TryGetValue(parent, out parentPath))
                parentPath = "$"; // the document changed since the match was made; best effort
        }

        return name != null ? parentPath + NormalizedPaths.Name(name) : parentPath + NormalizedPaths.Index(index);
    }

    private Dictionary<object, string> Build()
    {
        var paths = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
        var wanted = new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (_matches != null)
            foreach (var m in _matches)
                if (m.Parent != null) wanted.Add(m.Parent);

        if (wanted.Count == 0) return paths;

        var m_ = default(TModel);
        var remaining = wanted.Count;
        var steps = new List<string>();
        var stack = new Stack<Frame>();

        if (IsContainer(m_, _root))
        {
            if (wanted.Contains(_root!)) { paths[_root!] = "$"; remaining--; }
            stack.Push(new Frame(_root, m_.KindOf(_root) == NodeKind.Object, m_.Count(_root)));
        }

        while (stack.Count > 0 && remaining > 0)
        {
            var f = stack.Pop();
            if (f.Next >= f.Count)
            {
                if (steps.Count > 0) steps.RemoveAt(steps.Count - 1);
                continue;
            }

            string step;
            TNode child;
            if (f.IsObject)
            {
                m_.MemberAt(f.Node, f.Next, out var name, out child);
                step = NormalizedPaths.Name(name);
            }
            else
            {
                child = m_.ElementAt(f.Node, f.Next);
                step = NormalizedPaths.Index(f.Next);
            }

            f.Next++;
            stack.Push(f);

            if (!IsContainer(m_, child)) continue;

            steps.Add(step);
            if (wanted.Contains(child!))
            {
                paths[child!] = "$" + string.Concat(steps);
                remaining--;
            }

            stack.Push(new Frame(child, m_.KindOf(child) == NodeKind.Object, m_.Count(child)));
        }

        return paths;
    }

    private static bool IsContainer(TModel m, TNode node) => m.KindOf(node) is NodeKind.Object or NodeKind.Array;

    private struct Frame(TNode node, bool isObject, int count)
    {
        public readonly TNode Node = node;
        public readonly bool IsObject = isObject;
        public readonly int Count = count;
        public int Next;
    }
}
