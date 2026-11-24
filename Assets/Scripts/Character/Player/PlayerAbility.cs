using System;

// 보스를 처치해 얻는 플레이어 동작. (GDD 3.6) 획득 여부는 PlayerProfile에 보관하고, Player가 동작을 시작하기 전에 확인한다.
// 여러 동작을 함께 가지므로 비트 플래그로 둔다. 한 번에 여러 개를 얻을 때는 OR로 묶는다. (예: Knight 처치 = WallSlide | WallJump)
// 새 동작은 다음 2의 거듭제곱 값으로 추가하고, 기존 값은 바꾸지 않는다. (저장이 생기면 이 숫자가 그대로 기록된다)
// 체력 회복은 동작 해금이 아니라 회복 아이템(물약·음식) 장착으로 쓸 수 있으므로 여기에 넣지 않는다. (PlayerProfile.IsHealItemEquipped)
[Flags]
public enum PlayerAbility
{
    None = 0,
    Dash = 1 << 0,
    WallSlide = 1 << 1,
    WallJump = 1 << 2,
    AirJump = 1 << 3,
    Block = 1 << 4
}
