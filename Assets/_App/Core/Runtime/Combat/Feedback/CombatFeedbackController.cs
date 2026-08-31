using System;
using UnityEngine;

namespace _App
{
    internal sealed class CombatFeedbackController : IDisposable
    {
        private readonly ICombatHitEvents _outgoingHits;
        private readonly Health _localHealth;
        private readonly IPlayerDefenseState _localDefense;
        private readonly Transform _localRoot;
        private readonly CombatFeedbackSettings _settings;
        private readonly CombatImpactPresentation _presentation;

        private bool _enabled;

        public CombatFeedbackController(
            ICombatHitEvents outgoingHits,
            Health localHealth,
            IPlayerDefenseState localDefense,
            IPlayerCombatActor localActor,
            CombatFeedbackSettings settings,
            CombatImpactPresentation presentation)
        {
            _outgoingHits = outgoingHits ?? throw new ArgumentNullException(nameof(outgoingHits));
            _localHealth = localHealth != null
                ? localHealth
                : throw new ArgumentNullException(nameof(localHealth));
            _localDefense = localDefense ??
                throw new ArgumentNullException(nameof(localDefense));
            _localRoot = localActor?.Root != null
                ? localActor.Root
                : throw new ArgumentNullException(nameof(localActor));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _outgoingHits.HitResolved += OnOutgoingHitResolved;
            _localHealth.Damaged += OnIncomingHitResolved;
            _localDefense.Defended += OnIncomingDefenseResolved;
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _outgoingHits.HitResolved -= OnOutgoingHitResolved;
            _localHealth.Damaged -= OnIncomingHitResolved;
            _localDefense.Defended -= OnIncomingDefenseResolved;
            _presentation.Stop();
            _enabled = false;
        }

        public void Dispose()
        {
            Disable();
        }

        private void OnOutgoingHitResolved(CombatHitResult hit)
        {
            if (!_enabled || !IsLocalSource(hit.Source))
            {
                return;
            }

            Present(in hit);
        }

        private void OnIncomingHitResolved(CombatHitResult hit)
        {
            if (_enabled)
            {
                Present(in hit);
            }
        }

        private void OnIncomingDefenseResolved(PlayerDefenseResult result)
        {
            if (_enabled)
            {
                CombatHitResult hit = result.Hit;
                Present(in hit);
            }
        }

        private void Present(in CombatHitResult hit)
        {
            CombatFeedbackDefinition definition = _settings.Resolve(in hit);
            _presentation.Play(in hit, definition);
        }

        private bool IsLocalSource(GameObject source)
        {
            return source != null &&
                   (source.transform == _localRoot || source.transform.IsChildOf(_localRoot));
        }
    }
}
