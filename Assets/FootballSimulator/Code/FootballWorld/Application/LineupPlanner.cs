#nullable enable

using System;
using System.Collections.Generic;
using FStudio.FootballWorld.Domain;

namespace FStudio.FootballWorld.Application
{
    public static class LineupPlanner
    {
        public static LineupPlan Plan(DatabaseCatalog? catalog, string? clubId,
            IReadOnlyList<PlayerPosition>? slots)
        {
            if (catalog == null)
                return LineupPlan.Rejected("A database catalog is required to plan a lineup.");
            if (string.IsNullOrWhiteSpace(clubId))
                return LineupPlan.Rejected("A club ID is required to plan a lineup.");
            if (slots == null || slots.Count != 11)
                return LineupPlan.Rejected("A lineup must have exactly eleven formation slots.");

            var slotSnapshot = new PlayerPosition[11];
            for (var i = 0; i < slotSnapshot.Length; i++)
            {
                var position = slots[i];
                if (!Enum.IsDefined(typeof(PlayerPosition), position))
                    return LineupPlan.Rejected($"Formation slot {i} has an unknown position.");
                if ((i == 0 && position != PlayerPosition.GK) || (i != 0 && position == PlayerPosition.GK))
                    return LineupPlan.Rejected("The first formation slot must be GK and the remaining ten must be outfield positions.");
                slotSnapshot[i] = position;
            }

            IReadOnlyList<PlayerDefinition> roster;
            try
            {
                roster = catalog.GetRoster(clubId);
            }
            catch (ArgumentException exception)
            {
                return LineupPlan.Rejected("Invalid club ID: " + exception.Message);
            }
            catch (KeyNotFoundException)
            {
                return LineupPlan.Rejected($"Club '{clubId}' does not exist in the catalog.");
            }

            if (roster.Count < 11)
                return LineupPlan.Rejected($"Club '{clubId}' needs at least eleven registered players; it has {roster.Count}.");

            var orderedRoster = new List<PlayerDefinition>(roster);
            orderedRoster.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            PlayerDefinition? goalkeeper = null;
            foreach (var player in orderedRoster)
            {
                if (!HasPosition(player, PlayerPosition.GK))
                    continue;
                if (goalkeeper == null)
                    goalkeeper = player;
                if (player.NaturalPositions.Count == 1)
                {
                    goalkeeper = player;
                    break;
                }
            }

            if (goalkeeper == null)
                return LineupPlan.Rejected($"Club '{clubId}' needs a player with GK as a natural position.");

            var outfieldPlayers = new List<PlayerDefinition>();
            foreach (var player in orderedRoster)
            {
                if (player == goalkeeper)
                    continue;
                if (player.NaturalPositions.Count == 1 && player.NaturalPositions[0] == PlayerPosition.GK)
                    continue;
                outfieldPlayers.Add(player);
            }

            if (outfieldPlayers.Count < 10)
                return LineupPlan.Rejected($"Club '{clubId}' needs ten outfield-capable players besides the selected goalkeeper; it has {outfieldPlayers.Count}.");

            var matchedSlotByPlayer = new int[outfieldPlayers.Count];
            for (var i = 0; i < matchedSlotByPlayer.Length; i++)
                matchedSlotByPlayer[i] = -1;

            // An augmenting path can move a flexible player to another natural slot,
            // preserving the maximum number of natural matches instead of using a greedy choice.
            for (var slot = 1; slot < slotSnapshot.Length; slot++)
                TryAssignNaturalPosition(slot, slotSnapshot, outfieldPlayers, matchedSlotByPlayer,
                    new bool[outfieldPlayers.Count]);

            var selected = new PlayerDefinition[11];
            selected[0] = goalkeeper;
            for (var playerIndex = 0; playerIndex < outfieldPlayers.Count; playerIndex++)
            {
                var slot = matchedSlotByPlayer[playerIndex];
                if (slot != -1)
                    selected[slot] = outfieldPlayers[playerIndex];
            }

            var nextUnusedPlayer = 0;
            var outOfPositionIds = new List<string>();
            for (var slot = 1; slot < selected.Length; slot++)
            {
                if (selected[slot] == null)
                {
                    while (matchedSlotByPlayer[nextUnusedPlayer] != -1)
                        nextUnusedPlayer++;
                    selected[slot] = outfieldPlayers[nextUnusedPlayer];
                    matchedSlotByPlayer[nextUnusedPlayer++] = slot;
                }

                if (!HasPosition(selected[slot], slotSnapshot[slot]))
                    outOfPositionIds.Add(selected[slot].Id);
            }

            return LineupPlan.Accepted(selected, outOfPositionIds);
        }

        private static bool TryAssignNaturalPosition(int slot, IReadOnlyList<PlayerPosition> slots,
            IReadOnlyList<PlayerDefinition> players, int[] matchedSlotByPlayer, bool[] visitedPlayers)
        {
            for (var playerIndex = 0; playerIndex < players.Count; playerIndex++)
            {
                if (visitedPlayers[playerIndex] || !HasPosition(players[playerIndex], slots[slot]))
                    continue;
                visitedPlayers[playerIndex] = true;
                var previousSlot = matchedSlotByPlayer[playerIndex];
                if (previousSlot == -1 || TryAssignNaturalPosition(previousSlot, slots, players,
                    matchedSlotByPlayer, visitedPlayers))
                {
                    matchedSlotByPlayer[playerIndex] = slot;
                    return true;
                }
            }

            return false;
        }

        private static bool HasPosition(PlayerDefinition player, PlayerPosition position)
        {
            for (var i = 0; i < player.NaturalPositions.Count; i++)
                if (player.NaturalPositions[i] == position)
                    return true;
            return false;
        }
    }
}
