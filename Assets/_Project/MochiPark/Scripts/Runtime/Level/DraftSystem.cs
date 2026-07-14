using System.Collections.Generic;
using MochiPark.Runtime.Board;
using MochiPark.Runtime.Core;
using MochiPark.Runtime.Data;
using MochiPark.Runtime.Units;
using UnityEngine;

namespace MochiPark.Runtime.Level
{
    public sealed class DraftSystem
    {
        private const int MinimumDemoDraftCardCount = 3;

        private readonly RuntimeLevelState state;
        private readonly GameCatalogSO catalog;
        private readonly EventBus eventBus;
        private readonly PlacementSystem placementSystem;

        public DraftSystem(
            RuntimeLevelState state,
            GameCatalogSO catalog,
            EventBus eventBus,
            PlacementSystem placementSystem)
        {
            this.state = state;
            this.catalog = catalog;
            this.eventBus = eventBus;
            this.placementSystem = placementSystem;
        }

        public void PrepareDay(ResolvedDayConfig config)
        {
            if (state == null || config == null)
            {
                return;
            }

            uint seed = ComputeSeed(state.RunSeed, config.LevelId, config.DayIndexInLevel, 0, 0);
            state.Draft.PrepareDay(config.UpgradeRollCount, seed);

            if (state.Draft.IsCompletedForDay)
            {
                eventBus.Publish(new DraftCompletedForDayEvent(state));
                return;
            }

            OfferNextRoll();
        }

        public bool TryRerollCurrentOffer()
        {
            if (state == null ||
                state.Phase != GamePhase.Placement ||
                state.Draft.IsCompletedForDay ||
                state.Draft.HasPendingPick ||
                state.Draft.ActiveOffer == null)
            {
                return false;
            }

            return OfferNextRoll();
        }

        public bool TrySelectOfferCard(int cardIndex, out UnitInstanceState placedUnit)
        {
            placedUnit = null;
            if (!CanSelectCard(cardIndex, out DraftOfferState offer))
            {
                return false;
            }

            string unitTypeId = offer.UnitTypeIds[cardIndex];
            if (!catalog.TryGetUnit(unitTypeId, out UnitDefinitionSO unitDefinition))
            {
                eventBus.Publish(new PopupRequestedEvent("Draft unavailable", $"Unit not found: {unitTypeId}"));
                return false;
            }

            if (!TryFindPlacement(unitDefinition, out Vector2Int anchorCell, out GridRotation rotation))
            {
                eventBus.Publish(new PopupRequestedEvent("Draft placement blocked", $"No open tile for {unitDefinition.DisplayName}."));
                return false;
            }

            if (!placementSystem.PlaceUnit(unitDefinition, anchorCell, rotation, false, false, out placedUnit))
            {
                return false;
            }

            state.Draft.SetPendingPick(unitTypeId, placedUnit.InstanceId);
            return true;
        }

        public bool CancelPendingPick()
        {
            if (state == null || !state.Draft.HasPendingPick)
            {
                return false;
            }

            string pendingUnitInstanceId = state.Draft.PendingUnitInstanceId;
            state.Draft.ClearPendingPick();
            if (!placementSystem.RemoveUnit(pendingUnitInstanceId))
            {
                return false;
            }

            DraftOfferState offer = state.Draft.ActiveOffer;
            if (offer != null)
            {
                eventBus.Publish(new DraftOfferedEvent(state, offer));
            }

            return true;
        }

        public bool ConfirmPendingPick()
        {
            if (state == null || !state.Draft.HasPendingPick)
            {
                return false;
            }

            UnitInstanceState pendingUnit = state.FindUnit(state.Draft.PendingUnitInstanceId);
            if (pendingUnit == null)
            {
                state.Draft.ClearPendingPick();
                eventBus.Publish(new PopupRequestedEvent("Draft unavailable", "Pending draft unit was removed."));
                OfferNextRoll();
                return false;
            }

            DraftPickSummary pick = state.Draft.ConfirmPendingPick();
            eventBus.Publish(new DraftPickedEvent(state, pick));
            eventBus.Publish(new SaveRequestedEvent(SaveRequestReason.DraftChoiceConfirmed));

            if (state.Draft.IsCompletedForDay)
            {
                eventBus.Publish(new DraftCompletedForDayEvent(state));
            }
            else
            {
                OfferNextRoll();
            }

            return true;
        }

        public void CompleteForDay()
        {
            if (state == null)
            {
                return;
            }

            state.Draft.CompleteForDay();
            eventBus.Publish(new DraftCompletedForDayEvent(state));
        }

        private bool CanSelectCard(int cardIndex, out DraftOfferState offer)
        {
            offer = null;
            if (state == null || state.Phase != GamePhase.Placement)
            {
                return false;
            }

            if (state.Draft.HasPendingPick)
            {
                eventBus.Publish(new PopupRequestedEvent("Draft placement pending", "Confirm with V or cancel with C before choosing another card."));
                return false;
            }

            offer = state.Draft.ActiveOffer;
            return offer != null &&
                cardIndex >= 0 &&
                cardIndex < offer.UnitTypeIds.Count;
        }

        private bool OfferNextRoll()
        {
            if (state.Draft.CurrentRollIndex >= state.Draft.TotalRollsToday)
            {
                state.Draft.CompleteForDay();
                eventBus.Publish(new DraftCompletedForDayEvent(state));
                return false;
            }

            List<UnitDefinitionSO> candidates = BuildCandidatePool(state.CurrentDayConfig.UnitPoolRules);
            if (candidates.Count == 0)
            {
                Debug.LogWarning("DraftSystem could not build an offer because the candidate pool is empty.");
                eventBus.Publish(new PopupRequestedEvent("Draft unavailable", "No units passed the draft pool rules."));
                state.Draft.CompleteForDay();
                eventBus.Publish(new DraftCompletedForDayEvent(state));
                return false;
            }

            int cardCount = Mathf.Max(MinimumDemoDraftCardCount, state.CurrentDayConfig.DraftCardCount);
            uint seed = ComputeSeed(
                state.RunSeed,
                state.CurrentDayConfig.LevelId,
                state.CurrentDayConfig.DayIndexInLevel,
                state.Draft.CurrentRollIndex,
                state.Draft.RngStep);
            System.Random random = new System.Random(unchecked((int)seed));
            List<UnitDefinitionSO> selected = RollOfferCards(candidates, state.CurrentDayConfig.UnitPoolRules, cardCount, random);
            string guaranteeReason = ApplyGuarantees(selected, candidates, state.CurrentDayConfig.UnitPoolRules, random);

            List<string> unitTypeIds = new List<string>(selected.Count);
            for (int i = 0; i < selected.Count; i++)
            {
                unitTypeIds.Add(selected[i].UnitTypeId);
            }

            DraftOfferState offer = new DraftOfferState(
                $"draft_{state.CurrentDayConfig.DayIndexInLevel:00}_{state.Draft.CurrentRollIndex:00}",
                state.Draft.CurrentRollIndex,
                unitTypeIds,
                false,
                guaranteeReason);

            state.Draft.SetActiveOffer(offer);
            state.Draft.NextRngStep();
            eventBus.Publish(new DraftOfferedEvent(state, offer));
            return true;
        }

        private List<UnitDefinitionSO> BuildCandidatePool(UnitPoolRules rules)
        {
            List<UnitDefinitionSO> candidates = new List<UnitDefinitionSO>();
            IReadOnlyList<UnitDefinitionSO> units = catalog.Units;
            for (int i = 0; i < units.Count; i++)
            {
                UnitDefinitionSO unit = units[i];
                if (unit != null && UnitPassesRules(unit, rules))
                {
                    AddUnique(candidates, unit);
                }
            }

            if (candidates.Count == 0 && rules != null && rules.RequiredTags.Count > 0)
            {
                Debug.LogWarning("DraftSystem requiredTags filtered every unit; falling back to the pool without requiredTags.");
                for (int i = 0; i < units.Count; i++)
                {
                    UnitDefinitionSO unit = units[i];
                    if (unit != null && UnitPassesRules(unit, rules, false))
                    {
                        AddUnique(candidates, unit);
                    }
                }
            }

            return candidates;
        }

        private bool UnitPassesRules(UnitDefinitionSO unit, UnitPoolRules rules, bool applyRequiredTags = true)
        {
            if (rules == null)
            {
                return true;
            }

            if (rules.AvailableFamilies.Count > 0 && !ContainsFamily(rules.AvailableFamilies, unit.Family))
            {
                return false;
            }

            if (rules.UnitWhitelist.Count > 0 && !ContainsUnit(rules.UnitWhitelist, unit))
            {
                return false;
            }

            if (ContainsUnit(rules.UnitBlacklist, unit))
            {
                return false;
            }

            if (applyRequiredTags && rules.RequiredTags.Count > 0 && !ContainsAllTags(unit, rules.RequiredTags))
            {
                return false;
            }

            return true;
        }

        private List<UnitDefinitionSO> RollOfferCards(
            List<UnitDefinitionSO> candidates,
            UnitPoolRules rules,
            int cardCount,
            System.Random random)
        {
            List<UnitDefinitionSO> selected = new List<UnitDefinitionSO>(cardCount);
            bool enforceNoDuplicate = rules == null || rules.NoDuplicateInDraft;
            int maxGoldCards = rules == null ? 0 : rules.MaxGoldCardsPerDraft;
            int goldCards = 0;
            bool shouldAvoidDuplicates = enforceNoDuplicate && candidates.Count >= cardCount;

            for (int i = 0; i < cardCount; i++)
            {
                List<UnitDefinitionSO> pool = BuildRollPool(candidates, selected, shouldAvoidDuplicates, maxGoldCards, goldCards);
                if (pool.Count == 0 && shouldAvoidDuplicates && maxGoldCards > 0)
                {
                    pool = BuildRollPool(candidates, selected, true, 0, goldCards);
                }

                if (pool.Count == 0)
                {
                    pool = BuildRollPool(candidates, selected, false, maxGoldCards, goldCards);
                }

                if (pool.Count == 0)
                {
                    pool = candidates;
                }

                UnitDefinitionSO unit = pool[NextRandomIndex(random, pool.Count)];
                selected.Add(unit);
                if (unit.Rarity == UnitRarity.Gold)
                {
                    goldCards++;
                }
            }

            return selected;
        }

        private static List<UnitDefinitionSO> BuildRollPool(
            List<UnitDefinitionSO> candidates,
            List<UnitDefinitionSO> selected,
            bool avoidDuplicates,
            int maxGoldCards,
            int goldCards)
        {
            List<UnitDefinitionSO> pool = new List<UnitDefinitionSO>();
            for (int i = 0; i < candidates.Count; i++)
            {
                UnitDefinitionSO candidate = candidates[i];
                if (avoidDuplicates && ContainsUnit(selected, candidate))
                {
                    continue;
                }

                if (maxGoldCards > 0 && goldCards >= maxGoldCards && candidate.Rarity == UnitRarity.Gold)
                {
                    continue;
                }

                pool.Add(candidate);
            }

            return pool;
        }

        private string ApplyGuarantees(
            List<UnitDefinitionSO> selected,
            List<UnitDefinitionSO> candidates,
            UnitPoolRules rules,
            System.Random random)
        {
            if (rules == null || selected.Count == 0)
            {
                return string.Empty;
            }

            if (rules.GuaranteedOneBasicScorer && !AnyUnitHasTag(selected, UnitTag.BasicScorer))
            {
                UnitDefinitionSO guaranteed = PickUnitWithTag(candidates, UnitTag.BasicScorer, random);
                if (guaranteed != null)
                {
                    selected[0] = guaranteed;
                    return UnitTag.BasicScorer.ToString();
                }
            }

            for (int i = 0; i < rules.GuaranteedTags.Count; i++)
            {
                UnitTag tag = rules.GuaranteedTags[i];
                if (AnyUnitHasTag(selected, tag))
                {
                    continue;
                }

                UnitDefinitionSO guaranteed = PickUnitWithTag(candidates, tag, random);
                if (guaranteed != null)
                {
                    selected[0] = guaranteed;
                    return tag.ToString();
                }
            }

            return string.Empty;
        }

        private UnitDefinitionSO PickUnitWithTag(List<UnitDefinitionSO> candidates, UnitTag tag, System.Random random)
        {
            List<UnitDefinitionSO> tagged = new List<UnitDefinitionSO>();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (ContainsTag(candidates[i], tag))
                {
                    tagged.Add(candidates[i]);
                }
            }

            return tagged.Count > 0 ? tagged[NextRandomIndex(random, tagged.Count)] : null;
        }

        private int NextRandomIndex(System.Random random, int count)
        {
            state.Draft.NextRngStep();
            return random.Next(0, count);
        }

        private bool TryFindPlacement(UnitDefinitionSO unitDefinition, out Vector2Int anchorCell, out GridRotation rotation)
        {
            RuntimeBoardState board = state.Board;
            GridRotation[] rotations = unitDefinition.RotationAllowed
                ? new[] { GridRotation.North, GridRotation.East, GridRotation.South, GridRotation.West }
                : new[] { GridRotation.North };

            int bestAffectedPathCells = -1;
            anchorCell = default;
            rotation = GridRotation.North;

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    for (int i = 0; i < rotations.Length; i++)
                    {
                        GridRotation candidateRotation = rotations[i];
                        if (!placementSystem.CanPlace(unitDefinition, cell, candidateRotation).IsValid)
                        {
                            continue;
                        }

                        int affectedPathCells = CountAffectedPathCells(unitDefinition, cell, candidateRotation, board);
                        if (affectedPathCells > bestAffectedPathCells)
                        {
                            bestAffectedPathCells = affectedPathCells;
                            anchorCell = cell;
                            rotation = candidateRotation;
                        }
                    }
                }
            }

            return bestAffectedPathCells >= 0;
        }

        private static int CountAffectedPathCells(
            UnitDefinitionSO unitDefinition,
            Vector2Int anchorCell,
            GridRotation rotation,
            RuntimeBoardState board)
        {
            HashSet<Vector2Int> affectedPathCells = new HashSet<Vector2Int>();
            IReadOnlyList<InfluencePatternSpec> influencePatterns = unitDefinition.InfluencePatterns;
            for (int i = 0; i < influencePatterns.Count; i++)
            {
                InfluencePatternSpec influence = influencePatterns[i];
                if (influence == null ||
                    influence.Pattern == null ||
                    !influence.Pattern.TargetsMochiPath)
                {
                    continue;
                }

                List<Vector2Int> cells = GridRotationUtility.ResolveCells(influence.Pattern, anchorCell, rotation);
                for (int j = 0; j < cells.Count; j++)
                {
                    Vector2Int cell = cells[j];
                    if (board.IsInBounds(cell) && board.IsPathCell(cell))
                    {
                        affectedPathCells.Add(cell);
                    }
                }
            }

            return affectedPathCells.Count;
        }

        private static uint ComputeSeed(int runSeed, string levelId, int dayIndex, int rollIndex, int rngStep)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)runSeed) * 16777619u;
                hash = (hash ^ StableStringHash(levelId)) * 16777619u;
                hash = (hash ^ (uint)dayIndex) * 16777619u;
                hash = (hash ^ (uint)rollIndex) * 16777619u;
                hash = (hash ^ (uint)rngStep) * 16777619u;
                return hash;
            }
        }

        private static uint StableStringHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (value == null)
                {
                    return hash;
                }

                for (int i = 0; i < value.Length; i++)
                {
                    hash = (hash ^ value[i]) * 16777619u;
                }

                return hash;
            }
        }

        private static void AddUnique(List<UnitDefinitionSO> units, UnitDefinitionSO unit)
        {
            if (!ContainsUnit(units, unit))
            {
                units.Add(unit);
            }
        }

        private static bool ContainsUnit(IReadOnlyList<UnitDefinitionSO> units, UnitDefinitionSO unit)
        {
            if (unit == null)
            {
                return false;
            }

            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] != null && units[i].UnitTypeId == unit.UnitTypeId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsFamily(IReadOnlyList<UnitFamily> families, UnitFamily family)
        {
            for (int i = 0; i < families.Count; i++)
            {
                if (families[i] == family)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsAllTags(UnitDefinitionSO unit, IReadOnlyList<UnitTag> tags)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                if (!ContainsTag(unit, tags[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AnyUnitHasTag(IReadOnlyList<UnitDefinitionSO> units, UnitTag tag)
        {
            for (int i = 0; i < units.Count; i++)
            {
                if (ContainsTag(units[i], tag))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsTag(UnitDefinitionSO unit, UnitTag tag)
        {
            if (unit == null)
            {
                return false;
            }

            IReadOnlyList<UnitTag> tags = unit.Tags;
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] == tag)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
