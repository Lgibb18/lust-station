using System.Linq;
using Content.Server.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Whitelist;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Body.Systems
{
    public sealed class StomachSystem : EntitySystem
    {
        public const string DefaultSolutionName = "stomach";
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;

        public override void Initialize()
        {
            SubscribeLocalEvent<StomachComponent, MapInitEvent>(OnMapInit);
            SubscribeLocalEvent<StomachComponent, EntityUnpausedEvent>(OnUnpaused);
            SubscribeLocalEvent<StomachComponent, ApplyMetabolicMultiplierEvent>(OnApplyMetabolicMultiplier);
        }

        private void OnMapInit(Entity<StomachComponent> ent, ref MapInitEvent args)
        {
            ent.Comp.NextUpdate = _gameTiming.CurTime + ent.Comp.UpdateInterval;
        }

        private void OnUnpaused(Entity<StomachComponent> ent, ref EntityUnpausedEvent args)
        {
            ent.Comp.NextUpdate += args.PausedTime;
        }

        public override void Update(float frameTime)
        {
            var query = EntityQueryEnumerator<StomachComponent, OrganComponent, SolutionContainerManagerComponent>();
            while (query.MoveNext(out var uid, out var stomach, out var organ, out var sol))
            {
                if (_gameTiming.CurTime < stomach.NextUpdate)
                    continue;

                stomach.NextUpdate += stomach.UpdateInterval;

                // Get our solutions
                if (!_solutionContainerSystem.ResolveSolution((uid, sol),
                        DefaultSolutionName,
                        ref stomach.Solution,
                        out var stomachSolution))
                    continue;

                if (organ.Body is not { } body ||
                    !_solutionContainerSystem.TryGetSolution(body, stomach.BodySolutionName, out var bodySolution))
                    continue;

                var transferSolution = new Solution();

                var queue = new RemQueue<StomachComponent.ReagentDelta>();
                foreach (var delta in stomach.ReagentDeltas)
                {
                    delta.Increment(stomach.UpdateInterval);
                    if (delta.Lifetime > stomach.DigestionDelay)
                    {
                        if (stomachSolution.TryGetReagent(delta.ReagentQuantity.Reagent, out var reagent))
                        {
                            if (reagent.Quantity > delta.ReagentQuantity.Quantity)
                                reagent = new(reagent.Reagent, delta.ReagentQuantity.Quantity);

                            stomachSolution.RemoveReagent(reagent);
                            transferSolution.AddReagent(reagent);
                        }

                        queue.Add(delta);
                    }
                }

                foreach (var item in queue)
                {
                    stomach.ReagentDeltas.Remove(item);
                }

                _solutionContainerSystem.UpdateChemicals(stomach.Solution.Value);

                // Transfer everything to the body solution!
                _solutionContainerSystem.TryAddSolution(bodySolution.Value, transferSolution);
            }
        }

        private void OnApplyMetabolicMultiplier(
            Entity<StomachComponent> ent,
            ref ApplyMetabolicMultiplierEvent args)
        {
            if (args.Apply)
            {
                ent.Comp.UpdateInterval *= args.Multiplier;
                return;
            }

            // This way we don't have to worry about it breaking if the stasis bed component is destroyed
            ent.Comp.UpdateInterval /= args.Multiplier;
        }

        public bool CanTransferSolution(
            EntityUid uid,
            Solution solution,
            StomachComponent? stomach = null,
            SolutionContainerManagerComponent? solutions = null)
        {
            return Resolve(uid, ref stomach, ref solutions, logMissing: false)
                   && _solutionContainerSystem.ResolveSolution((uid, solutions),
                       DefaultSolutionName,
                       ref stomach.Solution,
                       out var stomachSolution)
                   // TODO: For now no partial transfers. Potentially change by design
                   && stomachSolution.CanAddSolution(solution);
        }

        public bool TryTransferSolution(
            EntityUid uid,
            Solution solution,
            StomachComponent? stomach = null,
            SolutionContainerManagerComponent? solutions = null)
        {
            if (!Resolve(uid, ref stomach, ref solutions, logMissing: false)
                || !_solutionContainerSystem.ResolveSolution((uid, solutions),
                    DefaultSolutionName,
                    ref stomach.Solution)
                || !CanTransferSolution(uid, solution, stomach, solutions))
            {
                return false;
            }

            _solutionContainerSystem.TryAddSolution(stomach.Solution.Value, solution);
            // Add each reagent to ReagentDeltas. Used to track how long each reagent has been in the stomach
            foreach (var reagent in solution.Contents)
            {
                stomach.ReagentDeltas.Add(new StomachComponent.ReagentDelta(reagent));
            }

            return true;
        }

        public void SetSpecialDigestible(StomachComponent component, EntityWhitelist? whitelist)
        {
            component.SpecialDigestible = whitelist;
        }

        // Sunrise-Start
        public bool TryChangeReagent(EntityUid uid,
            string fromReagent,
            string toReagent,
            StomachComponent? stomach = null,
            SolutionContainerManagerComponent? solutions = null)
        {
            if (!Resolve(uid, ref stomach, ref solutions, false))
                return false;

            if (!_solutionContainerSystem.ResolveSolution((uid, solutions), DefaultSolutionName, ref stomach.Solution))
                return false;

            foreach (var reagent in stomach.Solution.Value.Comp.Solution.Contents.ToList())
            {
                if (reagent.Reagent.Prototype != fromReagent)
                    continue;

                var amount = reagent.Quantity;

                stomach.Solution.Value.Comp.Solution.RemoveReagent(reagent.Reagent.Prototype, amount);
                foreach (var stomachReagentDelta in stomach.ReagentDeltas.ToList())
                {
                    if (stomachReagentDelta.ReagentQuantity.Reagent.Prototype != reagent.Reagent.Prototype)
                        continue;

                    stomach.ReagentDeltas.Remove(stomachReagentDelta);
                    var newDelta = new StomachComponent.ReagentDelta(new ReagentQuantity(
                        new ReagentId(toReagent, stomachReagentDelta.ReagentQuantity.Reagent.Data),
                        stomachReagentDelta.ReagentQuantity.Quantity));
                    stomach.ReagentDeltas.Add(newDelta);
                }

                stomach.Solution.Value.Comp.Solution.AddReagent(toReagent, amount);

                return true;
            }

            return false;
        }
        // Sunrise-End
    }
}
