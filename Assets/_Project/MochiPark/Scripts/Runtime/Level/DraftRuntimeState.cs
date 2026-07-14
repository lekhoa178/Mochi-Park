using System.Collections.Generic;

namespace MochiPark.Runtime.Level
{
    public sealed class DraftRuntimeState
    {
        private readonly List<DraftOfferState> activeOffers = new List<DraftOfferState>();
        private readonly List<DraftPickSummary> picksToday = new List<DraftPickSummary>();

        public int CurrentRollIndex { get; private set; }
        public int TotalRollsToday { get; private set; }
        public IReadOnlyList<DraftOfferState> ActiveOffers => activeOffers;
        public IReadOnlyList<DraftPickSummary> PicksToday => picksToday;
        public uint RngSeed { get; private set; }
        public int RngStep { get; private set; }
        public string PendingUnitInstanceId { get; private set; }
        public string PendingUnitTypeId { get; private set; }
        public bool IsCompletedForDay { get; private set; }
        public bool HasPendingPick => !string.IsNullOrEmpty(PendingUnitInstanceId);
        public DraftOfferState ActiveOffer => activeOffers.Count > 0 ? activeOffers[0] : null;

        public void PrepareDay(int totalRollsToday, uint rngSeed)
        {
            CurrentRollIndex = 0;
            TotalRollsToday = totalRollsToday < 0 ? 0 : totalRollsToday;
            RngSeed = rngSeed;
            RngStep = 0;
            PendingUnitInstanceId = null;
            PendingUnitTypeId = null;
            IsCompletedForDay = TotalRollsToday == 0;
            activeOffers.Clear();
            picksToday.Clear();
        }

        public void SetActiveOffer(DraftOfferState offer)
        {
            activeOffers.Clear();
            if (offer != null)
            {
                activeOffers.Add(offer);
            }
        }

        public void SetPendingPick(string unitTypeId, string unitInstanceId)
        {
            PendingUnitTypeId = unitTypeId;
            PendingUnitInstanceId = unitInstanceId;
        }

        public void ClearPendingPick()
        {
            PendingUnitTypeId = null;
            PendingUnitInstanceId = null;
        }

        public DraftPickSummary ConfirmPendingPick()
        {
            DraftOfferState offer = ActiveOffer;
            DraftPickSummary pick = new DraftPickSummary(
                CurrentRollIndex,
                offer == null ? string.Empty : offer.OfferId,
                PendingUnitTypeId,
                PendingUnitInstanceId);

            picksToday.Add(pick);
            CurrentRollIndex++;
            ClearPendingPick();
            activeOffers.Clear();
            IsCompletedForDay = CurrentRollIndex >= TotalRollsToday;
            return pick;
        }

        public void CompleteForDay()
        {
            activeOffers.Clear();
            ClearPendingPick();
            CurrentRollIndex = TotalRollsToday;
            IsCompletedForDay = true;
        }

        public int NextRngStep()
        {
            int step = RngStep;
            RngStep++;
            return step;
        }
    }

    public sealed class DraftOfferState
    {
        private readonly List<string> unitTypeIds;

        public DraftOfferState(
            string offerId,
            int rollIndex,
            List<string> unitTypeIds,
            bool isForcedByTutorial,
            string guaranteeReason)
        {
            OfferId = offerId;
            RollIndex = rollIndex;
            this.unitTypeIds = unitTypeIds ?? new List<string>();
            IsForcedByTutorial = isForcedByTutorial;
            GuaranteeReason = guaranteeReason;
        }

        public string OfferId { get; }
        public int RollIndex { get; }
        public IReadOnlyList<string> UnitTypeIds => unitTypeIds;
        public bool IsForcedByTutorial { get; }
        public string GuaranteeReason { get; }
    }

    public readonly struct DraftPickSummary
    {
        public DraftPickSummary(
            int rollIndex,
            string sourceOfferId,
            string pickedUnitTypeId,
            string createdUnitInstanceId)
        {
            RollIndex = rollIndex;
            SourceOfferId = sourceOfferId;
            PickedUnitTypeId = pickedUnitTypeId;
            CreatedUnitInstanceId = createdUnitInstanceId;
        }

        public int RollIndex { get; }
        public string SourceOfferId { get; }
        public string PickedUnitTypeId { get; }
        public string CreatedUnitInstanceId { get; }
    }
}
