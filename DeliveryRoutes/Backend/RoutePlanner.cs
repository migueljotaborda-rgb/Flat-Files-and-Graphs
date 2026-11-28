namespace Backend;

public readonly record struct Package(int Id, string Pickup, string Delivery);
public enum StopKind { Pickup, Delivery }
public readonly record struct Stop(string Location, StopKind Kind, int PackageId);

public sealed class RoutePlanner
{
    private readonly Graph graph;
    private readonly IReadOnlyList<Package> packages;
    private readonly string start;
    private readonly Dictionary<string, Dictionary<string, (long Distance, string[] Path)>> paths = new(StringComparer.Ordinal);
    private readonly HashSet<string> taskLocations;
    public RoutePlanner(Graph city, IReadOnlyList<Package> packages, string start)
    {
        graph = city; this.packages = packages; this.start = start;
        taskLocations = packages.SelectMany(p => new[] { p.Pickup, p.Delivery }).Append(start).Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        foreach (var source in taskLocations) paths[source] = city.Dijkstra(source);
        foreach (var source in taskLocations)
            foreach (var target in taskLocations)
                if (paths[source][target].Distance == long.MaxValue)
                    throw new InvalidOperationException($"No existe camino de {source} a {target}.");
    }

    public (long Distance, string[] Path) PathBetween(string from, string to) => paths[from][to];
    private long Segment(Stop a, Stop b) => paths[a.Location][b.Location].Distance;
    private long FromStart(Stop a) => paths[start][a.Location].Distance;

    public long TotalDistance(List<Stop> route)
    {
        long sum = 0; string current = start;
        foreach (var stop in route) { sum += paths[current][stop.Location].Distance; current = stop.Location; }
        return sum;
    }
    public bool IsValid(List<Stop> route)
    {
        var picked = new HashSet<int>(); var delivered = new HashSet<int>();
        var pids = packages.Select(p => p.Id).ToHashSet();
        foreach (var stop in route)
        {
            if (!pids.Contains(stop.PackageId)) return false;
            if (stop.Kind == StopKind.Pickup) { if (!picked.Add(stop.PackageId)) return false; }
            else if (!picked.Contains(stop.PackageId) || !delivered.Add(stop.PackageId)) return false;
        }
        return picked.Count == packages.Count && delivered.Count == packages.Count;
    }

    private long RouteCost(List<Stop> r) => TotalDistance(r);
    private static bool ValidOrder(List<Stop> r)
    {
        var seen = new HashSet<int>();
        foreach (var s in r) { if (s.Kind == StopKind.Pickup) seen.Add(s.PackageId); else if (!seen.Contains(s.PackageId)) return false; }
        return true;
    }

    private List<Stop> InsertPackages(IEnumerable<Package> order)
    {
        var route = new List<Stop>();
        foreach (var p in order)
        {
            var pick = new Stop(p.Pickup, StopKind.Pickup, p.Id);
            var drop = new Stop(p.Delivery, StopKind.Delivery, p.Id);
            long best = long.MaxValue; List<Stop>? bestRoute = null;
            for (int i = 0; i <= route.Count; i++)
            {
                for (int j = i + 1; j <= route.Count + 1; j++)
                {
                    var candidate = new List<Stop>(route); candidate.Insert(i, pick); candidate.Insert(j, drop);
                    long cost = RouteCost(candidate);
                    if (cost < best) { best = cost; bestRoute = candidate; }
                }
            }
            if (bestRoute != null) route = bestRoute;
        }
        return route;
    }

    private List<Stop> Improve(List<Stop> route)
    {
        bool improved = true; int rounds = 0;
        while (improved && rounds++ < 8)
        {
            improved = false; long currentCost = RouteCost(route);
            for (int idx = 0; idx < route.Count && !improved; idx++)
            {
                var item = route[idx]; var reduced = new List<Stop>(route); reduced.RemoveAt(idx);
                for (int pos = 0; pos <= reduced.Count; pos++)
                {
                    if (pos == idx) continue;
                    var candidate = new List<Stop>(reduced); candidate.Insert(pos, item);
                    if (!ValidOrder(candidate)) continue;
                    long cost = RouteCost(candidate);
                    if (cost < currentCost) { route = candidate; improved = true; break; }
                }
            }
        }
        return route;
    }

    public List<Stop> Plan()
    {
        List<Stop>? best = null; long bestCost = long.MaxValue;
        var rnd = new Random(2026);
        for (int attempt = 0; attempt < 24; attempt++)
        {
            var order = packages.OrderBy(_ => rnd.Next()).ToList();
            var candidate = Improve(InsertPackages(order));
            if (IsValid(candidate) && RouteCost(candidate) < bestCost) { best = candidate; bestCost = RouteCost(candidate); }
        }
        return best ?? throw new InvalidOperationException("No se pudo generar una ruta válida.");
    }
}