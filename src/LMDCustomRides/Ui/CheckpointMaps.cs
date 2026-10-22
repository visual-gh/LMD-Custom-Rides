using System.Globalization;
using System.Reflection;
using Il2CppMegagon.Downhill.UI.Screens;
using Il2CppTMPro;
using LMDCustomRides.Ui.Kit;
using UnityEngine;

namespace LMDCustomRides.Ui;

/// <summary>
/// Checkpoint trees of every trail (Data\checkpoints.txt, read once from the trail scenes with a dev-only recon mod):
/// the standard checkpoints and all alternative ones with their positions. The menu map only has markers for the
/// standard checkpoints, so the alternative ones are placed on the map by fitting the standard checkpoints' world
/// positions to their markers' dots.
/// </summary>
internal static class CheckpointMaps
{
    private sealed class Trail
    {
        public readonly List<Vector2> Standard = new();
        public readonly List<Vector2> Nodes = new();
        public readonly List<int[]> Children = new();
        public int Root;
    }

    private static Dictionary<string, Trail> _trails;
    private static readonly HashSet<string> Logged = new();

    private static Dictionary<string, Trail> Trails => _trails ??= Load();

    private static Dictionary<string, Trail> Load()
    {
        var trails = new Dictionary<string, Trail>();
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LMDCustomRides.Data.checkpoints.txt");
            if (stream == null)
            {
                return trails;
            }

            using var reader = new StreamReader(stream);
            Trail current = null;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    continue;
                }

                switch (parts[0])
                {
                    case "route":
                        current = new Trail();
                        trails[parts[1]] = current;
                        break;
                    case "std" when current != null:
                        current.Standard.Add(new Vector2(F(parts[1]), F(parts[2])));
                        break;
                    case "node" when current != null:
                        current.Nodes.Add(new Vector2(F(parts[1]), F(parts[2])));
                        current.Children.Add(parts.Length > 3 && parts[3].Length > 0
                            ? parts[3].Split(',').Select(int.Parse).ToArray()
                            : Array.Empty<int>());
                        break;
                    case "root" when current != null:
                        current.Root = int.Parse(parts[1]);
                        break;
                }
            }
        }
        catch (Exception e)
        {
            Mod.Log.Warning("checkpoint data could not be read: " + e.Message);
        }

        return trails;
    }

    private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    public static bool Has(string route) => route != null && Trails.ContainsKey(route);

    /// <summary>
    /// The checkpoints a path picks, as the game does: from the start, each checkpoint with next checkpoints takes the
    /// next digit d and goes to child round((count - 1) * d / 9).
    /// </summary>
    private static List<Vector2> Route(Trail trail, string path)
    {
        var points = new List<Vector2>();
        var node = trail.Root;
        var digit = 0;
        while (node >= 0 && node < trail.Nodes.Count)
        {
            points.Add(trail.Nodes[node]);
            var children = trail.Children[node];

            // The game stops at the finish, or after ten checkpoints.
            if (children.Length == 0 || points.Count == 10)
            {
                break;
            }

            var d = digit < path.Length ? path[digit] - '0' : 0;
            digit++;
            var index = (int)Math.Round((children.Length - 1) * d / 9.0, MidpointRounding.ToEven);
            node = children[Math.Clamp(index, 0, children.Length - 1)];
        }

        return points;
    }

    /// <summary>
    /// Draws the route's checkpoints (not start and finish) on the map trail, as copies of its first checkpoint marker.
    /// </summary>
    public static void Draw(MapTrail map, string routeId, string path, List<GameObject> drawn)
    {
        if (!Trails.TryGetValue(routeId, out var trail) || map.m_mapCheckpoints == null)
        {
            return;
        }

        // The map's checkpoint markers ("CP 1" ...) paired with the standard checkpoints' positions.
        var world = new List<Vector2>();
        var screen = new List<Vector2>();
        MapPoint template = null;
        // The labels are filled in by the game for trails it shows checkpoints for; otherwise the markers are still in
        // order (start, checkpoints, finish).
        var markers = map.m_mapCheckpoints;
        var inOrder = markers.Count == trail.Standard.Count;
        var numbered = false;
        for (var k = 0; k < markers.Count; k++)
        {
            var point = markers[k];
            if (point == null)
            {
                continue;
            }

            var label = point.m_mapPointLabel?.text;
            var digits = label == null ? "" : new string(label.Where(char.IsDigit).ToArray());
            int n;
            if (digits.Length > 0 && int.TryParse(digits, out n))
            {
                numbered = true;
            }
            else if (inOrder)
            {
                n = k;
            }
            else
            {
                continue;
            }

            if (n < 0 || n >= trail.Standard.Count)
            {
                continue;
            }

            if (template == null && n > 0 && n < trail.Standard.Count - 1)
            {
                template = point;
            }

            world.Add(trail.Standard[n]);
            // The dot's place, in the map's own space: markers can sit under positioned parents of their own, and a
            // marker with its label on the left has its dot well right of its own position.
            screen.Add(map.transform.InverseTransformPoint(Dot(point).position));
        }

        if (template == null || world.Count < 3 || !Fit(world, screen, out var fit))
        {
            if (Logged.Add(routeId))
            {
                var labels = new List<string>();
                foreach (var point in map.m_mapCheckpoints)
                {
                    labels.Add(point?.m_mapPointLabel?.text ?? "null");
                }

                Mod.Log.Msg($"map preview: {routeId} has {world.Count} usable markers ({string.Join(", ", labels)}), {trail.Standard.Count} standard checkpoints");
            }

            return;
        }

        var depth = map.transform.InverseTransformPoint(Dot(template).position).z;
        var toDot = Dot(template).position - template.transform.position;
        var route = Route(trail, path);
        for (var i = 1; i < route.Count - 1; i++)
        {
            var marker = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent, false);
            marker.name = "LMDCustomRides_CP" + i;
            var copy = marker.GetComponent<MapPoint>();
            var label = copy.m_mapPointLabel;
            var bestTime = copy.m_bestTimeBackground;
            UiTools.StripBehaviours(marker);
            if (bestTime != null)
            {
                bestTime.gameObject.SetActive(false);
            }

            if (label != null)
            {
                label.text = numbered ? WithNumber(label.text, i) : "CP " + i;
            }

            var at = Apply(fit, route[i]);
            marker.transform.position = map.transform.TransformPoint(new Vector3(at.x, at.y, depth)) - toDot;
            marker.SetActive(true);
            drawn.Add(marker);
        }
    }

    /// <summary>A marker's dot (its "Marker" child), where the checkpoint is.</summary>
    private static Transform Dot(MapPoint point) => point.transform.Find("Marker") ?? point.transform;

    /// <summary>"CP 3" with the number swapped, so the game's own wording and language stay.</summary>
    private static string WithNumber(string text, int number) =>
        System.Text.RegularExpressions.Regex.Replace(text, @"\d+", number.ToString());

    /// <summary>Least-squares affine map from world (x, z) to map positions: u = a x + b z + c, v = d x + e z + f.</summary>
    private static bool Fit(List<Vector2> world, List<Vector2> map, out double[] fit)
    {
        fit = null;
        // Normal equations for [x z 1] -> u and -> v.
        var m = new double[3, 3];
        var bu = new double[3];
        var bv = new double[3];
        for (var i = 0; i < world.Count; i++)
        {
            var row = new double[] { world[i].x, world[i].y, 1 };
            for (var r = 0; r < 3; r++)
            {
                for (var c = 0; c < 3; c++)
                {
                    m[r, c] += row[r] * row[c];
                }

                bu[r] += row[r] * map[i].x;
                bv[r] += row[r] * map[i].y;
            }
        }

        if (!Solve(m, bu, out var u) || !Solve(m, bv, out var v))
        {
            return false;
        }

        fit = new[] { u[0], u[1], u[2], v[0], v[1], v[2] };
        return true;
    }

    private static Vector2 Apply(double[] f, Vector2 p) =>
        new((float)(f[0] * p.x + f[1] * p.y + f[2]), (float)(f[3] * p.x + f[4] * p.y + f[5]));

    private static bool Solve(double[,] a, double[] b, out double[] x)
    {
        x = new double[3];
        var det = Det(a);
        if (Math.Abs(det) < 1e-9)
        {
            return false;
        }

        for (var col = 0; col < 3; col++)
        {
            var m = (double[,])a.Clone();
            for (var r = 0; r < 3; r++)
            {
                m[r, col] = b[r];
            }

            x[col] = Det(m) / det;
        }

        return true;
    }

    private static double Det(double[,] m) =>
        m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1]) -
        m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0]) +
        m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);
}
