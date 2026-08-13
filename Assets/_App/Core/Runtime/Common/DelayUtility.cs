using System;
using Cysharp.Threading.Tasks;

namespace _App
{
    public static class DelayUtility
    {
        public static void InvokeNextFrame(Action callback)
        {
            InvokeAfterFramesAsync(1, callback).Forget();
        }

        public static void InvokeAfterSecond(Action callback)
        {
            InvokeAfterSecondsAsync(1f, callback).Forget();
        }

        public static void InvokeAfterFrames(int skipFrames, Action callback)
        {
            InvokeAfterFramesAsync(skipFrames, callback).Forget();
        }

        public static void InvokeAfterSeconds(float seconds, Action callback)
        {
            InvokeAfterSecondsAsync(seconds, callback).Forget();
        }

        private static async UniTaskVoid InvokeAfterFramesAsync(int skipFrames, Action callback)
        {
            await UniTask.DelayFrame(skipFrames);
            callback?.Invoke();
        }

        private static async UniTaskVoid InvokeAfterSecondsAsync(float seconds, Action callback)
        {
            await UniTask.WaitForSeconds(seconds);
            callback?.Invoke();
        }
    }
}