using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace TLio.Json.SystemText.Internal;

/// <summary>
/// System.Text.Json has no node for JSON null — a null-valued property or array element is a
/// C# <c>null</c>, so there is nothing to hand back from SelectNodes, and every command that
/// works on the selected node (remove, rename, copy, merge, =fetch, …) reported "no nodes
/// matched" where Newtonsoft, XML and YAML all found one. .NET offers no way out at the
/// representation level: every <c>JsonValue.Create</c> overload maps a null back to C# null.
///
/// So the fetcher hands out a <b>placeholder</b> instead: a detached node that stands for the
/// null and remembers the slot (parent + property name or array index) it was found in. The
/// adapter recognises placeholders by reference and answers for them the way Newtonsoft answers
/// for a null JValue — null kind, no value, removable, replaceable, renameable through the
/// remembered slot — and converts them back to plain null the moment one is written into a
/// document. A placeholder never enters a tree.
///
/// The table is keyed by node identity and weakly referenced, so placeholders vanish with the
/// nodes themselves; a fresh empty JsonObject in a document is never confused with one, because
/// only instances created here are in the table.
/// </summary>
internal static class NullSlots
{
    internal sealed record Slot(JsonNode Parent, string? Key, int? Index);

    private static readonly ConditionalWeakTable<JsonNode, Slot> Slots = new();

    /// <summary>A detached node standing for the JSON null stored at the given slot.</summary>
    public static JsonNode CreatePlaceholder(JsonNode parent, string? key, int? index)
    {
        var placeholder = new JsonObject();
        Slots.Add(placeholder, new Slot(parent, key, index));
        return placeholder;
    }

    public static bool IsPlaceholder(JsonNode? node) =>
        node != null && Slots.TryGetValue(node, out _);

    public static bool TryGetSlot(JsonNode? node, out Slot slot)
    {
        if (node != null && Slots.TryGetValue(node, out var found))
        {
            slot = found;
            return true;
        }
        slot = null!;
        return false;
    }

    /// <summary>The value to store when a node is written into a document: null for a placeholder.</summary>
    public static JsonNode? ToStorable(JsonNode? node) => IsPlaceholder(node) ? null : node;

    // The slot writes only act while the slot still holds null. If something else already
    // rewrote or removed it, the placeholder is stale — the same situation as a detached
    // Newtonsoft node, which also no-ops.

    /// <summary>Writes <paramref name="value"/> into the slot the placeholder remembers.</summary>
    public static bool TryWriteSlot(JsonNode placeholder, JsonNode? value)
    {
        if (!TryGetSlot(placeholder, out var slot)) return false;

        if (slot.Parent is JsonObject obj && slot.Key != null &&
            obj.ContainsKey(slot.Key) && obj[slot.Key] is null)
        {
            obj[slot.Key] = value;
            return true;
        }
        if (slot.Parent is JsonArray arr && slot.Index is int idx &&
            idx < arr.Count && arr[idx] is null)
        {
            arr[idx] = value;
            return true;
        }
        return false;
    }

    /// <summary>Removes the slot the placeholder remembers from its parent.</summary>
    public static bool TryRemoveSlot(JsonNode placeholder)
    {
        if (!TryGetSlot(placeholder, out var slot)) return false;

        if (slot.Parent is JsonObject obj && slot.Key != null &&
            obj.ContainsKey(slot.Key) && obj[slot.Key] is null)
            return obj.Remove(slot.Key);
        if (slot.Parent is JsonArray arr && slot.Index is int idx &&
            idx < arr.Count && arr[idx] is null)
        {
            arr.RemoveAt(idx);
            return true;
        }
        return false;
    }
}
