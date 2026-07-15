using System.Collections.Generic;
using System.IO;

namespace MissileDisaster.Core
{
    /// <summary>汚染ゾーン台帳を byte[] に直列化/復元（セーブデータ保存用）。NuclearMeltdown から移植。</summary>
    public static class ZoneSerializer
    {
        public const byte Version = 3; // v3: Intensity を float 化（端数保持）

        public static byte[] Serialize(List<ContaminationZone> zones)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Version);
                w.Write(zones.Count);
                for (int i = 0; i < zones.Count; i++)
                {
                    var z = zones[i];
                    w.Write(z.CenterX);
                    w.Write(z.CenterZ);
                    w.Write(z.Radius);
                    w.Write(z.StartTicks);
                    w.Write(z.Intensity); // float
                }
                w.Flush();
                return ms.ToArray();
            }
        }

        public static List<ContaminationZone> Deserialize(byte[] data)
        {
            var result = new List<ContaminationZone>();
            if (data == null || data.Length < 5) return result;
            try
            {
                using (var ms = new MemoryStream(data))
                using (var r = new BinaryReader(ms))
                {
                    byte version = r.ReadByte();
                    if (version != Version) return new List<ContaminationZone>();
                    int count = r.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        float cx = r.ReadSingle();
                        float cz = r.ReadSingle();
                        float radius = r.ReadSingle();
                        long start = r.ReadInt64();
                        float intensity = r.ReadSingle();
                        result.Add(new ContaminationZone(cx, cz, radius, start, intensity));
                    }
                }
            }
            catch
            {
                return new List<ContaminationZone>(); // 破損時は空
            }
            return result;
        }
    }
}
