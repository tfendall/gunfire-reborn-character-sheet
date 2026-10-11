using System.Reflection;
using System.Diagnostics;
using SkillBolt;
using NetFunc;

internal static partial class CombatTypedPrefixes
{
    internal static void s2cwarskill_C2FSSkillStart(MethodBase __originalMethod, basenet netobj, CSkillBase skill)
        => OutgoingSkill(__originalMethod,skill);
    internal static void s2cwarskill_C2FSSkillTrigger(MethodBase __originalMethod, basenet netobj, CSkillBase skill)
        => OutgoingSkill(__originalMethod,skill,true);
    internal static void s2cwarskill_C2FSSkillThroughInfo(MethodBase __originalMethod, basenet netobj, CSkillBase skill)
        => OutgoingSkill(__originalMethod,skill);

    private static void OutgoingSkill(MethodBase method,CSkillBase skill,bool trigger=false)
    {
        CombatEvidence.CaptureOutgoing(method,copy=>{
            if(skill==null)return;
            copy.Copy("stage","send-attempt-not-server-acceptance");
            copy.Copy("attacker",skill.AttID);copy.Copy("actnum",skill.ActNum);
            copy.Copy("skill",skill.SkillID);copy.Copy("perform",skill.Perform);
            copy.Copy("weaponSid",skill.WeaponSID);copy.Copy("weapon",skill.Weapon);
            copy.Copy("isCtrl",skill.IsCtrl);copy.Copy("canTrigger",skill.CanTrigger);
            copy.Copy("victim",skill.Victim);copy.Copy("attType",skill.AttType);
            copy.Copy("element",skill.Element);
            // Scalar cache fields only: never traverse entity/Unity references.
            var cache=skill.CacheData;
            if(cache!=null)
            {
                copy.Copy("cache.IntPara",cache.IntPara);
                copy.Copy("cache.LstIntPara",cache.LstIntPara);
                copy.Copy("cache.LstIntSpecial",cache.LstIntSpecial);
                copy.Copy("cache.ElementType",cache.ElementType);
                copy.Copy("cache.ChargeLevel",cache.ChargeLevel);
                copy.Copy("cache.ParentActnum",cache.ParentActnum);
            }
        },trigger ? () => skill!=null && skill.CanTrigger && skill.m_NetSend!=null && skill.m_NetSend.Count>0 : null);
        // Do not call IsNeedSendNetData here: despite its name it removes
        // entries from the native queue. PacketCartoon remains unfiltered.
    }

    internal static void CartoonData_PacketCartoon(MethodBase __originalMethod,basenet netobj,CartoonData cd,int skillid,int actnum)
    {
        CombatEvidence.CaptureOutgoing(__originalMethod,copy=>{
            if(cd==null)return;
            copy.Copy("stage","serialize-not-server-acceptance");
            copy.Copy("skill",skillid);copy.Copy("actnum",actnum);
            copy.Copy("perform",cd.performid);copy.Copy("cartoonsid",cd.cartoonsid);
            copy.Copy("cartoonid",cd.cartoonid);copy.Copy("vitlist",cd.vitlist);
            copy.Copy("dict",cd.dict);
            // Known concrete generated types only; no reflective struct-list traversal.
            long start=Stopwatch.GetTimestamp();
            bool Budget(int i)
            {
                if(i<12 && copy.Fields.Count<70 && Stopwatch.GetTimestamp()-start<Stopwatch.Frequency/1000)return true;
                copy.Truncated=true;return false;
            }
            var rays=cd.skillray;
            copy.Copy("rayCount",rays?.Count??0);
            if(rays!=null)for(int i=0;i<rays.Count && Budget(i);i++)
            {
                var hit=rays[i];
                copy.Copy($"ray.{i}.victim",hit.vitid);
                copy.Copy($"ray.{i}.part",hit.hitpart);
                copy.Copy($"ray.{i}.isrealhit",hit.isrealhit);
            }
            var special=cd.skillspecialray;
            copy.Copy("specialRayCount",special?.Count??0);
            if(special!=null)for(int i=0;i<special.Count && Budget(i);i++)
            {
                var hit=special[i];
                copy.Copy($"specialRay.{i}.victim",hit.vitid);
                copy.Copy($"specialRay.{i}.part",hit.hitpart);
                copy.Copy($"specialRay.{i}.touch",hit.touch);
            }
        });
    }
}
