using System;
using System.Collections.Generic;
using System.Linq;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.DataContracts;
using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Infrastructure.LegacyMatch;
using UnityEngine;

namespace FStudio.FootballWorld.Infrastructure.GameModes
{
    public sealed partial class GameHubSession
    {
        private DatabaseCatalog careerAdapterCatalog;
        private CareerFormation careerAdapterFormation;
        private CareerMentality careerAdapterMentality;
        private static CatalogMatchAdapter CreateCareerAdapter(CareerSession career, IReadOnlyList<VisualProfileData> profiles)
            => new CatalogMatchAdapter(career.EffectiveCatalog, profiles, LoadBindings(),
                new CareerMatchOptions(career.Competition.ControlledClubId, career.Formation, career.Mentality));

        // Rebuild for a lineup preview or match only when career choices change.
        // The effective roster and tactics are
        // career state; neither the source catalog nor shared visual bindings change.
        private void RefreshCareerAdapter()
        {
            if (careerAdapter != null && ReferenceEquals(careerAdapterCatalog, careerSession.EffectiveCatalog)
                && careerAdapterFormation == careerSession.Formation && careerAdapterMentality == careerSession.Mentality) return;
            var replacement = CreateCareerAdapter(careerSession, careerVisualProfiles);
            if (replacement.Teams.Any(team => careerSession.Competition.Edition.ParticipantClubIds.Contains(team.ClubId) && !team.CanPlay))
            {
                replacement.Dispose();
                throw new InvalidOperationException("A career participant cannot field a team.");
            }
            careerAdapter?.Dispose();
            careerAdapter = replacement;
            RememberCareerAdapter();
        }

        private void RememberCareerAdapter()
        {
            careerAdapterCatalog = careerSession.EffectiveCatalog;
            careerAdapterFormation = careerSession.Formation;
            careerAdapterMentality = careerSession.Mentality;
        }

        public IReadOnlyList<PlayerDefinition> GetCareerStartingLineup()
        {
            if (careerSession == null || IsBusy) return Array.Empty<PlayerDefinition>();
            try
            {
                RefreshCareerAdapter();
                var selected = careerAdapter.Teams.First(team => team.ClubId == careerSession.Competition.ControlledClubId);
                return selected.PlayerIds.Select(id => careerSession.EffectiveCatalog.GetPlayer(id)).ToArray();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[FootballWorld] Career lineup unavailable: " + exception.Message);
                return Array.Empty<PlayerDefinition>();
            }
        }

        public bool SubmitCareerOffer(string playerId, long amount)
        {
            if (careerSession == null || IsBusy) return false;
            if (amount < 0 || amount > int.MaxValue) return ManagementError("career.offerInvalid");
            if (amount > careerSession.AvailableTransferBudget) return ManagementError("career.offerInsufficientFunds");
            return ChangeCareerManagement(() => careerSession.SubmitOffer(playerId, amount), "career.offerSubmitted", "career.offerUnavailable");
        }

        public bool CancelCareerOffer(string offerId)
            => ChangeCareerManagement(() => {
                if (!careerSession.CancelOffer(offerId)) throw new InvalidOperationException("Offer is no longer pending.");
            }, "career.offerCancelled", "career.offerUnavailable");

        public bool SetCareerTactics(string formationId, string mentalityId)
        {
            CareerFormation formation;
            CareerMentality mentality;
            switch (formationId)
            {
                case "4-4-2": formation = CareerFormation.FourFourTwo; break;
                case "4-3-3": formation = CareerFormation.FourThreeThree; break;
                case "4-2-3-1": formation = CareerFormation.FourTwoThreeOne; break;
                default: return ManagementError("career.actionUnavailable");
            }
            switch (mentalityId)
            {
                case "Defensive": mentality = CareerMentality.Defensive; break;
                case "Balanced": mentality = CareerMentality.Balanced; break;
                case "Attacking": mentality = CareerMentality.Attacking; break;
                default: return ManagementError("career.actionUnavailable");
            }
            return ChangeCareerManagement(() => careerSession.SetTactics(formation, mentality), "career.tacticsSaved", "career.actionUnavailable");
        }

        private bool ChangeCareerManagement(Action command, string successKey, string errorKey)
        {
            if (careerSession == null || IsBusy) return false;
            statusKey = null; statusDetails = null;
            try
            {
                command();
                PersistCareer();
                statusKey = successKey;
                Changed?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[FootballWorld] Management action rejected: " + exception.Message);
                return ManagementError(errorKey);
            }
        }

        private bool ManagementError(string key)
        {
            statusKey = key; statusDetails = null;
            Changed?.Invoke();
            return false;
        }
    }
}
