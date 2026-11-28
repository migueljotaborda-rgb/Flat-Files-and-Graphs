using System.Globalization;

namespace Backend;

public sealed class Graph
{
    public Dictionary<string, List<Edge>> Adjacency { get; } = new(StringComparer.Ordinal);
    public int StreetCount { get; private set; }
    public int OneWayCount { get; private set; }

    public void AddVertex(string name) { if (!Adjacency.ContainsKey(name)) Adjacency[name] = new(); }
    public void AddEdge(string from, string to, int meters)
    {
        AddVertex(from); AddVertex(to); Adjacency[from].Add(new Edge(to, meters));
    }

    public static Graph LoadCity(string path)
    {
        var graph = new Graph();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int lineNo = 0;
        foreach (var raw in File.ReadLines(path))
        {
            lineNo++;
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            var parts = line.Split(',').Select(p => p.Trim()).ToArray();
            if (parts.Length != 4) throw new FormatException($"city.txt línea {lineNo}: se esperaban 4 campos.");
            if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int meters) || meters <= 0)
                throw new FormatException($"city.txt línea {lineNo}: la distancia debe ser un entero positivo.");
            var direction = parts[3];
            if (!direction.Equals("TwoWay", StringComparison.OrdinalIgnoreCase) && !direction.Equals("OneWay", StringComparison.OrdinalIgnoreCase))
                throw new FormatException($"city.txt línea {lineNo}: sentido inválido '{direction}'.");
            string streetKey = string.Compare(parts[0], parts[1], StringComparison.Ordinal) < 0
                ? parts[0] + "|" + parts[1] : parts[1] + "|" + parts[0];
            if (!seen.Add(streetKey)) throw new FormatException($"city.txt línea {lineNo}: calle duplicada.");
            graph.AddEdge(parts[0], parts[1], meters);
            graph.StreetCount++;
            if (direction.Equals("TwoWay", StringComparison.OrdinalIgnoreCase)) graph.AddEdge(parts[1], parts[0], meters);
            else graph.OneWayCount++;
        }
        return graph;
    }

    public Dictionary<string, (long Distance, string[] Path)> Dijkstra(string source)
    {
        var distances = Adjacency.Keys.ToDictionary(v => v, _ => long.MaxValue, StringComparer.Ordinal);
        var previous = new Dictionary<string, string>(StringComparer.Ordinal);
        var queue = new PriorityQueue<string, long>();
        distances[source] = 0; queue.Enqueue(source, 0);
        while (queue.TryDequeue(out string? current, out long currentDist))
        {
            if (current is null || currentDist != distances[current]) continue;
            foreach (var edge in Adjacency[current])
            {
                long candidate = currentDist + edge.Meters;
                if (candidate < distances[edge.To])
                {
                    distances[edge.To] = candidate; previous[edge.To] = current;
                    queue.Enqueue(edge.To, candidate);
                }
            }
        }
        var result = new Dictionary<string, (long, string[])>(StringComparer.Ordinal);
        foreach (var target in Adjacency.Keys)
        {
            if (distances[target] == long.MaxValue) { result[target] = (long.MaxValue, Array.Empty<string>()); continue; }
            var path = new List<string>(); string cursor = target; path.Add(cursor);
            while (cursor != source && previous.TryGetValue(cursor, out var parent)) { cursor = parent; path.Add(cursor); }
            path.Reverse(); result[target] = (distances[target], path.ToArray());
        }
        return result;
    }
}
public sealed record Edge(string To, int Meters);