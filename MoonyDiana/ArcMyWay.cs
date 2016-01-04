using System;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Menu.Values;
using SharpDX;

namespace MoonyDiana
{
    /// <summary>
    /// Probably only valid for diana
    /// </summary>
    public class ArcMyWay
    {
        public static Vector2 PointOnCircle(float radius, float angleInDegrees, Vector2 origin)
        {
            float x = origin.X + (float)(radius * System.Math.Cos(angleInDegrees * Math.PI / 180));
            float y = origin.Y + (float)(radius * System.Math.Sin(angleInDegrees * Math.PI / 180));

            return new Vector2(x, y);
        }

        private int CircleLineSegmentN
        {
            // ReSharper disable once ConvertPropertyToExpressionBody
            get { return config.miscMenu.Get<Slider>("advancedQPolygonWidth").CurrentValue; }
        }

        private Vector2[] CircleCircleIntersection(Vector2 center1, Vector2 center2, float radius1, float radius2)
        {
            float num1 = center1.Distance(center2);
            if ((double)num1 > (double)radius1 + (double)radius2 || (double)num1 <= (double)Math.Abs(radius1 - radius2))
                return new Vector2[0];
            float num2 = (float)(((double)radius1 * (double)radius1 - (double)radius2 * (double)radius2 + (double)num1 * (double)num1) / (2.0 * (double)num1));
            float num3 = (float)Math.Sqrt((double)radius1 * (double)radius1 - (double)num2 * (double)num2);
            Vector2 v = (center2 - center1).Normalized();
            Vector2 vector2_1 = center1 + num2 * v;
            Vector2 vector2_2 = num3 * v.Perpendicular();
            Vector2 vector2_3 = vector2_1 + vector2_2;
            Vector2 vector2_4 = num3 * v.Perpendicular();
            Vector2 vector2_5 = vector2_1 - vector2_4;
            return new[]
            {
                vector2_3,
                vector2_5
            };
        }

        public Vector2 Start { get; private set; }
        public Vector2 End { get; private set; }

        public int HitBox { get; private set; }
        private float Distance { get; set; }
        public ArcMyWay(Vector2 start, Vector2 end, int hitbox)
        {
            Start = start;
            End = end;
            HitBox = hitbox;
            Distance = Start.Distance(End);
        }

        /// <summary>
        /// Returns polygon + circleCenter
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        public Tuple<Geometry.Polygon,Vector2> ToPolygonEx(int offset = 0)
        {
            offset += HitBox;
            var result = new Geometry.Polygon();

            var innerRadius = -0.1562f * Distance + 687.31f;
            var outerRadius = 0.35256f * Distance + 133f;

            outerRadius = outerRadius / (float)Math.Cos(2 * Math.PI / CircleLineSegmentN);

            var innerCenters = CircleCircleIntersection(Start, End, innerRadius, innerRadius);
            var outerCenters = CircleCircleIntersection(Start, End, outerRadius, outerRadius);

            var innerCenter = innerCenters.FirstOrDefault();
            var outerCenter = outerCenters.FirstOrDefault();

            if (innerCenters.Count() == 0 || outerCenters.Count() == 0)
            {
                return new Tuple<Geometry.Polygon, Vector2>(null, new Vector2());
            }

            var a = new Vector2(End.X + 0, End.Y + 100);
            //Render.Circle.DrawCircle(innerCenter.To3D(), 100, Color.White);

            var direction = (End - outerCenter).Normalized();
            var end = (Start - outerCenter).Normalized();
            var maxAngle = (float)(direction.AngleBetween(end) * Math.PI / 180);

            var step = -maxAngle / CircleLineSegmentN;
            //outercircle
            for (int i = 0; i < CircleLineSegmentN; i++)
            {
                var angle = step * i;
                var point = outerCenter + (outerRadius + 15 + offset) * direction.Rotated(angle);
                result.Add(point);
            }

            direction = (Start - innerCenter).Normalized();
            end = (End - innerCenter).Normalized();
            maxAngle = (float)(direction.AngleBetween(end) * Math.PI / 180);
            step = maxAngle / CircleLineSegmentN;
            //outercircle
            for (int i = 0; i < CircleLineSegmentN; i++)
            {
                var angle = step * i;
                var point = innerCenter + Math.Max(0, innerRadius - offset - 100) * direction.Rotated(angle);
                result.Add(point);
            }

            return new Tuple<Geometry.Polygon, Vector2>(result, a);
        }
    }
}
