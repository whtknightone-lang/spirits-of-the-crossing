// SpiritsCrossing — IRealmController.cs
// Contract every realm scene controller must implement.
// BeginRealm is called by GameBootstrapper when the player enters a realm.
// OnRealmComplete fires when the realm loop decides the session has ended.
// BuildOutcome returns the current (or final) metrics at any moment.

using System;

namespace SpiritsCrossing
{
    public interface IRealmController
    {
        /// <summary>Unique realm identifier matching PortalRealmRegistry keys.</summary>
        string RealmId { get; }

        /// <summary>Planet this realm belongs to (feeds PlanetState.RecordVisit).</summary>
        string PlanetId { get; }

        /// <summary>
        /// Called once when the player enters the realm.
        /// Provides the current player resonance snapshot and planet history.
        /// </summary>
        void BeginRealm(PlayerResponseSample playerSample, PlanetState planetState);

        /// <summary>
        /// Build a realm outcome from current runtime values.
        /// Safe to call mid-session for checkpoints, or at end for final record.
        /// </summary>
        RealmOutcome BuildOutcome();

        /// <summary>
        /// Fired when the realm controller determines the session is complete.
        /// Subscriber (GameBootstrapper / UniverseStateManager) records the outcome.
        /// </summary>
        event Action<RealmOutcome> OnRealmComplete;
    }
}
