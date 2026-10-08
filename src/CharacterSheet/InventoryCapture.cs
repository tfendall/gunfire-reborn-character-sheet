using System.Reflection;
using System.Globalization;
using SkillBolt;
using Relic;
using Talent;
using Benediction;

internal static class InventoryCapture
{
    internal static string Summary = "Inventory waiting for capture.";
    private static readonly PropertyInfo? rules = typeof(BulletChange.CRuleManagement).GetProperty("m_dRule");
    private static readonly Dictionary<string, string> sourceStatus = new();
    private static readonly Dictionary<(Type, string), PropertyInfo?> properties = new();
    private static readonly Dictionary<(Type, string), MethodInfo?> methods = new();
    private static int nextSource, selected, sourceIndex;
    private static Dictionary<int,BuffMeasurement>? pendingBuffs;
    private static bool collectingInscriptions;
    private static Dictionary<string,double>? pendingResources;
    private static int resourceUnits;
    private static bool partialBuffs;
    private static PropertyInfo? Property(Type type, string name)
    {
        var key = (type, name);
        if (!properties.TryGetValue(key, out var value)) properties[key] = value = type.GetProperty(name);
        return value;
    }
    private static MethodInfo? Method(Type type, string name)
    {
        var key = (type, name);
        if (!methods.TryGetValue(key, out var value)) methods[key] = value = type.GetMethod(name, Type.EmptyTypes);
        return value;
    }
    internal static void Sample(NewPlayerObject hero, NewItemProp? weapon)
    {
        var status = new List<string>();
        selected = nextSource++ % 16; sourceIndex = 0;
        Source("scrolls", () => Describe(RelicManager.m_EquipRelic), status);
        Source("extra-scrolls", () => Describe(RelicManager.m_ExtraRelic), status);
        Source("ascensions", () => Describe(TalentManager.m_TalentDict), status);
        Source("fused-ascensions", () => Describe(TalentManager.m_FuseTalentDict), status);
        Source("blessings", () => Describe(PlayerEntries(BenedictionManager.SaveBenedictionDict, hero.ObjectID)), status);
        Source("buffs", () => Describe((PlayerEntries(StateManager.StateCacheDict, hero.ObjectID) as GOStateCache)?.StateDict), status);
        Source("client-buffs", () => Describe(CtrlSideStateManager.m_instance?.GetStateDict()), status);
        Source("active-rules", () => Describe(hero.RuleManager == null ? null : rules?.GetValue(hero.RuleManager)), status);
        Source("weapon-effects", () => WeaponEffects(weapon), status);
        Source("weapon-actions", () => WeaponActions(hero, weapon), status);
        // Read existing instances, rather than constructing managers for inactive seasons.
        Source("season7", () => Season(typeof(S7Manager), "_instance", hero.ObjectID, "S7ItemDict", "S7PosPointDict", "LstSpecialPassive"), status);
        Source("season5", () => Season(typeof(S5Manager), "_instance", hero.ObjectID, "PlayerMagicWandDict", "PackageModuleDict"), status);
        Source("season6", () => Season(typeof(S6Manager), "instance", hero.ObjectID, "S6DiceInfosInSlot"), status);
        Source("season8", () => Season(typeof(S8Manager), "_instance", hero.ObjectID, "S8GemItemDict", "S8ThirdItemDict"), status);
        Source("season9", () => Season9(hero.ObjectID), status);
        Source("live-skills", () => "disabled: ephemeral skill snapshot polling", status);
        Summary = "Diagnostic sources: " + string.Join(" · ", sourceStatus.Values) + "\nBase LSC: weapon component only; effect totals not resolved.";
    }
    private static string WeaponActions(NewPlayerObject hero, NewItemProp? weapon)
    {
        if (weapon == null) return "null";
        // Reuse the existing ammo read, once. No extra polling or native hook.
        var ammo = weapon.CurBullet;
        var capacity = weapon.MaxBullet;
        BuildBonuses.SetMagazine(weapon.ObjectID, ammo);
        return $"ammo={ammo}/{capacity} secondaryAmmo={weapon.CurPFBullet}/{weapon.MaxPFBullet} chargeTime={Describe(weapon.ChargeTime)} fillTime={Describe(weapon.FillTime)} attackSpeed={Describe(weapon.AttSpeed)} primary={Describe(hero.PlayerCom?.LeftMouseASSlot)} secondary={Describe(hero.PlayerCom?.RightMouseASSlot)} reload={Describe(hero.PlayerCom?.ReloadMouseASSlot)} heroSkill={Describe(hero.PlayerCom?.PlayerSkillAttInfo)}";
    }
    private static string WeaponEffects(NewItemProp? weapon)
    {
        ModifierRuntime.Begin();
        if (weapon==null) { ModifierRuntime.Commit("weapon-inscriptions");return "null"; }
        try
        {
            collectingInscriptions=true;
            string own=Describe(weapon.Inscription), shared=Describe(weapon.ShareInscription);
            collectingInscriptions=false;
            if(own.Contains("limit") || shared.Contains("limit") || own.Contains("unsupported") || shared.Contains("unsupported")) ModifierRuntime.Abort("weapon-inscriptions");
            else ModifierRuntime.Commit("weapon-inscriptions");
            return "inscriptions="+own+" shared="+shared+" disabled="+Describe(weapon.DisableInscription)+" sealed="+Describe(weapon.SealedInscription)+" enhance="+Describe(weapon.Enhance);
        }
        catch { ModifierRuntime.Abort("weapon-inscriptions");throw; }
        finally { collectingInscriptions=false; }
    }
    private static void Source(string name, Func<string> read, List<string> status)
    {
        if (sourceIndex++ != selected) return;
        CrashDiagnostics.Reading("inventory-" + name);
        ReadBudget.Begin();
        try
        {
            pendingBuffs = name is "buffs" or "client-buffs" ? new() : null;
            partialBuffs=false;
            string detail = read();
            if(name=="season8")GemArtworkIds.Observe(detail);
            if (pendingBuffs != null)
                ModifierRuntime.Buffs(name == "buffs" ? "server" : "client", detail != "null" ? pendingBuffs : null,
                    !partialBuffs && !detail.Contains("limit") && !detail.Contains("unsupported") && !detail.Contains("unmapped"));
            TraceStore.Write("inventory-" + name, "player=" + Panel.LocalPlayerId + " " + detail);
            sourceStatus[name] = name + (detail.Contains("[budget limit]") ? ": partial" : detail == "null" || detail == "inactive" ? ": empty" : ": read");
        }
        catch (Exception ex)
        {
            if (name is "buffs" or "client-buffs") ModifierRuntime.Buffs(name == "buffs" ? "server" : "client", null);
            TraceStore.Write("inventory-error-" + name, ex.GetBaseException().Message);
            sourceStatus[name] = name + ": error";
            if (name == "weapon-actions") BuildBonuses.SetMagazine(Panel.CurrentWeaponId, null);
        }
        pendingBuffs = null;
        CrashDiagnostics.ReadDone("inventory-" + name);
    }
    private static string Season(Type type, string field, int player, params string[] fields)
    {
        var instance = type.GetProperty(field)?.GetValue(null);
        if (instance == null) { if(type==typeof(S8Manager))ModifierRuntime.Resources("pendant",null);return "inactive"; }
        return string.Join("; ", fields.Select(name =>
        {
            var raw = type.GetProperty(name)?.GetValue(instance);
            var own = PlayerEntries(raw, player);
            if(type==typeof(S8Manager) && name=="S8ThirdItemDict")
            {
                pendingResources=new();resourceUnits=0;
                try
                {
                    string detail=own!=null ? Describe(own) : "null";
                    ModifierRuntime.Resources("pendant",own!=null && resourceUnits==1 && !detail.Contains("limit") && !detail.Contains("unsupported") ? pendingResources : null);
                    return name+"=playerScoped:"+detail;
                }
                catch { ModifierRuntime.Resources("pendant",null);throw; }
                finally { pendingResources=null; }
            }
            return name + "=" + (own != null ? "playerScoped:" + Describe(own) : "scopeUnverified:" + Describe(raw));
        }));
    }
    private static string Season9(int player)
    {
        // Singleton's generated instance field can be read without invoking GetInstance().
        var type = typeof(S9.S9Manager).BaseType;
        var instance = type?.GetProperty("s_instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
        if (instance == null) return "inactive";
        return "equipped=" + Describe(PlayerEntries(typeof(S9.S9Manager).GetProperty("m_TalismanDictOnUse")?.GetValue(instance), player));
    }
    private static object? PlayerEntries(object? dict, int player)
    {
        if (dict == null) return null;
        var contains = dict.GetType().GetMethod("ContainsKey", new[] { typeof(int) });
        if (contains == null || !(bool)contains.Invoke(dict, new object[] { player })!) return null;
        return dict.GetType().GetProperty("Item")?.GetValue(dict, new object[] { player });
    }
    private static string Skills(int player)
    {
        var all = SkillManager.objectDict;
        if (all == null || !all.ContainsKey(player)) return "null";
        var values = new List<string>();
        foreach (var entry in all[player].skillDict)
        {
            if (!ReadBudget.Step()) { values.Add("[budget limit]"); break; }
            var skill = entry.Value;
            if (skill == null) continue;
            values.Add(SkillSnapshot(skill));
            if (values.Count >= 8) { values.Add("[limit 8]"); break; }
        }
        return string.Join("; ", values);
    }
    internal static string SkillSnapshot(CSkillBase skill)
        => TraceStore.Skill(skill) + " fixed=" + Describe(skill.ItemPropFixedArgs) + " scales=" + Describe(skill.ItemPropPerTenThousandArgs)
            + " item=" + CachedWeapon(skill.ItemPropCache) + " originalItem=" + CachedWeapon(skill.OriginalItemPropCache) + " perform=" + CachedWeapon(skill.PerformPropCache);
    private static string CachedWeapon(NewItemProp? prop)
        => prop == null ? "null" : FormattableString.Invariant($"{{id={prop.ObjectID},sid={prop.SID},lucky={prop.LuckyHit},crit={prop.CrazyEff},att={prop.Att}}}");
    // Bounded read-only traversal of known collections and selected native field wrappers.
    // Never call arbitrary computed getters or boxed IL2CPP Object.ToString().
    internal static string Describe(object? value, int depth = 0)
    {
        if (!ReadBudget.Step()) return "[budget limit]";
        if (value == null) return "null";
        var type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || value is string || value is decimal)
        {
            if(collectingInscriptions && value is int id && id>1000) ModifierRuntime.Add("inscription",id,1,"Weapon inscription");
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";
        }
        if (depth > 5) return "[depth limit]";
        if (type.FullName?.StartsWith("Il2CppSystem.Collections.Generic.") == true)
        {
            if (type.Name.StartsWith("KeyValuePair"))
            {
                var key=Property(type,"Key")?.GetValue(value);var element=Property(type,"Value")?.GetValue(value);
                if(pendingResources!=null && key is string name && element!=null && element.GetType().IsPrimitive && element is not bool)
                    pendingResources["pendant:"+name]=Convert.ToDouble(element,CultureInfo.InvariantCulture);
                return Describe(key,depth+1)+":"+Describe(element,depth+1);
            }
            var getEnumerator = Method(type, "GetEnumerator");
            if (getEnumerator != null)
            {
                object? iterator = getEnumerator.Invoke(value, null);
                if (iterator == null) return "[]";
                var it = iterator.GetType();
                var move = Method(it, "MoveNext");
                var current = Property(it, "Current");
                var parts = new List<string>();
                try
                {
                    if (move == null || current == null) return "[unsupported collection]";
                    while ((bool)move.Invoke(iterator, null)!)
                    {
                        if (!ReadBudget.Step()) { parts.Add("[budget limit]"); break; }
                        if (parts.Count >= 128) { parts.Add("[limit 128]"); break; }
                        parts.Add(Describe(current.GetValue(iterator), depth + 1));
                    }
                }
                finally { Method(it, "Dispose")?.Invoke(iterator, null); }
                parts.Sort(StringComparer.Ordinal);
                return "[" + string.Join(",", parts) + "]";
            }
        }
        string[] names = type.Name switch
        {
            "RelicObject" => new[] { "RelicID", "CurGrade", "Pid", "IsForceDisable", "Quality" },
            "TalentObject" => new[] { "TalentID", "BaseGrade", "MaxGrade" },
            "BenedictionObject" => new[] { "BenedictionSID", "Level", "layer" },
            "StateObject" or "MergeStateObject" => new[] { "SID", "StateID", "Count", "MaxCount", "RemainTime", "IsImmortal", "Attacker" },
            "CtrlSideStateObj" => new[] { "m_StateSID", "m_StateID", "m_Count", "m_RemainTime", "m_IsImmortal" },
            "CRuleHandler" => new[] { "m_RuleSID", "m_RuleID", "m_CurLevel", "m_initData" },
            "S8GemUnit" => new[] { "id", "sid", "quality", "pos", "abilility" },
            "S8ThirdUnit" => new[] { "id", "sid", "quality", "abilityList", "attrDict" },
            "S8Ability" => new[] { "sid", "level", "pos" },
            "SkillAttr" => new[] { "IsActive", "IsReload", "EatBullet", "LastAttTime", "HeatGunShootCount" },
            "S6DiceInfoBase" => new[] { "ID", "SID", "Quality", "RollPoint", "Pos", "IsEquip", "selectPoints" },
            "S7UnitBase" or "S7ModuleUnit" => new[] { "id", "sid", "packPos", "heroID", "beneMark", "lastPoint" },
            "AdditionUnit" => new[] { "point", "addPoint", "extraFlag" },
            "MagicWandUnit" => new[] { "Id", "Sid", "PlayerID", "RareLevel", "EquipModuleLst", "Abilities" },
            "WandModuleBase" or "WandModuleUnit" => new[] { "Sid", "ModuleType", "Num", "Level" },
            "WandAbility" => new[] { "Sid", "Level" },
            "S9TalismanUnit" => new[] { "ID", "m_SID", "m_Level", "m_Count", "m_Pos", "HeroID", "m_iStack", "m_triggerData" },
            _ => Array.Empty<string>()
        };
        if(pendingResources!=null && type.Name=="S8ThirdUnit")resourceUnits++;
        if (value is AttackSkillBase) names = new[] { "ItemID", "ItemSID", "PerformID", "PerformSID", "LastAttTime", "AttackSpeedRatio", "Prop" };
        if (names.Length == 0) return "[unmapped " + type.FullName + "]";
        var fields = new List<string>();
        var captured = pendingBuffs != null && type.Name is "StateObject" or "MergeStateObject" or "CtrlSideStateObj" ? new Dictionary<string,object?>() : null;
        foreach (string name in names)
        {
            if (!ReadBudget.Step()) { fields.Add("[budget limit]"); break; }
            var raw = Property(type,name)?.GetValue(value);
            if (captured != null) captured[name]=raw;
            fields.Add(name + "=" + Describe(raw, depth + 1));
        }
        if (captured != null && pendingBuffs != null)
        {
            bool client = type.Name == "CtrlSideStateObj";
            string prefix = client ? "m_" : "";
            string sidName = client ? "m_StateSID" : "SID";
            if (captured.TryGetValue(sidName,out var sid) && sid is int stateId && captured.TryGetValue(prefix+"Count",out var count) && count is int stacks
                && captured.TryGetValue(prefix+"RemainTime",out var time) && time is float seconds && captured.TryGetValue(prefix+"IsImmortal",out var immortal) && immortal is bool persistent)
                pendingBuffs[stateId] = new(stacks,seconds,persistent);
            else partialBuffs=true;
        }
        return type.Name + "{" + string.Join(",", fields) + "}";
    }
}
