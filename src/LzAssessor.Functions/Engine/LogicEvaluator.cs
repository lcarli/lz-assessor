using Newtonsoft.Json.Linq;

namespace LzAssessor.Functions.Engine;

/// <summary>
/// Avaliador simples para a estrutura declarativa:
/// Suporta: allOf / anyOf  com predicados: exists, equals, lte, gte, contains, not
/// - path: JsonPath (Newtonsoft)
/// - equals/gte/lte com suporte a ".length()" (conta tokens)
/// - contains: path -> JArray (ou arrays de arrays) contém value
/// </summary>
public static class LogicEvaluator
{
    public static bool Evaluate(JToken payload, JToken logic)
    {
        if (logic is JObject obj)
        {
            if (obj.TryGetValue("allOf", out var allOf)) return AllOf(payload, allOf);
            if (obj.TryGetValue("anyOf", out var anyOf)) return AnyOf(payload, anyOf);
            if (obj.TryGetValue("not", out var not)) return !Evaluate(payload, not);

            // predicado unitário:
            if (obj.TryGetValue("exists", out var exists)) return Exists(payload, exists);
            if (obj.TryGetValue("equals", out var equals)) return Equals(payload, equals);
            if (obj.TryGetValue("lte", out var lte)) return Compare(payload, lte, "<=");
            if (obj.TryGetValue("gte", out var gte)) return Compare(payload, gte, ">=");
            if (obj.TryGetValue("contains", out var contains)) return Contains(payload, contains);
        }

        // Se não reconhecido, por padrão false (não atende).
        return false;
    }

    private static bool AllOf(JToken payload, JToken list) =>
        list is JArray arr && arr.All(x => Evaluate(payload, x));

    private static bool AnyOf(JToken payload, JToken list) =>
        list is JArray arr && arr.Any(x => Evaluate(payload, x));

    private static bool Exists(JToken payload, JToken spec)
    {
        var path = spec.Value<string>("path")!;
        var tokens = SelectWithLengthSupport(payload, path, out var isLength, out var length);
        if (isLength) return length > 0;
        return tokens.Any();
    }

    private static bool Equals(JToken payload, JToken spec)
    {
        var path = spec.Value<string>("path")!;
        var expected = spec["value"];
        var tokens = SelectWithLengthSupport(payload, path, out var isLength, out var length);

        if (isLength)
        {
            if (expected is JValue v && v.Value is long l) return length == l;
            if (expected is JValue v2 && v2.Value is int i) return length == i;
            if (expected is JValue v3 && v3.Value is bool b) return (length > 0) == b;
            return false;
        }

        if (tokens.Count == 0) return expected is JValue ev && ev.Value == null;
        if (tokens.Count == 1) return JToken.DeepEquals(tokens[0], expected);
        // se múltiplos, true se algum igual
        return tokens.Any(t => JToken.DeepEquals(t, expected));
    }

    private static bool Compare(JToken payload, JToken spec, string op)
    {
        var path = spec.Value<string>("path")!;
        var expected = spec["value"]!;
        var tokens = SelectWithLengthSupport(payload, path, out var isLength, out var length);

        double left;
        if (isLength) left = length;
        else
        {
            var token = tokens.FirstOrDefault();
            if (token == null || token.Type == JTokenType.Null) return false;
            if (!double.TryParse(token.ToString(), out left)) return false;
        }

        var right = double.Parse(expected.ToString());
        return op switch
        {
            "<=" => left <= right,
            ">=" => left >= right,
            _ => false
        };
    }

    private static bool Contains(JToken payload, JToken spec)
    {
        // Se qualquer array em path contiver value -> true
        var path = spec.Value<string>("path")!;
        var value = spec["value"]?.ToString();
        var tokens = payload.SelectTokens(path).ToList();

        foreach (var t in tokens)
        {
            if (t is JArray arr)
            {
                if (arr.Any(x => string.Equals(x.ToString(), value, StringComparison.OrdinalIgnoreCase)))
                    return true;
                // arrays de arrays
                if (arr.Any(x => x is JArray inner && inner.Any(y => string.Equals(y.ToString(), value, StringComparison.OrdinalIgnoreCase))))
                    return true;
            }
            else
            {
                // item único igual ao value
                if (string.Equals(t.ToString(), value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Suporte a sufixo ".length()" em paths. Ex.: "$.caPolicies[*].conditions.users.excludeUsers.length()"
    /// </summary>
    private static List<JToken> SelectWithLengthSupport(JToken payload, string path, out bool isLength, out int length)
    {
        isLength = false; length = 0;
        const string lenSuffix = ".length()";
        if (path.EndsWith(lenSuffix, StringComparison.Ordinal))
        {
            isLength = true;
            var trimmed = path.Substring(0, path.Length - lenSuffix.Length);
            var toks = payload.SelectTokens(trimmed).ToList();
            if (toks.Count == 1 && toks[0] is JArray arr) length = arr.Count;
            else length = toks.Count;
            return toks;
        }
        return payload.SelectTokens(path).ToList();
    }
}