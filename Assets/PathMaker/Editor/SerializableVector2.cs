using System;
using UnityEngine;

namespace PathMaker
{
    [Serializable]
    public struct SerializableVector2
    {
        public float x;
        public float y;

        public float sqrMagnitude { get { return new Vector2(x, y).sqrMagnitude; } }

        public SerializableVector2(float _x, float _y)
        {
            x = _x;
            y = _y;
        }

        public static implicit operator Vector2(SerializableVector2 v)
        {
            return new Vector2(v.x, v.y);
        }

        public static implicit operator SerializableVector2(Vector2 v)
        {
            return new SerializableVector2(v.x, v.y);
        }

        public static implicit operator Vector3(SerializableVector2 v)
        {
            return new Vector3(v.x, v.y, 0);
        }

        public static Vector2 operator *(float v1, SerializableVector2 v2)
        {
            return v1 * (Vector2)v2;
        }

        public static Vector2 operator *(SerializableVector2 v1, float v2)
        {
            return (Vector2)v1 * v2;
        }

        public static Vector2 operator -(Vector2 v1, SerializableVector2 v2)
        {
            return v1 - (Vector2)v2;
        }
    }
}
