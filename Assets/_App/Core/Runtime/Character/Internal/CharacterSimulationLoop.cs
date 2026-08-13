using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace _App
{
    internal sealed class CharacterSimulationLoop
    {
        private readonly Action _fixedStep;
        private CancellationTokenSource _cancellation;

        public CharacterSimulationLoop(Action fixedStep)
        {
            _fixedStep = fixedStep ?? throw new ArgumentNullException(nameof(fixedStep));
        }

        public void Start()
        {
            Stop();

            _cancellation = new CancellationTokenSource();
            RunAsync(_cancellation).Forget();
        }

        public void Stop()
        {
            CancellationTokenSource cancellation = _cancellation;
            _cancellation = null;

            if (cancellation == null)
                return;

            cancellation.Cancel();
            cancellation.Dispose();
        }

        private async UniTask RunAsync(CancellationTokenSource cancellation)
        {
            try
            {
                CancellationToken token;
                try
                {
                    token = cancellation.Token;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                while (!token.IsCancellationRequested)
                {
                    await UniTask.WaitForFixedUpdate(token);
                    _fixedStep.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (ReferenceEquals(_cancellation, cancellation))
                    _cancellation = null;
            }
        }
    }
}
