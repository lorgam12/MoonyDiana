﻿using System;
using System.Collections.Generic;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using SharpDX;

namespace EvadePlus
{
    public class EvadePlus
    {
        public int ServerTimeBuffer
        {
            get { return 65; }
        }

        public bool EvadeEnabled
        {
            get { return true; }
        }

        public bool DodgeDangerousOnly
        {
            get { return false; }
        }

        public int ExtraEvadeRange
        {
            get { return 0; }
        }

        public bool RandomizeExtraEvadeRange
        {
            get { return false; }
        }

        public bool AllowRecalculateEvade
        {
            get { return false; }
        }

        public bool RestorePosition
        {
            get { return false; }
        }

        public bool DisableDrawings
        {
            get { return true; }
        }

        public bool DrawEvadePoint
        {
            get { return false; }
        }

        public bool DrawEvadeStatus
        {
            get { return false; }
        }

        public bool DrawDangerPolygon
        {
            get { return false; }
        }

        public int IssueOrderTickLimit
        {
            get { return 90; }
        }

        public SkillshotDetector SkillshotDetector { get; private set; }

        public EvadeSkillshot[] Skillshots { get; private set; }
        public Geometry.Polygon[] Polygons { get; private set; }
        public List<Geometry.Polygon> ClippedPolygons { get; private set; }
        public Vector2 LastIssueOrderPos;

        private readonly Dictionary<EvadeSkillshot, Geometry.Polygon> _skillshotPolygonCache =
            new Dictionary<EvadeSkillshot, Geometry.Polygon>();

        public EvadeResult LastEvadeResult;
        private int EvadeIssurOrderTime;

        public EvadePlus(SkillshotDetector detector)
        {
            Skillshots = new EvadeSkillshot[] {};
            Polygons = new Geometry.Polygon[] {};
            ClippedPolygons = new List<Geometry.Polygon>();

            SkillshotDetector = detector;
            SkillshotDetector.OnUpdateSkillshots += OnUpdateSkillshots;
            SkillshotDetector.OnSkillshotActivation += OnSkillshotActivation;
            SkillshotDetector.OnSkillshotDetected += OnSkillshotDetected;

            Player.OnIssueOrder += PlayerOnIssueOrder;
        }
        private void CacheSkillshots()
        {
            Skillshots =
                (DodgeDangerousOnly
                    ? SkillshotDetector.ActiveSkillshots.Where(c => c.SpellData.IsDangerous)
                    : SkillshotDetector.ActiveSkillshots).ToArray();

            _skillshotPolygonCache.Clear();
            Polygons = Skillshots.Select(c =>
            {
                var pol = c.ToPolygon();
                _skillshotPolygonCache.Add(c, pol);

                return pol;
            }).ToArray();
            ClippedPolygons = Geometry.ClipPolygons(Polygons).ToPolygons();
        }

        private void OnUpdateSkillshots(EvadeSkillshot skillshot, bool remove, bool isProcessSpell)
        {
            CacheSkillshots();
        }

        private void OnSkillshotActivation(EvadeSkillshot skillshot)
        {
            CacheSkillshots();
        }

        private void OnSkillshotDetected(EvadeSkillshot skillshot, bool isProcessSpell)
        {
            //TODO: update
            if (skillshot.ToPolygon().IsInside(Player.Instance))
            {
                LastEvadeResult = null;
            }
        }

        private void PlayerOnIssueOrder(Obj_AI_Base sender, PlayerIssueOrderEventArgs args)
        {
            if (!sender.IsMe)
            {
                return;
            }

            if (args.Order == GameObjectOrder.AttackUnit)
            {
                LastIssueOrderPos =
                    (Player.Instance.Distance(args.Target, true) >=
                     Player.Instance.GetAutoAttackRange(args.Target as AttackableUnit).Pow()
                        ? args.Target.Position
                        : Player.Instance.Position).To2D();
            }
            else
            {
                LastIssueOrderPos = (args.Target != null ? args.Target.Position : args.TargetPosition).To2D();
            }
        }

        public bool IsPointSafe(Vector2 point)
        {
            return !ClippedPolygons.Any(p => p.IsInside(point));
        }

        public bool IsPathSafe(Vector2[] path)
        {
            for (var i = 0; i < path.Length - 1; i++)
            {
                var start = path[i];
                var end = path[i + 1];

                if (
                    ClippedPolygons.Any(
                        p => p.IsInside(end) || p.IsInside(start) || p.IsIntersectingWithLineSegment(start, end)))
                {
                    return false;
                }
            }

            return true;
        }

        public bool IsPathSafe(Vector3[] path)
        {
            return IsPathSafe(path.ToVector2());
        }

        public bool IsHeroInDanger(AIHeroClient hero = null)
        {
            hero = hero ?? Player.Instance;
            return !IsPointSafe(hero.ServerPosition.To2D());
        }

        public int GetTimeAvailable(AIHeroClient hero = null)
        {
            hero = hero ?? Player.Instance;
            var skillshots = Skillshots.Where(c => _skillshotPolygonCache[c].IsInside(hero.Position)).ToArray();

            if (!skillshots.Any())
                return short.MaxValue;

            var times =
                skillshots.Select(c => c.GetAvailableTime(hero))
                    .Where(t => t > 0)
                    .OrderByDescending(t => t);

            return times.Any() ? times.Last() : short.MaxValue;
        }

        public int GetDangerValue(AIHeroClient hero = null)
        {
            hero = hero ?? Player.Instance;
            var skillshots = Skillshots.Where(c => _skillshotPolygonCache[c].IsInside(hero.Position)).ToArray();

            if (!skillshots.Any())
                return 0;

            var values = skillshots.Select(c => c.SpellData.DangerValue).OrderByDescending(t => t);
            return values.Any() ? values.First() : 0;
        }

        public EvadeResult CalculateEvade(Vector2 anchor)
        {
            var playerPos = Player.Instance.ServerPosition.To2D();
            var polygons = ClippedPolygons.Where(p => p.IsInside(playerPos)).ToArray();
            var maxTime = GetTimeAvailable();
            var time = Math.Max(0, maxTime - (Game.Ping + ServerTimeBuffer));
            var moveRadius = (0.8F*time/1000F)*Player.Instance.MoveSpeed;
            var segments = new List<Vector2[]>();

            foreach (var pol in polygons)
            {
                for (var i = 0; i < pol.Points.Count; i++)
                {
                    var start = pol.Points[i];
                    var end = i == pol.Points.Count - 1 ? pol.Points[0] : pol.Points[i + 1];

                    var intersections =
                        Utils.GetLineCircleIntersectionPoints(playerPos, moveRadius, start, end)
                            .Where(p => p.IsInLineSegment(start, end))
                            .ToList();

                    if (intersections.Count == 0)
                    {
                        if (start.Distance(playerPos, true) < moveRadius.Pow() &&
                            end.Distance(playerPos, true) < moveRadius.Pow())
                        {
                            intersections = new[] {start, end}.ToList();
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else if (intersections.Count == 1)
                    {
                        intersections.Add(playerPos.Distance(start, true) > playerPos.Distance(end, true)
                            ? end
                            : start);
                    }

                    segments.Add(intersections.ToArray());
                }
            }

            if (!segments.Any()) //not enough time
            {
                var point = GetClosestPoint(polygons, playerPos);
                return new EvadeResult(this, point, anchor, maxTime, time,
                    point.Distance(playerPos, true) <= (moveRadius + 10).Pow());
            }

            const int maxdist = 1500;
            const int division = 35;
            var points = new List<Vector2>();

            foreach (var segment in segments)
            {
                var dist = segment[0].Distance(segment[1]);
                if (dist > maxdist)
                {
                    segment[0] = segment[0].Extend(segment[1], dist/2 - maxdist/2);
                    segment[1] = segment[1].Extend(segment[1], dist/2 - maxdist/2);
                    dist = maxdist;
                }

                var step = maxdist/division;
                var count = dist/step;

                for (var i = 0; i < count; i++)
                {
                    var point = segment[0].Extend(segment[1], i*step);
                    if (!point.IsWall() &&
                        !Polygons.Where(pol => !pol.IsInside(playerPos))
                            .Any(pol => pol.IsIntersectingWithLineSegment(playerPos, point.Extend(playerPos, -50))) &&
                        Player.Instance.GetPath(point.To3DWorld(), true).Length <= 3)
                    {
                        points.Add(point);
                    }
                }
            }

            if (!points.Any())
            {
                return new EvadeResult(this, GetClosestPoint(polygons, playerPos), anchor, maxTime, time, true);
            }

            var evadePoint = points.OrderByDescending(p => p.Distance(anchor) + p.Distance(playerPos)).Last();
            return new EvadeResult(this, evadePoint, anchor, maxTime, time, true);
        }

        public Vector2 GetClosestPoint(Geometry.Polygon[] polygons, Vector2 from)
        {
            var polPoints =
                polygons.Select(pol => pol.ToDetailedPolygon())
                    .SelectMany(pol => pol.Points)
                    .OrderByDescending(p => p.Distance(from, true));

            return !polPoints.Any() ? Vector2.Zero : polPoints.Last();
        }

        public bool IsHeroPathSafe(EvadeResult evade, Vector3[] desiredPath, AIHeroClient hero = null)
        {
            return false; //temporarily disabled

            hero = hero ?? Player.Instance;

            var path = (desiredPath ?? hero.RealPath()).ToVector2();
            var polygons = ClippedPolygons;
            var points = new List<Vector2>();

            for (var i = 0; i < path.Length - 1; i++)
            {
                var start = path[i];
                var end = path[i + 1];

                foreach (var pol in polygons)
                {
                    var intersections = pol.GetIntersectionPointsWithLineSegment(start, end);
                    if (intersections.Length > 0 && !pol.IsInside(hero))
                    {
                        return false;
                    }

                    points.AddRange(intersections);
                }
            }

            if (points.Count == 1)
            {
                var walkTime = hero.WalkingTime(points[0]);
                return walkTime <= evade.TimeAvailable;
            }

            return false;
        }

        public class EvadeResult
        {
            private readonly EvadePlus Evade;
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

            public EvadeResult(EvadePlus evade, Vector2 evadePoint, Vector2 anchorPoint, int totalTimeAvailable,
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
}