using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace LzAssessor.NewVersion.Engine;

/// <summary>
/// Declarative logic evaluator that applies predicates to JSON payloads
/// Supports: allOf, anyOf, exists, equals, lte, gte, contains, not
/// </summary>
public static class LogicEvaluator
{
    /// <summary>
    /// Evaluate logic rules against a payload
    /// </summary>
    public static bool Evaluate(JObject payload, JsonElement logic)
    {
        if (logic.ValueKind == JsonValueKind.Undefined || logic.ValueKind == JsonValueKind.Null)
            return true;

        // Convert System.Text.Json to Newtonsoft.Json for JSONPath support
        var logicJson = JObject.Parse(logic.GetRawText());
        return Evaluate(payload, logicJson);
    }

    /// <summary>
    /// Evaluate logic rules against a payload (Newtonsoft version)
    /// </summary>
    public static bool Evaluate(JObject payload, JObject logic)
    {
        if (logic == null || !logic.HasValues)
            return true;

        // Handle allOf
        if (logic.TryGetValue("allOf", out var allOf) && allOf is JArray allArray)
        {
            return allArray.All(item => Evaluate(payload, item as JObject ?? new JObject()));
        }

        // Handle anyOf
        if (logic.TryGetValue("anyOf", out var anyOf) && anyOf is JArray anyArray)
        {
            return anyArray.Any(item => Evaluate(payload, item as JObject ?? new JObject()));
        }

        // Handle not
        if (logic.TryGetValue("not", out var notLogic) && notLogic is JObject notObj)
        {
            return !Evaluate(payload, notObj);
        }

        // Handle individual predicates
        foreach (var prop in logic.Properties())
        {
            var result = prop.Name switch
            {
                "exists" => EvaluateExists(payload, prop.Value),
                "equals" => EvaluateEquals(payload, prop.Value),
                "lte" => EvaluateLte(payload, prop.Value),
                "gte" => EvaluateGte(payload, prop.Value),
                "contains" => EvaluateContains(payload, prop.Value),
                _ => true // Unknown predicates are ignored
            };

            if (!result) return false;
        }

        return true;
    }

    private static bool EvaluateExists(JObject payload, JToken spec)
    {
        if (spec["path"] is not JToken pathToken) return false;
        var path = pathToken.ToString();
        var tokens = SelectWithLengthSupport(payload, path, out var isLength, out var length);
        
        if (isLength)
            return length > 0;
        
        return tokens.Any();
    }

    private static bool EvaluateEquals(JObject payload, JToken spec)
    {
        if (spec["path"] is not JToken pathToken || spec["value"] is not JToken valueToken) 
            return false;

        var path = pathToken.ToString();
        var expectedValue = valueToken.ToString();
        var tokens = SelectWithLengthSupport(payload, path, out var isLength, out var length);

        if (isLength)
        {
            return int.TryParse(expectedValue, out var expectedInt) && length == expectedInt;
        }

        return tokens.Any(t => string.Equals(t.ToString(), expectedValue, StringComparison.OrdinalIgnoreCase));
    }

    private static bool EvaluateLte(JObject payload, JToken spec)
    {
        return CompareNumeric(payload, spec, (actual, expected) => actual <= expected);
    }

    private static bool EvaluateGte(JObject payload, JToken spec)
    {
        return CompareNumeric(payload, spec, (actual, expected) => actual >= expected);
    }

    private static bool CompareNumeric(JObject payload, JToken spec, Func<int, int, bool> comparer)
    {
        if (spec["path"] is not JToken pathToken || spec["value"] is not JToken valueToken) 
            return false;

        var path = pathToken.ToString();
        var tokens = SelectWithLengthSupport(payload, path, out var isLength, out var length);

        if (!int.TryParse(valueToken.ToString(), out var expectedValue))
            return false;

        if (isLength)
            return comparer(length, expectedValue);

        return tokens.Any(t => int.TryParse(t.ToString(), out var actualValue) && comparer(actualValue, expectedValue));
    }

    private static bool EvaluateContains(JObject payload, JToken spec)
    {
        if (spec["path"] is not JToken pathToken || spec["value"] is not JToken valueToken) 
            return false;

        var path = pathToken.ToString();
        var expectedValue = valueToken.ToString();
        var tokens = SelectWithLengthSupport(payload, path, out _, out _);

        foreach (var token in tokens)
        {
            if (token is JArray array)
            {
                // Check if array contains the value (including nested arrays)
                if (array.Any(item => 
                    item is JArray innerArray 
                        ? innerArray.Any(innerItem => string.Equals(innerItem.ToString(), expectedValue, StringComparison.OrdinalIgnoreCase))
                        : string.Equals(item.ToString(), expectedValue, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
            else if (string.Equals(token.ToString(), expectedValue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Select tokens with support for .length() suffix
    /// </summary>
    private static List<JToken> SelectWithLengthSupport(JObject payload, string path, out bool isLength, out int length)
    {
        isLength = false;
        length = 0;

        if (path.EndsWith(".length()", StringComparison.OrdinalIgnoreCase))
        {
            isLength = true;
            var basePath = path[..^9]; // Remove ".length()"
            var tokens = payload.SelectTokens(basePath).ToList();
            length = tokens.Count;
            return tokens;
        }

        return payload.SelectTokens(path).ToList();
    }
}