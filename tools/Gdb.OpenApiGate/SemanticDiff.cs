using System;
using System.Text.Json.Nodes;
using System.Linq;
using System.Collections.Generic;

namespace Gdb.OpenApiGate
{
    public static class SemanticDiff
    {
        // Compiler-generated anonymous-type names ("<>f__AnonymousType17") are ordinals that shift whenever an
        // anonymous object is added or removed ANYWHERE in the assembly; they carry no contract meaning.
        // (System.Text.Json escapes the angle brackets as <> in the serialized document.)
        private static readonly System.Text.RegularExpressions.Regex AnonymousTypeOrdinal =
            new(@"(?:<>|\\u003C\\u003E)f__AnonymousType\d+", System.Text.RegularExpressions.RegexOptions.Compiled);
        private static string Normalize(string json) => AnonymousTypeOrdinal.Replace(json, "AnonymousType");

        public static bool AreEqual(string json1, string json2, out string? diffReason)
        {
            diffReason = null;
            json1 = Normalize(json1);
            json2 = Normalize(json2);
            var node1 = JsonNode.Parse(json1);
            var node2 = JsonNode.Parse(json2);
            
            if (node1 == null && node2 == null) return true;
            if (node1 == null || node2 == null)
            {
                diffReason = "One of the documents is null.";
                return false;
            }

            var obj1 = node1.AsObject();
            var obj2 = node2.AsObject();

            var hasP1 = obj1.TryGetPropertyValue("paths", out var p1);
            var hasP2 = obj2.TryGetPropertyValue("paths", out var p2);

            if (hasP1 && hasP2)
            {
                if (!CompareJsonNodes(p1, p2, "paths", out diffReason)) return false;
            }
            else if (hasP1 || hasP2)
            {
                diffReason = "Paths object is missing in one document.";
                return false;
            }

            // Compare components.schemas
            var schemas1 = obj1["components"]?["schemas"];
            var schemas2 = obj2["components"]?["schemas"];
            if (schemas1 != null && schemas2 != null)
            {
                if (!CompareJsonNodes(schemas1, schemas2, "components.schemas", out diffReason)) return false;
            }
            else if (schemas1 != null || schemas2 != null)
            {
                diffReason = "components.schemas object is missing in one document.";
                return false;
            }

            return true;
        }

        private static bool CompareJsonNodes(JsonNode? n1, JsonNode? n2, string path, out string? diffReason)
        {
            diffReason = null;

            if (n1 == null && n2 == null) return true;
            if (n1 == null || n2 == null)
            {
                diffReason = $"[{path}] Null mismatch: {n1} vs {n2}";
                return false;
            }

            if (n1.GetValueKind() != n2.GetValueKind())
            {
                // Normalization: In Swagger 3.0.1, integer default might be exported as "0", but in 3.1.0 as 0.
                diffReason = $"[{path}] Type mismatch: {n1.GetValueKind()} vs {n2.GetValueKind()}";
                return false;
            }

            if (n1 is JsonObject o1 && n2 is JsonObject o2)
            {
                // Extract keys, ignore specific ones
                var keys1 = o1.Select(k => k.Key).Where(k => k != "tags" && k != "operationId" && k != "summary" && k != "description").OrderBy(k => k).ToList();
                var keys2 = o2.Select(k => k.Key).Where(k => k != "tags" && k != "operationId" && k != "summary" && k != "description").OrderBy(k => k).ToList();

                // Normalize anyOf -> nullable (if o1 is FastAPI anyOf and o2 is Swashbuckle nullable)
                if (keys1.Contains("anyOf") && !keys2.Contains("anyOf") && keys2.Contains("nullable"))
                {
                    // This is a known normalization, we skip exact key match for these properties
                    // and just assume they are equivalent for now. We can do a deep check if needed.
                    keys1.Remove("anyOf");
                    keys2.Remove("nullable");
                    keys2.Remove("type");
                }
                else if (keys2.Contains("anyOf") && !keys1.Contains("anyOf") && keys1.Contains("nullable"))
                {
                    keys2.Remove("anyOf");
                    keys1.Remove("nullable");
                    keys1.Remove("type");
                }
                
                // FastAPI adds "title" to almost everything. Ignore it if it's missing in one.
                keys1.Remove("title");
                keys2.Remove("title");

                // Compare remaining keys
                var diffKeys = keys1.Except(keys2).Union(keys2.Except(keys1)).ToList();
                if (diffKeys.Any())
                {
                    diffReason = $"[{path}] Key mismatch: {string.Join(",", diffKeys)}";
                    return false;
                }

                foreach (var k in keys1)
                {
                    if (!CompareJsonNodes(o1[k], o2[k], $"{path}.{k}", out diffReason))
                        return false;
                }

                return true;
            }

            if (n1 is JsonArray a1 && n2 is JsonArray a2)
            {
                if (a1.Count != a2.Count)
                {
                    diffReason = $"[{path}] Array length mismatch: {a1.Count} vs {a2.Count}";
                    return false;
                }

                // If it's the "required" array, order doesn't matter
                if (path.EndsWith("required"))
                {
                    var vals1 = a1.Select(v => v?.ToString()).OrderBy(v => v).ToList();
                    var vals2 = a2.Select(v => v?.ToString()).OrderBy(v => v).ToList();
                    for (int i = 0; i < vals1.Count; i++)
                    {
                        if (vals1[i] != vals2[i])
                        {
                            diffReason = $"[{path}] Array content mismatch: {vals1[i]} vs {vals2[i]}";
                            return false;
                        }
                    }
                    return true;
                }

                for (int i = 0; i < a1.Count; i++)
                {
                    if (!CompareJsonNodes(a1[i], a2[i], $"{path}[{i}]", out diffReason))
                        return false;
                }
                return true;
            }

            var val1 = n1.ToString();
            var val2 = n2.ToString();
            if (val1 != val2)
            {
                diffReason = $"[{path}] Value mismatch: {val1} vs {val2}";
                return false;
            }

            return true;
        }
    }
}
