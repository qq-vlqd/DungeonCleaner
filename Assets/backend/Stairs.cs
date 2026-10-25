using UnityEngine;

public class Stairs : MonoBehaviour
{
    public int floorA;
    public int floorB;
    public Transform topPoint;
    public Transform bottomPoint;

    public int GetOtherFloor(int currentFloor)
    {
        if (currentFloor == floorA) return floorB;
        if (currentFloor == floorB) return floorA;
        return -1;
    }
}