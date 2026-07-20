using System;
using System.Collections.Generic;

namespace SteelCoatingTakeoff.Core.Sage
{
    /// <summary>
    /// The seam between this tool and Sage Estimating.
    ///
    /// Everything above this interface (UI, shape data, area math, routing) is
    /// fully portable and unit-tested. Everything below it is version-specific
    /// SDK code, isolated in a single implementation
    /// (<c>SteelCoatingTakeoff.App.Sage.SageEstimatingConnector</c>).
    ///
    /// Swap in <see cref="MockSageConnector"/> to run and test the whole app
    /// without Sage installed.
    /// </summary>
    public interface ISageConnector : IDisposable
    {
        bool IsConnected { get; }

        /// <summary>Open the configured estimate (or attach to the open one).</summary>
        SageConnectResult Connect(SageSettings settings);

        /// <summary>Perform one assembly takeoff.</summary>
        SageTakeoffResult Takeoff(SageTakeoffRequest request);

        /// <summary>Perform a batch of assembly takeoffs, then commit once.</summary>
        SageTakeoffResult TakeoffBatch(IEnumerable<SageTakeoffRequest> requests);

        /// <summary>
        /// List the standard database's assemblies in Sage display order (group headers
        /// followed by their members), for the assembly picker. Read-only; does not
        /// require <see cref="Connect"/>.
        /// </summary>
        SageAssemblyListResult ListAssemblies(SageSettings settings);

        /// <summary>Persist pending changes to the estimate.</summary>
        void Commit();
    }
}
