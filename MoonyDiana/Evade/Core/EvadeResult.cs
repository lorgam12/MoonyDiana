using System;
using EloBuddy;
using EloBuddy.SDK;
using EvadePlus;
using SharpDX;

namespace MoonyDiana.Evade.Core
{
    public class EvadeResult
    {
        private readonly EvadePlus.EvadePlus Evade;
        private int ExtraRange { get; set; }

        public int Time { get; set; }
        public Vector2 PlayerPos { get; set; }
        public Vector2 EvadePoint { get; set; }
        public Vector2 AnchorPoint { get; set; }
        public int TimeAvailable { get; set; }
        public int TotalTimeAvailable { get; set; }
        public bool EnoughTime { get; set; }

        public bool IsValid
        {
            get { return !EvadePoint.IsZero; }
        }

        public Vector3 WalkPoint
        {
            get
            {
                var walkPoint = EvadePoint.Extend(PlayerPos, -60);
                var newPoint = walkPoint.Extend(PlayerPos, -ExtraRange);
                if (Evade.IsPointSafe(newPoint))
                {
                    return newPoint.To3DWorld();
                }

                return walkPoint.To3DWorld();
            }
        }

        public EvadeResult(EvadePlus.EvadePlus evade, Vector2 evadePoint, Vector2 anchorPoint, int totalTimeAvailable,
            int timeAvailable,
            bool enoughTime)
        {
            Evade = evade;
            PlayerPos = Player.Instance.Position.To2D();
            Time = Environment.TickCount;

            EvadePoint = evadePoint;
            AnchorPoint = anchorPoint;
            TotalTimeAvailable = totalTimeAvailable;
            TimeAvailable = timeAvailable;
            EnoughTime = enoughTime;

            // extra evade range
            if (Evade.ExtraEvadeRange > 0)
            {
                ExtraRange = (Evade.RandomizeExtraEvadeRange
                    ? Utils.Random.Next(Evade.ExtraEvadeRange / 3, Evade.ExtraEvadeRange)
                    : Evade.ExtraEvadeRange);
            }
        }

        public bool Expired(int time = 4000)
        {
            return Elapsed(time);
        }

        public bool Elapsed(int time)
        {
            return Elapsed() > time;
        }

        public int Elapsed()
        {
            return Environment.TickCount - Time;
        }
    }
}
