using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu.Values;
using EloBuddy.SDK.Rendering;
using EvadePlus;
using SharpDX;

namespace MoonyDiana
{
    class Main
    {
        private readonly EvadePlus.EvadePlus evadePlus;
        private readonly AIHeroClient me;

        private readonly int qRadius = 195;

#pragma warning disable 169
        private readonly string buff = "dianamoonlight";
#pragma warning restore 169

        private int qRange = 830;
        private int rRange = 825;
        private int eRange = 250;
        private int eDelay = 500;
        private int wRange = 200;
        private int qDelay = 250;

        private readonly Spell.Skillshot q = new Spell.Skillshot(SpellSlot.Q, 830, SkillShotType.Circular, 250, 1600);

        public Main()
        {
            Chat.Print("MoonyDiana loaded!");

            config.InitMenu();
            var skillshotDetector = new SkillshotDetector(DetectionTeam.AnyTeam);
            evadePlus = new EvadePlus.EvadePlus(skillshotDetector);

            me = ObjectManager.Player;
            Game.OnTick += GameOnTick;
            Drawing.OnDraw += DrawingOnDraw;
            EloBuddy.SDK.Events.Gapcloser.OnGapcloser += GapcloserOnOnGapcloser;
            EloBuddy.SDK.Events.Interrupter.OnInterruptableSpell += InterrupterOnOnInterruptableSpell;
            Player.OnSpellCast += PlayerOnOnSpellCast;
            Player.OnProcessSpellCast += PlayerOnOnProcessSpellCast;
        }

        private Vector2 start = new Vector2(), end = new Vector2();
        private void PlayerOnOnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe && args.Slot == SpellSlot.R)
            {
                if (config.comboMenu.Get<CheckBox>("useEWhileR").CurrentValue && ready(SpellSlot.E))
                    Player.CastSpell(SpellSlot.E);
            }

            if (sender.IsMe && args.Slot == SpellSlot.Q)
            {
                start = args.Start.To2D();
                end = args.End.To2D();
            }
        }

        private void PlayerOnOnSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (args.Slot == SpellSlot.E && config.comboMenu.Get<CheckBox>("blockE").CurrentValue)
            {
                bool any = false;
                foreach (AIHeroClient enemy in EntityManager.Heroes.Enemies)
                {
                    if (GetEnemyTimeOuttaE(enemy) > eDelay)
                        any = true;
                }
                if (!any)
                    args.Process = false;
            }

            if ((args.Slot == SpellSlot.Q || args.Slot == SpellSlot.E || args.Slot == SpellSlot.R) &&
                    Orbwalker.CanAutoAttack && EntityManager.Heroes.Enemies.Any(x => x.IsValid && !x.IsDead &&
                    x.Distance(me) <= me.AttackRange))
                args.Process = false;
        }

        private float GetEnemyTimeOuttaE(AIHeroClient enemy)
        {
            var enemyDestination = Prediction.Position.GetRealPath(enemy).Last();

            Geometry.Polygon.Circle circle = new Geometry.Polygon.Circle(me.Position, eRange);
            var intersection =
                circle.GetIntersectionPointsWithLineSegment(enemy.Position.To2D(), enemyDestination.To2D()).OrderBy(x => x.Distance(enemy))
                    .First();

            float distToCircleEnd = enemy.Distance(intersection);
            float dt = (distToCircleEnd / enemy.MoveSpeed) * 1000; //ms

            return dt;
        }

        private void InterrupterOnOnInterruptableSpell(Obj_AI_Base sender, Interrupter.InterruptableSpellEventArgs interruptableSpellEventArgs)
        {
            if (interruptableSpellEventArgs.Sender.Position.Distance(me) <= eRange && config.miscMenu.Get<CheckBox>("interruptE").CurrentValue)
                Player.CastSpell(SpellSlot.E);
        }

        private void GapcloserOnOnGapcloser(AIHeroClient sender, Gapcloser.GapcloserEventArgs gapcloserEventArgs)
        {
            if (gapcloserEventArgs.End.Distance(me) <= eRange && config.miscMenu.Get<CheckBox>("antiGapE").CurrentValue)
                Player.CastSpell(SpellSlot.E);
        }

        private bool ready(SpellSlot slot)
        {
            return Player.CanUseSpell(slot) == SpellState.Ready;
        }

        private void DrawingOnDraw(EventArgs args)
        {
            NewMethod();

            if (config.drawMenu.Get<CheckBox>("drawQ").CurrentValue)
            {
                new Circle(Color.Blue, qRange).Draw(me.Position);
            }
            if (config.drawMenu.Get<CheckBox>("drawW").CurrentValue)
            {
                new Circle(Color.Blue, wRange).Draw(me.Position);
            }
            if (config.drawMenu.Get<CheckBox>("drawE").CurrentValue)
            {
                new Circle(Color.Blue, eRange).Draw(me.Position);
            }
            if (config.drawMenu.Get<CheckBox>("drawR").CurrentValue)
            {
                new Circle(Color.Red, rRange).Draw(me.Position);
            }
        }

        Vector2 GetBestQPos(List<Vector2> posArray, float radius, int minHit)
        {
            Vector2 center = new Vector2();

            while (posArray.Count > 0)
            {
                float rad;
                MEC.FindMinimalBoundingCircle(posArray, out center, out rad);

                var farestPos =
                    posArray.Where(x => x.Distance(center) > radius).OrderByDescending(x => x.Distance(center)).FirstOrDefault();

                if (posArray.Any(x => x.Distance(center) > radius))
                {
                    posArray.Remove(farestPos);
                }
                else
                {
                    if (posArray.Count < minHit)
                    {
                        return new Vector2();
                    }
                    break;
                }
            }

            return center;
        }

        private void GameOnTick(EventArgs args)
        {
            if (ready(SpellSlot.R) && config.miscMenu.Get<CheckBox>("useREvade").CurrentValue)
            {
                CheckREvade();
            }

            if (Orbwalker.ActiveModesFlags == Orbwalker.ActiveModes.LaneClear)
                LaneClear();

            else if (Orbwalker.ActiveModesFlags == Orbwalker.ActiveModes.JungleClear)
                JungleClear();

            if (Orbwalker.ActiveModesFlags == Orbwalker.ActiveModes.Harass)
                Harass();

            if (Orbwalker.ActiveModesFlags == Orbwalker.ActiveModes.LastHit)
                LastHit();

            if (Orbwalker.ActiveModesFlags == Orbwalker.ActiveModes.Combo)
                Combo();
                    
        }

        private void LastHit()
        {
            bool betterQLogic = config.waveClearMenu.Get<CheckBox>("useBetterQLogicWaveClear").CurrentValue;

            var lessHpMinions =
                    EntityManager.MinionsAndMonsters.EnemyMinions.Where(
                        x => me.GetSpellDamage(x, SpellSlot.Q) > x.Health);
            List<Vector2> minionsPos = lessHpMinions.Select(x => x.Position.To2D()).ToList();

            if (betterQLogic)
            {
                DoArcCalculations(1, minionsPos);
            }
            else
            {
                var castPos = GetBestQPos(minionsPos, qRadius, 1);
                Player.CastSpell(SpellSlot.Q, castPos.To3D());
            }
        }

        private void NewMethod()
        {
            //if (start == new Vector2() || end == new Vector2())
            //    return;

            //var arcPolyTuple =
            //    new ArcMyWay(start, end, (int)ObjectManager.Player.BoundingRadius).ToPolygonEx();
            //Geometry.Polygon polygon = arcPolyTuple.Item1;
            //Vector2 center = arcPolyTuple.Item2;

            //polygon.DrawPolygon(System.Drawing.Color.Red, 5);
            //new Circle(Color.Blue, qRadius).Draw(center.To3D());
        }

        private bool targetHadBuff = false;
        private int hadBuffTick;
        private void Combo()
        {      
            var target = TargetSelector.GetTarget(2000, DamageType.Magical);
            target = target ?? TargetSelector.GetTarget(1500, DamageType.Physical);

            if (targetHadBuff && Environment.TickCount - hadBuffTick >= 2000)
                targetHadBuff = false;
            
            /*R*/
            if (config.comboMenu.Get<CheckBox>("useR").CurrentValue && ready(SpellSlot.R))
            {
                if (me.GetSpellDamage(target, SpellSlot.R) > target.Health)
                {
                    Player.CastSpell(SpellSlot.R, target);
                }
                else if (!ready(SpellSlot.Q))
                {
                    if (config.comboMenu.Get<CheckBox>("useRmoonlightOnly").CurrentValue && (target.HasBuff(buff) || targetHadBuff))
                    {
                        Player.CastSpell(SpellSlot.R, target);
                        if (!targetHadBuff)
                        {
                            targetHadBuff = true;
                            hadBuffTick = Environment.TickCount;
                        }
                    }
                    else if (!config.comboMenu.Get<CheckBox>("useRmoonlightOnly").CurrentValue)
                        Player.CastSpell(SpellSlot.R, target);
                }

            }
            
            /*Q*/
            if (config.comboMenu.Get<CheckBox>("useBetterQCombo").CurrentValue)
            {
                DoQOnTargetAndOthers(target);
            }
            else
            {
                var pred = q.GetPrediction(target);
                if (config.comboMenu.Get<CheckBox>("useQ").CurrentValue && ready(SpellSlot.Q))
                {
                    if (pred.HitChance >= HitChance.High)
                    {
                        Player.CastSpell(SpellSlot.Q, pred.CastPosition);
                    }
                }
            }

            

            /*W*/
            if (target.Distance(me) <= wRange +250 && config.comboMenu.Get<CheckBox>("useW").CurrentValue && ready(SpellSlot.W))
                Player.CastSpell(SpellSlot.W);

            //E
            CheckEscaping(target);
        }

        /// <summary>
        /// Tries to hit main target and as much as possible other targets. 
        /// Using Movement Prediction to see if in ArcPolygon instead of circular polygon.
        /// </summary>
        /// <param name="target"></param>
        private void DoQOnTargetAndOthers(AIHeroClient target)
        {
            /*target pred is at the the end of the array*/
            var predExTarget = PredictionEx.GetPrediction(target, qDelay);
            List<Vector2> enemyPredictions = new List<Vector2>();
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var enemy in EntityManager.Heroes.Enemies.Where(x => x.Distance(me) <= 1500))
            {
                var p = PredictionEx.GetPrediction(enemy, qDelay);
                if (p.Hitchance >= HitChanceEx.High && p.CastPosition.Distance(me) <= qRange)
                {
                    enemyPredictions.Add(p.CastPosition.To2D());
                }
            }
            enemyPredictions.Add(predExTarget.CastPosition.To2D());

            if (config.comboMenu.Get<CheckBox>("useQ").CurrentValue && ready(SpellSlot.Q))
            {
                if (predExTarget.Hitchance >= HitChanceEx.High && predExTarget.CastPosition.Distance(me) <= qRange)
                {
                    /*make sure target pos is in the prospective polygons*/
                    DoArcCalculations(1, enemyPredictions, enemyPredictions.Count - 1);
                }
            }
        }

        private void CheckEscaping(AIHeroClient target)
        {
            if (target.Distance(me) <= eRange)
            {
                var targetFacingPos = target.Position.To2D() + 1000 * target.Direction.To2D().Perpendicular();
                var myFacingPos = me.Position.To2D() + 1000 * me.Direction.To2D().Perpendicular();

                var targetDestination = Prediction.Position.GetRealPath(target).Last();

                if (Math.Abs(targetFacingPos.AngleBetween(myFacingPos)) < 80 && targetDestination.Distance(me) > eRange + 200)
                {
                    if (GetEnemyTimeOuttaE(target) > eDelay) //eDelay
                    {
                        Player.CastSpell(SpellSlot.E);
                    }
                }
            }
        }

        private void Harass()
        {
            var target = TargetSelector.GetTarget(2000, DamageType.Magical);
            target = target ?? TargetSelector.GetTarget(1500, DamageType.Physical);

            if (me.ManaPercent >= config.harassMenu.Get<Slider>("minManaQHarass").CurrentValue && target.Distance(me) <= 1500 &&
                ready(SpellSlot.Q))
            {
                /*Q*/
                if (config.harassMenu.Get<CheckBox>("useBetterQHarass").CurrentValue)
                {
                    DoQOnTargetAndOthers(target);
                }
                else
                {
                    var pred = q.GetPrediction(target);
                    if (pred.HitChance >= HitChance.High)
                    {
                        Player.CastSpell(SpellSlot.Q, pred.CastPosition);
                    }
                }
            }
        }

        private void JungleClear()
        {
            List<Vector2> minionPos = ObjectManager.Get<Obj_AI_Minion>().Where(x => x.IsValid && x.Distance(me) <= 1000 && !x.IsDead).
                                                Select(minion => minion.Position.To2D()).ToList();

            Vector2 pos = GetBestQPos(minionPos, qRadius, 1);

            if (pos != new Vector2() && config.jungleClearMenu.Get<CheckBox>("useQJungleClear").CurrentValue && ready(SpellSlot.Q))
                Player.CastSpell(SpellSlot.Q, pos.To3D());

            if (config.jungleClearMenu.Get<CheckBox>("useRJungleClear").CurrentValue && ready(SpellSlot.R))
                foreach (var jungleCreep in
                    ObjectManager.Get<Obj_AI_Minion>().Where(x => x.Distance(me) <= rRange && x.IsValid).
                    OrderByDescending(x => x.MaxHealth))
                {
                    if (jungleCreep.HasBuff(buff))
                        Player.CastSpell(SpellSlot.R, jungleCreep);
                }

            int minionHits = minionPos.Count(x => x.Distance(me) <= eRange); //eRange

            if (ready(SpellSlot.E) && minionHits >= 2 && config.jungleClearMenu.Get<CheckBox>("useEJungleClear").CurrentValue
                && ready(SpellSlot.W))
                Player.CastSpell(SpellSlot.E);
            else if (ready(SpellSlot.W))
                if (minionPos.Any(x => x.Distance(me) <= 500) && config.jungleClearMenu.Get<CheckBox>("useWJungleClear").CurrentValue)
                Player.CastSpell(SpellSlot.W);
        }

        private void LaneClear()
        {
            List<Vector2> minionPos =
                                ObjectManager.Get<Obj_AI_Minion>().Where(x => x.IsEnemy && x.IsValid && x.Distance(me) <= qRange 
                                && !x.IsDead).
                                    Select(minion => minion.Position.To2D()).ToList();

            bool betterQLogic = config.waveClearMenu.Get<CheckBox>("useBetterQLogicWaveClear").CurrentValue;

            /*Q*/
            int minMinionHitCount = config.waveClearMenu.Get<Slider>("qWaveClear").CurrentValue;
            if (minMinionHitCount != -1)//enabled
            {
                if (betterQLogic)
                {
                    /*arc calculations*/
                    DoArcCalculations(minMinionHitCount);
                }
                else
                {
                    /*circular calculations*/
                    Vector2 pos = GetBestQPos(minionPos, qRadius, minMinionHitCount);

                    if (pos != new Vector2() && ready(SpellSlot.Q))
                        Player.CastSpell(SpellSlot.Q, pos.To3D());
                }
                
            }

            /*E*/
            int minionHits = minionPos.Count(x => x.Distance(me) <= eRange); //eRange

            int minimumMinionEHit = config.waveClearMenu.Get<Slider>("useEWaveClear").CurrentValue;

            if (ready(SpellSlot.E) && minionHits >= minimumMinionEHit && minimumMinionEHit != -1
                && ready(SpellSlot.W))
                Player.CastSpell(SpellSlot.E);
            /*W*/
            else if (ready(SpellSlot.W))
                if (minionPos.Any(x => x.Distance(me) <= 500) && config.waveClearMenu.Get<CheckBox>("useWWaveClear").CurrentValue)
                    Player.CastSpell(SpellSlot.W);
        }

        bool isMainVectorInPolygon(Vector2 mainvec, Geometry.Polygon poly, Geometry.Polygon poly2)
        {
            return poly.IsInside(mainvec) || poly2.IsInside(mainvec);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="minimalObjectHitCount"></param>
        /// <param name="ownPosArray"></param>
        /// <param name="mainIndex">Index in the array which has to get hit</param>
        private void DoArcCalculations(int minimalObjectHitCount, List<Vector2> ownPosArray = null, int mainIndex = -1)
        {
            // ReSharper disable once UnusedVariable
            Task t = Task.Factory.StartNew(() =>
            {
                Vector2 mostHits = new Vector2();
                int hitCount = 0;

                float quality = config.miscMenu.Get<Slider>("betterQLogicQuality").CurrentValue / 100.0f;
                float step = 50 - (quality * 49);
                float step2 = qRange / 2 - (quality * 365);

                for (float i = 0; i < 361; i += step)
                {
                    for (float range = qRange; range > (int)me.BoundingRadius; range -= step2)
                    {
                        Vector2 pointOnCirc = ArcMyWay.PointOnCircle(range, i, me.Position.To2D());

                        var polyTuple = new ArcMyWay(me.Position.To2D(), pointOnCirc, (int)me.BoundingRadius).ToPolygonEx();
                        var arcPolygon = polyTuple.Item1;
                        var center = polyTuple.Item2;

                        Geometry.Polygon.Circle qCircle = new Geometry.Polygon.Circle(center, qRadius, (int)quality);

                        int objectHitCount = ownPosArray != null ? ownPosArray.Count(x => arcPolygon.IsInside(x) || qCircle.IsInside(x)) 
                            : EntityManager.MinionsAndMonsters.EnemyMinions.Count(x => arcPolygon.IsInside(x.Position)
                                                                                                                                                                                || qCircle.IsInside(x.Position));

                        if ((hitCount == 0 || objectHitCount > hitCount) && objectHitCount >= minimalObjectHitCount)
                        {
                            // ReSharper disable once PossibleNullReferenceException
                            if (mainIndex > -1 && isMainVectorInPolygon(ownPosArray[mainIndex], arcPolygon, qCircle))
                                hitCount = objectHitCount;
                                mostHits = pointOnCirc;
                        }
                    }
                }

                if (mostHits != new Vector2())
                    Player.CastSpell(SpellSlot.Q, mostHits.To3D());
            });
        }

        private void CheckREvade()
        {
            if (evadePlus.IsHeroInDanger())
            {
                int minDangerValue = config.miscMenu.Get<Slider>("rEvadeMinDangerValue").CurrentValue;
                EvadePlus.EvadePlus.EvadeResult evadeResult = evadePlus.CalculateEvade(evadePlus.LastIssueOrderPos);

                if (evadeResult.IsValid && evadeResult.EnoughTime)
                {
                    if (!config.miscMenu.Get<CheckBox>("useREvadeUndodgeableOnly").CurrentValue)
                    {
                        if (evadePlus.GetDangerValue() >= minDangerValue)
                            DoREvade();
                    }
                }
                else if (evadeResult.IsValid)
                {
                    var myPath = me.GetPath(evadePlus.LastIssueOrderPos.To3D());

                    if (!evadeResult.EnoughTime && evadePlus.LastEvadeResult == null
                        && !evadePlus.IsHeroPathSafe(evadeResult, myPath))
                    {
                        if (evadePlus.GetDangerValue() >= minDangerValue)
                            DoREvade();
                    }
                }
            }
        }

        bool isFlyPathSafe(Vector3 jumpPos)
        {
            var myPath = me.GetPath(jumpPos);
            return myPath.Count(x => evadePlus.IsPointSafe(x.To2D())) > myPath.Length / 2;
        }

        private void DoREvade()
        {
            int minComfort = config.miscMenu.Get<Slider>("rEvadeMinComfortDistance").CurrentValue;
            bool toTarget = config.miscMenu.Get<CheckBox>("rEvadeDodgeToEnemyInCombo").CurrentValue &&
                            Orbwalker.ActiveModesFlags == Orbwalker.ActiveModes.Combo;

            if (!toTarget)
            {
                foreach (Obj_AI_Minion minion in ObjectManager.Get<Obj_AI_Minion>().Where(x => x.Distance(me) <= rRange && //rRange
                               x.IsValid && x.IsEnemy)
                    .OrderBy(x => x.Distance(me)))
                {
                    if (!isFlyPathSafe(minion.Position))
                    {
                        continue;
                    }     

                    var closestEnemy = EntityManager.Heroes.Enemies.OrderBy(x => x.Distance(minion)).First();

                    if (closestEnemy.Distance(minion) >= minComfort)
                    {
                        Player.CastSpell(SpellSlot.R, minion);
                    }
                }
            }
            else
            {
                var target = TargetSelector.GetTarget(1000, DamageType.Magical) ?? TargetSelector.GetTarget(1000, DamageType.Physical);
                foreach (Obj_AI_Minion minion in ObjectManager.Get<Obj_AI_Minion>().Where(x => x.Distance(me) <= rRange && //rRange
                    x.IsValid && x.IsEnemy)
                    .OrderBy(x => x.Distance(target))
                    .Where(minion => evadePlus.IsPointSafe(minion.Position.To2D())))
                {
                    if (!isFlyPathSafe(minion.Position))
                        continue;

                    Player.CastSpell(SpellSlot.R, minion);
                }
            }
        }
    }
}
