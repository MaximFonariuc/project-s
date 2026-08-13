using UnityEngine;

namespace _App
{
    public class EnemyIdleState : EnemyBaseState
    {
        public EnemyIdleState(EnemyController controller) 
            : base(controller) { }

        public override void Enter()
        {
            //Anim.CrossFade(AnimationHashes.Idle, 0.2f);
        }

        public override void Tick()
        {
            /*// Если увидели игрока → переход в Chase
            if (/*логика обнаружения#1#)
            {
                Controller.SwitchState(new EnemyChaseState(Controller, Character, Combat, Anim));
            }*/
        }

        public override void Exit() { }
    }
}