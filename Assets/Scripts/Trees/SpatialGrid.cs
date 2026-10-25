using System.Collections.Generic;
using UnityEngine;

// Things on the ground sorted into squares of the map, so "what is near this spot" only looks at a few squares
// instead of the whole island (thousands of trees and plants).
public class SpatialGrid<T> where T : class
{
    readonly float cellSize;
    readonly Dictionary<long, List<T>> cells = new Dictionary<long, List<T>>();

    public SpatialGrid(float cellSize) { this.cellSize = cellSize; }

    public float CellSize => cellSize;

    public long KeyOf(Vector3 p) => Key(Mathf.FloorToInt(p.x / cellSize), Mathf.FloorToInt(p.z / cellSize));

    static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

    public void Add(T item, Vector3 position)
    {
        long key = KeyOf(position);
        if (!cells.TryGetValue(key, out var list)) { list = new List<T>(8); cells[key] = list; }
        list.Add(item);
    }

    public void Remove(T item, Vector3 position)
    {
        if (cells.TryGetValue(KeyOf(position), out var list)) list.Remove(item);
    }

    public void Clear() => cells.Clear();

    // every item in the squares that touch the circle (some may be a bit outside it: check the distance)
    public void Query(Vector3 centre, float radius, List<T> result)
    {
        int x0 = Mathf.FloorToInt((centre.x - radius) / cellSize), x1 = Mathf.FloorToInt((centre.x + radius) / cellSize);
        int z0 = Mathf.FloorToInt((centre.z - radius) / cellSize), z1 = Mathf.FloorToInt((centre.z + radius) / cellSize);
        for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
                if (cells.TryGetValue(Key(x, z), out var list)) result.AddRange(list);
    }
}
