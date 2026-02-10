using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace X_Plane2Influx
{
    public static class Airplane
    {
        public static bool IsValid { get; set; }
        public static float AirSpeed { get; set; }
        public static float GndSpeed { get; set; }
        public static float WndSpeed { get; set; }
        public static float CoordLat { get; set; }
        public static float CoordLng { get; set; }
        public static float Altitude { get; set; }
        public static float Heading { get; set; }
        public static float VSpeed { get; set; }
        public static float EngineRpm { get; set; }
        public static float Yaw { get; set; }
        public static float Pitch { get; set; }
        public static float Roll { get; set; }
        public static float HeadingTrue { get; set; }
        public static float HeadingMagn { get; set; }

    }
}
