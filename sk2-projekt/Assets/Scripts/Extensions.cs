using System;
using UnityEngine;

public static class Extensions
{
    public static Vector2Int ToServer(this Vector3 position)
    {
        return new Vector2Int (Mathf.RoundToInt(position.x * 100), Mathf.RoundToInt(position.y * 100));
    }

    public static Vector3 ToHost(this Vector2Int position) {
        return new Vector3(position.x / 100f, position.y / 100f);
    }
}