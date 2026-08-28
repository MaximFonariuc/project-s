using System;
using ModularAnimation;
using VContainer;

namespace _App
{
    internal static class PlayerCameraComposition
    {
        public static void RegisterPlayerCamera(
            this IContainerBuilder builder,
            ThirdPersonController player)
        {
            if (player.FollowCameraPrefab == null || player.FollowTarget == null)
            {
                throw new InvalidOperationException(
                    "Player camera requires a Cinemachine camera prefab and follow target.");
            }

            PlayerCameraSettings settings = player.CameraSettings;
            builder.Register(
                    _ => new CinemachineCameraRig(
                        player.FollowCameraPrefab,
                        player.FollowTarget,
                        settings.DefaultDistance),
                    Lifetime.Scoped)
                .AsSelf()
                .As<IPlayerCameraRig>()
                .As<ICameraOrientation>();

            builder.Register<IPlayerCameraPresentation>(
                resolver => new PlayerCameraAnimationAdapter(
                    resolver.Resolve<IPlayerActorTransform>(),
                    player.FollowTarget,
                    resolver.Resolve<ICharacterLookAnimationDriver>()),
                Lifetime.Scoped);
            builder.Register(
                    resolver => new PlayerCameraController(
                        resolver.Resolve<IPlayerInput>(),
                        resolver.Resolve<IPlayerActorTransform>(),
                        resolver.Resolve<IPlayerCameraRig>(),
                        resolver.Resolve<IPlayerTargetingState>(),
                        resolver.Resolve<IPlayerCameraPresentation>(),
                        settings),
                    Lifetime.Scoped)
                .AsSelf()
                .As<IPlayerCameraState>()
                .As<IPlayerCameraCommands>();
        }
    }
}
