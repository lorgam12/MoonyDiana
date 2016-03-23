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
        private readonly string QBuffName = "dianamoonlight";

        private int eDelay = 500;
        private int qDelay = 250;

        private readonly Spell.Skillshot Q = new Spell.Skillshot(SpellSlot.Q, 830, SkillShotType.Circular, 250, 1600);
        private readonly Spell.Active W = new Spell.Active(SpellSlot.W, 200);
        private readonly Spell.Active E = new Spell.Active(SpellSlot.E, 250);
        private readonly Spell.Targeted R = new Spell.Targeted(SpellSlot.R, 825);

        public Main()
        {
            if (me.ChampionName != "Diana")
                return;

            Chat.Print("MoonyDiana loaded!");

            config.InitMenu();
            var skillshotDetector = new SkillshotDetector(DetectionTeam.EnemyTeam);
            evadePlus = new EvadePlus.EvadePlus(skillshotDetector);

            me = ObjectManager.Player;
            Game.OnUpdate += GameOnUpdate;
            Drawing.OnDraw += DrawingOnDraw;
            Gapcloser.OnGapcloser += GapcloserOnOnGapcloser;
            Interrupter.OnInterruptableSpell += InterrupterOnOnInterruptableSpell;
            Player.OnSpellCast += PlayerOnOnSpellCast;
            Player.OnProcessSpellCast += PlayerOnOnProcessSpellCast;
        }

        private void PlayerOnOnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe && args.Slot == SpellSlot.R)
            {
                float flyTime = (me.Distance(args.Target)/Player.GetSpell(SpellSlot.R).SData.MissileSpeed)*1000;
                if (config.comboMenu.Get<CheckBox>("useEWhileR").CurrentValue && ready(SpellSlot.E))
                    Core.RepeatAction(() => E.Cast(), (int)flyTime, 1000);
            }

            if (sender.IsMe && args.Slot == SpellSlot.Q)
            {
                args.Start.To2D();
                args.End.To2D();
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
                    Orbwalker.CanAutoAttack && EntityManager.Heroes.Enemies.Any(x => x.IsValid && x.Distance(me) <= me.AttackRange))
                args.Process = false;
        }

        private float GetEnemyTimeOuttaE(AIHeroClient enemy)
        {
            var enemyDestination = Prediction.Position.GetRealPath(enemy).Last();

            Geometry.Polygon.Circle circle = new Geometry.Polygon.Circle(me.Position, E.Range);
            var intersection =
                circle.GetIntersectionPointsWithLineSegment(enemy.Position.To2D(), enemyDestination.To2D()).OrderBy(x => x.Distance(enemy))
                    .First();

            float distToCircleEnd = enemy.Distance(intersection);
            float dt = (distToCircleEnd / enemy.MoveSpeed) * 1000; //ms

            return dt;
        }

        private void InterrupterOnOnInterruptableSpell(Obj_AI_Base sender, Interrupter.InterruptableSpellEventArgs interruptableSpellEventArgs)
        {
            if (interruptableSpellEventArgs.Sender.Position.Distance(me) <= E.Range && config.miscMenu.Get<CheckBox>("interruptE").CurrentValue)
                E.Cast();
        }

        private void GapcloserOnOnGapcloser(AIHeroClient sender, Gapcloser.GapcloserEventArgs gapcloserEventArgs)
        {
            if (gapcloserEventArgs.End.Distance(me) <= E.Range && config.miscMenu.Get<CheckBox>("antiGapE").CurrentValue)
                E.Cast();
        }

        private readonly int qCost = 55;
        private readonly int[] wCost = new[] {-1, 60, 70, 80, 90, 100};
        private readonly int eCost = 70;
        private readonly int[] rCost = new[] { -1, 50, 65, 80 };
        private bool ready(SpellSlot slot)
        {
            int neededMana = 0;
            switch (slot)
            {
                case SpellSlot.Q:
                    neededMana = qCost;
                    break;
                case SpellSlot.W:
                    neededMana = wCost[Player.GetSpell(slot).Level];
                    break;
                case SpellSlot.E:
                    neededMana = eCost;
                    break;
                case SpellSlot.R:
                    neededMana = rCost[Player.GetSpell(slot).Level];
                    break;
            }
            return Player.CanUseSpell(slot) == SpellState.Ready && ObjectManager.Player.Mana >= neededMana;
        }

        private void DrawingOnDraw(EventArgs args)
        {
            if (config.drawMenu.Get<CheckBox>("drawQ").CurrentValue)
            {
                new Circle(Color.Blue, Q.Range).Draw(me.Position);
            }
            if (config.drawMenu.Get<CheckBox>("drawW").CurrentValue)
            {
                new Circle(Color.Blue, W.Range).Draw(me.Position);
            }
            if (config.drawMenu.Get<CheckBox>("drawE").CurrentValue)
            {
                new Circle(Color.Blue, E.Range).Draw(me.Position);
            }
            if (config.drawMenu.Get<CheckBox>("drawR").CurrentValue)
            {
                new Circle(Color.Red, R.Range).Draw(me.Position);
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

        private void GameOnUpdate(EventArgs args)
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
                Q.Cast(castPos.To3D());
            }
        }

        private bool targetHadBuff = false;
        private int hadBuffTick;
        private void Combo()
        {      
            var target = TargetSelector.GetTarget(1500, DamageType.Magical) ?? TargetSelector.GetTarget(1500, DamageType.Physical);

            if (targetHadBuff && Environment.TickCount - hadBuffTick >= 2000)
                targetHadBuff = false;
            
            /*R*/
            if (config.comboMenu.Get<CheckBox>("useR").CurrentValue && ready(SpellSlot.R))
            {
                if (me.GetSpellDamage(target, SpellSlot.R) > target.Health)
                {
                    R.Cast(target);
                    if (ready(SpellSlot.Q))
                    {
                        Core.RepeatAction(() => Q.Cast(Q.GetPrediction(target).CastPosition), 1, 1000);
                    }
                }
                else if (!ready(SpellSlot.Q))
                {
                    if (config.comboMenu.Get<CheckBox>("useRmoonlightOnly").CurrentValue && (target.HasBuff(QBuffName) || targetHadBuff))
                    {
                        R.Cast(target);
                        if (!targetHadBuff)
                        {
                            targetHadBuff = true;
                            hadBuffTick = Environment.TickCount;
                        }
                    }
                    else if (!config.comboMenu.Get<CheckBox>("useRmoonlightOnly").CurrentValue)
                        R.Cast(target);
                }

            }

            /*Q*/
            if (config.comboMenu.Get<CheckBox>("useBetterQCombo").CurrentValue)
            {
                DoQOnTargetAndOthers(target);
            }
            else
            {
                var pred = Q.GetPrediction(target);
                if (config.comboMenu.Get<CheckBox>("useQ").CurrentValue && ready(SpellSlot.Q))
                {
                    if (pred.HitChance >= HitChance.High)
                    {
                        Q.Cast(pred.CastPosition);
                    }
                }
            }

            

            /*W*/
            if (target.Distance(me) <= W.Range + 250 && config.comboMenu.Get<CheckBox>("useW").CurrentValue && ready(SpellSlot.W))
                W.Cast();

            //E
            CheckEscaping(target);
        }

        /// <summary>
        /// Tries to hit main target and as much as possible other targets. 
        /// Using Movement Prediction to see if postiions in ArcPolygon.
        /// </summary>
        /// <param name="target"></param>
        private void DoQOnTargetAndOthers(AIHeroClient target)
        {
            /*target pred is at the the end of the array*/
            var predictedTargetPosition = Prediction.Position.PredictUnitPosition(target, qDelay);
            List<Vector2> enemyPredictions = new List<Vector2>();
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var enemy in EntityManager.Heroes.Enemies.Where(x => x.Distance(me) <= 1500))
            {
                var predictedUnitPosition = Prediction.Position.PredictUnitPosition(enemy, qDelay);
                if (predictedUnitPosition.Distance(me) <= Q.Range)
                {
                    enemyPredictions.Add(predictedUnitPosition);
                }
            }
            enemyPredictions.Add(predictedTargetPosition);

            if (config.comboMenu.Get<CheckBox>("useQ").CurrentValue && ready(SpellSlot.Q))
            {
                if (predictedTargetPosition.Distance(me) <= Q.Range)
                {
                    /*make sure target pos is in the prospective polygons*/
                    DoArcCalculations(1, enemyPredictions, enemyPredictions.Count - 1);
                }
            }
        }

        private void CheckEscaping(AIHeroClient target)
        {
            if (target.Distance(me) <= E.Range)
            {
                var targetFacingPos = target.Position.To2D() + 1000 * target.Direction.To2D().Perpendicular();
                var myFacingPos = me.Position.To2D() + 1000 * me.Direction.To2D().Perpendicular();

                var targetDestination = Prediction.Position.GetRealPath(target).Last();

                if (Math.Abs(targetFacingPos.AngleBetween(myFacingPos)) < 80 && targetDestination.Distance(me) > E.Range + 200)
                {
                    if (GetEnemyTimeOuttaE(target) > eDelay) //eDelay
                    {
                        E.Cast();
                    }
                }
            }
        }

        private void Harass()
        {
            var target = TargetSelector.GetTarget(1500, DamageType.Magical) ?? TargetSelector.GetTarget(1500, DamageType.Physical);

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
                    var pred = Q.GetPrediction(target);
                    if (pred.HitChance >= HitChance.High)
                    {
                        Q.Cast(pred.CastPosition);
                    }
                }
            }
        }

        private void JungleClear()
        {
            List<Vector2> minionPos = ObjectManager.Get<Obj_AI_Minion>().Where(x => x.IsValid && x.Distance(me) <= 1000 && !x.IsDead).
                                                Select(minion => minion.Position.To2D()).ToList();

            Vector2 pos = GetBestQPos(minionPos, qRadius, 1);

            if (pos != new Vector2() && config.jungleClearMenu.Get<CheckBox>("useQJungleClear").CurrentValue &&
                ready(SpellSlot.Q))
                Q.Cast(pos.To3D());

            if (config.jungleClearMenu.Get<CheckBox>("useRJungleClear").CurrentValue && ready(SpellSlot.R))
                foreach (var jungleCreep in
                    ObjectManager.Get<Obj_AI_Minion>().Where(x => x.Distance(me) <= R.Range && x.IsValid).
                    OrderByDescending(x => x.MaxHealth))
                {
                    if (jungleCreep.HasBuff(QBuffName))
                        R.Cast(jungleCreep);
                }

            int minionHits = minionPos.Count(x => x.Distance(me) <= E.Range); //E.Range

            if (ready(SpellSlot.E) && minionHits >= 2 && config.jungleClearMenu.Get<CheckBox>("useEJungleClear").CurrentValue
                && ready(SpellSlot.W))
                E.Cast();
            else if (ready(SpellSlot.W))
                if (minionPos.Any(x => x.Distance(me) <= 500) &&
                    config.jungleClearMenu.Get<CheckBox>("useWJungleClear").CurrentValue)
                    W.Cast();
        }

        private void LaneClear()
        {
            List<Vector2> minionPos =
                                ObjectManager.Get<Obj_AI_Minion>().Where(x => x.IsEnemy && x.IsValid && x.Distance(me) <= Q.Range 
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
                        Q.Cast(pos.To3D());
                }
                
            }

            /*E*/
            int minionHits = minionPos.Count(x => x.Distance(me) <= E.Range); //E.Range

            int minimumMinionEHit = config.waveClearMenu.Get<Slider>("useEWaveClear").CurrentValue;

            if (ready(SpellSlot.E) && minionHits >= minimumMinionEHit && minimumMinionEHit != -1
                && ready(SpellSlot.W))
                E.Cast();
            /*W*/
            else if (ready(SpellSlot.W))
                if (minionPos.Any(x => x.Distance(me) <= 500) &&
                    config.waveClearMenu.Get<CheckBox>("useWWaveClear").CurrentValue)
                    W.Cast();
        }

        float GetDistanceToPolygonEdge(Vector2 point, Geometry.Polygon polygon)
        {
            return polygon.Points.OrderBy(x => x.Distance(point)).First().Distance(point);
        }

        /// <summary>
        /// returns if main target gets hit by Q
        /// </summary>
        /// <param name="mainvec"></param>
        /// <param name="poly"></param>
        /// <param name="poly2"></param>
        /// <returns></returns>
        bool isMainVectorInPolygon(Vector2 mainvec, Geometry.Polygon poly, Geometry.Polygon poly2)
        {
            float maxAllowedDist = config.predictionMenu.Get<Slider>("qTargetPredictionQuality").CurrentValue;
            if (poly.IsInside(mainvec))
            {
                float dist = GetDistanceToPolygonEdge(mainvec, poly);
                if (dist <= maxAllowedDist)
                    return true;
            }
            else if (poly2.IsInside(mainvec))
            {
                float dist = GetDistanceToPolygonEdge(mainvec, poly2);
                if (dist <= maxAllowedDist)
                    return true;
            }

            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="minimalObjectHitCount"></param>
        /// <param name="ownPosArray">if null minion positions will be checked</param>
        /// <param name="mainIndex">Index in the array which has to get hit</param>
        private void DoArcCalculations(int minimalObjectHitCount, List<Vector2> ownPosArray = null, int mainIndex = -1)
        {
            // ReSharper disable once UnusedVariable
            Task t = Task.Factory.StartNew(() =>
            {
                Vector2 mostHitsPos = new Vector2();
                int hitCount = 0;

                float quality = config.predictionMenu.Get<Slider>("betterQLogicQuality").CurrentValue / 100.0f;
                float step = 50 - (quality * 49);
                float step2 = Q.Range / 2 - (quality * 365);

                for (float i = 0; i < 361; i += step)
                {
                    for (float range = Q.Range; range > (int)me.BoundingRadius; range -= step2)
                    {
                        Vector2 pointOnCirc = ArcMyWay.PointOnCircle(range, i, me.Position.To2D());

                        var polyTuple = new ArcMyWay(me.Position.To2D(), pointOnCirc, (int)me.BoundingRadius).ToPolygonA();
                        var arcPolygon = polyTuple.Item1;
                        var center = polyTuple.Item2;

                        Geometry.Polygon.Circle qCircle = new Geometry.Polygon.Circle(center, qRadius, (int)quality);

                        int objectHitCount = ownPosArray != null ? ownPosArray.Count(x => arcPolygon.IsInside(x) || qCircle.IsInside(x)) 
                            : EntityManager.MinionsAndMonsters.EnemyMinions.Count(x => arcPolygon.IsInside(x.Position)
                                                                                                                                                                                || qCircle.IsInside(x.Position));

                        if ((hitCount == 0 || objectHitCount > hitCount) && objectHitCount >= minimalObjectHitCount)
                        {
                            // ReSharper disable once PossibleNullReferenceException
                            bool mainTargetHit = mainIndex > -1 &&
                                                 isMainVectorInPolygon(ownPosArray[mainIndex], arcPolygon, qCircle);
                            if (mainTargetHit || mainIndex == -1)
                            {
                                hitCount = objectHitCount;
                                mostHitsPos = pointOnCirc;
                            }
                        }
                    }
                }

                if (mostHitsPos != new Vector2())
                    Q.Cast(mostHitsPos.To3D());
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
            bool checkFlyPath = config.miscMenu.Get<CheckBox>("useREvadeCheckFlyPath").CurrentValue;

            if (!toTarget)
            {
                foreach (Obj_AI_Minion minion in ObjectManager.Get<Obj_AI_Minion>().Where(x => x.Distance(me) <= R.Range && //R.Range
                               x.IsValid && x.IsEnemy)
                    .OrderBy(x => x.Distance(me)))
                {
                    if (!isFlyPathSafe(minion.Position) && checkFlyPath)
                        continue; 

                    var closestEnemy = EntityManager.Heroes.Enemies.OrderBy(x => x.Distance(minion)).FirstOrDefault(x => x.IsValid);

                    if (closestEnemy == null || closestEnemy.Distance(minion) >= minComfort)
                    {
                        R.Cast(minion);
                    }
                }
            }
            else
            {
                var target = TargetSelector.GetTarget(1000, DamageType.Magical) ?? TargetSelector.GetTarget(1000, DamageType.Physical);
                foreach (Obj_AI_Minion minion in ObjectManager.Get<Obj_AI_Minion>().Where(x => x.Distance(me) <= R.Range && //R.Range
                    x.IsValid && x.IsEnemy)
                    .OrderBy(x => x.Distance(target))
                    .Where(minion => evadePlus.IsPointSafe(minion.Position.To2D())))
                {
                    if (!isFlyPathSafe(minion.Position) && checkFlyPath)
                        continue;

                    R.Cast(minion);
                }
            }
        }
    }
}
