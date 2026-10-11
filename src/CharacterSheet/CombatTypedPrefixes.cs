using System.Reflection;
using DYPublic.Duonet;

// Explicit signatures avoid Harmony __args marshalling of IL2CPP packet structs.
internal static partial class CombatTypedPrefixes
{
    internal static void NewObjectCache_RemovePlayerProp(MethodBase __originalMethod, int pid)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{pid});
    }
    internal static void NewObjectCache_ClearAllNH(MethodBase __originalMethod)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{});
    }
    internal static void NewObjectCache_UpdatePlayerProp(MethodBase __originalMethod, int pid, string attrname, Il2CppSystem.Object attrValue)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{pid, attrname, attrValue});
    }
    internal static void NewPlayerManager_DelPlayer(MethodBase __originalMethod, int pid, string msg, bool checkTeamer)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{pid, msg, checkTeamer});
    }
    internal static void NewPlayerManager_ClearAll(MethodBase __originalMethod)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{});
    }
    internal static void s2cnetaddobject_MakeHeroAddPacket(MethodBase __originalMethod, s2cnetaddobject_MakeHeroAddPacketClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetaddobject_ExtHeroProp(MethodBase __originalMethod, s2cnetaddobject_ExtHeroPropClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetaddobject_MakeSummonAddPacket(MethodBase __originalMethod, s2cnetaddobject_MakeSummonAddPacketClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetaddobject_MakeMonsterAddPacket(MethodBase __originalMethod, s2cnetaddobject_MakeMonsterAddPacketClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetaddobject_MapMonsterAndPart(MethodBase __originalMethod, s2cnetaddobject_MapMonsterAndPartClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetaddobject_MakeBuildAddPacket(MethodBase __originalMethod, s2cnetaddobject_MakeBuildAddPacketClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetaddobject_MakeNpcAddPacket(MethodBase __originalMethod, s2cnetaddobject_MakeNpcAddPacketClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetaddobject_MakeServantAddPacket(MethodBase __originalMethod, s2cnetaddobject_MakeServantAddPacketClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetaddobject_MakeDeviceAddPacket(MethodBase __originalMethod, s2cnetaddobject_MakeDeviceAddPacketClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetaddobject_MakePetAddPacket(MethodBase __originalMethod, s2cnetaddobject_MakePetAddPacketClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetaddobject_GS2CPropChange(MethodBase __originalMethod, s2cnetaddobject_GS2CPropChangeClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetaddobject_GS2CPetPropChange(MethodBase __originalMethod, s2cnetaddobject_GS2CPetPropChangeClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetstate_GS2CStateAdd(MethodBase __originalMethod, s2cnetstate_GS2CStateAddClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetstate_GS2CStateDel(MethodBase __originalMethod, s2cnetstate_GS2CStateDelClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetstate_GS2CStateRefresh(MethodBase __originalMethod, s2cnetstate_GS2CStateRefreshClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetstate_GS2CStateRefreshCnt(MethodBase __originalMethod, s2cnetstate_GS2CStateRefreshCntClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetstate_GS2CStateRefreshExtraInfo(MethodBase __originalMethod, s2cnetstate_GS2CStateRefreshExtraInfoClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetwar_GS2CMonsterKillLine(MethodBase __originalMethod, s2cnetwar_GS2CMonsterKillLineClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetwar_GS2CHeroKillLine(MethodBase __originalMethod, s2cnetwar_GS2CHeroKillLineClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetwar_GS2CMonsterKillLineAddition(MethodBase __originalMethod, s2cnetwar_GS2CMonsterKillLineAdditionClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetwar_GS2CDie(MethodBase __originalMethod, s2cnetwar_GS2CDieClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetwar_GS2CRealDie(MethodBase __originalMethod, Il2CppSystem.Nullable<s2cnetwar_GS2CDieClass> data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetwar_GS2CWStatusHP(MethodBase __originalMethod, s2cnetwar_GS2CWStatusHPClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetwar_GS2CReceivedDamRecord(MethodBase __originalMethod, s2cnetwar_GS2CReceivedDamRecordClass data)
        => CombatEvidence.Capture(__originalMethod, new object?[] { data });

    internal static void s2cnetwar_GS2CImmunity(MethodBase __originalMethod, s2cnetwar_GS2CImmunityClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetwar_GS2CStruckMonster(MethodBase __originalMethod, s2cnetwar_GS2CStruckMonsterClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetwar_GS2CRelife(MethodBase __originalMethod, s2cnetwar_GS2CRelifeClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetwar_GS2CBreakShield(MethodBase __originalMethod, s2cnetwar_GS2CBreakShieldClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetwar_GS2CBreakArmor(MethodBase __originalMethod, s2cnetwar_GS2CBreakArmorClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetwar_GS2CLevelNodeLoadOk(MethodBase __originalMethod, s2cnetwar_GS2CLevelNodeLoadOkClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetwar_GS2CTeamDamage(MethodBase __originalMethod, s2cnetwar_GS2CTeamDamageClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetwar_GS2CShowDamageInfo(MethodBase __originalMethod, s2cnetwar_GS2CShowDamageInfoClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetwar_GS2CMonsterAdditionInfo(MethodBase __originalMethod, s2cnetwar_GS2CMonsterAdditionInfoClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetwar_GS2CDoubleHPBarUI(MethodBase __originalMethod, s2cnetwar_GS2CDoubleHPBarUIClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetwar_GS2CNotifyStartSkill(MethodBase __originalMethod, s2cnetwar_GS2CNotifyStartSkillClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetwar_GS2CShowDetailDam(MethodBase __originalMethod, s2cnetwar_GS2CShowDetailDamClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
    internal static void s2cnetwar_GS2CJumpFigure(MethodBase __originalMethod, s2cnetwar_GS2CJumpFigureClass data)
    {
        CombatEvidence.Capture(__originalMethod, new object?[]{data});
    }
}
