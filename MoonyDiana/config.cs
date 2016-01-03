using System.Collections.Generic;
using System.Linq;
using EloBuddy.SDK;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using EvadePlus;

namespace MoonyDiana
{
    class config
    {
        private static EvadeSkillshot GetSkillshot(string s)
        {
            return MenuSkillshots[s.ToLower().Split('/')[0]];
        }

        public static bool IsSkillshotEnabled(EvadeSkillshot skillshot)
        {
            var valueBase = SkillshotMenu[skillshot + "/enable"];
            return valueBase != null && valueBase.Cast<CheckBox>().CurrentValue;
        }

        public static Menu menu;
        public static Menu SkillshotMenu;
        public static Menu miscMenu;
        public static Menu comboMenu;
        public static Menu harassMenu;
        public static Menu waveClearMenu;
        public static Menu drawMenu;
        public static Menu jungleClearMenu;

        public static readonly Dictionary<string, EvadeSkillshot> MenuSkillshots =
            new Dictionary<string, EvadeSkillshot>();
        public static void InitMenu()
        {
            menu = MainMenu.AddMenu("MoonyDiana", "moonyDiana");


            comboMenu = menu.AddSubMenu("Combo", "combo");
            comboMenu.Add("useQ", new CheckBox("Use Q"));
            comboMenu.Add("useBetterQCombo", new CheckBox("Use advanced Q logic"));
            comboMenu.Add("useW", new CheckBox("Use W"));
            comboMenu.Add("useEEscapingEnemy", new CheckBox("Use E on escaping enemy"));
            comboMenu.Add("blockE", new CheckBox("Block E if nobody gets hit"));
            comboMenu.Add("useEWhileR", new CheckBox("Use E while R"));
            comboMenu.AddSeparator();
            comboMenu.Add("useR", new CheckBox("Use R"));
            //comboMenu.Add("useRQ", new CheckBox("Try instant R -> Q"));
            comboMenu.AddSeparator();


            harassMenu = menu.AddSubMenu("Harass", "harass");
            harassMenu.Add("minManaQHarass", new Slider("Use Q if at least % mana available", 50));
            harassMenu.Add("useBetterQHarass", new CheckBox("Use advanced Q logic"));

            waveClearMenu = menu.AddSubMenu("Wave Clear", "waveClear");
            waveClearMenu.Add("qWaveClear", new Slider("Use Q if hit at least x minions", 3, -1, 10));
            waveClearMenu.Add("useWWaveClear", new CheckBox("Use W"));
            waveClearMenu.Add("useEWaveClear", new Slider("Use E if X minions will be hit", 3, 1, 10));

            waveClearMenu.Add("useBetterQLogicWaveClear", new CheckBox("Use intelligent Q logic to clear"));
           // waveClearMenu.Add("useRWaveClear", new CheckBox("Use R (moonlight)"));

            jungleClearMenu = menu.AddSubMenu("Jungle Clear", "jungleClear");
            jungleClearMenu.Add("useQJungleClear", new CheckBox("Use Q"));
            jungleClearMenu.Add("useWJungleClear", new CheckBox("Use W"));
            jungleClearMenu.Add("useEJungleClear", new CheckBox("Use E"));
            jungleClearMenu.Add("useRJungleClear", new CheckBox("Use R (moonlight)"));

            miscMenu = menu.AddSubMenu("Misc", "misc");
            miscMenu.Add("betterQLogicQuality", new Slider("Advanced Q Quality in %", 20));
            miscMenu.AddLabel("This feature is more cpu intense by generating many polygons");
            miscMenu.AddSeparator();
            miscMenu.Add("interruptE", new CheckBox("Interrupt with E"));
            miscMenu.Add("antiGapE", new CheckBox("AntiGapCloser with E"));
            miscMenu.AddSeparator();
            miscMenu.Add("useWTargeted", new CheckBox("Use W on targeted skills"));
            miscMenu.AddSeparator();
            miscMenu.AddSeparator(50);

            miscMenu.Add("useREvade", new CheckBox("Use R to evade"));
            miscMenu.Add("rEvadeInfo", new Label("Evading to the closest origin position point"));
            miscMenu.AddSeparator();
            miscMenu.Add("useREvadeUndodgeableOnly", new CheckBox("Only if undodgeable"));
            miscMenu.Add("rEvadeMinDangerValue", new Slider("Min Danger Value of skillshot to evade", 5, 0, 5));
            miscMenu.Add("rEvadeDodgeToEnemyInCombo", new CheckBox("Evade to target if in combo", false));
            miscMenu.Add("rEvadeMinComfortDistance", new Slider("Min comfort distance to enemy heroes", 500, 0, 825));//rRange

            miscMenu.Add("fowDetection", new CheckBox("Use Fog of war detection"));
            miscMenu.Add("limitDetectionRange", new CheckBox("Limit Spell Detection Range"));
            miscMenu.Add("skillshotActivationDelay", new Slider("Skillshot Activation Delay", 0, 0, 400));
            miscMenu.Add("processSpellDetection", new CheckBox("Enable Process Spell Detection"));




            var heroes = EntityManager.Heroes.Enemies;
            var heroNames = heroes.Select(obj => obj.ChampionName).ToArray();
            var skillshots =
                SkillshotDatabase.Database.Where(s => heroNames.Contains(s.SpellData.ChampionName)).ToList();
            skillshots.AddRange(
                SkillshotDatabase.Database.Where(
                    s =>
                        s.SpellData.ChampionName == "AllChampions" &&
                        heroes.Any(obj => obj.Spellbook.Spells.Select(c => c.Name).Contains(s.SpellData.SpellName))));

            SkillshotMenu = menu.AddSubMenu("Skillshots");

            foreach (var c in skillshots)
            {
                var skillshotString = c.ToString().ToLower();

                if (MenuSkillshots.ContainsKey(skillshotString))
                    continue;

                MenuSkillshots.Add(skillshotString, c);

                SkillshotMenu.AddGroupLabel(c.DisplayText);
                SkillshotMenu.Add(skillshotString + "/enable", new CheckBox("Dodge"));

                var dangerValue = new Slider("Danger Value", c.SpellData.DangerValue, 1, 5);
                dangerValue.OnValueChange += delegate (ValueBase<int> sender, ValueBase<int>.ValueChangeArgs args)
                {
                    GetSkillshot(sender.SerializationId).SpellData.DangerValue = args.NewValue;
                };
                SkillshotMenu.Add(skillshotString + "/dangervalue", dangerValue);

                SkillshotMenu.AddSeparator();
            }

            drawMenu = menu.AddSubMenu("Drawing", "draw");
            drawMenu.Add("drawQ", new CheckBox("Q Range"));
            drawMenu.Add("drawW", new CheckBox("W Range", false));
            drawMenu.Add("drawE", new CheckBox("E Range", false));
            drawMenu.Add("drawR", new CheckBox("R Range"));
        }
    }
}
