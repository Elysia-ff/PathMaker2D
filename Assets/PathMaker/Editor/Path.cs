using System;
using UnityEngine;

namespace PathMaker
{
    [Serializable]
    public class Path
    {
        public SerializableVector2 Pos;
        public bool IsAnchor;

        public Path(Vector2 _pos, bool _isAnchor = false)
        {
            Pos = _pos;
            IsAnchor = _isAnchor;
        }
    }
}
