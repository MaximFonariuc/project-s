using System;
using VContainer.Unity;

namespace _App
{
    internal sealed class CombatStylePresenter : IStartable, IDisposable
    {
        private readonly ICombatStyleState _style;
        private readonly CombatStyleSettings _settings;
        private readonly CombatStyleHudView _view;

        private bool _started;

        public CombatStylePresenter(
            ICombatStyleState style,
            CombatStyleSettings settings,
            CombatStyleHudView view)
        {
            _style = style ?? throw new ArgumentNullException(nameof(style));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
        }

        public void Start()
        {
            if (_started)
            {
                return;
            }

            _style.Changed += OnStyleChanged;
            _started = true;
            CombatStyleSnapshot snapshot = _style.Snapshot;
            Render(in snapshot);
        }

        public void Dispose()
        {
            if (!_started)
            {
                return;
            }

            _style.Changed -= OnStyleChanged;
            _started = false;
        }

        private void OnStyleChanged(CombatStyleSnapshot snapshot)
        {
            Render(in snapshot);
        }

        private void Render(in CombatStyleSnapshot snapshot)
        {
            _view.Render(in snapshot, _settings.GetRankProgress(snapshot.Score));
        }
    }
}
