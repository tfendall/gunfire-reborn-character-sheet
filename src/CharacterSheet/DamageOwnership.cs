// Only explicit server ownership grants credit. ClientOwner/Side describe
// control/team membership and must not grant the local player damage credit.
internal enum DamageCredit { None, Player, Companion }
internal static class DamageOwnership
{
    internal static DamageCredit Classify(int attacker, int playerAtReceipt, int currentPlayer, int? owner)
    {
        if (attacker == 0 || currentPlayer == 0 || playerAtReceipt != currentPlayer) return DamageCredit.None;
        if (attacker == currentPlayer) return DamageCredit.Player;
        return owner == currentPlayer ? DamageCredit.Companion : DamageCredit.None;
    }
}
