using UnityEngine;
using System.Collections.Generic;

public class FloorManager : MonoBehaviour
{
    public static FloorManager Instance;

    [System.Serializable]
    public class Floor
    {
        public int floorId;
        public List<Transform> skeletons = new List<Transform>();
        public List<Stairs> connectedStairs = new List<Stairs>();
    }

    public List<Floor> floors = new List<Floor>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public Floor GetFloor(int floorId)
    {
        return floors.Find(f => f.floorId == floorId);
    }

    public int GetKnightFloor(Transform knight)
    {
        int bestFloor = 0;
        float minDiff = Mathf.Infinity;
        foreach (var floor in floors)
        {
            float avgY = GetFloorAverageY(floor);
            float diff = Mathf.Abs(knight.position.y - avgY);
            if (diff < minDiff)
            {
                minDiff = diff;
                bestFloor = floor.floorId;
            }
        }
        return bestFloor;
    }

    float GetFloorAverageY(Floor floor)
    {
        float sumY = 0;
        int count = 0;
        foreach (var s in floor.skeletons) { if (s != null) { sumY += s.position.y; count++; } }
        foreach (var st in floor.connectedStairs) { if (st != null) { sumY += st.transform.position.y; count++; } }
        return count > 0 ? sumY / count : 0f;
    }

    public void RegisterSkeleton(Transform skeleton, int floorId)
    {
        var floor = GetFloor(floorId);
        if (floor != null && !floor.skeletons.Contains(skeleton))
        {
            floor.skeletons.Add(skeleton);
        }
    }

    public void UnregisterSkeleton(Transform skeleton)
    {
        foreach (var floor in floors)
        {
            if (floor.skeletons.Contains(skeleton))
            {
                floor.skeletons.Remove(skeleton);
                break;
            }
        }
    }

    public bool HasAnySkeletonsLeft()
    {
        foreach (var floor in floors)
        {
            floor.skeletons.RemoveAll(s => s == null);
            if (floor.skeletons.Count > 0) return true;
        }
        return false;
    }

    public List<int> FindPathToFloor(int startFloor, int targetFloor)
    {
        Queue<int> queue = new Queue<int>();
        Dictionary<int, int> parentMap = new Dictionary<int, int>();
        HashSet<int> visited = new HashSet<int>();

        queue.Enqueue(startFloor);
        visited.Add(startFloor);

        bool found = false;
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            if (current == targetFloor) { found = true; break; }

            var floor = GetFloor(current);
            if (floor != null)
            {
                foreach (var stair in floor.connectedStairs)
                {
                    int nextFloor = stair.GetOtherFloor(current);
                    if (!visited.Contains(nextFloor))
                    {
                        visited.Add(nextFloor);
                        parentMap[nextFloor] = current;
                        queue.Enqueue(nextFloor);
                    }
                }
            }
        }

        List<int> path = new List<int>();
        if (!found) return path;

        int curr = targetFloor;
        while (curr != startFloor)
        {
            path.Insert(0, curr);
            curr = parentMap[curr];
        }
        return path;
    }
}