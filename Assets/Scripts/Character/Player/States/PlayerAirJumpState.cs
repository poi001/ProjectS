// 공중 점프 상태. 점프 상태와 같고(Jump 애니메이션 재사용), 뛰어오르는 높이만 공중 점프 배율(Player.AirJumpHeightRatio)을 따른다.
// 공중 점프 횟수는 이 상태로 전환하는 쪽(PlayerState.TryChangeToAirJumpState)이 소모한다.
public class PlayerAirJumpState : PlayerJumpState
{
    public PlayerAirJumpState(Player owner, StateMachine<Player> stateMachine)
        : base(owner, stateMachine)
    {
    }

    protected override void ApplyJumpVelocity()
    {
        owner.Movement.Jump(owner.Movement.GetJumpVelocity(owner.AirJumpHeightRatio));
    }
}
