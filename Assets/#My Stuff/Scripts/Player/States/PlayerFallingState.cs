using UnityEngine;

public class PlayerFallingState : IState
{
    Player Owner;

    public PlayerFallingState(Player _owner)
    {
        Owner = _owner;
    }

    public void Enter()
    {
        Owner.VelocityGravitational = Vector3.zero;
    }

    public void Execute()
    {
        SetGravity();

        CheckStateChanges();
    }

    public void Exit() { }

    void SetGravity()
    {
        {
            Owner.VelocityGravitational -= new Vector3(0, Owner.GravitationalAcceleration * Owner.FallModifier * Time.deltaTime, 0);
        }
    }

    public void CheckStateChanges()
    {
        if (Owner.IsReceivingFlapInput && Owner.HasStoppedReceivingJumpInput)
        {
            Owner.PlayerStateMachine.ChangeState(new PlayerFlappingState(Owner));
        }
        if (Owner.Grounded)
        {
            Owner.PlayerStateMachine.ChangeState(new PlayerIdleState(Owner));
        }
    }
}