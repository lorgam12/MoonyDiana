using System;
using System.Collections.Generic;
using System.Linq;
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

        private void PlayerOnOnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe && args.Slot == SpellSlot.R)
            {
                if (config.comboMenu.Get<CheckBox>("useEWhileR").CurrentValue && ready(SpellSlot.E))
                    Player.CastSpell(SpellSlot.E);
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

            if (Orbwalker.ActiveModesFlags == Orbwalker.ActiveModes.Combo)
            {
                Combo();
            }
        }

        private void Combo()
        {
            var target = TargetSelector.GetTarget(2000, DamageType.Magical);
            target = target ?? TargetSelector.GetTarget(1500, DamageType.Physical);

            target = target ?? TargetSelector.GetTarget(1500, DamageType.Physical);

            var pred = q.GetPrediction(target);

            if (config.comboMenu.Get<CheckBox>("useR").CurrentValue && ready(SpellSlot.R))
            {
                if (ready(SpellSlot.Q) && me.GetSpellDamage(target, SpellSlot.R) > target.Health)
                {
                    Player.CastSpell(SpellSlot.R, target);
                    ///*target cant evade Q*/
                    //bool highHitChance = pred.HitChance >= HitChance.High &&
                    //                config.comboMenu.Get<CheckBox>("useRQ").CurrentValue;

                    //if (highHitChance || me.GetSpellDamage(target, SpellSlot.R) > target.Health)
                    //{
                    //    Player.CastSpell(SpellSlot.R, target);
                    //    Player.CastSpell(SpellSlot.Q, pred.UnitPosition);
                    //}
                }
                else
                    Player.CastSpell(SpellSlot.R, target);

            }

            if (pred.HitChance >= HitChance.High && config.comboMenu.Get<CheckBox>("useQ").CurrentValue &&
                ready(SpellSlot.Q)
                && pred.CastPosition.Distance(me) <= qRange)
            {
                Player.CastSpell(SpellSlot.Q, pred.UnitPosition);
            }

            if (target.Distance(me) <= wRange +250 && config.comboMenu.Get<CheckBox>("useW").CurrentValue && ready(SpellSlot.W))
                Player.CastSpell(SpellSlot.W);

            //E
            CheckEscaping(target);
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
                var pred = q.GetPrediction(target);
                if (pred.HitChance >= HitChance.High)
                    Player.CastSpell(SpellSlot.Q, pred.CastPosition);
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

            int minHitCount = config.waveClearMenu.Get<Slider>("qWaveClear").CurrentValue;
            if (minHitCount != -1)//enabled
            {
                Vector2 pos = GetBestQPos(minionPos, qRadius, minHitCount);

                if (pos != new Vector2() && ready(SpellSlot.Q))
                    Player.CastSpell(SpellSlot.Q, pos.To3D());
            }

            int minionHits = minionPos.Count(x => x.Distance(me) <= eRange); //eRange

            if (ready(SpellSlot.E) && minionHits >= config.waveClearMenu.Get<Slider>("useEWaveClear").CurrentValue
                && ready(SpellSlot.W))
                Player.CastSpell(SpellSlot.E);
            else if (ready(SpellSlot.W))
                if (minionPos.Any(x => x.Distance(me) <= 500) && config.waveClearMenu.Get<CheckBox>("useWWaveClear").CurrentValue)
                    Player.CastSpell(SpellSlot.W);
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
