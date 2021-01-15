using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace PathMaker
{
    [Serializable]
    public class PathData : ICloneable
    {
        public float Left = -3f;
        public float Right = 3f;
        public float Top = 3f;
        public float Bottom = -3f;

        public List<Path> Data = new List<Path>();
        public SerializableVector2 Offset;

        public object Clone()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(memoryStream, this);
                memoryStream.Position = 0;

                return formatter.Deserialize(memoryStream);
            }
        }
    }
}
