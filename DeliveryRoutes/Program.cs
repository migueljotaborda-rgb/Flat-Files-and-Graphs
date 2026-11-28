using Backend;

static List<Package> LoadPackages(string path, Graph graph)
{
    var tasks = new Dictionary<int, (string? Pickup, string? Delivery)>();
    int lineNo = 0;
    foreach (var raw in File.ReadLines(path))
    {
        lineNo++; var line = raw.Trim();
        if (line.Length == 0 || line.StartsWith("#")) continue;
        var parts = line.Split(',').Select(p => p.Trim()).ToArray();
        if (parts.Length != 3) throw new FormatException($"packages.txt línea {lineNo}: se esperaban 3 campos.");
        if (!graph.Adjacency.ContainsKey(parts[0])) throw new FormatException($"packages.txt línea {lineNo}: ubicación '{parts[0]}' no existe.");
        if (!int.TryParse(parts[2], out int id) || id <= 0) throw new FormatException($"packages.txt línea {lineNo}: ID inválido.");
        bool pickup = parts[1].Equals("Pickup", StringComparison.OrdinalIgnoreCase);
        bool delivery = parts[1].Equals("Delivery", StringComparison.OrdinalIgnoreCase);
        if (!pickup && !delivery) throw new FormatException($"packages.txt línea {lineNo}: acción inválida.");
        tasks.TryGetValue(id, out var task);
        if (pickup && task.Pickup != null) throw new FormatException($"packages.txt línea {lineNo}: recogida repetida del paquete {id}.");
        if (delivery && task.Delivery != null) throw new FormatException($"packages.txt línea {lineNo}: entrega repetida del paquete {id}.");
        tasks[id] = pickup ? (parts[0], task.Delivery) : (task.Pickup, parts[0]);
    }
    var result = new List<Package>();
    foreach (var (id, task) in tasks.OrderBy(x => x.Key))
    {
        if (task.Pickup == null || task.Delivery == null) throw new FormatException($"Paquete {id}: falta recogida o entrega.");
        result.Add(new Package(id, task.Pickup, task.Delivery));
    }
    return result;
}

static void PrintRoute(Graph graph, RoutePlanner planner, List<Package> packages, List<Stop> route, string start)
{
    Console.WriteLine($"Posición inicial: {start}");
    Console.WriteLine($"Paquetes: {packages.Count}   Ubicaciones: {graph.Adjacency.Count}   Calles: {graph.StreetCount} ({graph.OneWayCount} de un solo sentido)");
    string current = start; long total = 0; int n = 0;
    foreach (var group in route.GroupBy(s => s.Location))
    {
        // Route stops are grouped only when consecutive tasks share the same location.
        var stops = group.ToList(); string target = stops[0].Location;
        var (distance, path) = planner.PathBetween(current, target);
        total += distance; n++;
        var drops = stops.Where(s => s.Kind == StopKind.Delivery).Select(s => s.PackageId).Order().ToList();
        var picks = stops.Where(s => s.Kind == StopKind.Pickup).Select(s => s.PackageId).Order().ToList();
        string action = string.Join(" y ", new[] { drops.Count > 0 ? "entregar paquetes " + string.Join(", ", drops) : "", picks.Count > 0 ? "recoger paquete" + (picks.Count > 1 ? "s " : " ") + string.Join(", ", picks) : "" }.Where(x => x.Length > 0));
        Console.WriteLine($" {n}. Ir de {current} a {target} ({distance} m): {action}.");
        Console.WriteLine($"      Camino: {string.Join(" -> ", path)}");
        current = target;
    }
    Console.WriteLine("Todos los paquetes fueron recogidos y entregados.");
    Console.WriteLine($"Recorrido total: {total / 1000.0:F2} km");
}

try
{
    string cityFile, packagesFile, start;
    if (args.Length >= 3) { cityFile = args[0]; packagesFile = args[1]; start = args[2]; }
    else
    {
        Console.Write("Ruta de city.txt: "); cityFile = Console.ReadLine() ?? "city.txt";
        Console.Write("Ruta de packages.txt: "); packagesFile = Console.ReadLine() ?? "packages.txt";
        Console.Write("Posición inicial: "); start = (Console.ReadLine() ?? "D").Trim();
    }
    var city = Graph.LoadCity(cityFile);
    if (!city.Adjacency.ContainsKey(start)) throw new ArgumentException($"La ubicación inicial '{start}' no existe.");
    var packages = LoadPackages(packagesFile, city);
    var planner = new RoutePlanner(city, packages, start);
    var route = planner.Plan();
    PrintRoute(city, planner, packages, route, start);
}
catch (Exception ex)
{
    Console.Error.WriteLine("Error: " + ex.Message);
    Environment.ExitCode = 1;
}